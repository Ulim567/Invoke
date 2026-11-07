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
///   같은 계열 안의 선행은 선으로 잇고, 다른 계열의 선행(예: 검 소환)은 이름 아래 「+ 검 소환」으로 적는다.<br/>
/// - 창이 열리면 내 캐릭터의 PlayerProgress에서 해금 목록과 재료를 읽고, PlayerProgress.Changed마다 다시 그린다. 내 캐릭터가 없으면(네트워크 없는 시험) SetUnlocked로 받은 목록, 그것도 없으면 처음부터 해금된 노드만.<br/>
/// - 「해금」 버튼은 UnlockRequested(노드 ID)만 알린다. SkillTreeUnlocker가 받아 서버에 보내고, 서버의 PlayerProgress.TryUnlock이 재료를 빼고 해금·저장한다. 실패하면 SkillTreeUnlocker.UnlockAnswered로 알려 준다.
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
	const float NodeWidth = 184f, NodeHeight = 52f, ColumnGap = 32f, RowGap = 12f; // 스킬 탭 전체 약 1208×732 (스크롤 없이 창 안에 들어감)
	const float LaneLabelWidth = 120f, Padding = 20f, LineWidth = 4f;

	// 색 -----------------------------------------------------------------------
	static readonly Color TabColor = new Color32(0x2A, 0x2F, 0x45, 0xFF);
	static readonly Color TabSelectedColor = new Color32(0x6A, 0x5A, 0xC9, 0xFF);
	static readonly Color SyntaxUnlockedColor = new Color32(0x2F, 0x8F, 0x86, 0xFF); // BlockLabels 변수 색
	static readonly Color SkillUnlockedColor = new Color32(0x6A, 0x5A, 0xC9, 0xFF);  // BlockLabels 함수 색
	static readonly Color AvailableFill = new Color32(0x2A, 0x2F, 0x45, 0xFF);
	static readonly Color AvailableOutline = new Color32(0xF2, 0xC1, 0x4E, 0xFF);
	static readonly Color LockedFill = new Color32(0x22, 0x25, 0x2F, 0xFF);
	static readonly Color LockedOutline = new Color32(0x3A, 0x3F, 0x52, 0xFF);
	static readonly Color LockedText = new Color32(0x9A, 0xA0, 0xB8, 0xFF); // 잠긴 노드 글자. 어두운 바탕에서도 읽히는 밝은 회색
	static readonly Color LineOn = new Color32(0x9A, 0xA3, 0xC7, 0xFF);
	static readonly Color LineOff = new Color32(0x3A, 0x3F, 0x52, 0xFF);
	const string Good = "#7CE0A0", Bad = "#E07070", Dim = "#8A90A8";

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

	// 문법은 한 계열. 5칸씩 지그재그
	static readonly string[] SyntaxLanes = { "문법" };
	static readonly Dictionary<string, (int lane, int col, int row)> SyntaxLayout = new Dictionary<string, (int, int, int)>
	{
		[SkillTreeDefaults.Sequence] = (0, 0, 0),
		[SkillTreeDefaults.Variable] = (0, 1, 0),
		[SkillTreeDefaults.Arithmetic] = (0, 2, 0),
		[SkillTreeDefaults.If] = (0, 3, 0),
		[SkillTreeDefaults.Logic] = (0, 4, 0),
		[SkillTreeDefaults.For] = (0, 4, 1),
		[SkillTreeDefaults.While] = (0, 3, 1),
		[SkillTreeDefaults.Function] = (0, 2, 1),
		[SkillTreeDefaults.List] = (0, 1, 1),
		[SkillTreeDefaults.TypeDefinition] = (0, 0, 1),
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

	void OnEnable()
	{
		progress = null;
		NetworkIdentity me = NetworkClient.localPlayer;
		if (me != null) progress = me.GetComponent<PlayerProgress>();
		if (progress != null)
		{
			unlocked = new HashSet<string>(progress.Unlocked);
			progress.Changed += OnProgressChanged;
		}
		SkillTreeUnlocker.UnlockAnswered += OnUnlockAnswered;
		failedId = null;
		ShowTab(tab);
	}

	void OnDisable()
	{
		if (progress != null) progress.Changed -= OnProgressChanged;
		progress = null;
		SkillTreeUnlocker.UnlockAnswered -= OnUnlockAnswered;
	}

	void OnProgressChanged()
	{
		if (progress != null) SetUnlocked(new List<string>(progress.Unlocked));
	}

	void OnUnlockAnswered(string nodeId, bool ok)
	{
		failedId = ok ? null : nodeId;
		RefreshDetail();
	}

	/// <summary>
	/// 해금한 노드 ID들로 화면을 다시 그린다. 「재료 써서 스킬 트리 해금하기」가 창을 열 때와 해금한 뒤에 부른다.
	/// </summary>
	public void SetUnlocked(ICollection<string> unlockedIds)
	{
		unlocked = new HashSet<string>(unlockedIds ?? Array.Empty<string>());
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
				TopLeft = new Vector2(Padding + LaneLabelWidth + p.col * (NodeWidth + ColumnGap), -(Padding + p.row * (NodeHeight + RowGap))),
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
				Place(label.rectTransform, new Vector2(Padding, -(Padding + inLane.Min(w => w.Row) * (NodeHeight + RowGap))), new Vector2(LaneLabelWidth - 4, NodeHeight));
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

		treeContent.sizeDelta = new Vector2(
			Padding * 2 + LaneLabelWidth + (maxColumn + 1) * (NodeWidth + ColumnGap) - ColumnGap,
			Padding * 2 + (lastRow + 1) * (NodeHeight + RowGap) - RowGap);

		if (selectedId == null || !placed.ContainsKey(selectedId))
			selectedId = placed.Values.FirstOrDefault(w => SkillTree.CanUnlock(w.Node.Id, Unlocked))?.Node.Id ?? placed.Keys.FirstOrDefault();
		Refresh();
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
				.Select(id => $"<color={(have.Contains(id) ? Good : Dim)}>+ {NameOf(id)}</color>")
				.ToList();
			// 글자 기본색은 흰색(SkillTreeNodeView). 잠긴 노드만 회색
			string title = isUnlocked || available ? node.DisplayName : $"<color=#{ColorUtility.ToHtmlStringRGB(LockedText)}>{node.DisplayName}</color>";
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

		detailTitle.text = node.DisplayName;
		var lines = new List<string>
		{
			$"<color={Dim}>{(node.Kind == SkillTreeNodeKind.Syntax ? "문법" : "스킬")}</color>  " +
			(isUnlocked ? $"<color={Good}>해금됨</color>" : available ? "<color=#F2C14E>해금할 수 있음</color>" : $"<color={Dim}>잠김</color>"),
		};
		if (node.Prerequisites.Count > 0)
			lines.Add("선행  " + string.Join(", ", node.Prerequisites.Select(id => $"<color={(have.Contains(id) ? Good : Bad)}>{NameOf(id)}</color>")));
		if (!isUnlocked)
		{
			lines.Add(node.Cost.Count == 0
				? (node.AutoUnlock ? "재료  없음 (선행을 열면 같이 열림)" : "재료  없음")
				: "재료  " + string.Join(", ", node.Cost.Select(CostText)));
		}
		var together = SkillTree.Instance.Nodes.Where(n => n.AutoUnlock && n.Prerequisites.Contains(node.Id)).Select(n => n.DisplayName).ToList();
		if (together.Count > 0) lines.Add("같이 열림  " + string.Join(", ", together));
		if (node.UnlocksBlocks.Count > 0) lines.Add("여는 블록  " + string.Join(", ", node.UnlocksBlocks.Select(BlockName)));
		if (failedId == node.Id && !isUnlocked) lines.Add($"<color={Bad}>해금하지 못했어요. 재료가 모자라요</color>");
		detailBody.text = string.Join("\n", lines);

		if (unlockButton != null)
		{
			// 선행을 열면 저절로 같이 열리는 노드(해제 등)는 버튼이 없다
			unlockButton.gameObject.SetActive(!(node.AutoUnlock && node.Cost.Count == 0));
			unlockButton.interactable = available;
			TMP_Text label = unlockButton.GetComponentInChildren<TMP_Text>();
			if (label != null) label.text = isUnlocked ? "해금됨" : available ? "해금" : "선행 노드를 먼저 해금";
		}
	}

	// 버튼 ---------------------------------------------------------------------

	void Select(string id)
	{
		selectedId = id;
		Refresh();
	}

	// 재료 한 줄. 내 캐릭터가 있으면 「버그 조각 3/5」처럼 가진 개수를 같이 (모자라면 빨강)
	string CostText(MaterialCost cost)
	{
		string name = MaterialName(cost.MaterialId);
		if (progress == null) return $"{name} {cost.Count}";
		int have = progress.GetMaterialCount(cost.MaterialId);
		return $"<color={(have >= cost.Count ? Good : Bad)}>{name} {have}/{cost.Count}</color>";
	}

	void RequestUnlock()
	{
		if (selectedId == null || !SkillTree.CanUnlock(selectedId, Unlocked)) return;
		Debug.Log($"[SkillTreeView] 해금 요청: {selectedId}");
		failedId = null;
		UnlockRequested?.Invoke(selectedId);
	}

	static void SetTabColor(Button tabButton, bool selected)
	{
		if (tabButton == null) return;
		Image image = tabButton.GetComponent<Image>();
		if (image != null) image.color = selected ? TabSelectedColor : TabColor;
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
