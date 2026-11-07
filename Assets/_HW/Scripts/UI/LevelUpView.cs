using System.Globalization;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 창 (우림이 시안 10, 10-1). 내 캐릭터의 레벨·경험치·능력치를 읽어서 보여 주기만 한다.
/// <para>
/// - 시안 10 (상태): HUD의 「레벨업」 버튼(HudView.LevelUpRequested)으로 연다. 지금 레벨, 경험치, 능력치 6종. X·Esc로 닫는다.<br/>
/// - 시안 10-1 (결과): 레벨이 오른 뒤 로비에 오면 저절로 뜬다. 이전 → 지금 레벨, 능력치의 이전·지금·늘어난 값. 「확인」·Enter·Esc로 닫는다.<br/>
///   사냥터에서 여러 번 올라도 로비에 왔을 때 한 번만, 처음 레벨부터 모아서 보여 준다. 다른 창이 열려 있으면 닫힐 때까지 기다린다.<br/>
/// - 레벨은 PlayerProgress(이우림), 다음 레벨 경험치는 LevelRules(김도훈), 능력치는 PlayerStats(김도훈)에서 읽는다.<br/>
/// - 「이전」 능력치: 레벨이 바뀌기 바로 전 프레임에 읽어 둔 값. 접속해서 처음 불러온 레벨은 레벨업으로 치지 않는다.<br/>
/// - LevelUpCanvas 프리팹(Base 씬)에 붙인다. 프리팹은 메뉴 Invoke → UI → 레벨업 화면 만들기.
/// </para>
/// </summary>
public class LevelUpView : InputBlockingPanel
{
	const int StatCount = 6;
	static readonly string[] StatNames = { "최대 체력", "공격력", "공격 속도", "메모리 크기", "쿨타임 감소", "이동 속도" };

	[Header("제목")]
	[SerializeField] TMP_Text titleText;
	[SerializeField, Tooltip("상태 창(시안 10)의 X 버튼")] Button closeButton;
	[SerializeField, Tooltip("결과 창(시안 10-1)의 확인 버튼")] Button confirmButton;

	[Header("왼쪽: 레벨, 경험치")]
	[SerializeField, Tooltip("결과 창에서만 보이는 이전 레벨 숫자")] TMP_Text fromLevelText;
	[SerializeField, Tooltip("결과 창에서만 보이는 화살표")] GameObject arrow;
	[SerializeField] TMP_Text levelText;
	[SerializeField] TMP_Text maxLevelText;
	[SerializeField] TMP_Text expText;
	[SerializeField, Tooltip("Filled, Horizontal, Left")] Image expFill;
	[SerializeField] TMP_Text expLeftText;

	[Header("오른쪽: 능력치 표 (최대 체력, 공격력, 공격 속도, 메모리 크기, 쿨타임 감소, 이동 속도 순서)")]
	[SerializeField, Tooltip("머리줄의 세 칸: 이전 / 지금 / 다음 레벨(늘어난 값)")] TMP_Text[] headerTexts;
	[SerializeField] TMP_Text[] beforeTexts;
	[SerializeField] TMP_Text[] nowTexts;
	[SerializeField] TMP_Text[] diffTexts;

	[Header("색")]
	[SerializeField] Color statusTitleColor = Color.white;
	[SerializeField] Color resultTitleColor = new Color32(0xE8, 0xA8, 0x67, 0xFF);
	[SerializeField] Color upColor = new Color32(0x7C, 0xF0, 0xA0, 0xFF);
	[SerializeField] Color sameColor = new Color32(0x7C, 0x84, 0xA8, 0xFF);

	// 내 캐릭터
	GameObject player;
	PlayerProgress progress;
	PlayerStats stats;
	PlayerLevelStats levelStats;

	// 레벨업 감지
	int lastLevel;
	readonly float[] lastStats = new float[StatCount];
	bool pending;
	int pendingFromLevel;
	readonly float[] pendingFromStats = new float[StatCount];

	bool showingResult;

	void Start()
	{
		if (closeButton != null) closeButton.onClick.AddListener(Close);
		if (confirmButton != null) confirmButton.onClick.AddListener(Close);
		HudView.LevelUpRequested += OpenStatus;
	}

	void OnDestroy() => HudView.LevelUpRequested -= OpenStatus;

	/// <summary>상태 창(시안 10)을 연다. HUD의 레벨업 버튼이 부른다</summary>
	public void OpenStatus()
	{
		if (IsOpen || progress == null) return;
		showingResult = false;
		Open();
	}

	// 결과 창: Enter도 확인
	protected override void OnSubmitKey()
	{
		if (showingResult) Close();
	}

	void Open()
	{
		Show(player != null ? player.GetComponentInChildren<PlayerInputReader>() : null);
		Refresh();
	}

	void Update()
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (me != player) Bind(me);
		if (progress == null || stats == null)
		{
			if (IsOpen) Close();
			return;
		}

		WatchLevel();

