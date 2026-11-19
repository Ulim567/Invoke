using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [표시] 획득 표시 (시안 8-5). 내 캐릭터가 받은 경험치·재료·블록을 HUD 왼쪽 위, 장소 이름 아래에 한 줄씩 쌓는다. 값은 바꾸지 않고 보여 주기만 한다.
/// <para>
/// - 받는 곳: MonsterReward.LocalRewardReceived(김도훈). 받은 플레이어의 화면에서만 불린다.<br/>
/// - 한 줄: 왼쪽 아래 알림과 같은 반투명 상자 안에 아이콘 8×8 + 글. 경험치는 아이콘 없이 「EXP +50」, 재료는 「버그 조각 x3」, 블록·함수는 「If 블록 x1」, 「검 찌르기 함수 x1」.<br/>
///   아이콘은 드랍될 때 날아오는 그림과 같다 (Resources/MaterialDrops/{재료 ID} 프리팹의 SpriteRenderer, 블록·함수는 item_block).<br/>
/// - 쌓기: 왼쪽 아래 알림 목록(HudView)과 방향만 반대(위 → 아래)다. 새 줄은 장소 이름 바로 아래(영역 맨 위)에 나타나 아래 줄들을 부드럽게 밀어 내리고,
///   영역 아래쪽에 닿은 줄은 마스크가 흐리게 잘라 낸다. 줄마다 Row Time이 지나면 제자리에서 흐려지고 빠진다.<br/>
/// - 차례로 띄우기: 한 마리를 잡아 여러 가지를 받으면 한꺼번에 띄우지 않고 Row Interval(0.15초) 간격으로 한 줄씩 띄워, 하나씩 차례로 밀려 내려가게 한다.<br/>
/// - 바로 앞 줄과 같은 것(같은 재료, 같은 블록, 경험치)이면 새 줄을 만들지 않고 개수를 더하고 시간을 다시 센다.<br/>
/// - 모아 두기: 레이드 던전 안, 로딩 화면이 덮은 동안, HUD가 숨은 동안 받은 것은 띄우지 않고 종류별로 합쳐 두었다가,
///   다시 보일 수 있게 되면(레이드를 마치고 로비에 도착하면) 차례로 띄운다.<br/>
/// - 내 캐릭터가 바뀌거나 없어지면(캐릭터 화면으로 돌아가기, 다시 접속) 떠 있던 줄과 모아 둔 것을 지운다.<br/>
/// - 피할 칸(Avoid, 튜토리얼 대사 창)이 켜져 있는 동안은 영역을 그 칸 바로 아래로 내리고, 꺼지면 원래 자리로 돌아온다 (#589, 이우림 결정 11/19 (가)).
/// </para>
/// <para>
/// 사용법: HudCanvas 프리팹 최상위(HudView와 같은 오브젝트)에 붙인다. 칸은 HudUIBuilder(메뉴 Invoke → UI → 게임 HUD 만들기)가 연결한다.
/// </para>
/// </summary>
public class AcquireFeedView : MonoBehaviour
{
	// 한 줄이 무엇을 받은 것인지
	enum Kind { Exp, Material, Item }

	[Header("칸")]
	[SerializeField, Tooltip("장소 이름 아래의 정해진 크기 영역 (RectMask2D). 줄이 없으면 숨긴다")] GameObject panel;
	[SerializeField, Tooltip("줄들을 세로로 쌓는 목록. 위 기준이며, 이 화면이 세로 위치를 움직여 새 줄이 아래 줄들을 부드럽게 밀어 내리게 한다")] RectTransform content;
	[SerializeField, Tooltip("줄 견본. 줄 하나에 하나씩 복사해 쓴다. 바탕 Image, CanvasGroup, 자식 Icon(Image)과 Text(TMP)를 가진다")] GameObject rowTemplate;
	[SerializeField, Tooltip("이 칸이 켜져 있는 동안 영역을 그 아래로 내린다 (튜토리얼 대사 창). 비워 두면 늘 제자리")] RectTransform avoid;
	[SerializeField, Min(0f), Tooltip("피할 칸 아래로 내릴 때 그 칸과 띄우는 간격")] float avoidGap = 2f;

