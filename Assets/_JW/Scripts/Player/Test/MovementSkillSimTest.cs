using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 이동 스킬 테스트 (#256). Test_JW의 MoveSkillTest. 켜고 Play → Host, Game 창을 한 번 클릭 (가짜 키 입력, 약 8초). 캐릭터가 바닥에 서 있고 좌우에 4칸 넘게 빈 공간이 있어야 한다.
/// <para>
/// 1. 대시: 바라보는 쪽으로 3칸 넘게 빠르게 가고, 대시 중 높이가 그대로<br/>
/// 2. 더블점프: 점프 뒤 공중에서 Space를 한 번 더 누르면 다시 올라감, 세 번째는 안 됨, 땅에 닿으면 다시 쓸 수 있음 (PlayerProgress가 없으면 Allow Double Jump Without Progress를 따름)<br/>
/// 3. 검 타기: 소환검이 없으면 NoSummonedSwordError. 있으면 검이 타는 중 상태, 떨어지지 않고 → 키로 옆으로 이동, 스킬이 끝나도 계속 탐(30초), 내리면 검이 돌아옴<br/>
/// 「[MoveSkillTest] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class MovementSkillSimTest : MonoBehaviour
{
	const string Tag = "[MoveSkillTest]";

	int failed;
	FunctionExecutor executor;

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		executor = player.GetComponent<FunctionExecutor>();
		PlayerMovement movement = player.GetComponent<PlayerMovement>();
		SummonedSwords swords = player.GetComponent<SummonedSwords>();
		Rigidbody2D body = player.GetComponent<Rigidbody2D>();
		if (executor == null || movement == null || swords == null || body == null || Keyboard.current == null)
		{
			Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, PlayerMovement, SummonedSwords, Rigidbody2D가 있어야 함 (키보드도)");
			yield break;
		}

		// 1. 대시
		yield return WaitUntil(() => movement.IsGrounded, 2f);
		Vector2 before = player.transform.position;
		int facing = movement.Facing;
		FunctionTask dash = CastTask(PredefinedFunctionIds.Dash);
		yield return WaitUntil(() => movement.IsDashing, 1f);
		Check("대시: 대시 중", movement.IsDashing);
		float midY = player.transform.position.y;
		yield return WaitUntil(() => !movement.IsDashing, 1f);
		Vector2 after = player.transform.position;
		Check($"대시: 바라보는 쪽으로 3칸 넘게 ({(after.x - before.x) * facing:0.0}칸)", (after.x - before.x) * facing > 3f);
		Check($"대시: 대시 중 높이 그대로 ({before.y:0.0} → {midY:0.0})", Mathf.Abs(midY - before.y) < 0.3f);
		yield return WaitUntil(() => dash == null || dash.IsEnded, 3f);
		Check($"대시: Completed (실제 {dash?.EndReason})", dash != null && dash.EndReason == FunctionTaskEndReason.Completed);

		// 2. 더블점프
		yield return WaitUntil(() => movement.IsGrounded, 2f);
		bool unlocked = movement.DoubleJumpUnlocked;
		Check("더블점프: 해금됨 (테스트 씬은 Allow Double Jump Without Progress)", unlocked);
		yield return Keys(0.05f, Key.Space);
		yield return Keys(0.25f);
		float firstVy = body.linearVelocity.y;
		yield return Keys(0.05f, Key.Space);
		yield return Keys(0.02f);
		Check($"더블점프: 공중에서 다시 올라감 (속도 {firstVy:0.0} → {body.linearVelocity.y:0.0})", movement.AirJumpUsed && body.linearVelocity.y > firstVy + 1f);
		yield return Keys(0.15f);
		float secondVy = body.linearVelocity.y;
		yield return Keys(0.05f, Key.Space);
		yield return Keys(0.02f);
		Check("더블점프: 세 번째는 안 됨", body.linearVelocity.y <= secondVy + 0.1f);
		yield return WaitUntil(() => movement.IsGrounded, 3f);
		yield return null;
		Check("더블점프: 땅에 닿으면 다시 쓸 수 있음", !movement.AirJumpUsed);

		// 3. 검 타기
		int guard = 8;
		while (swords.Swords.Count > 0 && guard-- > 0) yield return Cast(PredefinedFunctionIds.FreeSword);
		FunctionTask noSword = CastTask(PredefinedFunctionIds.SwordRide);
		yield return WaitUntil(() => noSword == null || noSword.IsEnded, 3f);
		Check("검 타기: 소환검이 없으면 NoSummonedSwordError", noSword != null && noSword.IsFailed && noSword.Error is NoSummonedSwordError);

		yield return Cast(PredefinedFunctionIds.SummonSword);
		yield return new WaitForSeconds(0.3f);
		player.transform.position += Vector3.up * 2f;
		yield return new WaitForFixedUpdate();
		FunctionTask ride = CastTask(PredefinedFunctionIds.SwordRide);
		yield return WaitUntil(() => movement.IsRiding, 1f);
		Check("검 타기: 타는 중", movement.IsRiding);
		bool swordRiding = false;
		foreach (SummonedSword s in swords.Swords) if (s != null && s.State == SummonedSwordState.Riding) swordRiding = true;
		Check("검 타기: 소환검이 타는 중 상태", swordRiding);
		Vector2 rideStart = player.transform.position;
		yield return Keys(0.3f, Key.RightArrow);
		yield return Keys(0.02f);
		Vector2 rideMid = player.transform.position;
		Check($"검 타기: 떨어지지 않음 (y {rideStart.y:0.0} → {rideMid.y:0.0})", Mathf.Abs(rideMid.y - rideStart.y) < 0.3f);
		Check($"검 타기: → 로 옆으로 이동 ({rideMid.x - rideStart.x:0.0}칸)", rideMid.x - rideStart.x > 1f);
		yield return WaitUntil(() => ride == null || ride.IsEnded, 3f);
		Check($"검 타기: Completed (실제 {ride?.EndReason})", ride != null && ride.EndReason == FunctionTaskEndReason.Completed);
		Check($"검 타기: 스킬이 끝나도 계속 탐 (남은 시간 {movement.RideRemaining:0.0}초 / {movement.RideSeconds}초)", movement.IsRiding && movement.RideRemaining > 0f);
		// 다 기다리면 오래 걸리므로 테스트에서 직접 내린다
		movement.StopRide();
		foreach (SummonedSword s in swords.Swords) swords.EndRide(s);
		yield return null;
		Check("검 타기: 내리면 타는 중이 아님", !movement.IsRiding);
		yield return WaitUntil(() => swords.ReadyCount == swords.Swords.Count, 2f);
		Check("검 타기: 검이 곁으로 돌아옴", swords.ReadyCount == swords.Swords.Count && swords.Swords.Count > 0);
		yield return Cast(PredefinedFunctionIds.FreeSword);

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	IEnumerator Keys(float hold, params Key[] keys)
	{
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
		yield return new WaitForSeconds(hold);
	}

	FunctionTask CastTask(string id)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); return null; }
		FunctionTask task = executor.Cast(function);
		if (task == null) Check($"{id} 시전", false);
		return task;
	}

	IEnumerator Cast(string id)
	{
		FunctionTask task = CastTask(id);
		if (task == null) yield break;
		yield return WaitUntil(() => task.IsEnded, 3f);
	}

	static IEnumerator WaitUntil(System.Func<bool> done, float timeout)
	{
		float end = Time.time + timeout;
		while (!done() && Time.time < end) yield return null;
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
