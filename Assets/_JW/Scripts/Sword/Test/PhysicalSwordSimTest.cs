using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 물리 검 테스트 (#69). 씬의 아무 오브젝트에 붙이고 Play → Host. 콘솔에 「[SwordTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 빗나감: 대상 없이 던지면 바로 떨어져 땅에 멈추고 맨손이 된다.<br/>
/// 2. 맨손 스킬: 검 찌르기는 UnarmedError, 검 돌아오기는 정상으로 끝난다.<br/>
/// 3. 줍기: 가짜 Z 키를 넣으면 다시 손에 든다 (TempPickUpInput).<br/>
/// 4. 맞음: 가장 가까운 몬스터에게 던지면 Hit가 불리고 떨어진다. Recall(검 돌아오기)로 다시 손에 든다.
/// </para>
/// 가짜 키 입력은 Game 창에 초점이 있어야 들어간다. 자동으로 돌기 때문에 평소에는 꺼 둔다.
/// </summary>
public class PhysicalSwordSimTest : MonoBehaviour
{
	int failures;

	IEnumerator Start()
	{
		PhysicalSword sword = null;
		while (sword == null)
		{
			yield return null;
			if (NetworkClient.localPlayer != null) sword = NetworkClient.localPlayer.GetComponent<PhysicalSword>();
		}
		FunctionExecutor executor = sword.GetComponent<FunctionExecutor>();
		yield return new WaitForSeconds(1f); // 착지 대기

		// 1. 빗나감
		Check("처음에는 손에 있음", sword.State == SwordState.InHand);
		Check("대상 없이 던지기", sword.Throw(null));
		Check("던지면 맨손", sword.IsUnarmed);
		Check("손에 없으면 다시 못 던짐", !sword.Throw(null));
		yield return WaitFor(() => sword.State == SwordState.OnGround, 3f);
		Check("빗나가면 땅에 떨어짐", sword.State == SwordState.OnGround);

		// 2. 맨손 스킬
		if (executor != null)
		{
			yield return CastAndCheck(executor, PredefinedFunctionIds.Thrust, FunctionTaskEndReason.Failed);
			yield return CastAndCheck(executor, PredefinedFunctionIds.Dash, FunctionTaskEndReason.Completed);
		}

		// 3. 줍기 (가짜 Z 키)
		Check("떨어진 검 근처라 주울 수 있음", sword.CanPickUp);
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Z));
		yield return null;
		yield return null;
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
		yield return WaitFor(() => sword.State == SwordState.InHand, 1f);
		Check("Z로 주우면 다시 손에 듦", sword.State == SwordState.InHand && sword.Dropped == null);

		// 4. 맞음 + 검 돌아오기
		Monster monster = NearestMonster(sword.transform.position);
		if (monster == null)
		{
			Debug.Log("[SwordTest] 몬스터가 없어 맞음 검사 건너뜀");
		}
		else
		{
			GameObject hit = null;
			System.Action<GameObject> onHit = target => hit = target;
			sword.Hit += onHit;
			Check($"{monster.name}에게 던지기", sword.Throw(monster.gameObject));
			yield return WaitFor(() => sword.State == SwordState.OnGround, 3f);
			sword.Hit -= onHit;
			Check("대상에 맞으면 Hit", hit == monster.gameObject);
			Check("맞은 뒤 땅에 떨어짐", sword.State == SwordState.OnGround);
			Check("떨어진 자리는 몬스터 근처", sword.Dropped != null && Mathf.Abs(sword.Dropped.transform.position.x - monster.transform.position.x) < 1.5f);
			Check("검 돌아오기로 회수", sword.Recall() && sword.State == SwordState.InHand);
		}

		if (failures == 0) Debug.Log("[SwordTest] 전체 통과");
		else Debug.LogError($"[SwordTest] 실패 {failures}개");
	}

	IEnumerator CastAndCheck(FunctionExecutor executor, string id, FunctionTaskEndReason expected)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); yield break; }
		FunctionTask task = executor.Cast(function);
		if (task == null) { Check($"{id} 시전", false); yield break; }
		yield return WaitFor(() => task.IsEnded, 5f);
		Check($"맨손 {function.Name}: {expected}", task.IsEnded && task.EndReason == expected);
	}

	static IEnumerator WaitFor(System.Func<bool> condition, float timeout)
	{
		float end = Time.time + timeout;
		while (!condition() && Time.time < end) yield return null;
	}

	static Monster NearestMonster(Vector3 from)
	{
		Monster nearest = null;
		float best = float.MaxValue;
		foreach (Monster monster in FindObjectsByType<Monster>())
		{
			if (monster.IsDead) continue;
			float distance = (monster.transform.position - from).sqrMagnitude;
			if (distance < best) { best = distance; nearest = monster; }
		}
		return nearest;
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[SwordTest] {label}: 통과");
		else { failures++; Debug.LogError($"[SwordTest] {label}: 실패"); }
	}
}