	[Header("값")]
	[SerializeField, Min(0.1f), Tooltip("한 줄을 보여 주는 시간(초)")] float rowTime = 3f;
	[SerializeField, Min(0.01f), Tooltip("시간이 다 된 줄이 제자리에서 흐려지는 시간(초). 보여 주는 시간의 끝부분이다")] float rowFadeTime = 0.4f;
	[SerializeField, Min(0.1f), Tooltip("새 줄이 아래 줄들을 밀어 내리는 빠르기. 클수록 빨리 제자리에 온다")] float riseSharpness = 12f;
	[SerializeField, Min(0f), Tooltip("모아 둔 것을 한 줄씩 띄우는 간격(초). 한 마리를 잡아 여러 가지를 받아도 한꺼번에 뜨지 않고 차례로 밀려 내려간다")] float rowInterval = 0.15f;
	[SerializeField, Min(1), Tooltip("한 번에 두는 줄 수. 넘치면 가장 오래된 줄부터 뺀다")] int maxRows = 6;
	[SerializeField, Min(0), Tooltip("영역 아래쪽에서 줄이 흐려지는 높이. 시작할 때 영역의 마스크(RectMask2D)에 넣는다")] int fadeHeight = 20;
	[SerializeField, Tooltip("경험치 줄의 글자 (아이콘 없음)")] string expLabel = "EXP";

	// 받은 것 한 가지 (종류, 재료·항목 ID, 개수). 줄 하나, 또는 모아 둔 것 하나
	struct Gain
	{
		public Kind Kind;
		public string Id;
		public int Count;

		public bool SameAs(Gain other) => Kind == other.Kind && Id == other.Id;
	}

	// 떠 있는 줄 하나와 그 줄을 보여 주는 상자
	sealed class Row
	{
		public Gain Gain;
		public float Until;
		public GameObject Box;
		public Image Icon;
		public TMP_Text Label;
		public CanvasGroup Group;
	}

	// 떠 있는 줄. 맨 앞이 가장 오래된 것(맨 아래), 맨 뒤가 가장 새 것(맨 위)
	readonly List<Row> rows = new List<Row>();
	// 다 쓴 상자. 줄이 생길 때마다 새로 만들지 않고 다시 쓴다
	readonly List<GameObject> freeBoxes = new List<GameObject>();
	// 아직 띄우지 못하고 모아 둔 것. 받은 순서대로, 같은 것은 합쳐 둔다
	readonly List<Gain> pending = new List<Gain>();
	// 재료 ID → 아이콘 그림. 없는 그림도 null로 기억해 매번 찾지 않는다
	readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

	// 목록의 세로 위치. 새 줄이 오면 그 높이만큼 위로 올렸다가 0으로 부드럽게 돌아가, 아래 줄들이 툭 튀지 않고 밀려 내려가게 한다
	float contentOffset;
	// 마지막에 붙인 줄 때문에 목록을 올린 높이. 새 줄이 내려오는 동안 그 줄을 서서히 나타나게 할 때 기준으로 쓴다
	float lastRise;
	// 다음 줄을 띄울 수 있는 시각 (Time.time). 모아 둔 것을 한 줄씩 차례로 띄운다
	float nextRowTime;
	// 지금 보고 있는 내 캐릭터. 바뀌면 줄과 모아 둔 것을 지운다
	GameObject boundPlayer;
	// 영역의 원래 세로 위치 (장소 이름 바로 아래). 피할 칸이 꺼지면 여기로 돌아온다
	float homeY;
	// 지금 피할 칸 아래로 내려가 있으면 true
	bool avoiding;
	// 칸의 네 모서리를 잴 때 쓰는 자리. 매 프레임 새로 만들지 않는다
	readonly Vector3[] corners = new Vector3[4];

	void Awake()
	{
		if (rowTemplate != null) rowTemplate.SetActive(false);
		if (panel == null) return;
		homeY = ((RectTransform)panel.transform).anchoredPosition.y;
		// 흐려지는 높이를 프리팹이 아니라 여기서 정한다. 값을 바꿔 볼 때 HUD 프리팹을 다시 만들지 않아도 되게 하기 위함 (HudView 알림 목록과 같은 방식)
		// softness는 위아래에 함께 걸리므로, 위 경계를 같은 만큼 영역 밖으로 올려(padding 음수, 순서는 왼쪽·아래·오른쪽·위) 위쪽은 흐려지지 않게 한다
		if (panel.TryGetComponent(out RectMask2D mask))
		{
			mask.softness = new Vector2Int(0, fadeHeight);
			mask.padding = new Vector4(0, 0, 0, -fadeHeight);
		}
		panel.SetActive(false);
	}

	void OnEnable() => MonsterReward.LocalRewardReceived += OnReward;

