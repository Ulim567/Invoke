using UnityEditor;
using UnityEngine;

/// <summary>
/// [에디터 메뉴, #568] 파이썬 스킬 17개의 효과음을 소리 표(SoundTable, 김도훈 sound.md)에 더한다. 메뉴 Invoke → JW → 파이썬 스킬 효과음 넣기.
/// <para>
/// - 소리 파일: Assets/_JW/Audio/SFX/Python/py_스킬이름.mp3 (Pixabay, 길이 자르고 음량 맞춤).<br/>
/// - 표에 없는 py.* 줄만 더한다. 이미 있는 줄(C 스킬, 손으로 고친 py.* 줄)은 건드리지 않는다.<br/>
/// - 소리는 판정이 나간 순간(SkillEffectEvents.Fired)에 SfxPlayer가 낸다. 코드는 고치지 않는다.
/// </para>
/// </summary>
public static class PythonSkillSounds
{
	const string SoundTablePath = "Assets/_DH/Audio/SoundTable.asset";
	const string SoundFolder = "Assets/_JW/Audio/SFX/Python";

	// 스킬 ID, 소리 파일 이름(확장자 빼고)
	static readonly (string id, string file)[] Sounds =
	{
		(PredefinedFunctionIds.PyMagicMissile, "py_magic_missile"),
		(PredefinedFunctionIds.PyFireball, "py_fireball"),
		(PredefinedFunctionIds.PyIceSpear, "py_ice_spear"),
		(PredefinedFunctionIds.PyChainLightning, "py_chain_lightning"),
		(PredefinedFunctionIds.PyMeteor, "py_meteor"),
		(PredefinedFunctionIds.PyBlizzard, "py_blizzard"),
		(PredefinedFunctionIds.PyVenomMist, "py_venom_mist"),
		(PredefinedFunctionIds.PyOrbCreate, "py_orb_create"),
		(PredefinedFunctionIds.PyOrbShot, "py_orb_shot"),
		(PredefinedFunctionIds.PyOrbBarrage, "py_orb_barrage"),
		(PredefinedFunctionIds.PyOrbBurst, "py_orb_burst"),
		(PredefinedFunctionIds.PyOrbShield, "py_orb_shield"),
		(PredefinedFunctionIds.PyHex, "py_hex"),
		(PredefinedFunctionIds.PySnakeBind, "py_snake_bind"),
		(PredefinedFunctionIds.PyFlash, "py_flash"),
		(PredefinedFunctionIds.PyBlink, "py_blink"),
		(PredefinedFunctionIds.PyLevitate, "py_levitate"),
	};

	[MenuItem("Invoke/JW/파이썬 스킬 효과음 넣기")]
	public static void Apply()
	{
		var table = AssetDatabase.LoadAssetAtPath<SoundTable>(SoundTablePath);
		if (table == null)
		{
			Debug.LogError($"[PythonSkillSounds] {SoundTablePath}가 없음");
			return;
		}
		var so = new SerializedObject(table);
		SerializedProperty list = so.FindProperty("skills");

		int added = 0;
		foreach (var (id, file) in Sounds)
		{
			if (HasRow(list, id)) continue;
			string path = $"{SoundFolder}/{file}.mp3";
			var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
			if (clip == null)
			{
				Debug.LogWarning($"[PythonSkillSounds] {path}가 없음. {id}는 건너뜀");
				continue;
			}
			// 한 칸 늘리면 마지막 줄을 복사한 칸이 생긴다. 아래에서 값을 새로 넣는다
			list.arraySize++;
			SerializedProperty row = list.GetArrayElementAtIndex(list.arraySize - 1);
			row.FindPropertyRelative("skillId").stringValue = id;
			SerializedProperty fired = row.FindPropertyRelative("fired");
			fired.arraySize = 1;
			fired.GetArrayElementAtIndex(0).objectReferenceValue = clip;
			added++;
		}
		if (added > 0)
		{
			so.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(table);
			AssetDatabase.SaveAssets();
		}
		Debug.Log($"[PythonSkillSounds] {added}줄 더함");
	}

	static bool HasRow(SerializedProperty list, string id)
	{
		for (int i = 0; i < list.arraySize; i++)
			if (list.GetArrayElementAtIndex(i).FindPropertyRelative("skillId").stringValue == id) return true;
		return false;
	}
}
