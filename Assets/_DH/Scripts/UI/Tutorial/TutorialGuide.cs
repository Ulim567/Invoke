using System;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼이 가야 할 곳을 눈에 보이게 가리킨다. 내 화면에만 그리는 연출이고 게임 값은 바꾸지 않는다.
/// <para>
/// - Goal이 가리킬 곳을 정하면(TutorialView가 쪽마다 정함), 내 장소에서 그 오브젝트를 찾아 머리 위에 주황 ▼ 마커를 통통 띄우고, 그 위에 이름표(예: 「연습장」)를 붙인다.<br/>
/// - 그 곳이 화면 밖이면 화면 가장자리에 그쪽을 가리키는 화살표를 띄우고, 화살표 옆에 같은 이름표를 붙인다.<br/>
/// - 오브젝트는 0.4초마다 다시 찾는다 (가장 가까운 것). 내 장소와 같은 이름의 씬에 있는 것만 본다 (호스트는 다른 장소 씬도 열려 있음).<br/>
/// - 그림은 코드로 만든다 (에셋 없음, 16 PPU 픽셀). 서버(화면 없음)에서는 만들지 않는다.
/// </para>
/// </summary>
public class TutorialGuide : MonoBehaviour
{
	const float PixelsPerUnit = 16f;
	const float RefreshInterval = 0.4f;
	const float MarkerHeight = 1.1f;
	const float EdgeMargin = 0.05f;   // 가장자리 화살표를 화면 끝에서 띄우는 거리 (화면 비율)
	const float OnScreenInset = 0.01f; // 마커 가운데가 이 안쪽에 있으면 화면 안으로 친다
	// 마커 크기 (월드). 예전에는 HUD 캔버스 아래에 두어 화면 해상도에 따라 크기가 바뀌었다
	const float MarkerScale = 2f;
	// 이름표: 글자 크기, 바탕 여백, 화면 가장자리 화살표에서 안쪽으로 떨어진 거리 (캔버스 픽셀)
	const float LabelFontSize = 8f;
	const float LabelPadX = 4f, LabelPadY = 2f;
	const float LabelFromEdgeArrow = 18f;

	static readonly Color Fill = new Color32(0xFF, 0xB3, 0x47, 0xFF);
	static readonly Color Outline = new Color32(0x1A, 0x1E, 0x3A, 0xFF);
	static Sprite arrowSprite;

	[SerializeField, Tooltip("화면 가장자리 화살표 (HUD Root 아래, 앵커 왼쪽 아래, 가운데 기준). 스프라이트는 코드로 넣는다")]
	Image edgeArrow;

	TutorialGoal goal;
	Transform target;
	string targetName;
	float nextRefresh;
	SpriteRenderer marker;
	RectTransform area;
	RectTransform labelBox;
	TMP_Text label;
	Camera cam;

	/// <summary>가리킬 곳. None이면 아무것도 보이지 않는다</summary>
	public TutorialGoal Goal
	{
		get => goal;
		set
		{
			if (goal == value) return;
			goal = value;
			target = null;
			nextRefresh = 0f;
			Hide();
		}
	}

	void Awake()
	{
		if (Application.isBatchMode) { enabled = false; return; }
		// 마커는 월드에 그리는 그림이라 HUD 캔버스 밖(씬 맨 위)에 둔다. 캔버스 아래에 두면 캔버스 배율만큼 커진다
		var go = new GameObject("TutorialGuideMarker");
		go.transform.localScale = Vector3.one * MarkerScale;
		marker = go.AddComponent<SpriteRenderer>();
		marker.sprite = ArrowSprite();
		// Default 정렬 레이어는 하늘(Sky)보다 뒤라 가려진다. 몬스터 체력바처럼 WorldUI에 그린다
		marker.sortingLayerName = "WorldUI";
		marker.sortingOrder = 500;
		go.SetActive(false);

		if (edgeArrow != null)
		{
			edgeArrow.sprite = ArrowSprite();
			edgeArrow.raycastTarget = false;
			edgeArrow.rectTransform.sizeDelta = new Vector2(16.5f, 10.5f); // 11×7 그림의 1.5배 (캔버스 640×360 기준)
			area = edgeArrow.transform.parent as RectTransform;
			edgeArrow.gameObject.SetActive(false);
			if (area != null) BuildLabel();
		}
	}

