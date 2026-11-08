using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프리팹 안의 TMP_InputField를 ImeSafeInputField로 바꾸는 에디터 도구. 게임에는 들어가지 않는다.
/// <para>
/// 메뉴 Invoke → UI → 입력 칸을 한글 조합 안전 칸으로 바꾸기.<br/>
/// - Assets 안의 모든 프리팹(Assets/Mirror 제외)에서 TMP_InputField 그 자체인 컴포넌트만 바꾼다.<br/>
/// - 컴포넌트를 지우고 새로 붙이지 않고 스크립트 참조(m_Script)만 바꾼다. ImeSafeInputField는 TMP_InputField를 상속해 칸이 같으므로
///   글자 컴포넌트·Placeholder 연결과 설정이 그대로 남고, 이 칸을 가리키던 다른 컴포넌트의 연결도 끊어지지 않는다.<br/>
/// - 다른 프리팹 안에 넣은 프리팹(중첩 프리팹)의 칸은 그 원본 프리팹에서 바꾼다 (바깥 프리팹에서는 스크립트를 바꿀 수 없기 때문).<br/>
/// - 여러 번 눌러도 된다. 이미 바꾼 칸은 건너뛴다.
/// </para>
/// </summary>
public static class ImeSafeInputFieldConverter
{
	[MenuItem("Invoke/UI/입력 칸을 한글 조합 안전 칸으로 바꾸기")]
	public static void ConvertAll()
	{
		MonoScript safeScript = FindScript();
		if (safeScript == null)
		{
			Debug.LogError("[ImeSafeInputFieldConverter] ImeSafeInputField 스크립트를 찾지 못함");
			return;
		}

		var changed = new List<string>();
		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (path.StartsWith("Assets/Mirror/")) continue;

			GameObject root = PrefabUtility.LoadPrefabContents(path);
			try
			{
				int count = 0;
				foreach (TMP_InputField field in root.GetComponentsInChildren<TMP_InputField>(true))
				{
					if (field.GetType() != typeof(TMP_InputField)) continue; // 이미 ImeSafeInputField
					if (PrefabUtility.IsPartOfPrefabInstance(field)) continue; // 중첩 프리팹의 칸은 원본에서 바꾼다

					var so = new SerializedObject(field);
					so.FindProperty("m_Script").objectReferenceValue = safeScript;
					so.ApplyModifiedPropertiesWithoutUndo();
					count++;
				}
				if (count == 0) continue;
				PrefabUtility.SaveAsPrefabAsset(root, path);
				changed.Add($"{path} ({count}개)");
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}

		AssetDatabase.SaveAssets();
		Debug.Log(changed.Count == 0
			? "[ImeSafeInputFieldConverter] 바꿀 입력 칸이 없음 (이미 모두 바꿈)"
			: "[ImeSafeInputFieldConverter] 입력 칸을 바꿈:\n" + string.Join("\n", changed));
	}

	static MonoScript FindScript()
	{
		foreach (string guid in AssetDatabase.FindAssets("ImeSafeInputField t:MonoScript"))
		{
			var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
			if (script != null && script.GetClass() == typeof(ImeSafeInputField)) return script;
		}
		return null;
	}
}
