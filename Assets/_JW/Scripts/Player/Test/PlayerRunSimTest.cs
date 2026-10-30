using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 달리기 테스트 (#93). 씬의 아무 오브젝트에 붙이고 Play → Host, Game 창을 한 번 클릭(가짜 키 입력이 들어가게).
/// 콘솔에 「[RunTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 걷기: → 만 누르면 MoveSpeed로 움직이고 IsRunning은 false<br/>
/// 2. 달리기: Shift + → 이면 MoveSpeed × 배율, IsRunning은 true<br/>
/// 3. 달리다 점프하고 공중에서 Shift를 떼도 착지할 때까지 달리는 속도 유지, 착지하면 걷는 속도<br/>
/// 4. 걷다 점프하고 공중에서 Shift를 눌러도 걷는 속도 유지
/// </para>
/// 각 단계 전에 캐릭터를 왼쪽(Start X)으로 옮겨 벽에 막히지 않게 한다. 자동으로 돌기 때문에 평소에는 꺼 둔다.
/// </summary>
public class PlayerRunSimTest : MonoBehaviour
{
	[Tooltip("각 단계를 시작할 x 위치. 오른쪽으로 달려도 벽에 닿지 않는 곳")]
	[SerializeField] float startX = -6f;

	int failures;
	PlayerMovement movement;
	Rigidbody2D body;
	PlayerStats stats;

	IEnumerator Start()
	{
		while (movement == null)
		{
			yield return null;
			if (NetworkClient.localPlayer != null) movement = NetworkClient.localPlayer.GetComponent<PlayerMovement>();
		}
		body = movement.GetComponent<Rigidbody2D>();
		stats = movement.GetComponent<PlayerStats>();
		float walk = stats.MoveSpeed;
		float run = walk * movement.RunSpeedMultiplier;

		// 1. 걷기
		yield return ResetPosition();
		Press(Key.RightArrow);
		yield return new WaitForSeconds(0.2f);
		yield return new WaitForFixedUpdate();
		CheckSpeed("걷기", walk);
		Check("걷기: IsRunning false", !movement.IsRunning);

		// 2. 달리기
		yield return ResetPosition();
		Press(Key.RightArrow, Key.LeftShift);
		yield return new WaitForSeconds(0.2f);
		yield return new WaitForFixedUpdate();
		CheckSpeed("달리기", run);
		Check("달리기: IsRunning true", movement.IsRunning);

		// 3. 달리다 점프 → 공중에서 Shift 뗌 → 착지까지 달리는 속도
		yield return ResetPosition();
		// 짧게 확인한다. 오래 달리면 머리 위 발판 옆면에 막혀 속도가 0이 될 수 있다
		Press(Key.RightArrow, Key.LeftShift);
		yield return new WaitForSeconds(0.05f);
		Press(Key.RightArrow, Key.LeftShift, Key.Space);
		yield return new WaitForSeconds(0.05f);
		Press(Key.RightArrow);
		yield return new WaitForSeconds(0.05f);
		yield return new WaitForFixedUpdate();
		Check("달리다 점프: 공중", !movement.IsGrounded);
		CheckSpeed("달리다 점프, 공중에서 Shift 뗌", run);
		yield return WaitGrounded();
		yield return new WaitForSeconds(0.1f);
		yield return new WaitForFixedUpdate();
		CheckSpeed("착지 후 (Shift 뗀 상태)", walk);

		// 4. 걷다 점프 → 공중에서 Shift 누름 → 걷는 속도 유지
		yield return ResetPosition();
		Press(Key.RightArrow);
		yield return new WaitForSeconds(0.05f);
		Press(Key.RightArrow, Key.Space);
		yield return new WaitForSeconds(0.05f);
		Press(Key.RightArrow, Key.LeftShift);
		yield return new WaitForSeconds(0.05f);
		yield return new WaitForFixedUpdate();
		Check("걷다 점프: 공중", !movement.IsGrounded);
		CheckSpeed("걷다 점프, 공중에서 Shift 누름", walk);

		Press();
		if (failures == 0) Debug.Log("[RunTest] 전체 통과");
		else Debug.LogError($"[RunTest] 실패 {failures}개");
	}

	IEnumerator ResetPosition()
	{
		Press();
		yield return WaitGrounded();
		body.position = new Vector2(startX, body.position.y);
		body.linearVelocity = Vector2.zero;
		yield return new WaitForSeconds(0.2f);
	}

	IEnumerator WaitGrounded()
	{
		float end = Time.time + 3f;
		while (!movement.IsGrounded && Time.time < end) yield return null;
	}

	static void Press(params Key[] keys)
	{
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
	}

	void CheckSpeed(string label, float expected)
	{
		float vx = body.linearVelocity.x;
		Check($"{label}: 속도 {vx:F2} (기대 {expected:F2})", Mathf.Abs(vx - expected) < 0.05f);
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[RunTest] {label}: 통과");
		else { failures++; Debug.LogError($"[RunTest] {label}: 실패"); }
	}
}
