using Mirror;
using UnityEngine;

/// <summary>
/// [임시 표시] 검 타기 남은 시간을 내 캐릭터 머리 위에 작은 가로 바로 그린다 (#256). 플레이어 프리팹에 PlayerMovement와 함께 붙인다.
/// <para>
/// - 바는 왼쪽 끝을 고정하고, 남은 시간이 줄수록 오른쪽 끝이 왼쪽으로 줄어든다.<br/>
/// - 값(PlayerMovement.RideRemaining, RideSeconds)은 읽기만 한다. 내 캐릭터에서만 그린다.<br/>
/// - 실제 그림은 임혜원 이슈에서 이 값을 읽어 바꾼다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class SwordRideTimerView : NetworkBehaviour
{
	[Tooltip("머리(몸 콜라이더 위쪽 끝)에서 바까지 띄울 간격 (유닛)")]
	[SerializeField] float headGap = 0.35f;
	[Tooltip("바 크기 (360p 화면 기준 픽셀, 화면 크기에 맞춰 늘어남)")]
	[SerializeField] Vector2 barSize = new Vector2(28f, 2.5f);
	[SerializeField] Color fillColor = new Color(0.55f, 0.85f, 1f, 0.95f);
	[SerializeField] Color backColor = new Color(0f, 0f, 0f, 0.55f);

	PlayerMovement movement;
	Texture2D white;

	void Awake() => movement = GetComponent<PlayerMovement>();

	// 머리 꼭대기: 플레이어 최상위의 몸 콜라이더(트리거 제외) 위쪽 끝. 없으면 발밑 + 2
	float HeadTop()
	{
		float top = float.MinValue;
		foreach (Collider2D col in GetComponents<Collider2D>())
			if (col.enabled && !col.isTrigger) top = Mathf.Max(top, col.bounds.max.y);
		return top > float.MinValue ? top : transform.position.y + 2f;
	}

	void OnGUI()
	{
		if (!isLocalPlayer || movement == null || !movement.IsRiding || movement.RideSeconds <= 0f) return;
		Camera cam = Camera.main;
		if (cam == null) return;

		Vector3 screen = cam.WorldToScreenPoint(new Vector3(transform.position.x, HeadTop() + headGap, transform.position.z));
		if (screen.z < 0f) return;
		float scale = Screen.height / 360f;
		float w = barSize.x * scale;
		float h = Mathf.Max(2f, barSize.y * scale);
		// GUI는 위쪽이 0이라 y를 뒤집는다. 바의 아래쪽 끝이 그 지점에 오게 한다
		Rect back = new Rect(screen.x - w / 2f, Screen.height - screen.y - h, w, h);
		float ratio = Mathf.Clamp01(movement.RideRemaining / movement.RideSeconds);
		Rect fill = new Rect(back.x, back.y, w * ratio, h);

		if (white == null) white = Texture2D.whiteTexture;
		Color old = GUI.color;
		GUI.color = backColor;
		GUI.DrawTexture(back, white);
		GUI.color = fillColor;
		GUI.DrawTexture(fill, white);
		GUI.color = old;
	}
}
