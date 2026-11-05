using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// 설정 화면. 화면 오른쪽 위 「설정」 버튼으로 열고, BGM·효과음 음량과 창/전체 화면을 바꾼다. 화면만 맡고 값은 GameSettings가 갖는다 (#292).
/// <para>
/// - SettingsCanvas 프리팹 최상위에 붙이고 Base 씬에 둔다 (Base 씬은 계속 떠 있으므로 로비·사냥터·레이드 어디서나 열 수 있다).<br/>
/// - 시작할 때 저장된 음량을 Mixer에 넣는다. Mixer는 Start부터 값을 받으므로 Awake가 아니라 Start에서 넣는다.<br/>
/// - 프리팹은 메뉴 Invoke → UI → 설정 화면 만들기(SettingsUIBuilder)가 만든다.
/// </para>
/// </summary>
public class SettingsView : MonoBehaviour
{
	[Header("소리")]
	[SerializeField, Tooltip("BGM·SFX 그룹이 있는 Audio Mixer (Assets/_HW/Audio/InvokeMixer)")]
	AudioMixer mixer;

	[SerializeField, Tooltip("Mixer에 노출한 BGM 그룹 음량 파라미터 이름")]
	string bgmParameter = "BGMVolume";

	[SerializeField, Tooltip("Mixer에 노출한 SFX 그룹 음량 파라미터 이름")]
	string sfxParameter = "SFXVolume";

	[Header("화면")]
	[SerializeField] GameObject window;
	[SerializeField] Button openButton;
	[SerializeField] Button closeButton;
	[SerializeField] Slider bgmSlider;
	[SerializeField] TMP_Text bgmValueText;
	[SerializeField] Slider sfxSlider;
	[SerializeField] TMP_Text sfxValueText;
	[SerializeField] Button windowedButton;
	[SerializeField] Button fullscreenButton;

	[SerializeField, Tooltip("고르지 않은 화면 모드 버튼 그림")]
	Sprite normalSprite;

	[SerializeField, Tooltip("고른 화면 모드 버튼 그림")]
	Sprite selectedSprite;

	void Start()
	{
		if (window != null) window.SetActive(false);
		if (mixer != null && !GameSettings.ApplyVolumes(mixer, bgmParameter, sfxParameter))
			Debug.LogWarning($"[SettingsView] Mixer에 {bgmParameter} / {sfxParameter} 파라미터가 노출되어 있지 않습니다 (settings.md).");

		if (openButton != null) openButton.onClick.AddListener(Open);
		if (closeButton != null) closeButton.onClick.AddListener(Close);
		if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
		if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
		if (windowedButton != null) windowedButton.onClick.AddListener(() => GameSettings.SetFullscreen(false));
		if (fullscreenButton != null) fullscreenButton.onClick.AddListener(() => GameSettings.SetFullscreen(true));
		GameSettings.Changed += Refresh;
		Refresh();
	}

	void OnDestroy() => GameSettings.Changed -= Refresh;

	// 화면 모드는 바꾼 다음 프레임에 반영되므로, 열려 있는 동안 계속 맞춘다
	void Update()
	{
		if (window != null && window.activeSelf) RefreshScreenMode();
	}

	public void Open()
	{
		if (window == null) return;
		window.SetActive(true);
		Refresh();
	}

	public void Close()
	{
		if (window != null) window.SetActive(false);
	}

	void OnBgmChanged(float value)
	{
		GameSettings.SetBgmVolume(value);
		GameSettings.ApplyVolumes(mixer, bgmParameter, sfxParameter);
	}

	void OnSfxChanged(float value)
	{
		GameSettings.SetSfxVolume(value);
		GameSettings.ApplyVolumes(mixer, bgmParameter, sfxParameter);
	}

	void Refresh()
	{
		if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
		if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
		if (bgmValueText != null) bgmValueText.text = Percent(GameSettings.BgmVolume);
		if (sfxValueText != null) sfxValueText.text = Percent(GameSettings.SfxVolume);
		RefreshScreenMode();
	}

	void RefreshScreenMode()
	{
		bool full = GameSettings.IsFullscreen;
		SetSelected(windowedButton, !full);
		SetSelected(fullscreenButton, full);
	}

	void SetSelected(Button button, bool selected)
	{
		if (button == null || button.image == null) return;
		Sprite sprite = selected ? selectedSprite : normalSprite;
		if (sprite != null && button.image.sprite != sprite) button.image.sprite = sprite;
	}

	static string Percent(float volume) => Mathf.RoundToInt(volume * 100f) + "%";
}
