using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬롯으로 고르고 있는 대상과 찍은 대상 위에 표시를 띄우는 임시 연출. SlotCaster와 같은 오브젝트(플레이어)에 붙인다.
/// <para>
/// SlotCaster.TargetChanged, MarkedChanged만 받아서 그린다 (코드 작성 규칙 1: 고르는 로직과 표시를 나눔).<br/>
/// 지금 고른 대상은 Color, 찍은 대상은 Marked Color로 그린다.<br/>
/// OnGUI로 그리는 임시 표시다. 실제 대상 표시 그림·연출은 임혜원 이슈에서 이 이벤트를 받아 바꾼다.
/// </para>
/// </summary>
[RequireComponent(typeof(SlotCaster))]
public class SlotTargetMarker : MonoBehaviour
{
	[SerializeField, Tooltip("지금 고른 대상 표시 색")]
	Color color = Color.yellow;
	[SerializeField, Tooltip("여러 명 선택에서 찍은 대상 표시 색")]
	Color markedColor = Color.cyan;

	SlotCaster caster;
	GameObject target;
	readonly List<GameObject> marked = new List<GameObject>();
	GUIStyle style;

	void Awake() => caster = GetComponent<SlotCaster>();

	void OnEnable()
	{
		caster.TargetChanged += OnTargetChanged;
		caster.MarkedChanged += OnMarkedChanged;
	}

	void OnDisable()
	{
		caster.TargetChanged -= OnTargetChanged;
		caster.MarkedChanged -= OnMarkedChanged;
	}

	void OnTargetChanged(GameObject newTarget) => target = newTarget;

	void OnMarkedChanged(IReadOnlyList<GameObject> targets)
	{
		marked.Clear();
		marked.AddRange(targets);
	}

	void OnGUI()
	{
		Camera cam = Camera.main;
		if (cam == null) return;

		float scale = Screen.height / 360f;
		style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
		style.fontSize = Mathf.RoundToInt(14 * scale);

		// 찍은 대상을 먼저 그리고, 지금 고른 대상을 위에 겹쳐 그린다
		foreach (GameObject m in marked)
		{
			if (m != null && m != target) Draw(cam, m, markedColor, 0f);
		}
		if (target != null) Draw(cam, target, color, 0f);
		// 지금 고른 대상이 찍힌 대상이면 위에 찍힘 표시를 하나 더 그린다
		if (target != null && marked.Contains(target)) Draw(cam, target, markedColor, 0.5f);
		GUI.color = Color.white;
	}

	void Draw(Camera cam, GameObject obj, Color c, float extraHeight)
	{
		// 대상 콜라이더의 위쪽 끝 바로 위에 표시를 그린다. ▼ 같은 기호는 기본 폰트에 없어서 네모로 나오므로 V를 쓴다
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		Vector3 head = col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y + 0.6f + extraHeight) : obj.transform.position;
		Vector3 screen = cam.WorldToScreenPoint(head);
		if (screen.z < 0f) return;

		GUI.color = c;
		GUI.Label(new Rect(screen.x - 50, Screen.height - screen.y - 25, 100, 50), "V", style);
	}
}
