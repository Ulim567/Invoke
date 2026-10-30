using System.Text;
using Mirror;
using UnityEngine;

/// <summary>
/// [임시 표시] 내 캐릭터의 소환검 개수와 포인터, 상태를 화면 왼쪽 위에 OnGUI로 그린다. SummonedSwords와 같은 오브젝트에 붙인다.
/// 실제 HUD 그림은 임혜원 이슈에서 SummonedSwords.Swords와 Changed를 읽어 바꾼다.
/// </summary>
[RequireComponent(typeof(SummonedSwords))]
public class SummonedSwordHud : NetworkBehaviour
{
	SummonedSwords swords;
	GUIStyle style;

	void Awake() => swords = GetComponent<SummonedSwords>();

	void OnGUI()
	{
		if (!isLocalPlayer) return;
		float scale = Screen.height / 360f;
		style ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
		style.fontSize = Mathf.RoundToInt(8 * scale);

		StringBuilder text = new StringBuilder();
		text.Append($"소환검 {swords.Count}/{SummonedSwords.MaxCount} (사용 가능 {swords.ReadyCount})");
		foreach (SummonedSword s in swords.Swords)
		{
			if (s == null) continue;
			text.Append($"\n  *{s.Pointer}  {Label(s.State)}");
		}
		GUI.Label(new Rect(10 * scale, 40 * scale, 200 * scale, 120 * scale), text.ToString(), style);
	}

	static string Label(SummonedSwordState state) => state switch
	{
		SummonedSwordState.Ready => "사용 가능",
		SummonedSwordState.Flying => "던져짐",
		SummonedSwordState.Dropped => "떨어짐",
		SummonedSwordState.Leaked => "누수",
		_ => state.ToString(),
	};
}
