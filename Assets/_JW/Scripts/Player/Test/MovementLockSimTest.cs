using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 움직임 잠그기 테스트 (#207). Test_JW의 LockTest. 켜고 Play → Host, Game 창을 한 번 클릭 (가짜 키 입력, 약 4초).
/// <para>
/// 1. 캐릭터를 3칸 위로 옮긴 뒤 잠그면 0.5초 동안 떨어지지 않음<br/>
/// 2. 잠근 동안 → + Space를 눌러도, StartLunge를 불러도 움직이지 않음<br/>
/// 3. 풀면 다시 떨어짐<br/>
/// 4. 돌진 중에 잠그면 돌진이 멈춤<br/>
/// 「[LockTest] 전체 통과」면 통과. 가짜 키를 넣으므로 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class MovementLockSimTest : MonoBehaviour
{
	const string Tag = "[LockTest]";

	int failed;

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		PlayerMovement movement = player.GetComponent<PlayerMovement>();
		if (movement == null || Keyboard.current == null)
		{
			Debug.LogError($"{Tag} 플레이어에 PlayerMovement가 있어야 함 (키보드도)");
			yield break;
		}

		// 1. 공중에서 잠그기 (테스트라서 위치를 직접 옮긴다)
		player.transform.position += Vector3.up * 3f;
		yield return new WaitForFixedUpdate();
		movement.SetMovementLocked(true);
		Vector2 locked = player.transform.position;
		yield return new WaitForSeconds(0.5f);
		Check("잠그면 공중에서 떨어지지 않음", movement.IsMovementLocked && Vector2.Distance(player.transform.position, locked) < 0.05f,
			$"{locked} → {(Vector2)player.transform.position}");

		// 2. 잠근 동안 입력·돌진 무시
		yield return Keys(0.3f, Key.RightArrow, Key.Space);
		yield return Keys(0.05f);
		movement.StartLunge(player.transform.position.x + 5f);
		yield return new WaitForSeconds(0.3f);
		Check("잠근 동안 → + Space, 돌진으로 안 움직임", !movement.IsLunging && Vector2.Distance(player.transform.position, locked) < 0.05f,
			$"{locked} → {(Vector2)player.transform.position}");

		// 3. 풀면 떨어짐
		movement.SetMovementLocked(false);
		yield return new WaitForSeconds(0.3f);
		Check("풀면 다시 떨어짐", !movement.IsMovementLocked && player.transform.position.y < locked.y - 0.2f,
			$"y {locked.y} → {player.transform.position.y}");
		yield return new WaitForSeconds(1f);

		// 4. 돌진 중에 잠그기
		movement.StartLunge(player.transform.position.x + 5f);
		yield return new WaitForFixedUpdate();
		bool wasLunging = movement.IsLunging;
		movement.SetMovementLocked(true);
		Vector2 at = player.transform.position;
		yield return new WaitForSeconds(0.3f);
		Check("돌진 중에 잠그면 돌진이 멈추고 제자리", wasLunging && !movement.IsLunging && Vector2.Distance(player.transform.position, at) < 0.05f,
			$"돌진 중이었음 {wasLunging}, {at} → {(Vector2)player.transform.position}");
		movement.SetMovementLocked(false);

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	IEnumerator Keys(float hold, params Key[] keys)
	{
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
		yield return new WaitForSeconds(hold);
	}

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
