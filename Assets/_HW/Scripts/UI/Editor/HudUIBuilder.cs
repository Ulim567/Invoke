using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 게임 중 HUD 프리팹(Assets/_HW/UI/Hud/HudCanvas)을 우림이 시안 8·8-1대로 만드는 에디터 도구 (#324). 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 게임 HUD 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다). 만든 뒤 Base 씬에 끌어다 놓는다.
/// 캔버스 기준 640x360. 위치 숫자는 시안의 아트보드 좌표를 옮긴 것.
/// </summary>
public static class HudUIBuilder
{
	const string Dir = "Assets/_HW/UI/Hud";
	const string PrefabPath = Dir + "/HudCanvas.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	static readonly string[] Keys = { "Q", "W", "E", "R", "A", "S", "D", "F" };

	static readonly Color Label = Hex("B8BED6");
	static readonly Color Muted = Hex("7C84A8");
	static readonly Color HpRed = Hex("E07070");
	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color DataTeal = Hex("2F8F86");
	static readonly Color StackPurple = Hex("6A5AC9");

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/게임 HUD 만들기")]
	public static void Build()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		var canvasObject = new GameObject("HudCanvas", typeof(RectTransform));
		canvasObject.layer = 5;
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 5; // 게임 화면 바로 위. 편집 화면(10), 레이드 결과(30), 설정(40), 로딩(100)보다 아래
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasObject.AddComponent<GraphicRaycaster>();
		HudView view = canvasObject.AddComponent<HudView>();

		RectTransform root = NewUI("Root", canvasObject.transform);
		Stretch(root);

		// 왼쪽 위 띠: 설정(Esc) | Lv | 버그 조각 | 보스 코어 ------------------------------------
		RectTransform bar = NewUI("TopBar", root);
		bar.anchorMin = bar.anchorMax = new Vector2(0, 1);
		bar.pivot = new Vector2(0, 1);
		bar.anchoredPosition = new Vector2(12, -10);
		bar.sizeDelta = new Vector2(0, 20);
		SetImage(bar.gameObject, "hud_bar");
		HorizontalLayoutGroup barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
		barLayout.padding = new RectOffset(6, 6, 2, 2); // 재료 그림 16×16이 들어가게 위아래 2
		barLayout.spacing = 6;
		barLayout.childAlignment = TextAnchor.MiddleLeft;
		barLayout.childControlWidth = barLayout.childControlHeight = true;
		barLayout.childForceExpandWidth = barLayout.childForceExpandHeight = false;
		bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

		// 설정 버튼: 「설정」 + Esc 키 표시
		RectTransform settings = NewUI("SettingsButton", bar);
		HorizontalLayoutGroup settingsLayout = settings.gameObject.AddComponent<HorizontalLayoutGroup>();
		settingsLayout.spacing = 3;
		settingsLayout.childAlignment = TextAnchor.MiddleLeft;
		settingsLayout.childControlWidth = settingsLayout.childControlHeight = true;
		settingsLayout.childForceExpandWidth = settingsLayout.childForceExpandHeight = false;
		Image settingsHit = settings.gameObject.AddComponent<Image>();
		settingsHit.color = new Color(0, 0, 0, 0);
		Button settingsButton = settings.gameObject.AddComponent<Button>();
		settingsButton.targetGraphic = settingsHit;
		Fit(Text(settings, "Label", "설정", 8, Color.white), 22);
		RectTransform esc = NewUI("Esc", settings);
		SetImage(esc.gameObject, "hud_key");
		Layout(esc.gameObject, 16, 11);
		TMP_Text escText = Text(esc, "Text", "Esc", 6, Label);
		Stretch(escText.rectTransform);
		escText.alignment = TextAlignmentOptions.Center;

		// 레이드 던전에서는 숨긴다 (시안 8-3: 설정만)
		var hideInRaid = new System.Collections.Generic.List<GameObject>();
		hideInRaid.Add(Divider(bar));
		TMP_Text level = Text(bar, "Level", "Lv. 1", 8, Label);
		Fit(level, 30);
		hideInRaid.Add(level.gameObject);
		hideInRaid.Add(Divider(bar));
		hideInRaid.Add(MaterialIcon(bar, "bug_fragment"));
		TMP_Text bug = Text(bar, "BugFragment", "버그 조각 0", 8, Color.white);
		Fit(bug, 52);
		hideInRaid.Add(bug.gameObject);
		hideInRaid.Add(Divider(bar));
		hideInRaid.Add(MaterialIcon(bar, "boss_core"));
		TMP_Text core = Text(bar, "BossCore", "보스 코어 0", 8, Color.white);
		Fit(core, 48);
		hideInRaid.Add(core.gameObject);

		// 장소 이름
		TMP_Text place = Text(root, "Place", "> 사냥터", 12, Color.white);
		place.fontStyle = FontStyles.Bold;
		Place(place.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -38), new Vector2(200, 18));

		BuildNameTags(root);
		BuildRaidParty(root);
		BuildBossBar(root);

		// 아래 띠 ------------------------------------------------------------------
		RectTransform band = NewUI("BottomBand", root);
		band.anchorMin = new Vector2(0, 0);
		band.anchorMax = new Vector2(1, 0);
		band.pivot = new Vector2(0.5f, 0);
		band.sizeDelta = new Vector2(0, 60);
		band.anchoredPosition = Vector2.zero;
		// 띠 전체를 칠하지 않고 (바닥을 너무 가림, #431) 가운데 막대·슬롯 뒤에만 아래에서 위로, 가운데에서 양옆으로 흐려지는 그림자
		RectTransform fade = NewUI("Fade", band);
		Place(fade, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-10, 0), new Vector2(440, 62));
		Image fadeImage = fade.gameObject.AddComponent<Image>();
		fadeImage.sprite = LoadSmoothSprite("hud_band_fade");
		fadeImage.raycastTarget = false;

		RectTransform center = NewUI("Center", band);
		Place(center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(240, 60));

		// HP
		TMP_Text hpLabel = Text(center, "HpLabel", "HP", 6, Label);
		Place(hpLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(-110, 44), new Vector2(26, 8));
		(Image hpFill, RectTransform hpBar) = Gauge(center, "HpBar", new Vector2(-5, 44), HpRed, Image.OriginHorizontal.Left);
		// 닳은 부분: 흰 잔상이 빨간 막대 뒤에서 잠시 남았다가 줄어든다 (#431, 몬스터 체력 바의 잔상 색)
		Image hpTrail = Fill(hpBar, "Trail", Hex("E8ECFF"), Image.OriginHorizontal.Left);
		hpTrail.transform.SetAsFirstSibling();
		hpTrail.fillAmount = hpFill.fillAmount = 1f;
		TMP_Text hpText = Text(center, "HpText", "100 / 100", 6, Label);
		hpText.alignment = TextAlignmentOptions.MidlineRight;
		Place(hpText.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0.5f), new Vector2(118, 44), new Vector2(40, 8));

		// Memory: Data·Heap 왼쪽부터, Stack 오른쪽부터
		TMP_Text memoryLabel = Text(center, "MemoryLabel", "Memory", 6, Label);
		Place(memoryLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(-110, 35), new Vector2(26, 8));
		(Image heapFill, RectTransform memoryBar) = Gauge(center, "MemoryBar", new Vector2(-5, 35), Teal, Image.OriginHorizontal.Left);
		Image dataFill = Fill(memoryBar, "DataFill", DataTeal, Image.OriginHorizontal.Left);
		Image stackFill = Fill(memoryBar, "StackFill", StackPurple, Image.OriginHorizontal.Right);
		TMP_Text memoryText = Text(center, "MemoryText", "0 / 0", 6, Label);
		memoryText.alignment = TextAlignmentOptions.MidlineRight;
		Place(memoryText.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0.5f), new Vector2(118, 35), new Vector2(40, 8));

		// 소환검: HP 막대 왼쪽 (#365)
		TMP_Text swordText = Text(center, "SwordLabel", "소환검 0/5", 6, Label);
		Place(swordText.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0.5f), new Vector2(-128, 44), new Vector2(50, 8));
		swordText.alignment = TextAlignmentOptions.MidlineRight;
		var swordPips = new Image[5];
		for (int i = 0; i < swordPips.Length; i++)
		{
			RectTransform pip = NewUI("SwordPip" + (i + 1), center);
			Place(pip, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(-170 + i * 9, 32), new Vector2(6, 10));
			swordPips[i] = pip.gameObject.AddComponent<Image>();
			swordPips[i].sprite = LoadSprite("hud_white");
			swordPips[i].color = Hex("2A2240");
			swordPips[i].raycastTarget = false;
		}

		// 슬롯 8칸
		var frames = new Image[8];
		var keys = new TMP_Text[8];
		var names = new TMP_Text[8];
		var cooldowns = new Image[8];
		var running = new Image[8];
		var icons = new Image[8];
		for (int i = 0; i < 8; i++)
		{
			RectTransform slot = NewUI("Slot" + Keys[i], center);
			Place(slot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-99 + i * 28, 5), new Vector2(22, 22));
			frames[i] = SetImage(slot.gameObject, "hud_slot");
			frames[i].raycastTarget = false;

			// 스킬 아이콘 (#444, 박진우): 칸 가운데 24×24 (판 20px이 칸 안쪽에 맞음). 실행 중·쿨타임 덮개와 키 글자가 위에 그려진다
			RectTransform iconRect = NewUI("Icon", slot);
			Place(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
			icons[i] = iconRect.gameObject.AddComponent<Image>();
			icons[i].raycastTarget = false;
			icons[i].preserveAspect = true;
			icons[i].enabled = false;

			RectTransform runRect = NewUI("Running", slot);
			Stretch(runRect, 1);
			running[i] = runRect.gameObject.AddComponent<Image>();
			running[i].sprite = LoadSprite("hud_white");
			running[i].color = new Color(0.37f, 0.95f, 0.84f, 0.45f);
			running[i].raycastTarget = false;
			running[i].enabled = false;

			RectTransform coolRect = NewUI("Cooldown", slot);
			Stretch(coolRect, 1);
			cooldowns[i] = coolRect.gameObject.AddComponent<Image>();
			cooldowns[i].sprite = LoadSprite("hud_white");
			cooldowns[i].color = new Color(0f, 0f, 0f, 0.65f);
			cooldowns[i].type = Image.Type.Filled;
			cooldowns[i].fillMethod = Image.FillMethod.Radial360;
			cooldowns[i].fillOrigin = (int)Image.Origin360.Top;
			cooldowns[i].fillClockwise = false; // 줄어들 때 걷히는 끝이 12시에서 시계 방향으로 돈다 (#431)
			cooldowns[i].raycastTarget = false;
			cooldowns[i].enabled = false;

			keys[i] = Text(slot, "Key", Keys[i], 9, Color.white);
			keys[i].alignment = TextAlignmentOptions.Center;
			keys[i].fontStyle = FontStyles.Bold;
			Stretch(keys[i].rectTransform);

			// 장착된 스킬 이름: 마우스를 올렸을 때만 칸 위에 말풍선으로 (SlotHoverTip)
			frames[i].raycastTarget = true;
			RectTransform tip = NewUI("Tip", slot);
			Place(tip, new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 3), new Vector2(0, 11));
			Image tipImage = SetImage(tip.gameObject, "hud_gauge"); // 불투명 바탕 + 테두리 (뒤의 Memory 글자가 비치지 않게)
			tipImage.raycastTarget = false;
			HorizontalLayoutGroup tipLayout = tip.gameObject.AddComponent<HorizontalLayoutGroup>();
			tipLayout.padding = new RectOffset(4, 4, 1, 1);
			tipLayout.childControlWidth = tipLayout.childControlHeight = true;
			tipLayout.childForceExpandWidth = tipLayout.childForceExpandHeight = false;
			ContentSizeFitter tipFit = tip.gameObject.AddComponent<ContentSizeFitter>();
			tipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			names[i] = Text(tip, "Name", "", 6, Color.white);
			names[i].alignment = TextAlignmentOptions.Center;
			names[i].textWrappingMode = TextWrappingModes.NoWrap;
			SlotHoverTip hover = slot.gameObject.AddComponent<SlotHoverTip>();
			var hoverSo = new SerializedObject(hover);
			hoverSo.FindProperty("tip").objectReferenceValue = tip.gameObject;
			hoverSo.FindProperty("label").objectReferenceValue = names[i];
			hoverSo.ApplyModifiedPropertiesWithoutUndo();
			tip.gameObject.SetActive(false);
		}

		// 오른쪽 아래: 스킬 트리 · 레벨업
		RectTransform shortcuts = NewUI("Shortcuts", band);
		Place(shortcuts, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 4), new Vector2(90, 28));
		SetImage(shortcuts.gameObject, "hud_bar");
		Button skillTree = Shortcut(shortcuts, "SkillTreeButton", "스킬 트리", "1", new Vector2(3, 0));
		Button levelUp = Shortcut(shortcuts, "LevelUpButton", "레벨업", "2", new Vector2(46, 0));
		hideInRaid.Add(shortcuts.gameObject);

		// 칸 연결 ------------------------------------------------------------------
		var so = new SerializedObject(view);
		Set(so, "root", root.gameObject);
		Set(so, "settingsButton", settingsButton);
		Set(so, "levelText", level);
		Set(so, "bugFragmentText", bug);
		Set(so, "bossCoreText", core);
		Set(so, "placeText", place);
		Set(so, "swordText", swordText);
		Array(so, "swordPips", swordPips);
		Array(so, "hideInRaid", hideInRaid.ToArray());
		Set(so, "hpFill", hpFill);
		Set(so, "hpTrail", hpTrail);
		Set(so, "hpText", hpText);
		Set(so, "heapFill", heapFill);
		Set(so, "dataFill", dataFill);
		Set(so, "stackFill", stackFill);
		Set(so, "memoryText", memoryText);
		Array(so, "slotFrames", frames);
		Array(so, "slotKeys", keys);
		Array(so, "slotNames", names);
		Array(so, "slotCooldowns", cooldowns);
		Array(so, "slotRunning", running);
		Array(so, "slotIcons", icons);
		Set(so, "slotSprite", LoadSprite("hud_slot"));
		Set(so, "slotEmptySprite", LoadSprite("hud_slot_empty"));
		Set(so, "slotRejectedSprite", LoadSprite("hud_slot_rejected"));
		Set(so, "skillTreeButton", skillTree);
		Set(so, "levelUpButton", levelUp);
		so.ApplyModifiedPropertiesWithoutUndo();

		// Esc: 다른 화면이 열려 있지 않을 때 설정 열기 (UIShortcut #298, 맨 위 화면만 반응)
		UIShortcut shortcut = root.gameObject.AddComponent<UIShortcut>();
		Bind(shortcut, "onCancel", view.OpenSettings);
		// 숫자 1·2: 스킬 트리 · 레벨업 바로가기 (#431). 같은 UIShortcut이라 다른 창이 열려 있으면 반응하지 않는다
		Bind(shortcut, "onShortcut1", view.OpenSkillTreeByKey);
		Bind(shortcut, "onShortcut2", view.OpenLevelUpByKey);

		root.gameObject.SetActive(false);
		PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[HudUIBuilder] {PrefabPath}를 만듦. Base 씬에 끌어다 놓으세요.");
	}

	// 부품 ---------------------------------------------------------------------

	static GameObject Divider(RectTransform bar)
	{
		RectTransform line = NewUI("Divider", bar);
		Image image = line.gameObject.AddComponent<Image>();
		image.sprite = LoadSprite("hud_white");
		image.color = Hex("4A3A6A");
		image.raycastTarget = false;
		Layout(line.gameObject, 1, 12);
		return line.gameObject;
	}

	// 재료 그림 (#431): 박진우의 재료 드롭 그림(material_{ID}.png 가로 시트)의 첫 장. 16×16 칸에 비율을 지켜 넣는다
	static GameObject MaterialIcon(RectTransform bar, string materialId)
	{
		RectTransform icon = NewUI("Icon", bar);
		Image image = icon.gameObject.AddComponent<Image>();
		image.raycastTarget = false;
		image.preserveAspect = true;
		string path = $"Assets/_JW/Effects/Materials/material_{materialId}.png";
		Sprite first = null;
		foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
			if (asset is Sprite sprite && (first == null || string.CompareOrdinal(sprite.name, first.name) < 0)) first = sprite;
		if (first == null) Debug.LogWarning($"[HudUIBuilder] {path}에서 그림을 찾지 못함");
		image.sprite = first;
		Layout(icon.gameObject, 16, 16);
		return icon.gameObject;
	}

	// 머리 위 닉네임 이름표 (#362): 화면 전체 크기 영역 + 이름표 8개 (작은 어두운 바탕 + 글자)
	static void BuildNameTags(RectTransform root)
	{
		RectTransform area = NewUI("NameTags", root);
		Stretch(area);
		area.SetAsFirstSibling(); // 다른 HUD 아래에 그린다
		NameTagView view = area.gameObject.AddComponent<NameTagView>();

		const int count = 8;
		var tags = new RectTransform[count];
		var texts = new TMP_Text[count];
		for (int i = 0; i < count; i++)
		{
			RectTransform tag = NewUI("Tag" + (i + 1), area);
			tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 0.5f);
			tag.pivot = new Vector2(0.5f, 0f);
			Image back = tag.gameObject.AddComponent<Image>();
			back.sprite = LoadSprite("hud_white");
			back.color = new Color(0.04f, 0.03f, 0.08f, 0.55f);
			back.raycastTarget = false;
			HorizontalLayoutGroup layout = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
			layout.padding = new RectOffset(3, 3, 1, 1);
			layout.childControlWidth = layout.childControlHeight = true;
			layout.childForceExpandWidth = layout.childForceExpandHeight = false;
			ContentSizeFitter fitter = tag.gameObject.AddComponent<ContentSizeFitter>();
			fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			texts[i] = Text(tag, "Text", "닉네임", 6, Color.white);
			texts[i].alignment = TextAlignmentOptions.Center;
			tags[i] = tag;
			tag.gameObject.SetActive(false);
		}

		var so = new SerializedObject(view);
		Array(so, "tags", tags);
		Array(so, "texts", texts);
		so.ApplyModifiedPropertiesWithoutUndo();
	}

	// 레이드 파티원 목록 (시안 8-3): 장소 이름 아래, 「파티」 + 2줄 (레이드 최대 3명에서 나를 뺀 수)
	static void BuildRaidParty(RectTransform root)
	{
		RectTransform party = NewUI("RaidParty", root);
		Place(party, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -102), new Vector2(124, 80));
		RaidPartyView view = party.gameObject.AddComponent<RaidPartyView>();

		RectTransform panel = NewUI("Panel", party);
		Stretch(panel);
		TMP_Text title = Text(panel, "Title", "파티", 6, Muted);
		Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(60, 9));

		const int rows = 2;
		var rowImages = new Image[rows];
		var names = new TMP_Text[rows];
		var fills = new Image[rows];
		var dead = new TMP_Text[rows];
		for (int i = 0; i < rows; i++)
		{
			RectTransform row = NewUI("Member" + (i + 1), panel);
			Place(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -12 - i * 32), new Vector2(124, 26));
			rowImages[i] = SetImage(row.gameObject, "hud_party");
			rowImages[i].raycastTarget = false;

			names[i] = Text(row, "Name", "[파티원 1]", 7, Color.white);
			names[i].fontStyle = FontStyles.Bold;
			Place(names[i].rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(7, -8), new Vector2(80, 10));

			dead[i] = Text(row, "Dead", "사망", 6, Muted);
			dead[i].alignment = TextAlignmentOptions.MidlineRight;
			Place(dead[i].rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-7, -8), new Vector2(30, 10));
			dead[i].gameObject.SetActive(false);

			TMP_Text hp = Text(row, "HpLabel", "HP", 4, Label);
			Place(hp.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(7, -18), new Vector2(12, 6));
			RectTransform bar = NewUI("HpBar", row);
			Place(bar, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(22, -18), new Vector2(90, 3));
			fills[i] = bar.gameObject.AddComponent<Image>();
			fills[i].sprite = LoadSprite("hud_white");
			fills[i].color = HpRed;
			fills[i].type = Image.Type.Filled;
			fills[i].fillMethod = Image.FillMethod.Horizontal;
			fills[i].fillOrigin = (int)Image.OriginHorizontal.Left;
			fills[i].raycastTarget = false;
		}

		var so = new SerializedObject(view);
		Set(so, "panel", panel.gameObject);
		Array(so, "rowImages", rowImages);
		Array(so, "nameTexts", names);
		Array(so, "hpFills", fills);
		Array(so, "deadTexts", dead);
		Set(so, "aliveSprite", LoadSprite("hud_party"));
		Set(so, "deadSprite", LoadSprite("hud_party_dead"));
		so.ApplyModifiedPropertiesWithoutUndo();
		panel.gameObject.SetActive(false);
	}

	// 보스 체력 바 (시안 8-3): 위쪽 가운데. 움직임은 김도훈의 BossHealthBar 그대로 (보스가 있을 때만 보임)
	static void BuildBossBar(RectTransform root)
	{
		RectTransform holder = NewUI("BossBar", root);
		Place(holder, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(300, 24));
		BossHealthBar bossBar = holder.gameObject.AddComponent<BossHealthBar>();

		RectTransform panel = NewUI("Panel", holder);
		Stretch(panel);

		TMP_Text name = Text(panel, "Name", "[보스 이름]", 7, HpRed);
		name.fontStyle = FontStyles.Bold;
		Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(200, 10));
		TMP_Text percent = Text(panel, "Percent", "100%", 6, Label);
		percent.alignment = TextAlignmentOptions.MidlineRight;
		Place(percent.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(60, 10));

		RectTransform frame = NewUI("Frame", panel);
		Place(frame, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -12), new Vector2(300, 8));
		SetImage(frame.gameObject, "hud_boss").raycastTarget = false;
		Image trail = Fill(frame, "Trail", Color.white, Image.OriginHorizontal.Left);
		Image fill = Fill(frame, "Fill", HpRed, Image.OriginHorizontal.Left);
		fill.fillAmount = trail.fillAmount = 1f;

		// 페이즈 구분선 70%, 40% (BossHealthBar가 위치를 맞춤)
		var marks = new RectTransform[2];
		for (int i = 0; i < marks.Length; i++)
		{
			marks[i] = NewUI("PhaseMark" + (i + 1), frame);
			Image mark = marks[i].gameObject.AddComponent<Image>();
			mark.sprite = LoadSprite("hud_white");
			mark.color = new Color(0.06f, 0.05f, 0.1f, 0.9f);
			mark.raycastTarget = false;
			// BossHealthBar가 앵커를 (비율, 0.5)로 옮기므로 높이는 sizeDelta로 준다
			marks[i].anchorMin = marks[i].anchorMax = new Vector2(i == 0 ? 0.7f : 0.4f, 0.5f);
			marks[i].sizeDelta = new Vector2(1, 6);
			marks[i].anchoredPosition = Vector2.zero;
		}

		BossBarPercent percentView = panel.gameObject.AddComponent<BossBarPercent>();
		var percentSo = new SerializedObject(percentView);
		Set(percentSo, "fill", fill);
		Set(percentSo, "text", percent);
		percentSo.ApplyModifiedPropertiesWithoutUndo();

		var so = new SerializedObject(bossBar);
		Set(so, "panel", panel.gameObject);
		Set(so, "fill", fill);
		Set(so, "trail", trail);
		Set(so, "nameText", name);
		Array(so, "phaseMarks", marks);
		so.ApplyModifiedPropertiesWithoutUndo();
		panel.gameObject.SetActive(false);
	}

	// 바탕 + 왼쪽(또는 오른쪽)부터 차는 막대 하나
	static (Image, RectTransform) Gauge(RectTransform parent, string name, Vector2 position, Color color, Image.OriginHorizontal origin)
	{
		RectTransform gauge = NewUI(name, parent);
		Place(gauge, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), position, new Vector2(156, 5));
		SetImage(gauge.gameObject, "hud_gauge").raycastTarget = false;
		return (Fill(gauge, "Fill", color, origin), gauge);
	}

	static Image Fill(RectTransform gauge, string name, Color color, Image.OriginHorizontal origin)
	{
		RectTransform r = NewUI(name, gauge);
		Stretch(r, 1);
		Image fill = r.gameObject.AddComponent<Image>();
		fill.sprite = LoadSprite("hud_white");
		fill.color = color;
		fill.type = Image.Type.Filled;
		fill.fillMethod = Image.FillMethod.Horizontal;
		fill.fillOrigin = (int)origin;
		fill.fillAmount = 0;
		fill.raycastTarget = false;
		return fill;
	}

	static Button Shortcut(RectTransform parent, string name, string label, string key, Vector2 left)
	{
		RectTransform r = NewUI(name, parent);
		Place(r, new Vector2(0, 0.5f), new Vector2(0, 0.5f), left, new Vector2(41, 24));
		Image hit = r.gameObject.AddComponent<Image>();
		hit.color = new Color(0, 0, 0, 0);
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = hit;
		// 아이콘 자리에 바로가기 숫자 키 (#431, Esc 키 표시와 같은 모양)
		RectTransform keyBox = NewUI("Key", r);
		Place(keyBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(11, 11));
		SetImage(keyBox.gameObject, "hud_key").raycastTarget = false;
		TMP_Text keyText = Text(keyBox, "Text", key, 6, Label);
		Stretch(keyText.rectTransform);
		keyText.alignment = TextAlignmentOptions.Center;
		TMP_Text text = Text(r, "Label", label, 6, Label);
		text.alignment = TextAlignmentOptions.Bottom;
		Place(text.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(41, 9));
		return button;
	}

	static void Bind(UIShortcut shortcut, string fieldName, UnityAction action)
	{
		FieldInfo field = typeof(UIShortcut).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (field == null) return;
		var unityEvent = field.GetValue(shortcut) as UnityEvent;
		if (unityEvent == null)
		{
			unityEvent = new UnityEvent();
			field.SetValue(shortcut, unityEvent);
		}
		UnityEventTools.AddPersistentListener(unityEvent, action);
	}

	// 기본 도구 -------------------------------------------------------------------

	static TMP_Text Text(Transform parent, string name, string value, float size, Color color)
	{
		RectTransform r = NewUI(name, parent);
		TextMeshProUGUI text = r.gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null) text.font = font;
		text.text = value;
		text.fontSize = size;
		text.color = color;
		text.alignment = TextAlignmentOptions.MidlineLeft;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.raycastTarget = false;
		return text;
	}

	// 글자 길이만큼 넓어지되 최소 너비는 지킨다 (띠의 HorizontalLayoutGroup이 글자의 preferred width를 씀)
	static void Fit(TMP_Text text, float minWidth)
	{
		LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
		element.minWidth = minWidth;
		element.minHeight = element.preferredHeight = 12;
	}

	static void Layout(GameObject go, float width, float height)
	{
		LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
		element.minWidth = element.preferredWidth = width;
		element.minHeight = element.preferredHeight = height;
	}

	static Image SetImage(GameObject go, string sprite)
	{
		Image image = go.AddComponent<Image>();
		image.sprite = LoadSprite(sprite);
		image.type = Image.Type.Sliced;
		return image;
	}

	static Sprite LoadSprite(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");
		if (sprite == null) Debug.LogWarning($"[HudUIBuilder] {Dir}/{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	// 부드럽게 늘어나야 하는 그림(그라데이션): 스프라이트, 선형 필터, 압축 없음으로 가져온다
	static Sprite LoadSmoothSprite(string name)
	{
		string path = $"{Dir}/{name}.png";
		if (AssetImporter.GetAtPath(path) is TextureImporter importer
			&& (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Bilinear || importer.textureCompression != TextureImporterCompression.Uncompressed))
		{
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Single;
			importer.filterMode = FilterMode.Bilinear;
			importer.mipmapEnabled = false;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();
		}
		return LoadSprite(name);
	}

	static void Set(SerializedObject so, string property, Object value)
	{
		SerializedProperty p = so.FindProperty(property);
		if (p == null) Debug.LogWarning($"[HudUIBuilder] HudView에 {property} 칸이 없음");
		else p.objectReferenceValue = value;
	}

	static void Array(SerializedObject so, string property, Object[] values)
	{
		SerializedProperty p = so.FindProperty(property);
		p.arraySize = values.Length;
		for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Stretch(RectTransform r, float inset = 0)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.offsetMin = new Vector2(inset, inset);
		r.offsetMax = new Vector2(-inset, -inset);
	}

	static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
	{
		r.anchorMin = r.anchorMax = anchor;
		r.pivot = pivot;
		r.sizeDelta = size;
		r.anchoredPosition = position;
	}

	static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
}
