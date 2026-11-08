using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 트리 화면. 문법·스킬 탭, 노드와 선행 선, 고른 노드의 설명, 「해금」 버튼. 로비 스킬 트리 창(SkillTreePanel) 안에 둔다.
/// <para>
/// - 노드·선·계열 이름은 프리팹(Node Prefab, Line Prefab, Lane Label Prefab)에서 만들고 지우는 일은 ClearTree 한 곳에서 한다 (CLAUDE.md 코드 작성 규칙 3, 4).<br/>
///   탭 버튼, 설명 글자, 해금 버튼은 창 안에 미리 놓아 두고 칸에 넣는다.<br/>
/// - 노드 위치는 SkillLayout·SyntaxLayout 표로 정한다. 표에 없는 노드는 맨 아래 「기타」 줄에 놓는다.<br/>
///   문법은 세로 한 줄이고, 트리 칸보다 길면 세로 스크롤을 켠다 (UpdateScroll).<br/>
///   같은 계열 안의 선행은 선으로 잇고, 다른 계열의 선행(예: 검 소환)은 이름 아래 「+ 검 소환」으로 적는다.<br/>
/// - 창이 열리면 내 캐릭터의 PlayerProgress에서 해금 목록과 재료를 읽고, PlayerProgress.Changed마다 다시 그린다. 내 캐릭터가 없으면(네트워크 없는 시험) SetUnlocked로 받은 목록, 그것도 없으면 처음부터 해금된 노드만.<br/>
/// - 「해금」 버튼은 UnlockRequested(노드 ID)를 알리고, 내 캐릭터의 PlayerProgress.RequestUnlock(이우림 #315)으로 서버에 해금을 요청한다.
///   서버가 재료를 빼고 해금·저장하며, 결과는 PlayerProgress.UnlockAnswered로 받아 실패 이유를 설명 칸에 적는다.
/// </para>
/// </summary>
public class SkillTreeView : MonoBehaviour
{
	/// <summary>[로직] 「해금」 버튼을 눌렀을 때 (노드 ID). 선행 조건은 채웠을 때만 불린다. 재료 확인·해금은 받는 쪽이 한다.</summary>
	public static event Action<string> UnlockRequested;

	[Header("프리팹")]
	[SerializeField, Tooltip("노드 하나 (SkillTreeNode 프리팹: Image + Button + Outline + SkillTreeNodeView, 자식 글자)")]
	SkillTreeNodeView nodePrefab;
	[SerializeField, Tooltip("선 한 토막 (Image, Raycast Target 끔). 길이·위치는 코드가 정한다")]
	Image linePrefab;
	[SerializeField, Tooltip("계열 이름 (Text - TextMeshPro, Raycast Target 끔)")]
	TMP_Text laneLabelPrefab;

	[Header("화면")]
	[SerializeField, Tooltip("노드와 선이 만들어질 곳. Scroll Rect의 Content (Anchor·Pivot 왼쪽 위)")]
	RectTransform treeContent;
	[SerializeField] Button syntaxTab;
	[SerializeField] Button skillTab;
	[SerializeField, Tooltip("고른 노드 이름")]
	TMP_Text detailTitle;
	[SerializeField, Tooltip("고른 노드 설명 (선행, 재료, 같이 열림, 여는 블록)")]
	TMP_Text detailBody;
	[SerializeField] Button unlockButton;

	// 크기 (LobbyUI Canvas 1920×1080 기준) -----------------------------------------
	const float NodeWidth = 170f, NodeHeight = 48f, ColumnGap = 28f, RowGap = 10f; // 스킬 탭 전체 약 1122×668. 시안 9 창의 트리 칸(1158×702)에 스크롤 없이 들어감
	// 문법 탭은 세로 한 줄이라 줄 사이를 넓힌다 (캔버스 9: 노드 44 : 줄 72). 나중에 노드 옆 설명이 들어갈 높이이고, 트리 칸보다 길어져 세로 스크롤이 생긴다
	const float SyntaxRowGap = 42f;
	const float WheelSensitivity = 10f; // 트리 칸 Scroll Rect의 휠 감도 (UpdateScroll)
	// 문법 탭 노드 옆 짧은 설명. 트리 칸(약 1140) - 노드 오른쪽 끝(310) - 간격 - 스크롤 막대(12)
	const float SummaryGap = 24f, SummaryWidth = 760f, SummaryFontSize = 20f;
	const float LaneLabelWidth = 120f, Padding = 20f, LineWidth = 4f;

