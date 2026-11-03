using System.Collections.Generic;
using UnityEngine;

// 소환 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 검 소환·해제는 #102에서 채웠고, Sword* 반환·파라미터는 #220에서 더했다. 나머지 효과(판정, 데미지, 상태 이상)는 각 스킬 이슈에서 채운다. 그 전까지는 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>
/// 검 소환 (malloc). 대상 없음, 누르면 바로 발동 (#102, #220에서 Sword* 반환)
/// <para>
/// - 시작할 때 소환검을 만들고 heap에 올린다(Malloc). 크기는 PredefinedStructs의 Sword 크기(임시 2칸, malloc(Sword) 블록과 같음).<br/>
/// - 반환: 만든 소환검의 포인터 Sword*. 블록코딩에서 Sword* s = SummonSword(); 로 받아 다른 스킬에 넘기거나 free(s)로 해제한다. 만들지 못하면 NULL.<br/>
/// - 포인터는 이 작업의 것이고, 정상으로 끝나면 숨은 시스템 전역으로 옮겨진다. 받은 변수를 잃어도 누수가 아니다 (predefined-function.md).<br/>
/// - 도중에 실행 중단·실패로 끝나면 그 소환검은 누수된다 (heap 누수 규칙, SummonedSwords가 ObjectLeaked를 받아 멈춤).<br/>
/// - 소환검이 이미 5개(누수 포함)거나 heap이 모자라면 만들지 않는다. C의 malloc처럼 에러로 끝내지 않는다.
/// </para>
/// </summary>
public class CSummonSword : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonSword;

	public override VariableType ReturnType => SwordPointerType();

	/// <summary>소환검 하나의 heap 크기. malloc(Sword) 블록과 같은 값 (PredefinedStructs)</summary>
	public static int SwordHeapSize => PredefinedStructs.TryGet(PredefinedStructs.Sword, out StructDefinition def) ? def.HeapSize : 1;

	protected override void OnSkillStart(FunctionContext task)
	{
		// 일찍 끝나도 반환 값이 있게 NULL을 먼저 정하고, 만들면 실제 포인터로 덮어쓴다 (실행기 규칙: 반환 타입이 있으면 Return 필수)
		Return(RuntimeValue.FromPointer(SwordPointerType(), HeapStorage.NullPointer));

		SummonedSwords swords = task.Caster.GetComponent<SummonedSwords>();
		if (swords == null)
		{
			Debug.LogError($"{Name}: {task.Caster.name}에 SummonedSwords가 없습니다.");
			return;
		}
		// 이미 5개(누수 포함)면 만들지 않는다
		if (CountHeapObjects<SummonedSword>(task) >= SummonedSwords.MaxCount) return;

		SummonedSword sword = swords.SpawnSword();
		// 만들지 못했으면(프리팹 설정 오류 등) 소환하지 않는다. 원인은 NetworkSpawner가 로그로 남긴다
		if (sword == null) return;
		int pointer = Malloc(task, sword, SwordHeapSize);
		// heap 부족: 만든 오브젝트를 없애고 끝낸다 (에러 아님)
		if (pointer == HeapStorage.NullPointer)
		{
			swords.DespawnSword(sword);
			return;
		}
		swords.Register(sword, pointer);
		Return(RuntimeValue.FromPointer(SwordPointerType(), pointer));
	}
}

/// <summary>
/// 해제 (free). 대상 없음, 누르면 바로 발동 (#102, #220에서 Sword* 파라미터)
/// <para>
/// - sword 파라미터로 포인터를 넘기면 그 소환검을 해제한다 (free(s)와 같음). 플레이어가 malloc(Sword)로 만든 검도 된다.<br/>
/// - 안 넘기면 마지막에 소환한 소환검부터 해제한다 (숨은 시스템 전역 목록의 마지막). 플레이어가 malloc으로 만든 검은 이 목록에 없다.<br/>
/// - 날아가는 중이거나 떨어진 소환검도 거리와 상관없이 해제한다. 누수된 소환검, 아직 진행 중인 검 소환의 소환검은 해제하지 않는다.<br/>
/// - 해제할 소환검이 없으면 NoSummonedSwordError. 해제하면 heap 칸이 돌아오고, 소환검 오브젝트는 SummonedSwords가 ObjectFreed를 받아 없앤다.
/// </para>
/// </summary>
public class CFreeSword : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.FreeSword;

	protected override bool TakesSwordPointer => true;

	// 포인터를 넘겼으면 그 포인터가 맞는지는 Free가 본다. 안 넘겼으면 해제할 수 있는 소환검 수(숨은 전역 목록)를 센다
	protected override int CountUsableSummonedSwords(FunctionContext task) =>
		TryGetArgument("sword", out _) ? int.MaxValue : GetGlobalPointers<SummonedSword>(task).Count;

	protected override void OnSkillStart(FunctionContext task)
	{
		if (TryGetArgument("sword", out RuntimeValue value))
		{
			int given = value.AsPointer();
			// 소환검이 아닌 포인터면 InvalidPointerError로 끝난다
			if (!TryGetHeapObject(task, given, out SummonedSword _)) return;
			Free(task, given);
			return;
		}

		List<int> pointers = GetGlobalPointers<SummonedSword>(task);
		if (pointers.Count == 0)
		{
			Fail(new NoSummonedSwordError());
			return;
		}
		int pointer = pointers[pointers.Count - 1];
		if (!TryGetHeapObject(task, pointer, out SummonedSword _)) return;
		// 오브젝트 삭제는 SummonedSwords가 ObjectFreed를 받아서 한다 (여기서 지우면 두 번 지움, #191)
		Free(task, pointer);
	}
}

/// <summary>패링. 성공 시 적 공격을 검 이펙트로 1회 되돌려줌 (대상 없음)</summary>
public class CParry : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.Parry;
}

/// <summary>표식 남기기. 검이 적 좌우로 찔러 표식 + 1.0초 기절 (단일)</summary>
public class CMark : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.Mark;
}

/// <summary>검무. 검 개수만큼 적 근처에 두고 휘두름, 데미지는 낮지만 출혈 부여 (범위)</summary>
public class CSwordDance : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SwordDance;
}
