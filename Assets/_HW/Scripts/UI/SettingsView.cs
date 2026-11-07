using System;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 설정 창 (우림이 시안 8-2, 8-4). 전체·배경음·효과음 음량, 「돌아가기」, 「로그아웃」(레이드 던전에서는 「레이드 포기」). 화면만 맡고 값은 GameSettings가 갖는다 (#292).
/// <para>
/// - 여는 법: HUD의 「설정」 버튼이나 Esc (HudView.SettingsRequested). 닫는 법: 「돌아가기」, Esc.<br/>
/// - 열려 있는 동안 내 캐릭터의 Player·Attack 맵을 끈다 (InputBlockingPanel). 뒤의 HUD는 숨기지 않고 어둡게 덮는다.<br/>
/// - 방향키 위·아래로 고르고 왼쪽·오른쪽으로 음량을 바꾸고 Enter로 누른다 (EventSystem 이동). 열 때 「돌아가기」를 고른다.<br/>
/// - 로그아웃: LogoutRequested를 알린다. 받는 쪽(계정)이 없으면 버튼을 끈다.<br/>
/// - 레이드 포기: 내 캐릭터가 레이드 던전에 있으면 로그아웃 자리에 나온다. RaidParties.RequestGiveUp()을 부른다.<br/>
/// - SettingsCanvas 프리팹 최상위에 붙이고 Base 씬에 둔다. 프리팹은 메뉴 Invoke → UI → 설정 화면 만들기(SettingsUIBuilder).
/// </para>
/// </summary>
public class SettingsView : InputBlockingPanel
{
	/// <summary>[표시] 설정 창의 「로그아웃」을 눌렀을 때. 계정 쪽이 받아서 접속을 끊고 로그인 화면으로 돌아간다. 받는 쪽이 없으면 버튼이 꺼진다</summary>
	public static event Action LogoutRequested;

	[Header("소리")]
	[SerializeField, Tooltip("BGM·SFX 그룹이 있는 Audio Mixer (Assets/_HW/Audio/InvokeMixer)")]
	AudioMixer mixer;

	[SerializeField, Tooltip("Mixer에 노출한 BGM 그룹 음량 파라미터 이름")]
	string bgmParameter = "BGMVolume";

	[SerializeField, Tooltip("Mixer에 노출한 SFX 그룹 음량 파라미터 이름")]
	string sfxParameter = "SFXVolume";

	[Header("화면")]
	[SerializeField] Slider masterSlider;
	[SerializeField] TMP_Text masterValueText;
	[SerializeField] Slider bgmSlider;
	[SerializeField] TMP_Text bgmValueText;
	[SerializeField] Slider sfxSlider;
	[SerializeField] TMP_Text sfxValueText;
	[SerializeField, Tooltip("돌아가기 (창 닫기)")] Button backButton;
	[SerializeField, Tooltip("로그아웃 / 레이드 포기")] Button exitButton;
	[SerializeField] TMP_Text exitText;
	[SerializeField] Color exitColor = new Color32(0xF0, 0x90, 0x7E, 0xFF);
	[SerializeField] Color exitDisabledColor = new Color32(0x6A, 0x60, 0x78, 0xFF);

	// 뒤의 HUD는 어둡게 덮기만 한다 (시안 8-2)
	protected override bool HidesHud => false;

	void Start()
	{
		if (!GameSettings.ApplyVolumes(mixer, bgmParameter, sfxParameter) && mixer != null)
			Debug.LogWarning($"[SettingsView] Mixer에 {bgmParameter} / {sfxParameter} 파라미터가 노출되어 있지 않습니다 (settings.md).");

		if (masterSlider != null) masterSlider.onValueChanged.AddListener(GameSettings.SetMasterVolume);
		if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(GameSettings.SetBgmVolume);
		if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(GameSettings.SetSfxVolume);
		if (backButton != null) backButton.onClick.AddListener(Close);
		if (exitButton != null) exitButton.onClick.AddListener(OnExit);

		HudView.SettingsRequested += Open;
		GameSettings.Changed += OnSettingsChanged;
		Refresh();
	}

	void OnDestroy()
	{
		HudView.SettingsRequested -= Open;
		GameSettings.Changed -= OnSettingsChanged;
	}

	/// <summary>설정 창을 연다. HUD의 설정 버튼·Esc가 부른다</summary>
	public void Open()
	{
		if (IsOpen) return;
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		Show(me != null ? me.GetComponentInChildren<PlayerInputReader>() : null);
		Refresh();
		if (backButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(backButton.gameObject);
	}

	void OnSettingsChanged()
	{
		GameSettings.ApplyVolumes(mixer, bgmParameter, sfxParameter);
		Refresh();
	}

	// 열려 있는 동안 장소가 바뀔 수 있으므로 (레이드가 끝나 로비로) 계속 맞춘다
	void Update()
	{
		if (!IsOpen) return;
		RefreshExit();
		// 마우스로 빈 곳을 눌러 고른 것이 없어지면 방향키가 먹지 않으므로 「돌아가기」를 다시 고른다
		if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && backButton != null)
			EventSystem.current.SetSelectedGameObject(backButton.gameObject);
	}

	static bool InRaid
	{
		// 장소는 PlayerPublicInfo.Place로 본다 (#362)
		get => PlayerPlace.Mine == PlaceScenes.RaidDungeon;
	}

	void OnExit()
	{
		bool raid = InRaid;
		Close();
		if (raid) RaidParties.RequestGiveUp();
		else LogoutRequested?.Invoke();
	}

	void Refresh()
	{
		Set(masterSlider, masterValueText, GameSettings.MasterVolume);
		Set(bgmSlider, bgmValueText, GameSettings.BgmVolume);
		Set(sfxSlider, sfxValueText, GameSettings.SfxVolume);
		RefreshExit();
	}

	void RefreshExit()
	{
		if (exitButton == null) return;
		bool raid = InRaid;
		bool usable = raid || LogoutRequested != null;
		if (exitButton.interactable != usable)
		{
			exitButton.interactable = usable;
			// 꺼진 버튼으로는 ↓로 내려가지 않게
			if (backButton != null)
			{
				Navigation nav = backButton.navigation;
				nav.selectOnDown = usable ? exitButton : null;
				backButton.navigation = nav;
			}
		}
		if (exitText != null)
		{
			string label = raid ? "레이드 포기" : "로그아웃";
			if (exitText.text != label) exitText.text = label;
			exitText.color = usable ? exitColor : exitDisabledColor;
		}
	}

	static void Set(Slider slider, TMP_Text text, float volume)
	{
		if (slider != null) slider.SetValueWithoutNotify(volume);
		if (text != null) text.text = Mathf.RoundToInt(volume * 100f).ToString();
	}
}
