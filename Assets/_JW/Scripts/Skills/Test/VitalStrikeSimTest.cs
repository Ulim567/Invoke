using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 급소공략 미니게임 테스트 (#161). Test_JW의 VitalTest. 켜고 Play → Host, Game 창을 한 번 클릭 (가짜 키 입력, 약 10초).
/// <para>
/// 슬롯 1(Q)에 급소공략을 넣고(끝나면 되돌림) Q를 누른 채 아래를 확인한다. 몬스터에 StatusEffects가 없으면 붙인다.<br/>
/// 1. Space로 시작 → 미니게임 중 → 를 눌러도 대상이 안 바뀜 → 바늘이 눈 칸일 때 Space → 떼면 3.0배 데미지 + 실명<br/>
/// 2. 발목 칸에서 멈추면 3.0배 + 이속 감소<br/>
/// 3. Space를 안 누르고 떼면 기본 데미지, 상태 이상 없음<br/>
/// 4. 시작하자마자 멈추면(칸 밖) 실패: 기본 데미지, 상태 이상 없음<br/>
/// 「[VitalTest] 전체 통과」면 통과. 가짜 키를 넣으므로 평소에는 꺼 둔다.<br/>
/// 직접 해 보려면 Restore Slot을 끄고 한 번 돌린다. 그 뒤로는 테스트를 꺼도 Q가 급소공략이다.
/// </para>
/// </summary>
public class VitalStrikeSimTest : MonoBehaviour
{
	const string Tag = "[VitalTest]";

	[Tooltip("끄면 테스트가 끝나도 Q에 급소공략을 남겨 둔다 (직접 해 볼 때). 장착 내용은 저장되므로 다음 Play에도 남는다")]
	[SerializeField] bool restoreSlot = true;

	int failed;
	GameObject player;
	SlotCaster caster;
	VitalStrikeMinigame minigame;
	GameObject hitTarget;
	int hitDamage;

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		player = NetworkClient.localPlayer.gameObject;
		caster = player.GetComponent<SlotCaster>();
		minigame = player.GetComponent<VitalStrikeMinigame>();
		SlotLoadout loadout = player.GetComponent<SlotLoadout>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		if (caster == null || minigame == null || loadout == null || effects == null || Keyboard.current == null)
		{
			Debug.LogError($"{Tag} 플레이어에 SlotCaster, VitalStrikeMinigame, SlotLoadout, SkillEffectEvents가 있어야 함");
			yield break;
		}

		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.GetComponent<StatusEffects>() == null) m.gameObject.AddComponent<StatusEffects>();
		}

		effects.Hit += (id, target, point, result) =>
		{
			if (id != PredefinedFunctionIds.VitalStrike) return;
			hitTarget = target;
			hitDamage = result.Damage;
		};

		SlotLoadout.Entry backup = loadout.GetEntry(0);
		loadout.EquipSkill(0, PredefinedFunctionIds.VitalStrike);
		// 장착이 바뀌면 FunctionExecutorNetwork가 다음 프레임에 서버로 보낸다. 서버가 받기 전에 누르면 예전 슬롯이 시전되므로 기다린다 (#212)
		yield return new WaitForSeconds(0.3f);

		yield return Round("눈", VitalStrikeResult.Eye, StatusType.Blind, true);
		yield return Round("발목", VitalStrikeResult.Ankle, StatusType.Slow, false);
		yield return Round("미니게임 안 함", VitalStrikeResult.None, null, false);
		yield return Round("실패", VitalStrikeResult.Miss, null, false);

		if (!restoreSlot) { }
		else if (backup.IsEmpty) loadout.Unequip(0);
		else if (backup.Kind == SlotLoadout.EntryKind.Skill) loadout.EquipSkill(0, backup.Id);
		else loadout.EquipFunction(0, backup.Id);

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	IEnumerator Round(string label, VitalStrikeResult want, StatusType? wantStatus, bool checkLock)
	{
		foreach (StatusEffects s in FindObjectsByType<StatusEffects>()) s.ClearAll();
		hitTarget = null;
		hitDamage = -1;

		yield return Keys(0.2f, Key.Q);
		GameObject chosen = caster.CurrentTarget;
		if (chosen == null) { Check($"{label}: 대상 후보가 있음", false); yield break; }

		VitalStrikeResult got = VitalStrikeResult.None;
		if (want != VitalStrikeResult.None)
		{
			yield return Keys(0.05f, Key.Q, Key.Space);
			yield return Keys(0.02f, Key.Q);
			Check($"{label}: Space로 미니게임 시작", minigame.IsRunning);

			if (checkLock)
			{
				yield return Keys(0.05f, Key.Q, Key.RightArrow);
				yield return Keys(0.02f, Key.Q);
				Check($"{label}: 미니게임 중 → 를 눌러도 대상 고정", caster.CurrentTarget == chosen);
			}

			// 바늘이 원하는 칸 앞쪽에 들어오면 멈춘다 (실패는 칸이 나오기 전에 바로)
			if (want != VitalStrikeResult.Miss)
			{
				float start = want == VitalStrikeResult.Eye ? minigame.EyeZoneStart : minigame.AnkleZoneStart;
				float end = Time.time + 3f;
				while (minigame.IsRunning && minigame.NeedleAngle < start + 2f && Time.time < end) yield return null;
			}
			yield return Keys(0.05f, Key.Q, Key.Space);
			yield return Keys(0.05f, Key.Q);
			got = minigame.PendingResult;
			Check($"{label}: 판정 {want} (실제 {got})", got == want);
		}

		yield return Keys(0.05f);
		float until = Time.time + 2f;
		while (hitTarget == null && Time.time < until) yield return null;

		Monster monster = chosen.GetComponent<Monster>();
		PlayerStats stats = player.GetComponent<PlayerStats>();
		float multiplier = SkillTable.TryGet(PredefinedFunctionIds.VitalStrike, out SkillData data) ? data.DamageMultiplier : 1f;
		bool success = want == VitalStrikeResult.Eye || want == VitalStrikeResult.Ankle;
		DamageRequest request = new DamageRequest(stats.AttackPower, multiplier) { BonusRate = success ? DamageBonus.VitalStrikeMinigame : 0f };
		int expected = DamageCalculator.Calculate(request, DamageTarget.FromMonster(monster)).Damage;
		Check($"{label}: 고른 대상이 맞음, 데미지 {hitDamage} (기대 {expected})", hitTarget == chosen && hitDamage == expected);

		StatusEffects status = chosen.GetComponent<StatusEffects>();
		bool blind = status.Has(StatusType.Blind), slow = status.Has(StatusType.Slow);
		bool statusOk = wantStatus == StatusType.Blind ? blind && !slow
			: wantStatus == StatusType.Slow ? slow && !blind
			: !blind && !slow;
		Check($"{label}: 상태 이상 {(wantStatus.HasValue ? wantStatus.ToString() : "없음")} (실명 {blind}, 이속 감소 {slow})", statusOk);
		Check($"{label}: 결과는 한 번 쓰면 지워짐", minigame.PendingResult == VitalStrikeResult.None);

		yield return new WaitForSeconds(0.5f);
	}

	IEnumerator Keys(float hold, params Key[] keys)
	{
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
		yield return new WaitForSeconds(hold);
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"{Tag} {label}: 통과");
		else
		{
			failed++;
			Debug.LogError($"{Tag} {label}: 실패");
		}
	}
}
