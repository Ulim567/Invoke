using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 소환 계열 스킬 이펙트(표식 남기기, 검무, 패링) 프리팹을 만들고 공용 플레이어 프리팹에 SkillHitEffectView를 붙이는 에디터 도구 (#290). 게임에는 들어가지 않는다.
/// <para>
/// 메뉴 Invoke → 이펙트 → 스킬 이펙트 붙이기. 여러 번 눌러도 된다 (그림을 고친 뒤 다시 누르면 클립·프리팹을 새로 만든다).<br/>
/// - 그림(Mark_Flash, Dance_Slash, Parry_Flash, 각 5장면)으로 20fps 클립과 컨트롤러를 만든다. 5장면 = 0.25초.<br/>
/// - 프리팹: SpriteRenderer(ThrustFlash와 같은 재질·정렬) + Animator + OneShotEffect(0.25초).<br/>
/// - Assets/Prefabs/Player.prefab 최상위에 SkillHitEffectView를 붙이고 세 프리팹을 넣는다 (없을 때만 붙임).
/// </para>
/// </summary>
public static class SkillEffectBuilder
{
	const string Dir = "Assets/_HW/Effects/";
	const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
	const string ThrustFlashPath = "Assets/_JW/ThrustFlash.prefab";
	const float Fps = 20f;

	[MenuItem("Invoke/이펙트/스킬 이펙트 붙이기")]
	public static void Build()
	{
		GameObject mark = MakeEffect("Mark_Flash");
		GameObject dance = MakeEffect("Dance_Slash");
		GameObject parry = MakeEffect("Parry_Flash");
		if (mark == null || dance == null || parry == null) return;

		GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
		try
		{
			if (root.GetComponent<SkillEffectEvents>() == null)
			{
				Debug.LogError($"[SkillEffectBuilder] {PlayerPrefabPath}에 SkillEffectEvents가 없어 붙이지 않음");
				return;
			}
			SkillHitEffectView view = root.GetComponent<SkillHitEffectView>();
			if (view == null) view = root.AddComponent<SkillHitEffectView>();
			var so = new SerializedObject(view);
			so.FindProperty("markFlashPrefab").objectReferenceValue = mark;
			so.FindProperty("danceSlashPrefab").objectReferenceValue = dance;
			so.FindProperty("parryFlashPrefab").objectReferenceValue = parry;
			so.ApplyModifiedPropertiesWithoutUndo();
			PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}

		AssetDatabase.SaveAssets();
		Debug.Log("[SkillEffectBuilder] Mark_Flash, Dance_Slash, Parry_Flash 프리팹을 만들고 Player.prefab에 SkillHitEffectView를 붙임");
	}

	static GameObject MakeEffect(string name)
	{
		Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Dir + name + ".png").OfType<Sprite>()
			.OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
		if (frames.Length == 0)
		{
			Debug.LogError($"[SkillEffectBuilder] {name}.png의 장면을 찾지 못함");
			return null;
		}

		// 클립 (반복 없음)
		var clip = new AnimationClip { name = name, frameRate = Fps };
		var keys = new ObjectReferenceKeyframe[frames.Length];
		for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = frames[i] };
		AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
		AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
		settings.loopTime = false;
		AnimationUtility.SetAnimationClipSettings(clip, settings);
		string clipPath = Dir + name + ".anim";
		AnimationClip oldClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
		if (oldClip != null)
		{
			EditorUtility.CopySerialized(clip, oldClip);
			clip = oldClip;
		}
		else AssetDatabase.CreateAsset(clip, clipPath);

		// 컨트롤러 (상태 하나)
		string controllerPath = Dir + name + ".controller";
		AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
		if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);

		// 프리팹: ThrustFlash와 같은 재질·정렬
		var obj = new GameObject(name);
		var sr = obj.AddComponent<SpriteRenderer>();
		sr.sprite = frames[0];
		GameObject thrust = AssetDatabase.LoadAssetAtPath<GameObject>(ThrustFlashPath);
		SpriteRenderer thrustSr = thrust != null ? thrust.GetComponentInChildren<SpriteRenderer>() : null;
		if (thrustSr != null)
		{
			sr.sharedMaterial = thrustSr.sharedMaterial;
			sr.sortingLayerID = thrustSr.sortingLayerID;
			sr.sortingOrder = thrustSr.sortingOrder;
		}
		else sr.sortingOrder = 10;
		obj.AddComponent<Animator>().runtimeAnimatorController = controller;
		obj.AddComponent<OneShotEffect>(); // 기본 0.25초
		GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, Dir + name + ".prefab");
		Object.DestroyImmediate(obj);
		return prefab;
	}
}
