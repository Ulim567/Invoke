using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 설정 값(전체·BGM·효과음 음량, 창/전체 화면)을 갖고, 저장하고, 적용한다. 값만 다루고 화면은 SettingsView가 그린다 (#292).
/// <para>
/// - 전체 음량은 AudioListener.volume (모든 소리, 시안 8-2). BGM·효과음은 아래 Mixer.<br/>
/// - 음량은 0~1. PlayerPrefs에 저장해서 다음에 켜도 그대로다. 처음 값은 0.8.<br/>
/// - 음량은 Audio Mixer의 노출된 파라미터(BGMVolume, SFXVolume)에 데시벨로 넣는다. 소리를 내는 AudioSource는 Output을 그 Mixer의 BGM / SFX 그룹으로 둔다 (settings.md).<br/>
/// - 화면 모드는 Unity가 스스로 기억하므로 따로 저장하지 않는다. 에디터에서는 바뀌지 않고 빌드에서만 바뀐다.
/// </para>
/// </summary>
public static class GameSettings
{
	const string MasterKey = "settings.master_volume";
	const string BgmKey = "settings.bgm_volume";
	const string SfxKey = "settings.sfx_volume";
	const float DefaultVolume = 0.8f;

	/// <summary>창 모드로 바꿀 때 창 크기 (640x360의 2배)</summary>
	public const int WindowedWidth = 1280;
	public const int WindowedHeight = 720;

	static bool loaded;
	static float masterVolume = 1f;
	static float bgmVolume = DefaultVolume;
	static float sfxVolume = DefaultVolume;

	/// <summary>전체 음량 (0~1). 처음 값 1</summary>
	public static float MasterVolume { get { Load(); return masterVolume; } }

	/// <summary>BGM 음량 (0~1)</summary>
	public static float BgmVolume { get { Load(); return bgmVolume; } }

	/// <summary>효과음 음량 (0~1)</summary>
	public static float SfxVolume { get { Load(); return sfxVolume; } }

	/// <summary>지금 전체 화면인지</summary>
	public static bool IsFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

	/// <summary>[표시] 값이 바뀌었을 때</summary>
	public static event Action Changed;

	static void Load()
	{
		if (loaded) return;
		loaded = true;
		masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
		bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmKey, DefaultVolume));
		sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultVolume));
	}

	public static void SetMasterVolume(float volume)
	{
		Load();
		masterVolume = Mathf.Clamp01(volume);
		PlayerPrefs.SetFloat(MasterKey, masterVolume);
		PlayerPrefs.Save();
		Changed?.Invoke();
	}

	public static void SetBgmVolume(float volume)
	{
		Load();
		bgmVolume = Mathf.Clamp01(volume);
		PlayerPrefs.SetFloat(BgmKey, bgmVolume);
		PlayerPrefs.Save();
		Changed?.Invoke();
	}

	public static void SetSfxVolume(float volume)
	{
		Load();
		sfxVolume = Mathf.Clamp01(volume);
		PlayerPrefs.SetFloat(SfxKey, sfxVolume);
		PlayerPrefs.Save();
		Changed?.Invoke();
	}

	/// <summary>전체 화면(테두리 없는 창, 모니터 크기) 또는 창 모드(1280x720)로 바꾼다</summary>
	public static void SetFullscreen(bool fullscreen)
	{
		if (fullscreen) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
		else Screen.SetResolution(WindowedWidth, WindowedHeight, FullScreenMode.Windowed);
		Changed?.Invoke();
	}

	/// <summary>
	/// 지금 음량을 넣는다. 전체 음량은 AudioListener에, BGM·효과음은 Mixer에. Mixer가 없거나 파라미터가 노출되어 있지 않으면 false (Mixer 설정을 확인한다).
	/// </summary>
	public static bool ApplyVolumes(AudioMixer mixer, string bgmParameter, string sfxParameter)
	{
		AudioListener.volume = MasterVolume;
		if (mixer == null) return false;
		bool bgm = mixer.SetFloat(bgmParameter, ToDecibel(BgmVolume));
		bool sfx = mixer.SetFloat(sfxParameter, ToDecibel(SfxVolume));
		return bgm && sfx;
	}

	/// <summary>0~1 음량을 Mixer의 데시벨(-80 ~ 0)로 바꾼다</summary>
	public static float ToDecibel(float volume) => volume <= 0.0001f ? -80f : Mathf.Log10(volume) * 20f;
}
