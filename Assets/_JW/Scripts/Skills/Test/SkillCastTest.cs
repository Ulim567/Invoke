using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// C 스킬 24개 데이터 테스트 (#67). 씬의 아무 오브젝트에 붙이고 Play → Host. 콘솔에 「[SkillTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 표 검사: 표의 모든 줄이 등록된 스킬이고, 이름·틱 수·파라미터를 표에서 읽는지. 등록된 DataDrivenSkill이 모두 표에 있는지.<br/>
/// 2. 시전: 표의 스킬을 하나씩 내 캐릭터의 FunctionExecutor로 시전해서, 표의 틱 수만큼 실행되고 정상으로 끝나는지.<br/>
/// 3. 맨손: 내 캐릭터에 TempSwordState를 잠깐 붙여 맨손으로 만들고, 검 찌르기는 UnarmedError로 끝나고 검 돌아오기는 정상으로 끝나는지. 끝나면 떼어 낸다.
/// </para>
/// 자동으로 돌기 때문에 평소에는 꺼 둔다.
/// </summary>
public class SkillCastTest : MonoBehaviour
{
	int failures;

	IEnumerator Start()
	{
		FunctionExecutor executor = null;
		while (executor == null)
		{
			yield return null;
			if (NetworkClient.localPlayer != null) executor = NetworkClient.localPlayer.GetComponent<FunctionExecutor>();
		}

		SkillTable table = SkillTable.Instance;
		if (table == null)
		{
			Debug.LogError("[SkillTest] SkillTable 에셋이 없음: 실패");
			yield break;
		}

		CheckTable(table);
		foreach (SkillData data in table.Skills)
		{
			yield return CastAndWait(executor, data.Id, FunctionTaskEndReason.Completed, null, data.TickCount);
		}
		yield return CheckUnarmed(executor);

		if (failures == 0) Debug.Log($"[SkillTest] 전체 통과 (스킬 {table.Skills.Count}개)");
		else Debug.LogError($"[SkillTest] 실패 {failures}개");
	}

	void CheckTable(SkillTable table)
	{
		HashSet<string> ids = new HashSet<string>();
		foreach (SkillData data in table.Skills)
		{
			ids.Add(data.Id);
			if (!PredefinedFunctionRegistry.TryGet(data.Id, out PredefinedFunction function) || !(function is DataDrivenSkill))
			{
				Fail($"{data.Id}: 등록된 스킬이 없음");
				continue;
			}
			if (function.Name != data.DisplayName) Fail($"{data.Id}: 이름 {function.Name} ≠ 표 {data.DisplayName}");
			if (function.TickCount != data.TickCount) Fail($"{data.Id}: 틱 수 {function.TickCount} ≠ 표 {data.TickCount}");
			if (function.CooldownTicks != data.CooldownTicks) Fail($"{data.Id}: 쿨타임 {function.CooldownTicks} ≠ 표 {data.CooldownTicks}");

			int expectedParams = data.Targeting == SkillTargeting.Single || data.Targeting == SkillTargeting.SingleMultiHit
				|| data.Targeting == SkillTargeting.Pierce || data.Targeting == SkillTargeting.MultiSelect ? 1 : 0;
			if (function.Parameters.Count != expectedParams) Fail($"{data.Id}: 파라미터 {function.Parameters.Count}개 ≠ {expectedParams}개 ({data.Targeting})");
		}

		foreach (PredefinedFunction function in PredefinedFunctionRegistry.All)
		{
			if (function is DataDrivenSkill && !ids.Contains(function.Id)) Fail($"{function.Id}: 표에 줄이 없음");
		}
		Debug.Log($"[SkillTest] 표 검사 끝 (실패 누적 {failures}개)");
	}

	IEnumerator CheckUnarmed(FunctionExecutor executor)
	{
		// 물리 검 관리가 생기기 전이라 프리팹에 붙이지 않고 테스트 동안만 붙인다
		TempSwordState sword = executor.GetComponent<TempSwordState>();
		bool added = sword == null;
		if (added) sword = executor.gameObject.AddComponent<TempSwordState>();

		sword.SetUnarmed(true);
		yield return CastAndWait(executor, PredefinedFunctionIds.Thrust, FunctionTaskEndReason.Failed, typeof(UnarmedError), 0);
		yield return CastAndWait(executor, PredefinedFunctionIds.SwordReturn, FunctionTaskEndReason.Completed, null, 0);
		sword.SetUnarmed(false);

		if (added) Destroy(sword);
	}

	// expectedTicks가 0이면 실행 틱 수는 검사하지 않는다
	IEnumerator CastAndWait(FunctionExecutor executor, string id, FunctionTaskEndReason expected, System.Type expectedError, int expectedTicks)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function))
		{
			Fail($"{id}: 등록된 스킬이 없음");
			yield break;
		}

		// 틱은 FixedUpdate에서 진행하므로, 프레임마다 확인하면 몇 틱씩 늦게 볼 수 있다. 끝난 순간의 틱은 TaskEnded로 기록한다
		int startTick = executor.CurrentTick;
		int endTick = -1;
		FunctionTask task = null;
		System.Action<FunctionTask> onEnded = ended => { if (ended == task) endTick = executor.CurrentTick; };
		executor.TaskEnded += onEnded;

		task = executor.Cast(function);
		if (task == null)
		{
			executor.TaskEnded -= onEnded;
			Fail($"{id}: 시전 거부됨");
			yield break;
		}

		float timeout = Time.time + 10f;
		while (!task.IsEnded && Time.time < timeout) yield return null;
		executor.TaskEnded -= onEnded;
		int ran = endTick - startTick;

		if (!task.IsEnded) { Fail($"{id}: 10초 안에 끝나지 않음"); yield break; }
		if (task.EndReason != expected) { Fail($"{id}: {task.EndReason}로 끝남 (기대 {expected})"); yield break; }
		if (expectedError != null && (task.Error == null || task.Error.GetType() != expectedError))
		{
			Fail($"{id}: 에러 {task.Error?.GetType().Name ?? "없음"} (기대 {expectedError.Name})");
			yield break;
		}
		// 시전한 다음 틱부터 진행하므로 끝난 틱까지 TickCount 또는 +1틱 차이가 난다
		if (expectedTicks > 0 && (ran < expectedTicks || ran > expectedTicks + 1))
		{
			Fail($"{id}: {ran}틱 실행 (기대 {expectedTicks}틱)");
			yield break;
		}
		Debug.Log($"[SkillTest] {id} ({function.Name}): {task.EndReason}{(task.Error != null ? " " + task.Error.GetType().Name : "")}, {ran}틱 통과");
	}

	void Fail(string message)
	{
		failures++;
		Debug.LogError($"[SkillTest] {message}: 실패");
	}
}
