using UnityEngine;

/// <summary>
/// 내 캐릭터가 가까이 와서 ↑(상호작용)를 누르면 반응하는 오브젝트의 공통 부분. 로비의 입구·패널 장소와 각 장소의 출구(PlaceExit)가 물려받는다.
/// <para>
/// - 이 오브젝트에 Is Trigger를 켠 Collider 2D를 붙인다. 그 범위 안에 내 캐릭터가 들어오면 ↑ 안내(prompt)를 보여 준다.<br/>
/// - 내 캐릭터(isLocalPlayer)만 반응한다. 다른 플레이어가 들어와도 내 화면의 안내는 켜지지 않는다.<br/>
/// - 키는 PlayerInputReader.InteractPressed로만 읽는다 (CLAUDE.md 코드 작성 규칙 5).<br/>
/// - 내 캐릭터가 다른 장소로 가면 놓아준다. 호스트에서는 씬이 서버에 남아 있어도 OnTriggerExit2D가 불리지 않기 때문이다.<br/>
/// - 검을 타고 나는 중(PlayerMovement.IsRiding)이거나 죽었거나 경직 중(PlayerHealth.CanAct가 false)이면 ↑를 무시한다.<br/>
/// - 표시 (#434): ↑ 안내 자리에 작은 민트 마름모가 늘 천천히 위아래로 떠 있다. 가까이 가면 밝아지고 위에 ↑ 키 그림이 뜬다. 이때 예전 ↑ 안내(노란 세모)는 켜지 않는다.
///   그림은 코드로 만들고(마름모 7×7, 키 9×9픽셀, 16 PPU) 안내와 같은 Sorting Layer에 그린다. 씬·프리팹은 고치지 않는다. Show Idle Marker를 끄면 예전 안내만.
/// </para>
/// </summary>
public abstract class PlaceInteractable : MonoBehaviour
{
	[SerializeField, Tooltip("내 캐릭터가 범위 안에 있을 때 켜지는 ↑ 안내. 비워 두면 안내 없음")]
	GameObject prompt;

	[SerializeField, Tooltip("상호작용할 수 있는 물체 표시 (#434). 켜면 위의 ↑ 안내 대신, 평소에는 민트 마름모, 가까이 가면 밝은 마름모 + ↑ 키를 보여 준다")]
	bool showIdleMarker = true;

	PlayerInputReader localPlayer;