	void OnDisable() => Hide();

	void OnDestroy()
	{
		if (marker != null) Destroy(marker.gameObject);
	}

	void Update()
	{
		if (goal == TutorialGoal.None) return;

		if (Time.unscaledTime >= nextRefresh)
		{
			nextRefresh = Time.unscaledTime + RefreshInterval;
			target = FindTarget();
			targetName = NameOf(goal, PlayerPlace.Mine);
		}
		if (target == null) { Hide(); return; }

		// 마커: 목표 위에서 1픽셀 단위로 통통
		Vector3 top = TopOf(target);
		float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 4f) * 2f) / PixelsPerUnit;
		if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
		marker.transform.position = top + new Vector3(0f, bob, 0f);

		UpdateScreenGuides(top + new Vector3(0f, bob, 0f));
	}

	// 화면 안이면 마커 위에 이름표, 화면 밖이면 가장자리 화살표와 그 옆에 이름표
	void UpdateScreenGuides(Vector3 markerPosition)
	{
		if (area == null) return;
		if (cam == null) cam = Camera.main;
		if (cam == null) { HideScreenGuides(); return; }

		Vector2 size = area.rect.size;
		Vector3 view = cam.WorldToViewportPoint(markerPosition);
		// 마커가 화면에 보이면(가장자리에 걸쳐도) 가장자리 화살표는 띄우지 않는다. 맵 끝의 출구처럼 카메라가 더 못 가서 늘 가장자리에 걸치는 곳이 있다
		bool onScreen = view.z > 0f && view.x > OnScreenInset && view.x < 1f - OnScreenInset && view.y > OnScreenInset && view.y < 1f - OnScreenInset;
		if (edgeArrow != null && edgeArrow.gameObject.activeSelf == onScreen) edgeArrow.gameObject.SetActive(!onScreen);

		Vector2 labelAt;
		if (onScreen)
		{
			// 마커(▼) 바로 위
			float markerTop = 0.5f * 7f / PixelsPerUnit * MarkerScale + 0.15f;
			Vector3 above = cam.WorldToViewportPoint(markerPosition + new Vector3(0f, markerTop, 0f));
			labelAt = new Vector2(above.x * size.x, above.y * size.y);
			ShowLabel(labelAt, new Vector2(0.5f, 0f));
			return;
		}

		Vector2 d = new Vector2(view.x - 0.5f, view.y - 0.5f);
		float half = 0.5f - EdgeMargin;
		float t = Mathf.Min(half / Mathf.Max(0.0001f, Mathf.Abs(d.x)), half / Mathf.Max(0.0001f, Mathf.Abs(d.y)));
		Vector2 p = new Vector2(0.5f, 0.5f) + d * t;
		Vector2 arrowAt = new Vector2(p.x * size.x, p.y * size.y);
		if (edgeArrow != null)
		{
			RectTransform rect = edgeArrow.rectTransform;
			rect.anchoredPosition = arrowAt;
			// ▼가 기본이라 가리킬 방향으로 돌린다
			rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg);
		}
		// 화살표에서 화면 안쪽으로 조금 들어온 자리. 이름표가 화살표 반대쪽으로 붙도록 기준점을 정한다
		Vector2 inward = -d.normalized;
		labelAt = arrowAt + inward * LabelFromEdgeArrow;
		ShowLabel(labelAt, new Vector2(d.x > 0.2f ? 1f : d.x < -0.2f ? 0f : 0.5f, d.y > 0.2f ? 1f : d.y < -0.2f ? 0f : 0.5f));
	}

	void ShowLabel(Vector2 at, Vector2 pivot)
	{
		if (labelBox == null) return;
		if (string.IsNullOrEmpty(targetName)) { labelBox.gameObject.SetActive(false); return; }
		if (label.text != targetName) label.text = targetName;
		Vector2 textSize = label.GetPreferredValues(targetName);
		Vector2 boxSize = new Vector2(Mathf.Ceil(textSize.x) + LabelPadX * 2f, Mathf.Ceil(textSize.y) + LabelPadY * 2f);
		labelBox.sizeDelta = boxSize;
		labelBox.pivot = pivot;
		// 화면 밖으로 나가지 않게
		Vector2 size = area.rect.size;
		at.x = Mathf.Clamp(at.x, boxSize.x * pivot.x + 2f, size.x - boxSize.x * (1f - pivot.x) - 2f);
		at.y = Mathf.Clamp(at.y, boxSize.y * pivot.y + 2f, size.y - boxSize.y * (1f - pivot.y) - 2f);
		labelBox.anchoredPosition = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
		if (!labelBox.gameObject.activeSelf) labelBox.gameObject.SetActive(true);
	}

	void HideScreenGuides()
	{
		if (edgeArrow != null && edgeArrow.gameObject.activeSelf) edgeArrow.gameObject.SetActive(false);
		if (labelBox != null && labelBox.gameObject.activeSelf) labelBox.gameObject.SetActive(false);
	}

	void Hide()
	{
		if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
		HideScreenGuides();
	}

	// 이름표: 어두운 바탕(대사 창과 같은 그림) 위 주황 글자. 내 화면에만 쓰는 UI라 코드로 만든다 (네트워크 오브젝트 아님)
	void BuildLabel()
	{
		TMP_Text fontSource = GetComponentInChildren<TMP_Text>(true);
		Image panelSource = GetComponentInChildren<Image>(true);

		var box = new GameObject("TutorialGuideLabel", typeof(RectTransform));
		labelBox = (RectTransform)box.transform;
		labelBox.SetParent(area, false);
		labelBox.anchorMin = labelBox.anchorMax = Vector2.zero;
		Image background = box.AddComponent<Image>();
		background.raycastTarget = false;
		if (panelSource != null && panelSource.sprite != null)
		{
			background.sprite = panelSource.sprite;
			background.type = Image.Type.Sliced;
			background.color = new Color(1f, 1f, 1f, 0.85f);
		}
		else background.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);

		var text = new GameObject("Text", typeof(RectTransform));
		var textRect = (RectTransform)text.transform;
		textRect.SetParent(labelBox, false);
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = textRect.offsetMax = Vector2.zero;
		label = text.AddComponent<TextMeshProUGUI>();
		if (fontSource != null) { label.font = fontSource.font; label.fontSharedMaterial = fontSource.fontSharedMaterial; }
		label.fontSize = LabelFontSize;
		label.color = Fill;
		label.alignment = TextAlignmentOptions.Center;
		label.textWrappingMode = TextWrappingModes.NoWrap;
		label.raycastTarget = false;
		box.SetActive(false);
	}

	// 이름표에 쓰는 이름. 같은 Goal이라도 장소에 따라 가리키는 것이 다르다 (FindTarget과 맞춘다)
	static string NameOf(TutorialGoal goal, string place) => goal switch
	{
		TutorialGoal.TrainingDoor => "연습장",
		TutorialGoal.RaidDoor => "레이드",
		TutorialGoal.ToHunting => place == PlaceScenes.Lobby ? "사냥터" : "출구",
		TutorialGoal.ToLobby => "로비 출구",
		TutorialGoal.EditorTerminal => "편집 터미널",
		TutorialGoal.EquipStation => "장착대",
		TutorialGoal.Dummy => "허수아비",
		_ => "",
	};

	// 오브젝트 위 (콜라이더가 있으면 그 위 끝에서 조금 위)
	static Vector3 TopOf(Transform t)
	{
		if (t.TryGetComponent(out Collider2D c))
			return new Vector3(c.bounds.center.x, c.bounds.max.y + MarkerHeight, t.position.z);
		return t.position + new Vector3(0f, 2f, 0f);
	}

	// ----- 가리킬 오브젝트 찾기 -----

	Transform FindTarget()
	{
		string place = PlayerPlace.Mine;
		if (string.IsNullOrEmpty(place) || NetworkClient.localPlayer == null) return null;
		Vector3 me = NetworkClient.localPlayer.transform.position;

		switch (goal)
		{
			case TutorialGoal.TrainingDoor:
				return place == PlaceScenes.Lobby ? Entrance(LobbyEntrance.Destination.TrainingRoom, place, me) : null;
			case TutorialGoal.RaidDoor:
				return place == PlaceScenes.Lobby ? Entrance(LobbyEntrance.Destination.RaidDungeon, place, me) : null;
			case TutorialGoal.ToHunting:
				if (place == PlaceScenes.Lobby) return Entrance(LobbyEntrance.Destination.HuntingGround, place, me);
				return place == PlaceScenes.TrainingRoom ? Exit(place, me) : null;
			case TutorialGoal.ToLobby:
				return place == PlaceScenes.TrainingRoom || place == PlaceScenes.HuntingGround ? Exit(place, me) : null;
			case TutorialGoal.EditorTerminal:
				return place == PlaceScenes.TrainingRoom ? EditorTerminal(place, me) : null;
			case TutorialGoal.EquipStation:
				return place == PlaceScenes.TrainingRoom ? EquipStation(place, me) : null;
			case TutorialGoal.Dummy:
				return place == PlaceScenes.TrainingRoom ? Dummy(place, me) : null;
			default:
				return null;
		}
	}

	static Transform Entrance(LobbyEntrance.Destination destination, string place, Vector3 me)
		=> Nearest(FindObjectsByType<LobbyEntrance>(FindObjectsSortMode.None), place, me, e => e.Target == destination);

	static Transform Exit(string place, Vector3 me)
		=> Nearest(FindObjectsByType<PlaceExit>(FindObjectsSortMode.None), place, me, null);

	// 편집 터미널: 연습장의 PanelStation 중 이름에 Editor가 들어간 것 (장착대 EquipStation과 구분). 씬의 오브젝트 이름이 바뀌면 여기도 맞춘다
	static Transform EditorTerminal(string place, Vector3 me)
		=> Nearest(FindObjectsByType<PanelStation>(FindObjectsSortMode.None), place, me, s => s.name.Contains("Editor"));

	// 장착대: 연습장의 PanelStation 중 이름에 Equip이 들어간 것 (씬 오브젝트 EquipStation)
	static Transform EquipStation(string place, Vector3 me)
		=> Nearest(FindObjectsByType<PanelStation>(FindObjectsSortMode.None), place, me, s => s.name.Contains("Equip"));

	static Transform Dummy(string place, Vector3 me)
		=> Nearest(FindObjectsByType<Monster>(FindObjectsSortMode.None), place, me, m => m.IsInvulnerable);

	// 내 장소와 같은 이름의 씬에 있는 것 중 가장 가까운 것
	static Transform Nearest<T>(T[] list, string place, Vector3 me, Func<T, bool> filter) where T : Component
	{
		Transform best = null;
		float bestDistance = float.MaxValue;
		for (int i = 0; i < list.Length; i++)
		{
			T item = list[i];
			if (item.gameObject.scene.name != place) continue;
			if (filter != null && !filter(item)) continue;
			float distance = (item.transform.position - me).sqrMagnitude;
			if (distance >= bestDistance) continue;
			bestDistance = distance;
			best = item.transform;
		}
		return best;
	}

	// ▼ 11×7: 주황 바탕, 어두운 테두리. 가장자리 화살표는 이 그림을 돌려서 쓴다
	static Sprite ArrowSprite()
	{
		if (arrowSprite != null) return arrowSprite;
		const int w = 11, h = 7;
		int[] half = { 0, 1, 2, 3, 4, 5, 5 }; // 아래(y=0)부터 줄마다 가운데에서 좌우로 뻗는 칸 수
		bool Inside(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && Mathf.Abs(x - 5) <= half[y];
		var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
		var pixels = new Color32[w * h];
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				if (!Inside(x, y)) { pixels[y * w + x] = new Color32(0, 0, 0, 0); continue; }
				bool edge = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1);
				pixels[y * w + x] = edge ? Outline : Fill;
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply();
		arrowSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
		arrowSprite.name = "TutorialArrow";
		return arrowSprite;
	}
}
