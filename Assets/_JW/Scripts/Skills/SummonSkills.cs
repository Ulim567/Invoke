using System.Collections.Generic;
using UnityEngine;

// 소환 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 검 소환·해제는 #102에서 채웠다. 나머지 효과(판정, 데미지, 상태 이상)는 각 스킬 이슈에서 채운다. 그 전까지는 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>
/// 검 소환 (malloc). 대상 없음, 누르면 바로 발동 (#102)
/// <para>
/// - 시작할 때 소환검을 만들고 heap에 올린다(Malloc). 포인터는 이 작업의 것이고, 정상으로 끝나면 전역 포인터 목록으로 옮겨진다.<br/>
/// - 도중에 실행 중단·실패로 끝나면 그 소환검은 누수된다 (heap 누수 규칙, SummonedSwords가 ObjectLeaked를 받아 멈춤).<br/>
/// - 소환검이 이미 5개(누수 포함)거나 heap이 모자라면 만들지 않는다. C의 malloc처럼 에러로 끝내지 않는다.
/// </para>
/// </summary>
public class CSummonSword : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonSword;

	protected override void OnSkillStart(FunctionTask task)
	{
		SummonedSwords swords = task.Caster.GetComponent<SummonedSwords>();
		if (swords == null)
		{
			Debug.LogError($"{Name}: {task.Caster.name}에 SummonedSwords가 없습니다.");
			return;
		}
		// 이미 5개(누수 포함)면 만들지 않는다
		if (CountHeapObjects<SummonedSword>(task) >= SummonedSwords.MaxCount) return;

		int heapSize = Data != null ? Data.HeapSize : 1;
		SummonedSword sword = swords.SpawnSword();
		int pointer = Malloc(task, sword, heapSize);
		// heap 부족: 만든 오브젝트를 없애고 끝낸다 (에러 아님)
		if (pointer == HeapStorage.NullPointer)
		{
			swords.DespawnSword(sword);
			return;
		}
		swords.Register(sword, pointer);
	}
}

/// <summary>
/// 해제 (free). 대상 없음, 누르면 바로 발동 (#102)
/// <para>
/// - 마지막에 소환한 소환검부터 해제한다 (전역 포인터 목록의 마지막).<br/>
/// - 날아가는 중이거나 떨어진 소환검도 거리와 상관없이 해제한다. 물리 검, 누수된 소환검, 아직 진행 중인 검 소환의 소환검은 해제하지 않는다.<br/>
/// - 해제할 소환검이 없으면 NoSummonedSwordError. 해제하면 heap 칸이 돌아오고 소환검이 사라진다.
/// </para>
/// </summary>
public class CFreeSword : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.FreeSword;

	// 해제는 사용 가능한 검뿐 아니라 던진 검도 대상이라, 해제할 수 있는 소환검 수(전역 포인터 목록)를 센다
	protected override int CountUsableSummonedSwords(FunctionTask task) => GetGlobalPointers<SummonedSword>(task).Count;

	protected override void OnSkillStart(FunctionTask task)
	{
		SummonedSwords swords = task.Caster.GetComponent<SummonedSwords>();
		List<int> pointers = GetGlobalPointers<SummonedSword>(task);
		if (swords == null || pointers.Count == 0)
		{
			Fail(new NoSummonedSwordError());
			return;
		}

		int pointer = pointers[pointers.Count - 1];
		if (!TryGetHeapObject(task, pointer, out SummonedSword sword)) return;
		if (!Free(task, pointer)) return;
		swords.DespawnSword(sword);
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