	void OnDisable() => MonsterReward.LocalRewardReceived -= OnReward;

	// 받은 것은 바로 띄우지 않고 일단 모아 둔다. 띄울 수 있으면 Update가 한 줄씩 띄운다
	// 바로 띄우지 않는 이유: 레이드 던전·로딩 중처럼 지금 보이면 안 되는 때를 한 곳(CanShow)에서만 판단하고, 여러 줄을 차례로 띄우기 위함
	void OnReward(int exp, IReadOnlyList<MonsterReward.MaterialDrop> materials, IReadOnlyList<string> itemIds)
	{
		if (exp > 0) Queue(new Gain { Kind = Kind.Exp, Id = "", Count = exp });
		if (materials != null)
			for (int i = 0; i < materials.Count; i++)
				if (!string.IsNullOrEmpty(materials[i].materialId) && materials[i].count > 0)
					Queue(new Gain { Kind = Kind.Material, Id = materials[i].materialId, Count = materials[i].count });
		if (itemIds != null)
			for (int i = 0; i < itemIds.Count; i++)
				if (!string.IsNullOrEmpty(itemIds[i]))
					Queue(new Gain { Kind = Kind.Item, Id = itemIds[i], Count = 1 });
	}

	// 모아 둔 것에 더한다. 같은 것이 이미 있으면 개수만 더한다 (레이드 보상을 로비에서 종류별로 합쳐 보이기 위함)
	void Queue(Gain gain)
	{
		for (int i = 0; i < pending.Count; i++)
		{
			if (!pending[i].SameAs(gain)) continue;
			Gain merged = pending[i];
			merged.Count += gain.Count;
			pending[i] = merged;
			return;
		}
		pending.Add(gain);
	}

	void Update()
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (me != boundPlayer)
		{
			// 다른 캐릭터로 바뀌면 이전 캐릭터가 받은 것을 남기지 않는다
			Clear();
			boundPlayer = me;
		}

