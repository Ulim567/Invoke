using UnityEngine;

/// <summary>
/// (임시) 급소공략 미니게임 다이얼을 OnGUI로 그린다 (#161). 실제 그림(임혜원)이 생기면 바꾼다.
/// <para>
/// - VitalStrikeMinigame의 Started / Finished, NeedleAngle, EyeZoneStart, AnkleZoneStart, ZoneDegrees만 읽는다 (표시 전용).<br/>
/// - 캐릭터 머리 위에 원을 점으로 그린다. 빨간 칸 = 눈, 파란 칸 = 발목, 노란 선 = 바늘.<br/>
/// - 끝나면 결과 글자를 잠깐 보여 준다.
/// </para>
/// </summary>
[RequireComponent(typeof(VitalStrikeMinigame))]
public class VitalStrikeMinigameView : MonoBehaviour
{
	[SerializeField, Min(10f)] float radius = 48f;
	[SerializeField] Vector3 worldOffset = new Vector3(0f, 3.2f, 0f);
	[Tooltip("끝난 뒤 다이얼과 결과를 보여 주는 시간 (초)")]
	[SerializeField, Min(0f)] float resultSeconds = 0.6f;

	static readonly Color RingColor = new Color(1f, 1f, 1f, 0.35f);
	static readonly Color EyeColor = new Color(1f, 0.3f, 0.3f);
	static readonly Color AnkleColor = new Color(0.35f, 0.6f, 1f);
	static readonly Color NeedleColor = new Color(1f, 0.9f, 0.2f);

	VitalStrikeMinigame minigame;
	bool visible;
	float hideAt;
	string resultText;

	void Awake() => minigame = GetComponent<VitalStrikeMinigame>();

	void OnEnable()
	{
		minigame.Started += OnStarted;
		minigame.Finished += OnFinished;
	}

	void OnDisable()
	{
		minigame.Started -= OnStarted;
		minigame.Finished -= OnFinished;
	}

	void OnStarted()
	{
		visible = true;
		resultText = null;
		hideAt = float.MaxValue;
	}

	void OnFinished(VitalStrikeResult result)
	{
		if (result == VitalStrikeResult.None) { visible = false; return; }
		resultText = result == VitalStrikeResult.Eye ? "눈!" : result == VitalStrikeResult.Ankle ? "발목!" : "실패";
		hideAt = Time.time + resultSeconds;
	}

	void OnGUI()
	{
		if (!visible) return;
		if (Time.time >= hideAt) { visible = false; return; }

		Camera cam = Camera.main;
		if (cam == null) return;
		Vector3 screen = cam.WorldToScreenPoint(transform.position + worldOffset);
		if (screen.z < 0f) return;
		Vector2 center = new Vector2(screen.x, Screen.height - screen.y);

		// 다이얼: 5도마다 점 하나
		for (float a = 0f; a < 360f; a += 5f)
		{
			Color color = RingColor;
			if (InZone(a, minigame.EyeZoneStart)) color = EyeColor;
			else if (InZone(a, minigame.AnkleZoneStart)) color = AnkleColor;
			Dot(center + Dir(a) * radius, 6f, color);
		}

		// 바늘: 가운데에서 바깥까지
		for (float r = 0f; r <= radius; r += 4f) Dot(center + Dir(minigame.NeedleAngle) * r, 4f, NeedleColor);

		GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold };
		Label(center + Dir(minigame.EyeZoneStart + minigame.ZoneDegrees * 0.5f) * (radius + 16f), "눈", EyeColor, style);
		Label(center + Dir(minigame.AnkleZoneStart + minigame.ZoneDegrees * 0.5f) * (radius + 16f), "발목", AnkleColor, style);
		if (resultText != null) Label(center + new Vector2(0f, -radius - 26f), resultText, Color.white, style);
	}

	bool InZone(float angle, float start) => angle >= start && angle <= start + minigame.ZoneDegrees;

	// 0도 = 12시, 시계 방향 (GUI 좌표는 y가 아래로 커진다)
	static Vector2 Dir(float degrees)
	{
		float rad = degrees * Mathf.Deg2Rad;
		return new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad));
	}

	static void Dot(Vector2 p, float size, Color color)
	{
		Color old = GUI.color;
		GUI.color = color;
		GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), Texture2D.whiteTexture);
		GUI.color = old;
	}

	static void Label(Vector2 p, string text, Color color, GUIStyle style)
	{
		Color old = GUI.color;
		GUI.color = color;
		GUI.Label(new Rect(p.x - 40f, p.y - 12f, 80f, 24f), text, style);
		GUI.color = old;
	}
}
