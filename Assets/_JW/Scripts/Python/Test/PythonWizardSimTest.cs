using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 파이썬 마법사 테스트 (#551). Test_JW의 PythonWizardTest. 실제 파이썬 플레이어 프리팹(Player_Python)으로 Host에서만 볼 수 있는 것을 확인한다.
/// <para>
/// 준비: Test_JW의 NetworkManager에서 Player Prefab을 Player_Python으로 잠깐 바꾸고(씬은 저장하지 않음), 이 오브젝트를 켜고 Play → Host (약 12초).
/// 근처(12칸 안)에 StatusEffects가 붙은 몬스터가 있어야 구슬 발사·중독 항목을 본다 (Test_JW의 TestMonster).<br/>
/// 1. 캐릭터: 내 캐릭터 언어가 Python, heap 가비지 컬렉션이 켜짐<br/>
/// 2. 구슬 그림: 전역 리스트에 구슬 2개를 담으면 구슬 2개, 머리 위 구슬 그림 2개 (HUD가 있으면 「마나 구슬 2/5」).
/// 2개인 이유: 레벨 1 마법사의 메모리(10칸)로는 리스트와 구슬(하나에 2칸) 3개를 담지 못한다<br/>
/// 3. 구슬 발사: 슬롯에서처럼 구슬 없이 바로 시전하면 먼저 만든 구슬을 써서 몬스터를 맞히고, 그 구슬은 다 쓴 구슬<br/>
/// 4. 구슬 보호막: 켜진 뒤 첫 피해는 막고(체력 그대로) 꺼짐, 다음 피해는 들어감<br/>
/// 5. 중독: 3번 걸면 3겹, 4번째도 3겹<br/>
/// 6. 공중 부양: 떠 있고 타이머 전체 시간 6초, 끝나면 내려옴. 경직 중에는 떠 있던 것이 끝나므로 보호막 테스트의 경직이 풀린 뒤 시전한다<br/>
/// 구슬 만들기·다 쓴 구슬·None·NoManaOrbError 같은 규칙은 이우림 PythonOrbTest(#532, 네트워크 없이)가 본다. 자세는 0으로 두고 본다 (자세가 데미지·시간을 바꾸므로).
/// 「[PythonTest] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class PythonWizardSimTest : MonoBehaviour
{
	const string Tag = "[PythonTest]";

	int failed;
	FunctionExecutor executor;
	ProcessMemory memory;
	ManaOrbs orbs;
	FunctionTask lastEnded;
	readonly HashSet<string> waiting = new HashSet<string>();

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		executor = player.GetComponent<FunctionExecutor>();
		memory = player.GetComponent<ProcessMemory>();
		orbs = player.GetComponent<ManaOrbs>();
		ManaOrbView orbView = player.GetComponent<ManaOrbView>();
		OrbShield shield = player.GetComponent<OrbShield>();
		PlayerHealth health = player.GetComponent<PlayerHealth>();
		PlayerMovement movement = player.GetComponent<PlayerMovement>();

		// 1. 캐릭터
		bool python = PlayerCharacter.LanguageOf(player) == CharacterLanguages.Python;
		Check("캐릭터: 내 캐릭터 언어가 Python", python, "Test_JW의 NetworkManager에서 Player Prefab을 Player_Python으로 바꾸고 Play");
		if (!python || executor == null || memory == null || orbs == null || orbView == null || shield == null || health == null || movement == null)
		{
			if (python) Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, ProcessMemory, ManaOrbs, ManaOrbView, OrbShield, PlayerHealth, PlayerMovement가 있어야 함");
			Finish();
			yield break;
		}
		Check("캐릭터: heap 가비지 컬렉션 켜짐", memory.Heap.CollectsGarbage);
		executor.TaskEnded += OnTaskEnded;
		// 자세가 데미지·시간을 바꾸지 않게 0으로 둔다
		if (player.TryGetComponent(out PlayerStance stance)) stance.Clear();

		// 2. 구슬 그림: keep = [] 로 시작해서 구슬 2개를 담는다 (레벨 1 메모리 10칸에는 리스트와 구슬 3개가 들어가지 않는다)
		GlobalVariablesData globals = new GlobalVariablesData();
		globals.Declarations.Add(BlockData.ListNew("keep"));
		executor.SetGlobalVariablesFile(globals);
		executor.RestartProcess();
		yield return null;
		FunctionData keep = FunctionData.Create("PyTestKeep");
		for (int i = 0; i < 2; i++)
			keep.Blocks.Add(BlockData.ListAppend("keep", SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.PyOrbCreate))));
		yield return RunUser(keep);
		Check("구슬 그림: 담은 구슬 2개", lastEnded != null && lastEnded.EndReason == FunctionTaskEndReason.Completed && orbs.Count == 2,
			$"실제 {lastEnded?.EndReason}, 구슬 {orbs.Count}");
		yield return null;
		Check("구슬 그림: 머리 위 구슬 그림 2개", orbView.ShownCount == 2, $"실제 {orbView.ShownCount}");
		yield return CheckHud(2);

		// 3. 구슬 발사: 구슬 값 없이 바로 시전 → 먼저 만든 구슬을 쓴다
		Monster target = NearestMonster(player, MagicBolts.TryGet(PredefinedFunctionIds.PyOrbShot, out MagicBoltSpec shotSpec) ? shotSpec.Range : 12f);
		if (target == null) Debug.LogWarning($"{Tag} 구슬 발사·중독: 12칸 안에 살아 있는 몬스터가 없어 건너뜀");
		else
		{
			int hpBefore = target.Hp;
			FunctionTask shot = Cast(PredefinedFunctionIds.PyOrbShot, RuntimeValue.FromBug(target));
			yield return WaitEnded(shot, 3f);
			Check("구슬 발사: Completed", shot != null && shot.EndReason == FunctionTaskEndReason.Completed, $"실제 {shot?.EndReason}, {shot?.Error?.GetType().Name}");
			Check("구슬 발사: 쓴 구슬은 다 쓴 구슬 (쓸 수 있는 구슬 1개, 다 쓴 구슬 1개)", orbs.Count == 1 && orbs.SpentCount == 1,
				$"실제 쓸 수 있음 {orbs.Count}, 다 씀 {orbs.SpentCount}");
			Check($"구슬 발사: 몬스터가 맞음 (체력 {hpBefore} → {target.Hp})", target.Hp < hpBefore || target.IsDead);
		}

		// 4. 구슬 보호막
		yield return new WaitUntil(() => !health.IsInvincible);
		FunctionTask shieldTask = Cast(PredefinedFunctionIds.PyOrbShield);
		yield return WaitEnded(shieldTask, 3f);
		Check("구슬 보호막: 켜짐", shield.IsActive);
		int hp = health.Hp;
		health.TakeDamage(5);
		Check($"구슬 보호막: 첫 피해를 막음 (체력 {hp} → {health.Hp})", health.Hp == hp);
		Check("구슬 보호막: 막은 뒤 꺼짐", !shield.IsActive);
		health.TakeDamage(5);
		Check($"구슬 보호막: 다음 피해는 들어감 (체력 {hp} → {health.Hp})", health.Hp < hp);
		health.RestoreFullHp();

		// 5. 중독
		if (target != null && !target.IsDead && target.TryGetComponent(out StatusEffects status))
		{
			status.Clear(StatusType.Poison);
			status.Apply(StatusType.Poison);
			status.Apply(StatusType.Poison);
			status.Apply(StatusType.Poison);
			Check("중독: 3번 걸면 3겹", status.PoisonStacks == 3, $"실제 {status.PoisonStacks}");
			status.Apply(StatusType.Poison);
			Check("중독: 4번째도 3겹", status.PoisonStacks == StatusDefaults.PoisonMaxStacks, $"실제 {status.PoisonStacks}");
			status.Clear(StatusType.Poison);
		}
		else if (target != null) Debug.LogWarning($"{Tag} 중독: 몬스터에 StatusEffects가 없어 건너뜀");

		// 6. 공중 부양: 보호막 테스트에서 받은 피해의 경직이 풀린 뒤 (경직 중에는 떠 있던 것이 끝난다)
		float stunEnd = Time.time + 3f;
		while (!health.CanAct && Time.time < stunEnd) yield return null;
		FunctionTask levitate = Cast(PredefinedFunctionIds.PyLevitate);
		yield return WaitEnded(levitate, 2f);
		Check("공중 부양: 떠 있음", movement.IsRiding);
		Check($"공중 부양: 타이머 전체 시간 {PyLevitate.LevitateSeconds}초 (실제 {movement.RideDuration:0.0}초)", Mathf.Abs(movement.RideDuration - PyLevitate.LevitateSeconds) < 0.01f);
		yield return new WaitForSeconds(PyLevitate.LevitateSeconds + 0.5f);
		Check("공중 부양: 시간이 다 되면 내려옴", !movement.IsRiding);

		// 정리: 담아 둔 구슬과 전역 리스트를 비운다
		executor.SetGlobalVariablesFile(new GlobalVariablesData());
		executor.RestartProcess();
		executor.TaskEnded -= OnTaskEnded;
		Finish();
	}

	IEnumerator CheckHud(int count)
	{
		HudView hud = FindAnyObjectByType<HudView>();
		if (hud == null)
		{
			Debug.Log($"{Tag} 구슬 그림: HUD가 없어 HUD 숫자는 건너뜀");
			yield break;
		}
		yield return null;
		string expected = $"마나 구슬 {count}/{ManaOrbs.MaxCount}";
		bool found = false;
		foreach (TMPro.TMP_Text text in hud.GetComponentsInChildren<TMPro.TMP_Text>(true))
			if (text.text == expected) found = true;
		Check($"구슬 그림: HUD 「{expected}」", found);
	}

	FunctionTask Cast(string id, params RuntimeValue[] arguments)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); return null; }
		FunctionTask task = executor.Cast(function, arguments);
		if (task == null) Check($"{id} 시전", false);
		return task;
	}

	IEnumerator RunUser(FunctionData data)
	{
		lastEnded = null;
		if (!FunctionDataConverter.TryConvert(data, Array.Empty<FunctionData>(), LanguageFamily.Python, out UserFunction function))
		{
			Check($"{data.Name}: 변환", false);
			yield break;
		}
		waiting.Add(data.Name);
		if (executor.Cast(function) == null)
		{
			waiting.Remove(data.Name);
			Check($"{data.Name}: 시전", false);
			yield break;
		}
		float end = Time.time + 3f;
		while (waiting.Contains(data.Name) && Time.time < end) yield return null;
	}

	void OnTaskEnded(FunctionTask task)
	{
		if (waiting.Remove(task.Name)) lastEnded = task;
	}

	static IEnumerator WaitEnded(FunctionTask task, float timeout)
	{
		float end = Time.time + timeout;
		while (task != null && !task.IsEnded && Time.time < end) yield return null;
	}

	static Monster NearestMonster(GameObject player, float range)
	{
		Monster best = null;
		float bestDist = range;
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m == null || m.IsDead || m.gameObject.scene != player.scene) continue;
			float dist = Vector2.Distance(m.transform.position, player.transform.position);
			if (dist < bestDist) { bestDist = dist; best = m; }
		}
		return best;
	}

	void Finish()
	{
		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	void Check(string label, bool ok, string detail = null)
	{
		string message = $"{Tag} {label}: {(ok ? "통과" : "실패")}";
		if (!ok && detail != null) message += $" ({detail})";
		if (ok) Debug.Log(message);
		else
		{
			failed++;
			Debug.LogError(message);
		}
	}
}