		// 모아 둔 것은 한 줄씩 Row Interval 간격으로 띄운다. 한꺼번에 붙이면 여러 줄이 한 번에 나타나, 하나씩 차례로 밀려 내려가는 느낌이 나지 않기 때문
		if (pending.Count > 0 && Time.time >= nextRowTime && CanShow())
		{
			AddRow(pending[0]);
			pending.RemoveAt(0);
			nextRowTime = Time.time + rowInterval;
		}
		RefreshRows();
		RefreshPlace();
	}

	// 피할 칸(튜토리얼 대사 창)이 떠 있는 동안 영역을 그 칸 바로 아래에 둔다. 꺼지면 원래 자리로 돌아온다
	// 내리는 이유: 튜토리얼 대사 창도 장소 이름 바로 아래에 떠서, 튜토리얼 중(사냥터에서 버그 조각 모으기)에 받은 것이 대사 창에 가려지기 때문
	// 떠 있는 동안 매 프레임 다시 재는 이유: TutorialView가 쪽마다 대사·해 볼 일 줄 수에 맞춰 창 높이를 바꾸기 때문
	void RefreshPlace()
	{
		if (panel == null) return;
		bool shouldAvoid = avoid != null && avoid.gameObject.activeInHierarchy;
		if (!shouldAvoid && !avoiding) return;
		avoiding = shouldAvoid;

		RectTransform area = (RectTransform)panel.transform;
		float targetY = homeY;
		if (avoiding)
		{
			// 영역 위 끝을 피할 칸 아래 끝보다 Avoid Gap만큼 아래로. 두 칸의 앵커가 달라도 맞도록 같은 부모 좌표로 바꿔 잰다
			Transform parent = area.parent;
			avoid.GetWorldCorners(corners);
			float avoidBottom = parent.InverseTransformPoint(corners[0]).y;
			area.GetWorldCorners(corners);
			// 원래 자리에 있을 때의 위 끝 = 지금 위 끝 + (원래 위치 − 지금 위치)
			float homeTop = parent.InverseTransformPoint(corners[1]).y + (homeY - area.anchoredPosition.y);
			targetY = homeY + Mathf.Min(0f, avoidBottom - avoidGap - homeTop);
		}
		if (!Mathf.Approximately(area.anchoredPosition.y, targetY)) area.anchoredPosition = new Vector2(area.anchoredPosition.x, targetY);
	}

	// 지금 줄을 띄워도 되면 true.
	// - 레이드 던전: 결과 화면(RaidResultView)이 보상 줄을 보여 주므로, 받은 것은 로비에 돌아가서 보인다 (이우림 결정 11/19)
	// - 로딩 화면이 덮은 동안: 띄워도 보이지 않은 채 시간이 지나 사라지므로 다 걷힌 뒤에 띄운다
	// - HUD가 숨은 동안(내 캐릭터 없음, 창이 열림): 같은 이유
	bool CanShow()
	{
		if (boundPlayer == null || panel == null) return false;
		Transform area = panel.transform.parent;
		if (area != null && !area.gameObject.activeInHierarchy) return false;
		if (LoadingScreen.Showing) return false;
		string place = PlayerPlace.Mine;
		// 서버가 아직 장소를 정하지 않았으면(접속 직후) 기다린다
		return !string.IsNullOrEmpty(place) && place != PlaceScenes.RaidDungeon;
	}

	// 줄을 맨 위에 붙인다. 바로 앞에 붙인 줄과 같은 것이면 그 줄의 개수를 더하고 시간을 다시 센다
	// 합치는 이유: 같은 몬스터를 연달아 잡아 같은 것이 이어지면, 같은 줄이 목록을 채워 다른 것이 밀려나기 때문
	void AddRow(Gain gain)
	{
		if (rowTemplate == null || content == null) return;
		if (!panel.activeSelf) panel.SetActive(true);

		Row last = rows.Count > 0 ? rows[rows.Count - 1] : null;
		if (last != null && last.Gain.SameAs(gain))
		{
			last.Gain.Count += gain.Count;
			last.Until = Time.time + rowTime;
			last.Group.alpha = 1f;
			SetLabel(last);
			return;
		}

		// 붙이기 전의 목록 높이. 붙인 뒤의 높이와의 차이만큼만 목록을 내린다
		LayoutRebuilder.ForceRebuildLayoutImmediate(content);
		float heightBefore = content.rect.height;

		var row = new Row { Gain = gain, Until = Time.time + rowTime, Box = TakeBox() };
		row.Group = row.Box.GetComponent<CanvasGroup>();
		if (row.Group == null) row.Group = row.Box.AddComponent<CanvasGroup>();
		row.Group.alpha = 1f;
		Transform icon = row.Box.transform.Find("Icon");
		row.Icon = icon != null ? icon.GetComponent<Image>() : null;
		row.Label = row.Box.GetComponentInChildren<TMP_Text>(true);
		SetIcon(row);
		SetLabel(row);
		// 목록 맨 위(첫 자식)에 둔다. 세로로 쌓는 목록은 자식 순서대로 위에서 아래로 놓기 때문
		row.Box.transform.SetAsFirstSibling();
		rows.Add(row);

		// 늘어난 높이만큼 목록을 올렸다가 RefreshRows가 0으로 되돌린다. 그래서 아래 줄들은 처음 자리에서 그대로 밀려 내려가고 새 줄은 위에서 내려온다 (HudView 알림 목록을 거꾸로 한 방식)
		// 넘친 줄을 빼기 전에 잰다. 맨 아래 줄을 빼도 위 기준인 목록에서 다른 줄은 움직이지 않는다
		LayoutRebuilder.ForceRebuildLayoutImmediate(content);
		lastRise = Mathf.Max(0f, content.rect.height - heightBefore);
		contentOffset += lastRise;
		// 올린 위치를 바로 적용한다. 다음 프레임까지 미루면 그 사이 한 번 그려질 때 아래 줄들이 먼저 튀어 내려갔다가 올라와 보인다
		content.anchoredPosition = new Vector2(0f, contentOffset);
		while (rows.Count > maxRows) RemoveRow(0);
	}

	// 줄 글자. 「×」 대신 「x」: HUD 글꼴(Galmuri)에 없는 글자라 빈칸으로 보이기 때문 (HudView 알림의 (x4)와 같은 이유)
	void SetLabel(Row row)
	{
		if (row.Label == null) return;
		row.Label.text = LabelOf(row.Gain);
		// 다시 쓰는 상자는 이전 글의 크기를 기억하고 있다. 바로 다시 계산해야 목록이 새 글의 너비로 상자를 놓는다
		row.Label.ForceMeshUpdate();
	}

	string LabelOf(Gain gain)
	{
		switch (gain.Kind)
		{
			case Kind.Exp:
				return $"{expLabel} +{gain.Count}";
			case Kind.Material:
				return $"{ShopItemInfo.MaterialNameOf(gain.Id)} x{gain.Count}";
			default:
				string word = ShopItemInfo.IsSyntaxItem(gain.Id) ? "블록" : "함수";
				return $"{ShopItemInfo.DisplayNameOf(gain.Id)} {word} x{gain.Count}";
		}
	}

	// 경험치는 아이콘 없이 글자만. 재료·블록은 드랍될 때 날아오는 그림. 그림이 없으면 아이콘 칸을 숨긴다
	void SetIcon(Row row)
	{
		if (row.Icon == null) return;
		Sprite sprite = null;
		if (row.Gain.Kind == Kind.Material) sprite = IconOf(row.Gain.Id);
		else if (row.Gain.Kind == Kind.Item) sprite = IconOf(MonsterReward.ItemDropIconId);
		row.Icon.sprite = sprite;
		row.Icon.gameObject.SetActive(sprite != null);
	}

	// 드랍 아이콘 프리팹(Resources/MaterialDrops/{ID}, MaterialDropView와 같은 곳)의 그림. 프리팹의 반짝임(SpriteFrameLoop)이 시작하는 첫 그림이다
	Sprite IconOf(string id)
	{
		if (icons.TryGetValue(id, out Sprite sprite)) return sprite;
		GameObject prefab = Resources.Load<GameObject>("MaterialDrops/" + id);
		SpriteRenderer renderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
		sprite = renderer != null ? renderer.sprite : null;
		icons[id] = sprite;
		return sprite;
	}

	// 다 쓴 상자가 있으면 다시 쓰고, 없으면 견본을 복사한다 (상자는 UI 안에서만 쓰는 표시용이라 네트워크와 상관없다)
	GameObject TakeBox()
	{
		GameObject box;
		if (freeBoxes.Count > 0)
		{
			box = freeBoxes[freeBoxes.Count - 1];
			freeBoxes.RemoveAt(freeBoxes.Count - 1);
		}
		else
		{
			box = Instantiate(rowTemplate, content);
			box.name = "Row";
		}
		box.SetActive(true);
		return box;
	}

	// index번 줄을 빼고 상자를 돌려 놓는다. 목록은 위 기준이라 아래 줄을 빼도 위 줄들은 움직이지 않는다
	void RemoveRow(int index)
	{
		GameObject box = rows[index].Box;
		box.SetActive(false);
		freeBoxes.Add(box);
		rows.RemoveAt(index);
	}

	// 줄과 모아 둔 것을 모두 지운다
	void Clear()
	{
		while (rows.Count > 0) RemoveRow(rows.Count - 1);
		pending.Clear();
		contentOffset = 0f;
		lastRise = 0f;
		if (content != null) content.anchoredPosition = Vector2.zero;
		if (panel != null) panel.SetActive(false);
	}

	// 줄마다: 보여 주는 시간의 끝부분에 제자리에서 흐려지고, 시간이 다 되면 뺀다
	// 목록: 새 줄 때문에 올려 둔 위치를 0으로 부드럽게 되돌린다. 영역 아래쪽을 지나는 줄은 마스크가 흐리게 잘라 낸다
	void RefreshRows()
	{
		if (panel == null || content == null) return;

		for (int i = rows.Count - 1; i >= 0; i--)
		{
			float remaining = rows[i].Until - Time.time;
			if (remaining <= 0f) RemoveRow(i);
			else rows[i].Group.alpha = Mathf.Clamp01(remaining / rowFadeTime);
		}
		// 새 줄은 위에서 내려오는 동안 서서히 나타난다. 내려오기 전에는 장소 이름과 겹치는 자리(영역 위)에 있어, 그대로 보이면 이름을 가리기 때문
		if (rows.Count > 0 && lastRise > 0f)
		{
			Row newest = rows[rows.Count - 1];
			newest.Group.alpha = Mathf.Min(newest.Group.alpha, Mathf.Clamp01(1f - contentOffset / lastRise));
		}

		if (contentOffset != 0f)
		{
			// 남은 거리의 일정 비율씩 다가가 처음엔 빠르고 끝에선 천천히 멈춘다. 프레임 빠르기와 상관없이 같은 모양이 되게 시간으로 비율을 정한다
			contentOffset = Mathf.Lerp(contentOffset, 0f, 1f - Mathf.Exp(-riseSharpness * Time.deltaTime));
			if (Mathf.Abs(contentOffset) < 0.5f) contentOffset = 0f;
			content.anchoredPosition = new Vector2(0f, contentOffset);
		}

		bool show = rows.Count > 0;
		if (panel.activeSelf != show) panel.SetActive(show);
	}
}
