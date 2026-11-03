using System.Collections;
using System.IO;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 슬롯 장착(#159) 테스트용 컴포넌트. Test_JW의 LoadoutTest. 켜고 Play → Host (약 8초). 플레이어 주변 8칸 안에 몬스터가 있어야 한다.
/// <para>
/// 1. 장착 API: 스킬·블록 함수 넣기, 넣을 수 없는 것 거부, 같은 것을 다른 슬롯에 넣으면 옮겨짐, 자리 바꾸기, 해제, 코드 영역 크기<br/>
/// 2. 저장: 새 SlotLoadout이 파일에서 같은 내용을 불러옴<br/>
/// 3. 블록 함수 Thrust(GetBug()) 를 W에 넣고 가짜 키로 W를 누른 채 있으면 대상 고르기가 시작되고, 떼면 고른 몬스터가 찔림<br/>
/// 4. 슬롯이 아닌 곳에서 시전한 같은 함수는 가장 가까운 몬스터가 바로 들어가 찔림<br/>
/// 5. while (true) { } 함수를 E로 시전하고 C를 누르면 그 작업이 Cancelled로 끝남<br/>
/// 끝나면 원래 장착 내용으로 되돌리고 테스트용 함수 파일을 지운다. 「[LoadoutTest] 전체 통과」면 통과. 가짜 키를 넣으므로 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class SlotLoadoutSimTest : MonoBehaviour
{
	const string Tag = "[LoadoutTest]";

	int failed;

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		SlotLoadout loadout = player.GetComponent<SlotLoadout>();
		SlotCaster caster = player.GetComponent<SlotCaster>();
		FunctionExecutor executor = player.GetComponent<FunctionExecutor>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		Keyboard keyboard = Keyboard.current;
		if (loadout == null || caster == null || executor == null || effects == null || keyboard == null)
		{
			Debug.LogError($"{Tag} 플레이어에 SlotLoadout, SlotCaster, FunctionExecutor, SkillEffectEvents가 있어야 함 (키보드도)");
			yield break;
		}

		// 원래 장착 내용. 끝나면 되돌린다
		SlotLoadout.Entry[] backup = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < backup.Length; i++) backup[i] = loadout.GetEntry(i);

		// 테스트용 블록 함수 두 개
		FunctionData thrustBug = FunctionData.Create("LoadoutTest_ThrustBug");
		thrustBug.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust,
			SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug))));
		// 끝나지 않는 함수 while (true) { }. 취소 키로만 끝난다
		FunctionData waitLong = FunctionData.Create("LoadoutTest_Loop");
		waitLong.Blocks.Add(BlockData.While(SlotData.FromLiteral(new VariableType(VariableTypeKind.Bool), "true")));
		FunctionStorage.Save(thrustBug);
		FunctionStorage.Save(waitLong);

		// ── 1. 장착 API ──
		for (int i = 0; i < SlotLoadout.SlotCount; i++) loadout.Unequip(i);
		Check("빈 슬롯은 꺼낼 수 없음", !loadout.TryGetFunction(0, out _));

		Check("스킬 넣기", loadout.EquipSkill(0, PredefinedFunctionIds.Thrust));
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.Thrust, out PredefinedFunction thrustOriginal);
		Check("스킬 슬롯은 등록된 원본을 돌려줌", loadout.TryGetFunction(0, out FunctionBlock q) && q == thrustOriginal);
		Check("입력 함수는 거부", !loadout.EquipSkill(3, PredefinedFunctionIds.GetBug));
		Check("없는 스킬은 거부", !loadout.EquipSkill(3, "c.not_a_skill"));
		Check("저장 안 된 함수는 거부", !loadout.EquipFunction(3, System.Guid.NewGuid().ToString()));

		Check("블록 함수 넣기", loadout.EquipFunction(1, thrustBug.Guid));
		bool gotW = loadout.TryGetFunction(1, out FunctionBlock w1);
		loadout.TryGetFunction(1, out FunctionBlock w2);
		Check("블록 함수는 UserFunction, 꺼낼 때마다 같은 원본", gotW && w1 is UserFunction && w1 == w2);

		int sizeBefore = loadout.CodeSize;
		loadout.EquipSkill(5, PredefinedFunctionIds.SummonSword);
		Check("코드 영역 크기가 장착하면 늘어남", loadout.CodeSize > sizeBefore, $"{sizeBefore} → {loadout.CodeSize}");
		loadout.EquipSkill(6, PredefinedFunctionIds.SummonSword);
		Check("같은 스킬을 다른 슬롯에 넣으면 옮겨짐", loadout.GetEntry(5).IsEmpty && loadout.GetEntry(6).Id == PredefinedFunctionIds.SummonSword);
		loadout.Swap(6, 7);
		Check("자리 바꾸기", loadout.GetEntry(6).IsEmpty && loadout.GetEntry(7).Id == PredefinedFunctionIds.SummonSword);
		loadout.Unequip(7);
		Check("해제", loadout.GetEntry(7).IsEmpty && loadout.CodeSize == sizeBefore);

		Check("반복 함수 넣기", loadout.EquipFunction(2, waitLong.Guid));

		// ── 2. 저장 ──
		SlotLoadout reloaded = gameObject.AddComponent<SlotLoadout>();
		bool same = true;
		for (int i = 0; i < SlotLoadout.SlotCount; i++)
		{
			SlotLoadout.Entry a = loadout.GetEntry(i), b = reloaded.GetEntry(i);
			if (a.IsEmpty != b.IsEmpty || (!a.IsEmpty && (a.Kind != b.Kind || a.Id != b.Id))) same = false;
		}
		Check("다시 불러와도 같은 내용", same);
		Destroy(reloaded);

		// 장착이 바뀌면 FunctionExecutorNetwork가 다음 프레임에 서버로 보낸다. 서버가 받기 전에 누르면 예전 슬롯이 시전되므로 기다린다 (#212)
		yield return new WaitForSeconds(0.3f);

		// ── 3. W 누른 채 GetBug 고르기 → 떼면 찌름 ──
		GameObject hitTarget = null;
		effects.Hit += (id, target, point, result) => { if (id == PredefinedFunctionIds.Thrust) hitTarget = target; };

		yield return Keys(0.3f, Key.W);
		Check("W를 누르고 있으면 GetBug 대상 고르기가 시작됨", caster.SelectingSlot == 1, $"SelectingSlot {caster.SelectingSlot}");
		GameObject chosen = caster.CurrentTarget;
		yield return Keys(0.1f);
		yield return WaitHit(() => hitTarget, 3f);
		Check("떼면 고른 몬스터가 찔림", chosen != null && hitTarget == chosen, $"고름 {Name(chosen)}, 맞음 {Name(hitTarget)}");

		// ── 4. 슬롯 밖에서 시전 → 가장 가까운 몬스터가 바로 들어감 ──
		hitTarget = null;
		// 같은 원본은 실행 중이거나 쿨타임이면 거부되므로 끝날 때까지 기다린다
		float until = Time.time + 5f;
		while ((executor.IsRunning(w1) || executor.GetRemainingCooldownTicks(w1) > 0) && Time.time < until) yield return null;
		executor.Cast(w1);
		yield return null;
		bool selectedOutside = caster.SelectingSlot >= 0;
		// 안의 찌르기는 쿨타임이 끝날 때까지 기다릴 수 있다
		yield return WaitHit(() => hitTarget, 4f);
		Check("슬롯 밖 시전은 가장 가까운 몬스터를 바로 찌름", hitTarget != null && !selectedOutside, $"맞음 {Name(hitTarget)}");

		// ── 5. E로 끝나지 않는 함수 시전 → C로 취소 ──
		FunctionTaskEndReason waitEnd = FunctionTaskEndReason.None;
		executor.TaskEnded += task => { if (task.Name == waitLong.Name) waitEnd = task.EndReason; };
		yield return Keys(0.1f, Key.E);
		yield return Keys(0.3f);
		yield return Keys(0.1f, Key.C);
		yield return Keys(0.3f);
		Check("C를 누르면 가장 최근 슬롯 작업이 Cancelled", waitEnd == FunctionTaskEndReason.Cancelled, $"실제 {waitEnd}");

		// 되돌리기
		for (int i = 0; i < SlotLoadout.SlotCount; i++) loadout.Unequip(i);
		for (int i = 0; i < backup.Length; i++)
		{
			if (backup[i].IsEmpty) continue;
			if (backup[i].Kind == SlotLoadout.EntryKind.Skill) loadout.EquipSkill(i, backup[i].Id);
			else loadout.EquipFunction(i, backup[i].Id);
		}
		File.Delete(FunctionStorage.PathOf(thrustBug));
		File.Delete(FunctionStorage.PathOf(waitLong));

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	IEnumerator Keys(float hold, params Key[] keys)
	{
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
		yield return new WaitForSeconds(hold);
	}

	static IEnumerator WaitHit(System.Func<GameObject> hit, float timeout)
	{
		float end = Time.time + timeout;
		while (hit() == null && Time.time < end) yield return null;
	}

	static string Name(GameObject go) => go != null ? go.name : "없음";

	void Check(string label, bool ok, string detail = "")
	{
		if (ok) Debug.Log($"{Tag} {label}: 통과");
		else
		{
			failed++;
			Debug.LogError($"{Tag} {label}: 실패 {detail}");
		}
	}
}
