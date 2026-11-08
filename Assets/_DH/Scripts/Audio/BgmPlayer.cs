using Mirror;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 장소마다 BGM을 튼다 (#436). 내 캐릭터가 있는 장소(씬 이름)의 곡을 SoundTable에서 찾아 반복 재생하고, 장소가 바뀌면 바꿔 튼다(페이드).
/// <para>
/// - 곡이 같으면(로비 → 사냥터처럼 같은 곡을 쓰는 장소) 끊지 않고 이어서 튼다.<br/>
/// - 내 캐릭터가 없으면(로그인 화면 등) 곡을 끈다. 네트워크 없는 테스트에서는 열린 장소 씬 중 표에 있는 첫 씬의 곡을 튼다.<br/>
/// - 각 화면에서만 도는 표시 기능이라 네트워크를 붙이지 않는다.
/// </para>
/// <para>사용법: Base 씬의 Sound 오브젝트에 붙인다. Table(SoundTable), Output(InvokeMixer의 BGM 그룹)을 넣는다. AudioSource 2개는 시작할 때 이 오브젝트에 붙인다.</para>
/// </summary>
public class BgmPlayer : MonoBehaviour
{
	[SerializeField, Tooltip("소리 표")]
	SoundTable table;

	[SerializeField, Tooltip("BGM을 보낼 Mixer 그룹 (InvokeMixer의 BGM). 설정 창의 BGM 음량을 따른다")]
	AudioMixerGroup output;

	[SerializeField, Range(0f, 1f), Tooltip("BGM 기본 크기")]
	float volume = 0.6f;

	[SerializeField, Min(0f), Tooltip("곡을 바꿀 때 페이드 시간(초)")]
	float fadeTime = 1f;

	[SerializeField, Min(0.05f), Tooltip("장소를 다시 확인하는 간격(초)")]
	float checkInterval = 0.25f;

	// 두 개를 번갈아 써서, 하나가 줄어드는 동안 다른 하나가 커진다
	AudioSource current;
	AudioSource previous;
	float nextCheckTime;
	float fadeStart;

	void Awake()
	{
		current = CreateSource();
		previous = CreateSource();
	}

	AudioSource CreateSource()
	{
		AudioSource s = gameObject.AddComponent<AudioSource>();
		s.playOnAwake = false;
		s.loop = true;
		s.spatialBlend = 0f;
		s.volume = 0f;
		s.outputAudioMixerGroup = output;
		return s;
	}

	void Update()
	{
		float now = Time.unscaledTime;
		if (now >= nextCheckTime)
		{
			nextCheckTime = now + checkInterval;
			AudioClip wanted = table != null ? table.BgmOf(CurrentPlace()) : null;
			if (wanted != current.clip) SwitchTo(wanted, now);
		}

		// 페이드: 새 곡은 커지고 이전 곡은 작아진다
		float t = fadeTime > 0f ? Mathf.Clamp01((now - fadeStart) / fadeTime) : 1f;
		current.volume = current.clip != null ? volume * t : 0f;
		previous.volume = volume * (1f - t);
		if (t >= 1f && previous.isPlaying) previous.Stop();
	}

	void SwitchTo(AudioClip clip, float now)
	{
		// 지금 곡을 이전 곡으로 돌려 줄이고, 새 곡을 처음부터 튼다
		AudioSource temp = previous;
		previous = current;
		current = temp;

		current.Stop();
		current.clip = clip;
		if (clip != null) current.Play();
		fadeStart = now;
	}

	// 내 캐릭터가 있는 씬 이름. 내 캐릭터가 없으면: 서버·클라이언트 중이면 null(곡 끔), 네트워크 없는 테스트면 표에 있는 첫 장소 씬
	string CurrentPlace()
	{
		NetworkIdentity me = NetworkClient.localPlayer;
		if (me != null) return me.gameObject.scene.name;
		if (NetworkClient.active || NetworkServer.active) return null;

		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene scene = SceneManager.GetSceneAt(i);
			if (scene.isLoaded && table != null && table.BgmOf(scene.name) != null) return scene.name;
		}
		return null;
	}
}
