using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 고정 머리글 (sticky header, #578). 스크롤 목록을 내려도 지금 보고 있는 구역의 이름을 목록 맨 위에 붙여 보인다. 블록 편집 화면의 함수 탭이 쓴다.
/// <para>
/// - 사용법: Attach로 붙이고, 목록을 다시 만들 때 Clear()로 비운 뒤 머리글을 만들 때마다 Mark(머리글, 대구분, 소구분)로 알려 준다.<br/>
/// - 보이는 영역 위로 지나간 마지막 머리글을 찾아 「대구분 > 소구분」(예: 스킬 > 찌르기)을 맨 위 머리글에 적는다.
///   아무 머리글도 지나가지 않았으면(목록 맨 위) 숨긴다. 목록의 머리글이 그대로 보이기 때문.<br/>
/// - 맨 위 머리글은 목록 머리글과 같은 프리팹(ListHeader)을 ScrollRect 오브젝트의 자식(보이는 영역 Viewport 바깥)으로 만든다. 목록(Content)을 비워도 지워지지 않는다.
/// </para>
/// </summary>
public class StickyListHeader : MonoBehaviour
{
	// 구역 표시 하나. Target은 목록 안의 머리글 (위에서부터 만든 순서대로 쌓인다)
	struct Marker
	{
		public RectTransform Target;
		public string Label;
	}

	const string Separator = " > "; // 분류 태그와 같은 표기 (「·」는 Galmuri11 글꼴에 없음)
	const float TopCover = 4f;      // 고정 머리글을 보이는 영역 위로 더 올려 그리는 양 (AlignTo). 편집 화면의 검색 칸 아래 끝과 목록 위 끝 사이의 틈(4)

	readonly List<Marker> markers = new List<Marker>();
	ScrollRect scroll;
	ListHeaderView pinned;
	float pinnedHeight;
	string shownLabel;
	readonly Vector3[] corners = new Vector3[4];

	/// <summary>
	/// scroll에 고정 머리글을 붙인다 (이미 있으면 그것을 돌려준다). style은 맨 위 머리글의 모양을 칠한다 (목록의 대구분과 같게).
	/// </summary>
	public static StickyListHeader Attach(ScrollRect scroll, ListHeaderView prefab, Action<ListHeaderView> style)
	{
		if (scroll == null || prefab == null) return null;
		if (!scroll.TryGetComponent(out StickyListHeader sticky)) sticky = scroll.gameObject.AddComponent<StickyListHeader>();
		sticky.scroll = scroll;
		if (sticky.pinned == null) sticky.CreatePinned(prefab, style);
		return sticky;
	}

	/// <summary>표시를 모두 지운다. 목록을 다시 만들기 전에 부른다</summary>
	public void Clear()
	{
		markers.Clear();
		Hide();
	}

	/// <summary>
	/// 목록 안의 머리글 하나를 알린다. 위에서부터 만든 순서대로 부른다. minor가 있으면 「major > minor」로 보인다.
	/// </summary>
	public void Mark(RectTransform target, string major, string minor = null)
	{
		if (target == null) return;
		markers.Add(new Marker { Target = target, Label = string.IsNullOrEmpty(minor) ? major : major + Separator + minor });
	}

	void CreatePinned(ListHeaderView prefab, Action<ListHeaderView> style)
	{
		// 보이는 영역(Viewport)이 아니라 그 바깥(ScrollRect 오브젝트)에 둔다. Viewport의 RectMask2D가 게임 글꼴(Galmuri11, Bitmap 셰이더) 글자를 칸 경계에서 잘라 내지 못해,
		// 위로 걸친 줄의 글자가 보이는 영역 위 틈(검색 칸과의 사이)에 비쳤다 (사용자 확인, 화면을 찍어 원인 확인). 바깥에 두면 그 틈까지 덮을 수 있다
		pinned = Instantiate(prefab, scroll.transform);
		pinned.name = "StickyHeader";
		style?.Invoke(pinned);

		// 보이는 영역 맨 위 왼쪽 기준. 가로 자리와 너비는 LateUpdate가 목록 머리글과 같게 맞춘다 (스크롤할 때 상자 크기가 바뀌어 보이지 않게)
		var rect = (RectTransform)pinned.transform;
		pinnedHeight = pinned.TryGetComponent(out LayoutElement size) && size.preferredHeight > 0 ? size.preferredHeight : 16f;
		if (size != null) size.ignoreLayout = true;
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = new Vector2(0f, pinnedHeight);
		rect.SetAsLastSibling(); // 보이는 영역(목록)보다 위에 그린다

		// 띠가 클릭을 받아, 띠 아래에 가려진 항목이 눌리거나 끌리지 않게 한다. 휠 스크롤은 부모의 ScrollRect로 올라간다
		if (pinned.TryGetComponent(out Image band)) band.raycastTarget = true;
		pinned.gameObject.SetActive(false);
	}

	// 스크롤이 움직일 때마다 바뀌므로 매 프레임 본다. 표시는 구역 수(10개 안팎)만큼이라 가볍다
	void LateUpdate()
	{
		if (scroll == null || pinned == null) return;
		RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
		float top = viewport.rect.yMax;

		string label = null;
		RectTransform passed = null;
		foreach (Marker marker in markers)
		{
			if (marker.Target == null || !marker.Target.gameObject.activeInHierarchy) continue;
			// 머리글의 위 끝이 보이는 영역 위 끝보다 위로 올라갔으면 지나간 것. 표시는 위에서부터 쌓였으므로 처음으로 안 지나간 곳에서 멈춘다
			Vector3 markerTop = marker.Target.TransformPoint(new Vector3(0f, marker.Target.rect.yMax, 0f));
			if (viewport.InverseTransformPoint(markerTop).y <= top + 0.5f) break;
			label = marker.Label;
			passed = marker.Target;
		}

		if (label == null) { Hide(); return; }
		if (!pinned.gameObject.activeSelf) pinned.gameObject.SetActive(true);
		AlignTo(passed, viewport);
		if (label != shownLabel)
		{
			shownLabel = label;
			pinned.Show(label, null);
		}
	}

	// 고정 머리글의 왼쪽 끝과 너비를 목록 머리글(target)과 같게 한다. 목록 안쪽 여백·스크롤 막대 자리를 따로 셈하지 않아도 같은 상자가 된다
	void AlignTo(RectTransform target, RectTransform viewport)
	{
		var parent = (RectTransform)pinned.transform.parent;
		target.GetWorldCorners(corners);
		float left = parent.InverseTransformPoint(corners[0]).x;
		float right = parent.InverseTransformPoint(corners[2]).x;
		viewport.GetWorldCorners(corners);
		float top = parent.InverseTransformPoint(corners[1]).y;
		var rect = (RectTransform)pinned.transform;
		// 보이는 영역 위 끝에서 TopCover만큼 더 올려, 검색 칸과의 틈에 비치는 글자까지 덮는다
		var position = new Vector2(left - parent.rect.xMin, top - parent.rect.yMax + TopCover);
		var size = new Vector2(right - left, pinnedHeight + TopCover);
		if (rect.anchoredPosition != position) rect.anchoredPosition = position;
		if (rect.sizeDelta != size) rect.sizeDelta = size;
	}

	void Hide()
	{
		shownLabel = null;
		if (pinned != null && pinned.gameObject.activeSelf) pinned.gameObject.SetActive(false);
	}
}
