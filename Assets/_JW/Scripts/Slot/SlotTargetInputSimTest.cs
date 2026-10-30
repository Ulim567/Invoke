using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 슬롯 대상 고르기를 가짜 키 입력으로 확인하는 테스트용 컴포넌트. 씬의 아무 오브젝트에 붙이고 Play → Host.
/// 2초 뒤 자동으로 Q 누름 → Q+→ → Q+→ → Q+← → 모두 뗌 순서로 키보드 상태를 넣고, SlotCaster 이벤트를 로그로 남긴다.
/// <para>
/// 슬롯 1(Q)에 대상이 필요한 함수(c.thrust)가 들어 있어야 한다. 「통과」가 뜨면 ← →가 대상 넘기기로 들어간 것이다.<br/>
/// 자동으로 돌기 때문에, 손으로 테스트할 때는 이 오브젝트를 꺼 둔다.
/// </para>
/// </summary>
public class SlotTargetInputSimTest : MonoBehaviour
{
	[SerializeField, Min(0.05f)] float stepDelay = 0.25f;

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(2f);

		SlotCaster caster = NetworkClient.localPlayer.GetComponent<SlotCaster>();
		PlayerInputReader input = NetworkClient.localPlayer.GetComponent<PlayerInputReader>();
		Keyboard keyboard = Keyboard.current;
		if (caster == null || keyboard == null)
		{
			Debug.LogError("[SlotSim] SlotCaster 또는 키보드가 없음");
			yield break;
		}

		int changes = 0;
		caster.TargetChanged += t => { changes++; Debug.Log($"[SlotSim] TargetChanged → {(t != null ? t.name : "null")}"); };
		caster.CastRequested += (slot, f, targets) => Debug.Log($"[SlotSim] CastRequested 슬롯 {slot + 1} {f.Name} → {targets.Count}명");

		yield return Press(keyboard, "Q", Key.Q);
		yield return Press(keyboard, "Q+→", Key.Q, Key.RightArrow);
		Debug.Log($"[SlotSim]   HeldSlot={input.HeldSlot}");
		yield return Press(keyboard, "Q", Key.Q);
		yield return Press(keyboard, "Q+→", Key.Q, Key.RightArrow);
		yield return Press(keyboard, "Q", Key.Q);
		yield return Press(keyboard, "Q+←", Key.Q, Key.LeftArrow);
		yield return Press(keyboard, "Q", Key.Q);
		yield return Press(keyboard, "(모두 뗌)");

		// 시작(1) + → → ←(3) + 끝(1) = 5번이면 ← →가 대상 넘기기로 들어간 것이다
		bool ok = changes >= 5;
		string msg = $"[SlotSim] TargetChanged {changes}번 (기대 5번): {(ok ? "통과" : "실패")}";
		if (ok) Debug.Log(msg); else Debug.LogError(msg);
	}

	IEnumerator Press(Keyboard keyboard, string label, params Key[] keys)
	{
		InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
		Debug.Log($"[SlotSim] 키: {label}");

		yield return new WaitForSeconds(stepDelay);
	}
}
