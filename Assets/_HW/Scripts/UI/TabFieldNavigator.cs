using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Tab을 누르면 이 화면 안의 다음 입력칸으로 넘어간다 (#430). 로그인·회원가입·레이드 방 만들기처럼 입력칸이 여러 개인 화면(SetActive로 여닫는 오브젝트)에 붙인다.
/// <para>
/// - 키는 GameInput.UI.NextFieldPressed (Tab)로만 읽는다.<br/>
/// - 순서는 입력칸의 화면 위치로 정한다: 위에서 아래, 같은 줄이면 왼쪽에서 오른쪽. 마지막 칸에서는 처음 칸으로 돌아간다.<br/>
/// - 지금 선택된 것이 이 화면의 입력칸일 때만 움직인다. 아무것도 선택되지 않았으면 이 화면이 맨 위 화면(UIShortcut.IsTop)일 때 첫 칸을 고른다.<br/>
/// - 켜져 있고 누를 수 있는 TMP_InputField만 고른다. 블록 코딩 화면의 값 입력칸(BlockField)은 넣지 않는다.
/// </para>
/// </summary>
public class TabFieldNavigator : MonoBehaviour
{
	readonly List<TMP_InputField> fields = new List<TMP_InputField>();
	static int handledFrame = -1; // 화면 두 개가 같은 Tab에 함께 움직이지 않게

	void Update()
	{
		if (!GameInput.UI.NextFieldPressed || handledFrame == Time.frameCount) return;
		EventSystem events = EventSystem.current;
		if (events == null) return;

		Collect();
		if (fields.Count == 0) return;

		GameObject selected = events.currentSelectedGameObject;
		TMP_InputField current = selected != null ? selected.GetComponentInParent<TMP_InputField>() : null;
		int index = current != null ? fields.IndexOf(current) : -1;
		if (index < 0)
		{
			// 다른 화면의 칸이 선택되어 있으면 건드리지 않는다
			if (selected != null && selected.activeInHierarchy) return;
			UIShortcut shortcut = GetComponentInParent<UIShortcut>();
			if (shortcut != null && !shortcut.IsTop) return;
		}

		// 입력칸이 Tab 글자를 받았으면 지운다
		if (current != null && current.text.Contains("\t")) current.text = current.text.Replace("\t", "");

		TMP_InputField next = fields[(index + 1) % fields.Count];
		handledFrame = Time.frameCount;
		events.SetSelectedGameObject(next.gameObject);
		next.Select();
	}

	void Collect()
	{
		fields.Clear();
		foreach (TMP_InputField field in GetComponentsInChildren<TMP_InputField>(false))
			if (field.isActiveAndEnabled && field.interactable && field.GetComponentInParent<BlockField>() == null) fields.Add(field);
		fields.Sort((a, b) =>
		{
			Vector3 pa = TopLeft(a), pb = TopLeft(b);
			if (Mathf.Abs(pa.y - pb.y) > 1f) return pb.y.CompareTo(pa.y); // 위가 먼저
			return pa.x.CompareTo(pb.x);
		});
	}

	static readonly Vector3[] corners = new Vector3[4];

	static Vector3 TopLeft(TMP_InputField field)
	{
		((RectTransform)field.transform).GetWorldCorners(corners);
		return corners[1];
	}
}
