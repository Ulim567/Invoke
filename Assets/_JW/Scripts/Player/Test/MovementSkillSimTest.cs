using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 이동 스킬 테스트 (#256). Test_JW의 MoveSkillTest. 켜고 Play → Host, Game 창을 한 번 클릭 (가짜 키 입력, 약 12초). 캐릭터가 바닥에 서 있고 좌우에 4칸 넘게 빈 공간이 있어야 한다.
/// <para>
/// 1. 대시: 바라보는 쪽으로 3칸 넘게 빠르게 가고, 대시 중 높이가 그대로<br/>
/// 2. 더블점프: 점프 뒤 공중에서 한 번 더 뛰면 다시 올라감, 세 번째는 안 됨, 땅에 닿으면 다시 쓸 수 있음.
/// 더블점프를 해금했으면(또는 PlayerProgress가 없으면) Space로, 아니면 블록코딩 경로(RequestAirJump)로 확인한다. 공용 Player에 PlayerProgress가 붙어 테스트 씬도 해금 목록을 따르기 때문 (#303)<br/>
/// 3. 검 타기: 소환검이 없으면 NoSummonedSwordError. 있으면 검이 타는 중 상태, 떨어지지 않고 → 키로 옆으로 이동, 스킬이 끝나도 계속 탐(30초), 내리면 검이 돌아옴<br/>
/// 4. 위아래 검 타기 (#317): ↑로 올라가고 떼면 그 높이에 멈춤, ↓로 내려가다 아래 바닥과 Ride Clearance만큼 떠서 멈춤(바닥에 선 것이 아님), ↓ + Space로 아래 발판을 뚫음(땅이면 그대로), 빈 슬롯 키를 누른 채 ↑를 누르면 MoveY가 0이고 높이 그대로 (슬롯 8칸이 다 차 있으면 이 항목은 실패로 알림)<br/>
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
		string how = unlocked ? "Space" : "RequestAirJump";
		Debug.Log($"{Tag} 더블점프: 해금 {(unlocked ? "됨 → Space로" : "안 됨(PlayerProgress) → RequestAirJump로")} 확인");
		yield return Keys(0.05f, Key.Space);
		yield return Keys(0.25f);
		float firstVy = body.linearVelocity.y;
		yield return AirJump(unlocked, movement);
		Check($"더블점프({how}): 공중에서 다시 올라감 (속도 {firstVy:0.0} → {body.linearVelocity.y:0.0})", movement.AirJumpUsed && body.linearVelocity.y > firstVy + 1f);
		yield return Keys(0.15f);
		float secondVy = body.linearVelocity.y;
		yield return AirJump(unlocked, movement);
		Check($"더블점프({how}): 세 번째는 안 됨", body.linearVelocity.y <= secondVy + 0.1f);
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

		// 4. 위아래 검 타기 (#317)
		PlayerInputReader input = player.GetComponent<PlayerInputReader>();
		float y0 = player.transform.position.y;
		yield return Keys(0.3f, Key.UpArrow);
		float yUp = player.transform.position.y;
		Check($"위아래: ↑ 로 올라감 (y {y0:0.0} → {yUp:0.0})", yUp - y0 > 1f);
		yield return Keys(0.05f);
		float yStop = player.transform.position.y;
		yield return Keys(0.3f);
		float yHold = player.transform.position.y;
		Check($"위아래: 떼면 그 높이에 멈춤 (y {yStop:0.00} → {yHold:0.00})", Mathf.Abs(yHold - yStop) < 0.1f);
		// ↓ 를 오래 눌러도 아래 바닥(땅·발판)과 Ride Clearance만큼 떠서 멈춘다 (소환검이 바닥에 묻히지 않음)
		yield return Keys(1.2f, Key.DownArrow);
		float yDown = player.transform.position.y;
		float gap = movement.RideGapBelow(10f);
		float clearance = movement.RideClearance;
		Check($"위아래: ↓ 로 내려감 (y {yHold:0.0} → {yDown:0.0})", yHold - yDown > 0.3f || gap < clearance + 0.2f);
		Check($"위아래: ↓ 를 계속 눌러도 바닥 위 Ride Clearance에서 멈춤 (높이 {gap:0.00} / {clearance:0.00})", float.IsInfinity(gap) || Mathf.Abs(gap - clearance) < 0.15f);
		Check("위아래: 바닥에 선 것으로 보지 않음 (걷기 모션 안 나옴)", !movement.IsGrounded);
		yield return Keys(0.05f);

		// ↓ + Space: 아래가 발판이면 뚫고 내려가고, 땅이면 그대로 (평소 발판 내려가기와 같은 키, ↓ 먼저)
		Bounds feet = player.GetComponent<Collider2D>().bounds;
		bool platformBelow = player.scene.GetPhysicsScene2D().Raycast(new Vector2(feet.center.x, feet.min.y), Vector2.down, clearance + 0.3f, LayerMask.GetMask("Platform")).collider != null;
		float yBeforeDrop = player.transform.position.y;
		yield return Keys(0.05f, Key.DownArrow);
		yield return Keys(0.05f, Key.DownArrow, Key.Space);
		yield return Keys(0.6f, Key.DownArrow);
		float yAfterDrop = player.transform.position.y;
		if (platformBelow) Check($"위아래: ↓ + Space 로 아래 발판을 뚫고 내려감 (y {yBeforeDrop:0.0} → {yAfterDrop:0.0})", yBeforeDrop - yAfterDrop > 1f);
		else Check($"위아래: 아래가 땅이면 ↓ + Space 로도 안 내려감 (y {yBeforeDrop:0.00} → {yAfterDrop:0.00})", Mathf.Abs(yBeforeDrop - yAfterDrop) < 0.15f);
		yield return Keys(0.05f);

		// 빈 슬롯을 눌러야 떼는 순간 아무것도 시전되지 않는다
		int emptySlot = FindEmptySlot(player.GetComponent<SlotLoadout>());
		if (emptySlot < 0 || input == null)
		{
			Check("위아래: 슬롯 키를 누르는 동안 멈춤 (빈 슬롯이 없거나 PlayerInputReader가 없어 확인 못 함)", false);
		}
		else
		{
			Key slotKey = SlotKeys[emptySlot];
			yield return Keys(0.1f, slotKey);
			float ySlot = player.transform.position.y;
			yield return Keys(0.3f, slotKey, Key.UpArrow);
			float moveY = input.MoveY;
			float ySlotHold = player.transform.position.y;
			Check($"위아래: 슬롯 키({slotKey})를 누르는 동안 MoveY 0 (실제 {moveY})", moveY == 0f);
			Check($"위아래: 슬롯 키를 누르는 동안 높이 그대로 (y {ySlot:0.00} → {ySlotHold:0.00})", Mathf.Abs(ySlotHold - ySlot) < 0.1f);
			yield return Keys(0.1f);
		}
		Check("위아래: 아직 타는 중", movement.IsRiding);
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

	// 공중 점프 한 번: 해금했으면 Space, 아니면 블록코딩 더블점프와 같은 RequestAirJump
	IEnumerator AirJump(bool withSpace, PlayerMovement movement)
	{
		if (withSpace) yield return Keys(0.05f, Key.Space);
		else movement.RequestAirJump();
		yield return Keys(0.02f);
	}

	// 슬롯 번호(0~7) 순서의 키. InputSystem_Actions의 Slot1~Slot8 (Q W E R A S D F)
	static readonly Key[] SlotKeys = { Key.Q, Key.W, Key.E, Key.R, Key.A, Key.S, Key.D, Key.F };

	static int FindEmptySlot(SlotLoadout loadout)
	{
		if (loadout == null) return -1;
		for (int i = 0; i < SlotLoadout.SlotCount; i++) if (loadout.GetEntry(i).IsEmpty) return i;
		return -1;
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
