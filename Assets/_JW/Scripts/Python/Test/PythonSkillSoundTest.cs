using System.Collections;
using UnityEngine;

/// <summary>
/// 파이썬 스킬 효과음 테스트 (#568). Test_JW의 PythonSoundTest. 켜고 Play (Host 안 눌러도 됨, 약 30초).
/// <para>
/// - 소리 표(SoundTable)에서 py.* 17개를 SfxPlayer와 같은 방법(SkillFiredOf)으로 찾아, 소리가 있는지·길이가 2초 이하인지 본다.<br/>
/// - 찾은 소리를 하나씩 1.8초 간격으로 틀고 콘솔에 스킬 이름을 남긴다. 귀로 듣고 어색한 소리를 고른다.<br/>
/// - Table 칸을 비워 두면 에디터에서 Assets/_DH/Audio/SoundTable.asset을 읽는다.<br/>
/// 「[PythonSoundTest] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class PythonSkillSoundTest : MonoBehaviour
{
	const string Tag = "[PythonSoundTest]";

	[SerializeField, Tooltip("비워 두면 에디터에서 Assets/_DH/Audio/SoundTable.asset을 읽는다")]
	SoundTable table;
	[SerializeField, Range(0f, 1f), Tooltip("틀 때 크기")]
	float volume = 0.8f;
	[SerializeField, Min(0.5f), Tooltip("소리 사이 간격 (초)")]
	float gap = 1.8f;

	static readonly (string id, string name)[] Skills =
	{
		(PredefinedFunctionIds.PyMagicMissile, "매직 미사일"),
		(PredefinedFunctionIds.PyFireball, "파이어볼"),
		(PredefinedFunctionIds.PyIceSpear, "얼음 창"),
		(PredefinedFunctionIds.PyChainLightning, "체인 라이트닝"),
		(PredefinedFunctionIds.PyMeteor, "메테오"),
		(PredefinedFunctionIds.PyBlizzard, "블리자드"),
		(PredefinedFunctionIds.PyVenomMist, "독 안개"),
		(PredefinedFunctionIds.PyOrbCreate, "구슬 생성"),
		(PredefinedFunctionIds.PyOrbShot, "구슬 발사"),
		(PredefinedFunctionIds.PyOrbBarrage, "구슬 난사"),
		(PredefinedFunctionIds.PyOrbBurst, "구슬 폭발"),
		(PredefinedFunctionIds.PyOrbShield, "구슬 보호막"),
		(PredefinedFunctionIds.PyHex, "저주"),
		(PredefinedFunctionIds.PySnakeBind, "뱀 속박"),
		(PredefinedFunctionIds.PyFlash, "섬광"),
		(PredefinedFunctionIds.PyBlink, "블링크"),
		(PredefinedFunctionIds.PyLevitate, "공중 부양"),
	};

	int failed;

	IEnumerator Start()
	{
#if UNITY_EDITOR
		if (table == null) table = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundTable>("Assets/_DH/Audio/SoundTable.asset");
#endif
		if (table == null)
		{
			Debug.LogError($"{Tag} SoundTable을 찾지 못함 (Table 칸에 넣어 주세요)");
			yield break;
		}

		AudioSource source = gameObject.AddComponent<AudioSource>();
		source.playOnAwake = false;
		source.spatialBlend = 0f;
		yield return new WaitForSeconds(0.5f);

		for (int i = 0; i < Skills.Length; i++)
		{
			(string id, string name) = Skills[i];
			AudioClip[] clips = table.SkillFiredOf(id);
			AudioClip clip = clips != null && clips.Length > 0 ? clips[0] : null;
			Check($"{i + 1}. {name} ({id}): 소리 있음", clip != null);
			if (clip == null) continue;
			Check($"{i + 1}. {name}: 길이 2초 이하 ({clip.length:0.00}초)", clip.length <= 2f);
			Debug.Log($"{Tag} ▶ {i + 1}/{Skills.Length} {name} ({clip.name})");
			source.PlayOneShot(clip, volume);
			yield return new WaitForSeconds(Mathf.Max(gap, clip.length + 0.3f));
		}

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	void Check(string label, bool ok)
	{
		if (ok) return;
		failed++;
		Debug.LogError($"{Tag} {label}: 실패");
	}
}
