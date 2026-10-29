using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 발판 내려가기(↓ + Space)를 가짜 키 입력으로 확인하는 테스트용 컴포넌트. 씬의 아무 오브젝트에 붙이고 Play → Host.
/// <para>
/// 내 캐릭터가 발판(Platform) 위에 1초 서 있으면 자동으로 ↓ + Space를 넣고, 0.8초 뒤 발판보다 아래에 있고<br/>
/// 2초 뒤 발판 충돌이 다시 켜져 있으면(발판 속에 끼지 않았으면) 「통과」.<br/>
/// 땅(Ground) 위에 1초 서 있으면 ↓ + Space를 넣고, 높이가 거의 그대로면 「통과」(땅에서는 내려가지 않음).<br/>
/// 각 경우를 한 번씩만 확인한다. 자동으로 돌기 때문에, 손으로 테스트할 때는 이 오브젝트를 꺼 둔다.
/// </para>
/// </summary>
public class PlayerDropInputSimTest : MonoBehaviour
{
	[SerializeField] LayerMask platformLayers;
	[SerializeField] LayerMask groundOnlyLayers;

	bool platformChecked, groundChecked, running;
	float standStart = -1f;
	int standingOn; // 0 없음, 1 발판, 2 땅

	void Reset()
	{
		platformLayers = LayerMask.GetMask("Platform");
		groundOnlyLayers = LayerMask.GetMask("Ground");
	}

	void Update()
	{
		if (running || (platformChecked && groundChecked)) return;
		NetworkIdentity player = NetworkClient.localPlayer;
		if (player == null) return;

		PlayerMovement movement = player.GetComponent<PlayerMovement>();
		Collider2D body = player.GetComponent<Collider2D>();
		if (movement == null || body == null || !movement.IsGrounded) { standStart = -1f; return; }

		Vector2 feet = new Vector2(body.bounds.center.x, body.bounds.min.y);
		Vector2 size = new Vector2(body.bounds.size.x * 0.9f, 0.1f);
		int now = Physics2D.OverlapBox(feet, size, 0f, groundOnlyLayers) ? 2
			: Physics2D.OverlapBox(feet, size, 0f, platformLayers) ? 1 : 0;

		if (now != standingOn) { standingOn = now; standStart = Time.time; return; }
		if (now == 0 || Time.time - standStart < 1f) return;
		if (now == 1 && !platformChecked) StartCoroutine(Check(movement, true));
		if (now == 2 && !groundChecked) StartCoroutine(Check(movement, false));
	}

	IEnumerator Check(PlayerMovement movement, bool onPlatform)
	{
		running = true;
		Transform player = movement.transform;
		float startY = player.position.y;
		Keyboard keyboard = Keyboard.current;

		Debug.Log($"[DropSim] {(onPlatform ? "발판" : "땅")} 위에서 ↓ + Space (y={startY:F2})");
		InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
		yield return null;
		InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow, Key.Space));
		yield return new WaitForSeconds(0.1f);
		InputSystem.QueueStateEvent(keyboard, new KeyboardState());
		yield return new WaitForSeconds(0.8f);

		float endY = player.position.y;
		bool ok = onPlatform ? endY < startY - 0.5f : Mathf.Abs(endY - startY) < 0.2f;
		if (ok && onPlatform)
		{
			// 내려간 뒤 충돌이 다시 켜졌는지 (발판 속에 끼어 있으면 계속 IsDropping)
			yield return new WaitForSeconds(1.2f);
			ok = !movement.IsDropping;
			if (!ok) Debug.LogError("[DropSim] 1.2초가 더 지나도 발판 충돌이 다시 켜지지 않음");
		}
		string msg = $"[DropSim] {(onPlatform ? "발판에서 내려감" : "땅에서는 안 내려감")}: {(ok ? "통과" : "실패")} (y {startY:F2} → {endY:F2})";
		if (ok) Debug.Log(msg); else Debug.LogError(msg);

		if (onPlatform) platformChecked = true; else groundChecked = true;
		standingOn = 0;
		running = false;
	}
}
