using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [에디터 메뉴, #566] C 스킬·공통 이동 스킬의 쿨타임(틱)을 SkillTable 에셋에 넣는다. 메뉴 Invoke → JW → C 스킬 쿨타임 넣기.
/// <para>
/// - 아래 표에 있는 줄의 cooldownTicks만 바꾼다. 다른 칸(틱 수, 배율 등)과 파이썬 스킬(py.*)은 건드리지 않는다.<br/>
/// - 1틱 = 0.02초. 수치는 전부 임시다 (기본기 0, 스킬 트리 뒤쪽 스킬일수록 길게. 파이썬 마법사와 같은 크기).<br/>
/// - 다시 눌러도 같은 값이 들어간다. 표에 없는 ID는 경고만 남긴다.
/// </para>
/// </summary>
public static class CSkillCooldowns
{
	const string SkillTablePath = "Assets/_JW/Resources/SkillTable.asset";

	// 고정 ID, 쿨타임(틱). 50틱 = 1초
	static readonly (string id, int ticks)[] Cooldowns =
	{
		// 찌르기
		(PredefinedFunctionIds.Thrust, 0),
		(PredefinedFunctionIds.FocusedThrust, 50),
		(PredefinedFunctionIds.Skewer, 100),
		(PredefinedFunctionIds.SummonedThrust, 50),
		(PredefinedFunctionIds.WoundExploit, 100),
		(PredefinedFunctionIds.DeepThrust, 150),
		(PredefinedFunctionIds.VitalStrike, 200),
		// 던지기 (검 던지기는 던지면 맨손이라 0)
		(PredefinedFunctionIds.SwordThrow, 0),
		(PredefinedFunctionIds.SummonedThrow, 50),
		(PredefinedFunctionIds.SwordReturn, 50),
		// 휘두르기 (검 휘두르기는 검기 heap이 제한이라 0)
		(PredefinedFunctionIds.SwordSlash, 0),
		(PredefinedFunctionIds.WideSlash, 100),
		(PredefinedFunctionIds.SummonedSlash, 100),
		(PredefinedFunctionIds.WoundSlash, 150),
		(PredefinedFunctionIds.MultiSlash, 200),
		(PredefinedFunctionIds.BattlefieldSweep, 300),
		// 소환 (검 소환·해제는 heap이 제한이라 0)
		(PredefinedFunctionIds.SummonSword, 0),
		(PredefinedFunctionIds.FreeSword, 0),
		(PredefinedFunctionIds.Parry, 150),
		(PredefinedFunctionIds.Mark, 200),
		(PredefinedFunctionIds.SwordDance, 300),
		// 이동 (대시는 블록코딩에서 부를 때만. ←← / →→ 대시는 PlayerMovement의 쿨타임을 따름)
		(PredefinedFunctionIds.Dash, 25),
		(PredefinedFunctionIds.DoubleJump, 0),
		(PredefinedFunctionIds.SwordRide, 300),
	};

	[MenuItem("Invoke/JW/C 스킬 쿨타임 넣기")]
	public static void Apply()
	{
		var table = AssetDatabase.LoadAssetAtPath<SkillTable>(SkillTablePath);
		if (table == null)
		{
			Debug.LogError($"[CSkillCooldowns] {SkillTablePath}가 없음");
			return;
		}
		var so = new SerializedObject(table);
		SerializedProperty list = so.FindProperty("skills");
		var rows = new Dictionary<string, SerializedProperty>();
		for (int i = 0; i < list.arraySize; i++)
		{
			SerializedProperty row = list.GetArrayElementAtIndex(i);
			rows[row.FindPropertyRelative("id").stringValue] = row;
		}

		int changed = 0;
		foreach (var (id, ticks) in Cooldowns)
		{
			if (!rows.TryGetValue(id, out SerializedProperty row))
			{
				Debug.LogWarning($"[CSkillCooldowns] 표에 {id} 줄이 없음");
				continue;
			}
			SerializedProperty cooldown = row.FindPropertyRelative("cooldownTicks");
			if (cooldown.intValue == ticks) continue;
			Debug.Log($"[CSkillCooldowns] {id}: 쿨타임 {cooldown.intValue} → {ticks}틱");
			cooldown.intValue = ticks;
			changed++;
		}
		if (changed > 0)
		{
			so.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(table);
			AssetDatabase.SaveAssets();
		}
		Debug.Log($"[CSkillCooldowns] {changed}줄 바꿈");
	}
}
