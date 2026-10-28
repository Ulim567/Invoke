using UnityEngine;

/// <summary>
/// 슬롯으로 고르고 있는 대상 위에 표시를 띄우는 임시 연출. SlotCaster와 같은 오브젝트(플레이어)에 붙인다.
/// <para>
/// SlotCaster.TargetChanged만 받아서 그린다 (코드 작성 규칙 1: 고르는 로직과 표시를 나눔).<br/>
/// OnGUI로 그리는 임시 표시다. 실제 대상 표시 그림·연출은 임혜원 이슈에서 이 이벤트를 받아 바꾼다.
/// </para>
/// </summary>
[RequireComponent(typeof(SlotCaster))]
public class SlotTargetMarker : MonoBehaviour
{
	[SerializeField, Tooltip("표시 색")]
	Color color = Color.yellow;

	SlotCaster caster;
	GameObject target;
	GUIStyle style;

	void Awake() => caster = GetComponent<SlotCaster>();

	void OnEnable() => caster.TargetChanged += OnTargetChanged;

	void OnDisable() => caster.TargetChanged -= OnTargetChanged;

	void OnTargetChanged(GameObject newTarget) => target = newTarget;

	void OnGUI()
	{
		if (target == null) return;
		Camera cam = Camera.main;
		if (cam == null) return;

		// 대상 콜라이더의 위쪽 끝 바로 위에 표시를 그린다. ▼ 같은 기호는 기본 폰트에 없어서 네모로 나오므로 V를 쓴다
		Collider2D col = target.GetComponentInChildren<Collider2D>();
		Vector3 head = col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y + 0.6f) : target.transform.position;
		Vector3 screen = cam.WorldToScreenPoint(head);
		if (screen.z < 0f) return;

		float scale = Screen.height / 360f;
		style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
		style.fontSize = Mathf.RoundToInt(14 * scale);
		GUI.color = color;
		GUI.Label(new Rect(screen.x - 50, Screen.height - screen.y - 25, 100, 50), "V", style);
		GUI.color = Color.white;
	}
}