		if (pending && !IsOpen && !AnyOpen && PlayerPlace.Of(player) == PlaceScenes.Lobby)
		{
			showingResult = true;
			Open();
			pending = false;
		}
		else if (IsOpen) Refresh();
	}

	void Bind(GameObject me)
	{
		if (IsOpen) Close();
		player = me;
		progress = me != null ? me.GetComponent<PlayerProgress>() : null;
		stats = me != null ? me.GetComponent<PlayerStats>() : null;
		levelStats = me != null ? me.GetComponent<PlayerLevelStats>() : null;
		pending = false;
		// 접속해서 불러온 레벨은 레벨업이 아니다. 서버가 플레이어를 만든 직후 불러온 값을 넣으므로 첫 동기화에 이미 들어 있다 (progress-store.md)
		if (progress != null) lastLevel = progress.Level;
		if (stats != null) ReadStats(lastStats);
	}

	// 레벨이 오르면 바로 전 프레임의 능력치를 「이전」으로 기억한다
	void WatchLevel()
	{
		int level = progress.Level;
		if (level > lastLevel)
		{
			if (!pending)
			{
				pending = true;
				pendingFromLevel = lastLevel;
				System.Array.Copy(lastStats, pendingFromStats, StatCount);
			}
			lastLevel = level;
		}
		else if (level < lastLevel)
		{
			// 다른 캐릭터 데이터를 불러온 경우 등. 레벨업이 아니다
			lastLevel = level;
			pending = false;
		}
		ReadStats(lastStats);
	}

	// 그리기 -------------------------------------------------------------------

	void Refresh()
	{
		int level = progress.Level;
		bool max = level >= LevelRules.MaxLevel;

		SetText(titleText, showingResult ? "> 레벨 업!" : "> 레벨업");
		if (titleText != null) titleText.color = showingResult ? resultTitleColor : statusTitleColor;
		if (closeButton != null) closeButton.gameObject.SetActive(!showingResult);
		if (confirmButton != null) confirmButton.gameObject.SetActive(showingResult);

		if (fromLevelText != null)
		{
			fromLevelText.gameObject.SetActive(showingResult);
			SetText(fromLevelText, pendingFromLevel.ToString());
		}
		if (arrow != null) arrow.SetActive(showingResult);
		SetText(levelText, level.ToString());
		SetText(maxLevelText, $"최대 레벨 {LevelRules.MaxLevel}");

		int need = LevelRules.ExpToNext(level);
		int exp = progress.Exp;
		SetText(expText, max ? "MAX" : $"{exp} / {need}");
		if (expFill != null) expFill.fillAmount = max ? 1f : need > 0 ? Mathf.Clamp01((float)exp / need) : 0f;
		SetText(expLeftText, max ? "최대 레벨이에요" : $"다음 레벨까지 {Mathf.Max(0, need - exp)}");

		SetText(At(headerTexts, 0), showingResult ? "이전" : "");
		SetText(At(headerTexts, 1), "지금");
		SetText(At(headerTexts, 2), showingResult ? "늘어난 값" : "다음 레벨");

		var now = new float[StatCount];
		ReadStats(now);
		for (int i = 0; i < StatCount; i++)
		{
			SetText(At(beforeTexts, i), showingResult ? Format(i, pendingFromStats[i]) : "");
			SetText(At(nowTexts, i), Format(i, now[i]));

			TMP_Text diff = At(diffTexts, i);
			if (diff == null) continue;
			// 결과 창: 이전과 지금의 차이. 상태 창: 다음 레벨에 오르는 값 (PlayerLevelStats, 김도훈 #340)
			float delta = showingResult ? now[i] - pendingFromStats[i] : NextLevelGain(i, level);
			bool up = delta > 0.0001f;
			SetText(diff, up ? "+" + Format(i, delta) : "-");
			diff.color = up ? upColor : sameColor;
		}
	}

	// 다음 레벨이 되면 오르는 값. 최대 레벨이거나 PlayerLevelStats가 없으면 0
	float NextLevelGain(int stat, int level)
	{
		if (levelStats == null || level >= LevelRules.MaxLevel) return 0f;
		return stat switch
		{
			0 => levelStats.MaxHpPerLevel,
			1 => levelStats.AttackPowerPerLevel,
			3 => levelStats.MemoryBonusAt(level + 1) - levelStats.MemoryBonusAt(level),
			_ => 0f,
		};
	}

	void ReadStats(float[] into)
	{
		if (stats == null) return;
		into[0] = stats.MaxHp;
		into[1] = stats.AttackPower;
		into[2] = stats.AttackSpeed;
		into[3] = stats.MemorySize;
		into[4] = stats.CooldownReduction;
		into[5] = stats.MoveSpeed;
	}

	// 공격 속도 1.0, 쿨타임 감소 0%, 나머지 정수
	static string Format(int stat, float value) => stat switch
	{
		2 => value.ToString("0.0", CultureInfo.InvariantCulture),
		4 => Mathf.RoundToInt(value * 100f) + "%",
		5 => value.ToString("0.#", CultureInfo.InvariantCulture),
		_ => Mathf.RoundToInt(value).ToString(),
	};

	static T At<T>(T[] array, int index) where T : class => array != null && index < array.Length ? array[index] : null;

	static void SetText(TMP_Text text, string value)
	{
		if (text != null && text.text != value) text.text = value;
	}

	/// <summary>능력치 표의 줄 이름 (빌더가 쓴다)</summary>
	public static string StatName(int index) => StatNames[index];

	/// <summary>능력치 표의 줄 수 (빌더가 쓴다)</summary>
	public static int Rows => StatCount;
}