	// 색 -----------------------------------------------------------------------
	// 시안 9 색: 해금됨 민트 바탕, 해금할 수 있음 주황 테두리, 잠김 회색 테두리, 고른 노드 흰 테두리
	static readonly Color TabColor = new Color32(0x1D, 0x0A, 0x28, 0xFF);
	static readonly Color TabSelectedColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);
	static readonly Color TabOutline = new Color32(0x46, 0x3C, 0x55, 0xFF);
	static readonly Color SyntaxUnlockedColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);
	static readonly Color SkillUnlockedColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);
	static readonly Color AvailableFill = new Color32(0x1A, 0x12, 0x28, 0xFF);
	static readonly Color AvailableOutline = new Color32(0xE8, 0xA8, 0x67, 0xFF);
	static readonly Color LockedFill = new Color32(0x1A, 0x12, 0x28, 0xFF);
	static readonly Color LockedOutline = new Color32(0x3A, 0x35, 0x50, 0xFF);
	static readonly Color LockedText = new Color32(0x8A, 0x84, 0xA8, 0xFF); // 잠긴 노드 글자
	static readonly Color LineOn = new Color32(0x8A, 0x84, 0xA8, 0xFF);
	static readonly Color LineOff = new Color32(0x3A, 0x35, 0x50, 0xFF);
	const string Good = "#5EF2D6", Bad = "#E07070", Dim = "#8A84A8", Warn = "#E8A867", UnlockedText = "#1A1E3A";
	const string DescriptionText = "#B8B3C7"; // 설명 글자 (캔버스 9의 설명 색). 이름표(Dim)보다 밝게
	static readonly Color SummaryColor = new Color32(0xB8, 0xB3, 0xC7, 0xFF);

	// 노드 위치: (계열, 칸, 줄). 줄은 위에서부터 --------------------------------------
	static readonly string[] SkillLanes = { "소환", "찌르기", "던지기", "휘두르기", "이동" };
	static readonly Dictionary<string, (int lane, int col, int row)> SkillLayout = new Dictionary<string, (int, int, int)>
	{
		[PredefinedFunctionIds.SummonSword] = (0, 0, 0),
		[PredefinedFunctionIds.FreeSword] = (0, 1, 0),
		[PredefinedFunctionIds.Mark] = (0, 1, 1),
		[PredefinedFunctionIds.SwordDance] = (0, 2, 1),
		[PredefinedFunctionIds.Parry] = (0, 0, 2),

		[PredefinedFunctionIds.Thrust] = (1, 0, 3),
		[PredefinedFunctionIds.FocusedThrust] = (1, 1, 3),
		[PredefinedFunctionIds.VitalStrike] = (1, 2, 3),
		[PredefinedFunctionIds.Skewer] = (1, 1, 4),
		[PredefinedFunctionIds.SummonedThrust] = (1, 1, 5),
		[PredefinedFunctionIds.WoundExploit] = (1, 2, 5),
		[PredefinedFunctionIds.DeepThrust] = (1, 2, 6),

		[PredefinedFunctionIds.SwordThrow] = (2, 0, 7),
		[PredefinedFunctionIds.SummonedThrow] = (2, 1, 7),
		[PredefinedFunctionIds.SwordReturn] = (2, 2, 7),

		[PredefinedFunctionIds.SwordSlash] = (3, 0, 8),
		[PredefinedFunctionIds.WideSlash] = (3, 1, 8),
		[PredefinedFunctionIds.SummonedSlash] = (3, 2, 8),
		[PredefinedFunctionIds.WoundSlash] = (3, 2, 9),
		[PredefinedFunctionIds.MultiSlash] = (3, 3, 8),
		[PredefinedFunctionIds.BattlefieldSweep] = (3, 4, 8),

		[PredefinedFunctionIds.Dash] = (4, 0, 10),
		[PredefinedFunctionIds.DoubleJump] = (4, 1, 10),
		[PredefinedFunctionIds.SwordRide] = (4, 2, 10),
	};

	// 문법은 한 계열. 앞 단계가 선행이라 위에서 아래로 한 줄 (같은 칸끼리는 Connect가 세로선으로 잇는다)
	static readonly string[] SyntaxLanes = { "문법" };
	static readonly Dictionary<string, (int lane, int col, int row)> SyntaxLayout = new Dictionary<string, (int, int, int)>
	{
		[SkillTreeDefaults.Sequence] = (0, 0, 0),
		[SkillTreeDefaults.Variable] = (0, 0, 1),
		[SkillTreeDefaults.Arithmetic] = (0, 0, 2),
		[SkillTreeDefaults.If] = (0, 0, 3),
		[SkillTreeDefaults.Logic] = (0, 0, 4),
		[SkillTreeDefaults.For] = (0, 0, 5),
		[SkillTreeDefaults.While] = (0, 0, 6),
		[SkillTreeDefaults.Function] = (0, 0, 7),
		[SkillTreeDefaults.List] = (0, 0, 8),
		[SkillTreeDefaults.TypeDefinition] = (0, 0, 9),
	};

	class Placed
	{
		public SkillTreeNode Node;
		public int Lane, Column, Row;
		public Vector2 TopLeft;
		public SkillTreeNodeView View;
		public readonly List<(Image line, string from)> Lines = new List<(Image, string)>();
	}

	SkillTreeNodeKind tab = SkillTreeNodeKind.Skill;
	float rowGap = RowGap; // 지금 탭의 줄 간격 (스킬 RowGap, 문법 SyntaxRowGap)
	string selectedId;
	HashSet<string> unlocked;
	readonly Dictionary<string, Placed> placed = new Dictionary<string, Placed>();
	readonly List<GameObject> spawned = new List<GameObject>();

	void Awake()
	{
		if (syntaxTab != null) syntaxTab.onClick.AddListener(() => ShowTab(SkillTreeNodeKind.Syntax));
		if (skillTab != null) skillTab.onClick.AddListener(() => ShowTab(SkillTreeNodeKind.Skill));
		if (unlockButton != null) unlockButton.onClick.AddListener(RequestUnlock);
	}

	PlayerProgress progress; // 내 캐릭터의 진행도. 없으면 null
	string failedId;         // 방금 해금에 실패한 노드 (설명 칸에 이유를 적는다)
	UnlockResult failedResult;

	void OnEnable()
	{
		progress = null;
		NetworkIdentity me = NetworkClient.localPlayer;
		if (me != null) progress = me.GetComponent<PlayerProgress>();
		if (progress != null)
		{
			unlocked = new HashSet<string>(progress.Unlocked);
			if (unlocked.Count == 0) unlocked = null; // 임시 계정: Unlocked가 처음 노드로 채운다
			progress.Changed += OnProgressChanged;
			progress.UnlockAnswered += OnUnlockAnswered;
		}
		failedId = null;
		ShowTab(tab);
	}

	void OnDisable()
	{
		if (progress != null)
		{
			progress.Changed -= OnProgressChanged;
			progress.UnlockAnswered -= OnUnlockAnswered;
		}
		progress = null;
	}

	void OnProgressChanged()
	{
		if (progress != null) SetUnlocked(new List<string>(progress.Unlocked));
	}

	void OnUnlockAnswered(string nodeId, UnlockResult result)
	{
		failedId = result == UnlockResult.Unlocked ? null : nodeId;
		failedResult = result;
		RefreshDetail();
	}

	// 서버가 해금을 거부한 이유 (UnlockResult, progress-store.md 「노드 해금 검사」)
	static string FailText(UnlockResult result) => result switch
	{
		UnlockResult.MaterialShort => "재료가 모자라요",
		UnlockResult.PrerequisiteMissing => "선행 노드를 먼저 해금하세요",
		UnlockResult.AlreadyUnlocked => "이미 해금했어요",
		_ => "지금은 해금할 수 없어요",
	};

	/// <summary>
	/// 해금한 노드 ID들로 화면을 다시 그린다. 「재료 써서 스킬 트리 해금하기」가 창을 열 때와 해금한 뒤에 부른다.
	/// </summary>
	public void SetUnlocked(ICollection<string> unlockedIds)
	{
		unlocked = new HashSet<string>(unlockedIds ?? Array.Empty<string>());
		// 해금 목록이 비어 있으면(로그인 건너뛰기 임시 계정) 처음부터 해금된 노드를 보여 준다. 실제 계정은 늘 처음 노드가 들어 있다
		if (unlocked.Count == 0)
		{
			foreach (string id in SkillTree.StartingNodes()) unlocked.Add(id);
			foreach (string id in SkillTree.AutoUnlockedBy(unlocked)) unlocked.Add(id);
		}
		Refresh();
	}

	ICollection<string> Unlocked
	{
		get
		{
			// 해금 목록을 아직 받지 않았으면 처음부터 해금된 노드만 (새 캐릭터와 같음)
			if (unlocked == null)
			{
				unlocked = new HashSet<string>(SkillTree.StartingNodes());
				foreach (string id in SkillTree.AutoUnlockedBy(unlocked)) unlocked.Add(id);
			}
			return unlocked;
		}
	}

	// 트리 그리기 ---------------------------------------------------------------

	void ShowTab(SkillTreeNodeKind kind)
	{
		tab = kind;
		SetTabColor(syntaxTab, kind == SkillTreeNodeKind.Syntax);
		SetTabColor(skillTab, kind == SkillTreeNodeKind.Skill);

		ClearTree();
		if (SkillTree.Instance == null || treeContent == null || nodePrefab == null)
		{
			if (detailBody != null) detailBody.text = "SkillTree 에셋 또는 화면 칸이 비어 있음";
			return;
		}

		bool skills = kind == SkillTreeNodeKind.Skill;
		rowGap = skills ? RowGap : SyntaxRowGap;
		string[] lanes = skills ? SkillLanes : SyntaxLanes;
		Dictionary<string, (int lane, int col, int row)> layout = skills ? SkillLayout : SyntaxLayout;

		// 위치 정하기. 표에 없는 노드는 맨 아래 「기타」 줄
		int lastRow = layout.Values.Select(p => p.row).DefaultIfEmpty(-1).Max();
		int extraRow = lastRow + 1, extraColumn = 0, maxColumn = 0;
		foreach (SkillTreeNode node in SkillTree.Instance.Nodes.Where(n => n.Kind == kind))
		{
			(int lane, int col, int row) p = layout.TryGetValue(node.Id, out var known) ? known : (lanes.Length, extraColumn++, extraRow);
			placed[node.Id] = new Placed
			{
				Node = node, Lane = p.lane, Column = p.col, Row = p.row,
				TopLeft = new Vector2(Padding + LaneLabelWidth + p.col * (NodeWidth + ColumnGap), -(Padding + p.row * (NodeHeight + rowGap))),
			};
			maxColumn = Mathf.Max(maxColumn, p.col);
			lastRow = Mathf.Max(lastRow, p.row);
		}

		// 계열 이름: 그 계열의 가장 위 줄 왼쪽
		if (laneLabelPrefab != null)
		{
			for (int lane = 0; lane <= lanes.Length; lane++)
			{
				var inLane = placed.Values.Where(w => w.Lane == lane).ToList();
				if (inLane.Count == 0) continue;
				TMP_Text label = Spawn(laneLabelPrefab);
				label.text = lane < lanes.Length ? lanes[lane] : "기타";
				Place(label.rectTransform, new Vector2(Padding, -(Padding + inLane.Min(w => w.Row) * (NodeHeight + rowGap))), new Vector2(LaneLabelWidth - 4, NodeHeight));
			}
		}

		// 선 먼저 (노드 뒤에 그려지게). 같은 계열 안의 선행만
		if (linePrefab != null)
		{
			foreach (Placed child in placed.Values)
			{
				foreach (string prerequisite in child.Node.Prerequisites)
				{
					if (!placed.TryGetValue(prerequisite, out Placed parent) || parent.Lane != child.Lane) continue;
					foreach (Image line in Connect(parent, child)) child.Lines.Add((line, prerequisite));
				}
			}
		}

		foreach (Placed p in placed.Values)
		{
			p.View = Spawn(nodePrefab);
			Place(p.View.Rect, p.TopLeft, new Vector2(NodeWidth, NodeHeight));
			string id = p.Node.Id;
			p.View.Button.onClick.AddListener(() => Select(id));
		}

		// 문법 탭: 노드 옆에 짧은 설명 (스킬 탭은 노드 사이가 좁아 오른쪽 설명 칸에만 보인다)
		if (!skills && laneLabelPrefab != null)
		{
			foreach (Placed p in placed.Values)
			{
				string summary = p.Node.Description?.Summary;
				if (string.IsNullOrEmpty(summary)) continue;
				// 글자 프리팹은 계열 이름과 같은 것을 쓰고 모양만 바꾼다 (새 프리팹 없이)
				TMP_Text label = Spawn(laneLabelPrefab);
				label.richText = false; // 설명의 < > (예: a < b)를 태그로 읽지 않게
				label.text = summary;
				label.fontSize = SummaryFontSize;
				label.color = SummaryColor;
				label.alignment = TextAlignmentOptions.MidlineLeft;
				label.textWrappingMode = TextWrappingModes.Normal;
				label.overflowMode = TextOverflowModes.Overflow;
				label.raycastTarget = false;
				Place(label.rectTransform, new Vector2(p.TopLeft.x + NodeWidth + SummaryGap, p.TopLeft.y), new Vector2(SummaryWidth, NodeHeight));
			}
		}

		treeContent.sizeDelta = new Vector2(
			Padding * 2 + LaneLabelWidth + (maxColumn + 1) * (NodeWidth + ColumnGap) - ColumnGap,
			Padding * 2 + (lastRow + 1) * (NodeHeight + rowGap) - rowGap);
		UpdateScroll();

		if (selectedId == null || !placed.ContainsKey(selectedId))
			selectedId = placed.Values.FirstOrDefault(w => SkillTree.CanUnlock(w.Node.Id, Unlocked))?.Node.Id ?? placed.Keys.FirstOrDefault();
		Refresh();
		ResetDetailScroll();
	}

	// 부모 → 자식 선. 같은 칸이면 세로 한 줄, 아니면 ㄱ자로 꺾는다
	List<Image> Connect(Placed from, Placed to)
	{
		var lines = new List<Image>();
		float fromMidY = from.TopLeft.y - NodeHeight / 2, toMidY = to.TopLeft.y - NodeHeight / 2;
		if (from.Column == to.Column)
		{
			float x = from.TopLeft.x + NodeWidth / 2;
			bool down = to.Row > from.Row;
			lines.Add(Segment(new Vector2(x, down ? from.TopLeft.y - NodeHeight : from.TopLeft.y), new Vector2(x, down ? to.TopLeft.y : to.TopLeft.y - NodeHeight)));
			return lines;
		}
		bool right = to.Column > from.Column;
		float startX = right ? from.TopLeft.x + NodeWidth : from.TopLeft.x;
		float endX = right ? to.TopLeft.x : to.TopLeft.x + NodeWidth;
		float bendX = right ? endX - ColumnGap / 2 : endX + ColumnGap / 2;
		lines.Add(Segment(new Vector2(startX, fromMidY), new Vector2(bendX, fromMidY)));
		if (!Mathf.Approximately(fromMidY, toMidY)) lines.Add(Segment(new Vector2(bendX, fromMidY), new Vector2(bendX, toMidY)));
		lines.Add(Segment(new Vector2(bendX, toMidY), new Vector2(endX, toMidY)));
		return lines;
	}

	Image Segment(Vector2 a, Vector2 b)
	{
		Image line = Spawn(linePrefab);
		RectTransform rect = line.rectTransform;
		rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = (a + b) / 2;
		rect.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) + LineWidth, Mathf.Abs(b.y - a.y) + LineWidth);
		line.raycastTarget = false;
		return line;
	}

	// 트리가 트리 칸보다 길 때만 세로 스크롤을 켠다 (LobbyUI의 TreeScroll은 가로·세로 스크롤이 꺼진 채 저장되어 있음).
	// 탭을 바꾸면 맨 위부터 보이게 되돌린다. Content는 Anchor·Pivot이 왼쪽 위라 y 0이 맨 위
	void UpdateScroll()
	{
		ScrollRect scroll = treeContent.GetComponentInParent<ScrollRect>();
		if (scroll == null) return;
		RectTransform view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
		scroll.horizontal = false;
		scroll.vertical = treeContent.sizeDelta.y > view.rect.height;
		// 휠 값(보통 마우스 한 칸 = 6, InputSystemUIInputModule의 Scroll Delta Per Tick)에 곱하는 값. 한 칸에 60px(문법 한 줄 90px의 2/3).
		// 곱하기만 하므로 터치패드·고해상도 휠처럼 작은 값을 여러 번 보내는 기기도 움직인 양에 비례한다
		scroll.scrollSensitivity = WheelSensitivity;
		scroll.StopMovement();
		treeContent.anchoredPosition = new Vector2(treeContent.anchoredPosition.x, 0);
	}

	static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
	{
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
		rect.anchoredPosition = topLeft;
		rect.sizeDelta = size;
	}

	// 트리 안의 오브젝트를 만드는 곳과 지우는 곳 (규칙 3)
	T Spawn<T>(T prefab) where T : Component
	{
		T made = Instantiate(prefab, treeContent);
		spawned.Add(made.gameObject);
		return made;
	}

	void ClearTree()
	{
		foreach (GameObject go in spawned)
		{
			if (go != null) Destroy(go);
		}
		spawned.Clear();
		placed.Clear();
	}

	// 상태 그리기 ---------------------------------------------------------------

	void Refresh()
	{
		ICollection<string> have = Unlocked;
		foreach (Placed p in placed.Values)
		{
			SkillTreeNode node = p.Node;
			bool isUnlocked = have.Contains(node.Id);
			bool available = !isUnlocked && SkillTree.CanUnlock(node.Id, have);
			bool selected = node.Id == selectedId;

			Color fill = isUnlocked ? (node.Kind == SkillTreeNodeKind.Syntax ? SyntaxUnlockedColor : SkillUnlockedColor)
				: available ? AvailableFill : LockedFill;
			Color border = selected ? Color.white : isUnlocked ? Color.clear : available ? AvailableOutline : LockedOutline;

			// 다른 계열의 선행은 이름 아래에 작게
			var others = node.Prerequisites
				.Where(id => !placed.TryGetValue(id, out Placed w) || w.Lane != p.Lane)
				.Select(id => $"<color={(isUnlocked ? UnlockedText : have.Contains(id) ? Good : Dim)}>+ {NameOf(id)}</color>")
				.ToList();
			// 글자 기본색은 흰색(SkillTreeNodeView). 잠긴 노드만 회색
			// 해금된 노드는 민트 바탕이라 어두운 글자 (시안 9)
			string title = isUnlocked ? $"<color={UnlockedText}><b>{node.DisplayName}</b></color>"
				: available ? node.DisplayName
				: $"<color=#{ColorUtility.ToHtmlStringRGB(LockedText)}>{node.DisplayName}</color>";
			string text = others.Count == 0 ? title : $"{title}\n<size=60%>{string.Join(" ", others)}</size>";

			if (p.View != null) p.View.Show(fill, border, selected ? 2f : 1f, text);
			foreach ((Image line, string from) in p.Lines) line.color = have.Contains(from) ? LineOn : LineOff;
		}
		RefreshDetail();
	}

	void RefreshDetail()
	{
		if (detailTitle == null || detailBody == null) return;
		if (selectedId == null || !SkillTree.TryGet(selectedId, out SkillTreeNode node))
		{
			detailTitle.text = "";
			detailBody.text = "노드를 고르세요";
			if (unlockButton != null) unlockButton.gameObject.SetActive(false);
			return;
		}

		ICollection<string> have = Unlocked;
		bool isUnlocked = have.Contains(node.Id);
		bool available = !isUnlocked && SkillTree.CanUnlock(node.Id, have);
		// 선행은 채웠지만 재료가 모자라면 해금 버튼을 막는다 (서버도 MaterialShort로 거부함)
		bool enough = HasMaterials(node);

		// 스킬은 이름 옆에 블록 편집기에서 부르는 함수 이름 (예: 검 찌르기  thrust)
		detailTitle.text = node.Kind == SkillTreeNodeKind.Skill
			? $"{node.DisplayName}  <color={Dim}>{BlockLabels.PredefinedFunctionName(node.Id)}</color>"
			: node.DisplayName;
		// 시안 9: 왼쪽 회색 이름표, 오른쪽 값 (표처럼 줄 맞춤)
		string Row(string label, string value) => $"<color={Dim}>{label}</color><pos=46%>{value}";
		var lines = new List<string>
		{
			Row("종류", node.Kind == SkillTreeNodeKind.Syntax ? "문법" : "스킬"),
			Row("상태", isUnlocked ? $"<color={Good}>해금됨</color>"
				: available && enough ? $"<color={Warn}>해금할 수 있음</color>"
				: available ? $"<color={Bad}>재료가 모자라요</color>"
				: $"<color={Dim}>잠김</color>"),
			Row("선행", node.Prerequisites.Count == 0 ? "없음"
				: string.Join(", ", node.Prerequisites.Select(id => $"<color={(have.Contains(id) ? Good : Bad)}>{NameOf(id)}</color>"))),
		};
		if (!isUnlocked)
		{
			lines.Add(Row("재료", node.Cost.Count == 0
				? (node.AutoUnlock ? "없음 (선행을 열면 같이 열림)" : "없음")
				: string.Join(", ", node.Cost.Select(CostText))));
		}
		var together = SkillTree.Instance.Nodes.Where(n => n.AutoUnlock && n.Prerequisites.Contains(node.Id)).Select(n => n.DisplayName).ToList();
		lines.Add(Row("같이 열리는 노드", together.Count > 0 ? string.Join(", ", together) : "없음"));
		if (node.UnlocksBlocks.Count > 0) lines.Add(Row("여는 블록", string.Join(", ", node.UnlocksBlocks.Select(BlockName))));
		if (failedId == node.Id && !isUnlocked) lines.Add($"<color={Bad}>해금하지 못했어요. {FailText(failedResult)}</color>");
		// 자세한 설명은 이름 바로 아래 (캔버스 9-1). noparse: 설명의 < > 를 태그로 읽지 않게
		string detail = node.Description?.Detail;
		if (!string.IsNullOrEmpty(detail)) lines.InsertRange(0, new[] { $"<color={DescriptionText}><noparse>{detail}</noparse></color>", "" });
		detailBody.text = string.Join("\n", lines);

		if (unlockButton != null)
		{
			// 선행을 열면 저절로 같이 열리는 노드(해제 등)는 버튼이 없다
			unlockButton.gameObject.SetActive(!(node.AutoUnlock && node.Cost.Count == 0));
			unlockButton.interactable = available && enough;
			TMP_Text label = unlockButton.GetComponentInChildren<TMP_Text>();
			if (label != null) label.text = isUnlocked ? "해금됨" : !available ? "선행 노드를 먼저 해금" : enough ? "해금" : "재료가 모자라요";
		}
	}

	// 버튼 ---------------------------------------------------------------------

	void Select(string id)
	{
		selectedId = id;
		Refresh();
		ResetDetailScroll();
	}

	// 다른 노드를 고르면 설명 칸을 맨 위부터 보이게 한다. 설명 칸 스크롤(DetailScroll, SkillTreeUIBuilder)이 없으면 아무것도 안 함.
	// 해금 결과 등으로 다시 그릴 때(RefreshDetail)는 부르지 않는다. 읽던 자리가 튀지 않게
	void ResetDetailScroll()
	{
		if (detailBody == null) return;
		ScrollRect scroll = detailBody.GetComponentInParent<ScrollRect>();
		if (scroll == null || scroll.content == null || !detailBody.transform.IsChildOf(scroll.content)) return;
		scroll.StopMovement();
		scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, 0); // Content Pivot이 위라 y 0이 맨 위
	}

	// 재료 한 줄. 시안 9: 「버그 조각 4 (보유 34)」. 모자라면 빨강
	string CostText(MaterialCost cost)
	{
		string name = MaterialName(cost.MaterialId);
		if (progress == null) return $"{name} {cost.Count}";
		int have = progress.GetMaterialCount(cost.MaterialId);
		return $"<color={(have >= cost.Count ? "#FFFFFF" : Bad)}>{name} {cost.Count}</color> <color={Dim}>(보유 {have})</color>";
	}

	// 노드의 재료를 다 가졌는가. 내 캐릭터를 모르면(서버 접속 전) 막지 않는다
	bool HasMaterials(SkillTreeNode node)
	{
		if (progress == null) return true;
		return node.Cost.All(cost => progress.GetMaterialCount(cost.MaterialId) >= cost.Count);
	}

	void RequestUnlock()
	{
		if (selectedId == null || !SkillTree.CanUnlock(selectedId, Unlocked)) return;
		Debug.Log($"[SkillTreeView] 해금 요청: {selectedId}");
		failedId = null;
		UnlockRequested?.Invoke(selectedId);
		if (progress != null) progress.RequestUnlock(selectedId);
	}

	// 시안 9: 고른 탭은 민트 바탕 + 어두운 글자, 안 고른 탭은 어두운 바탕 + 테두리 + 흰 글자
	static void SetTabColor(Button tabButton, bool selected)
	{
		if (tabButton == null) return;
		Image image = tabButton.GetComponent<Image>();
		if (image != null) image.color = selected ? TabSelectedColor : TabColor;
		Outline outline = tabButton.GetComponent<Outline>();
		if (outline != null) outline.effectColor = selected ? TabSelectedColor : TabOutline;
		TMP_Text label = tabButton.GetComponentInChildren<TMP_Text>();
		if (label != null) label.color = selected ? new Color32(0x1A, 0x1E, 0x3A, 0xFF) : Color.white;
	}

	// 이름 -----------------------------------------------------------------------

	static string NameOf(string id) => SkillTree.TryGet(id, out SkillTreeNode node) ? node.DisplayName : id;

	// 재료 이름. 재료 ID가 정해지면 같이 고친다 (skill-tree.md 「재료 ID」)
	static string MaterialName(string materialId) => materialId switch
	{
		SkillTreeDefaults.BugFragment => "버그 조각",
		SkillTreeDefaults.FieldBossCore => "사냥터 보스 코어",
		SkillTreeDefaults.BossCore => "레이드 보스 코어",
		_ => materialId,
	};

	static string BlockName(BlockKind kind) => kind switch
	{
		BlockKind.Declare => "선언",
		BlockKind.Assign => "대입",
		BlockKind.Arithmetic => "연산",
		BlockKind.Compare => "비교",
		BlockKind.Logic => "논리",
		BlockKind.Not => "Not",
		BlockKind.Conditional => "조건문",
		BlockKind.For => "for",
		BlockKind.While => "while",
		BlockKind.Break => "break",
		BlockKind.Continue => "continue",
		BlockKind.ArrayDeclare => "배열 만들기",
		BlockKind.Malloc => "malloc",
		BlockKind.Free => "free",
		BlockKind.Function => "함수",
		BlockKind.Return => "return",
		BlockKind.Wait => "기다리기",
		_ => kind.ToString(),
	};
}