	// 표시 (#434)
	const float PixelsPerUnit = 16f;
	static readonly Color MarkerColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);
	static Sprite markerSprite, keySprite;
	SpriteRenderer marker, key;
	Vector3 markerBase;
	float markerPhase;

	bool UsesMarker => marker != null;

	void Awake()
	{
		if (prompt != null) prompt.SetActive(false);
		if (showIdleMarker) CreateMarker();
	}

	// ↑ 안내: 마름모를 쓰면 예전 안내(노란 세모)는 켜지 않는다
	void SetPrompt(bool near)
	{
		if (prompt != null) prompt.SetActive(near && !UsesMarker);
	}

	// 서버(화면 없음)에서는 만들지 않는다
	void CreateMarker()
	{
		if (Application.isBatchMode) return;
		var go = new GameObject("InteractMarker");
		go.transform.SetParent(transform, false);
		marker = go.AddComponent<SpriteRenderer>();
		marker.sprite = MarkerSprite();

		// 자리: ↑ 안내가 있으면 그 자리, 없으면 범위(콜라이더) 위 끝에서 반 칸 위
		if (prompt != null && prompt.transform.parent == transform) markerBase = prompt.transform.localPosition;
		else if (prompt != null) markerBase = transform.InverseTransformPoint(prompt.transform.position);
		else if (TryGetComponent(out Collider2D area)) markerBase = transform.InverseTransformPoint(new Vector3(area.bounds.center.x, area.bounds.max.y + 0.5f, transform.position.z));
		else markerBase = new Vector3(0f, 1.5f, 0f);
		// 부모 크기가 바뀌어 있어도 늘 그림 픽셀 그대로 보이게 한다
		Vector3 lossy = transform.lossyScale;
		go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)), 1f);

		// 가까이 갔을 때 마름모 위에 뜨는 ↑ 키 (9×9)
		var keyObject = new GameObject("Key");
		keyObject.transform.SetParent(go.transform, false);
		keyObject.transform.localPosition = new Vector3(0f, 9f / PixelsPerUnit, 0f);
		key = keyObject.AddComponent<SpriteRenderer>();
		key.sprite = KeySprite();
		keyObject.SetActive(false);

		if (prompt != null && prompt.TryGetComponent(out SpriteRenderer promptRenderer))
		{
			marker.sortingLayerID = key.sortingLayerID = promptRenderer.sortingLayerID;
			marker.sortingOrder = key.sortingOrder = promptRenderer.sortingOrder;
		}
		markerPhase = Mathf.Abs(transform.position.x) * 0.7f; // 여러 개가 같이 움직이지 않게
		UpdateMarker();
	}

	// 7×7 마름모 (흰색, 색은 SpriteRenderer.color로): 테두리는 진하게, 안은 흐리게
	static Sprite MarkerSprite()
	{
		if (markerSprite != null) return markerSprite;
		markerSprite = MakeSprite(7, (x, y) =>
		{
			int d = Mathf.Abs(x - 3) + Mathf.Abs(y - 3);
			return d == 3 ? new Color32(255, 255, 255, 255) : d < 3 ? new Color32(255, 255, 255, 90) : new Color32(0, 0, 0, 0);
		}, "InteractMarker");
		return markerSprite;
	}

	// 9×9 키 모양: 어두운 바탕, 밝은 테두리, 가운데 흰 ↑ (HUD의 키 표시와 같은 색)
	static Sprite KeySprite()
	{
		if (keySprite != null) return keySprite;
		var border = new Color32(0xB8, 0xBE, 0xD6, 0xFF);
		var back = new Color32(0x1A, 0x1E, 0x3A, 0xE6);
		var arrow = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
		keySprite = MakeSprite(9, (x, y) =>
		{
			if (x == 0 || y == 0 || x == 8 || y == 8) return (x == 0 || x == 8) && (y == 0 || y == 8) ? new Color32(0, 0, 0, 0) : border;
			bool shaft = x == 4 && y >= 2 && y <= 6;
			bool head = (y == 5 && (x == 3 || x == 5)) || (y == 4 && (x == 2 || x == 6));
			return shaft || head ? arrow : back;
		}, "InteractKey");
		return keySprite;
	}

	static Sprite MakeSprite(int size, System.Func<int, int, Color32> pixel, string name)
	{
		var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
		var pixels = new Color32[size * size];
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				pixels[y * size + x] = pixel(x, y);
		texture.SetPixels32(pixels);
		texture.Apply();
		Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
		sprite.name = name;
		return sprite;
	}

	// 평소: 1픽셀씩 천천히 위아래로 뜨며 살짝 깜빡인다. 가까이: 밝게, 조금 빨리 뜨고 위에 ↑ 키
	// Renderer.enabled는 건드리지 않는다 (호스트에서 HostPlaceVisibility가 다른 장소의 그림을 끄는 데 씀). 키는 오브젝트를 켜고 끈다
	void UpdateMarker()
	{
		if (marker == null) return;
		bool near = localPlayer != null;
		if (key.gameObject.activeSelf != near) key.gameObject.SetActive(near);
		float t = Time.time * (near ? 3.5f : 2f) + markerPhase;
		float bob = Mathf.Round(Mathf.Sin(t) * 1.5f) / PixelsPerUnit;
		Vector3 lossy = transform.lossyScale;
		marker.transform.localPosition = markerBase + new Vector3(0f, bob / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)), 0f);
		Color color = near ? Color.Lerp(MarkerColor, Color.white, 0.35f) : MarkerColor;
		color.a = near ? 1f : 0.6f + 0.25f * (0.5f + 0.5f * Mathf.Sin(t * 0.8f));
		marker.color = color;
	}

	void OnTriggerEnter2D(Collider2D other)
	{
		PlayerInputReader reader = other.GetComponentInParent<PlayerInputReader>();
		if (reader == null || !reader.isLocalPlayer) return;
		localPlayer = reader;
		SetPrompt(true);
	}

	void OnTriggerExit2D(Collider2D other)
	{
		PlayerInputReader reader = other.GetComponentInParent<PlayerInputReader>();
		if (reader == null || reader != localPlayer) return;
		localPlayer = null;
		SetPrompt(false);
	}

	void Update()
	{
		UpdateMarker();
		if (localPlayer == null) return;

		// 호스트에서는 내 캐릭터가 다른 장소로 가도 로비 씬이 서버에 남아 있고, 다른 물리 세계로 옮겨진 캐릭터에는 OnTriggerExit2D가 불리지 않는다
		// 놓아주지 않으면 다른 장소에서 ↑를 누를 때 로비 창이 열리므로, 내 캐릭터가 이 물리 세계를 떠났으면 여기서 놓아준다
		// 씬이 아니라 물리 세계로 비교하는 이유: 클라이언트에서는 플레이어가 기준 씬(Base)에 있어서 씬이 늘 다르지만, 로비와 Base는 같은 물리 세계라 트리거는 정상으로 동작한다
		if (localPlayer.gameObject.scene.GetPhysicsScene2D() != gameObject.scene.GetPhysicsScene2D())
		{
			localPlayer = null;
			if (prompt != null) prompt.SetActive(false);
			return;
		}

		// 창이 열려 있는 동안은 Player 액션 맵이 꺼져 있어서 InteractPressed가 false이므로, 같은 창이 또 열리지 않는다
		if (!localPlayer.InteractPressed) return;

		// 검을 타고 위로 날 때 누르는 ↑가 상호작용으로도 읽히므로, 검 타기 중에는 무시한다 (#285)
		if (localPlayer.TryGetComponent(out PlayerMovement movement) && movement.IsRiding) return;
		// 죽었거나 경직 중이면 창을 열거나 장소를 옮기지 않는다 (#310)
		if (localPlayer.TryGetComponent(out PlayerHealth health) && !health.CanAct) return;

		Interact(localPlayer);
	}

	/// <summary>
	/// 범위 안에서 ↑를 눌렀을 때 불린다. 물려받는 쪽이 창을 열거나 장소 이동을 요청한다.
	/// </summary>
	protected abstract void Interact(PlayerInputReader player);
}
