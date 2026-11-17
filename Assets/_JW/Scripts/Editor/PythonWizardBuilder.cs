using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [에디터 전용] 파이썬 마법사에 필요한 에셋을 한 번에 만든다. 메뉴 Invoke → JW → 파이썬 마법사 만들기. 여러 번 눌러도 된다.
/// <para>
/// 1. 이펙트: Assets/_JW/Effects/Magic/*.png 시트를 잘라 Assets/_JW/Resources/Magic/{이름}.prefab을 만든다 (MagicEffectView, ManaOrbView, OrbShieldView가 이름으로 찾는다). 중독 그림은 Resources/StatusEffects/Poison.prefab.<br/>
/// 2. 캐릭터 모션: Assets/_JW/Python/Py_*.png(64×64 칸, 24 PPU, 발밑 기준)를 잘라 12fps 클립과 Py_Player.controller(Idle, Run, Jump, Cast, Hit, Death)를 만든다.<br/>
/// 3. 언어 규칙: LanguageRule_C를 복사해 Assets/_JW/Python/LanguageRule_Python.asset을 만들고 Python 규칙으로 바꾼다 (이미 있으면 건드리지 않는다).<br/>
/// 4. 스킬 수치: SkillTable에 없는 py.* 줄만 처음 값으로 더한다 (이미 있는 줄은 건드리지 않는다. 수치는 그 뒤 Inspector에서 고친다).<br/>
/// 5. 플레이어 프리팹: Assets/Prefabs/Player.prefab의 Variant로 Assets/_JW/Python/Player_Python.prefab을 만들고, 마법사 컴포넌트·컨트롤러·언어 규칙을 넣는다.
///    이미 있으면 빠진 컴포넌트와 연결만 채운다.<br/>
/// 이펙트 프리팹과 클립은 다시 누르면 같은 결과로 덮어쓴다 (프리팹에서 바꾼 값도 되돌아가니 주의).<br/>
/// 6. 등록: Base 씬의 NetworkManager(InvokeNetworkManager)의 Registered Spawnable Prefabs와 Character Prefabs에 Player_Python을 넣고 씬을 저장한다 (이미 있으면 그대로).
/// </para>
/// </summary>
public static class PythonWizardBuilder
{
	const string EffectSheetDir = "Assets/_JW/Effects/Magic/";
	const string EffectPrefabDir = "Assets/_JW/Resources/Magic/";
	const string PoisonSheet = "Assets/_JW/Effects/Status/status_poison.png";
	const string PoisonPrefab = "Assets/_JW/Resources/StatusEffects/Poison.prefab";
	const string PythonDir = "Assets/_JW/Python/";
	const string AnimationDir = PythonDir + "Animations/";
	const string ControllerPath = AnimationDir + "Py_Player.controller";
	const string RulePath = PythonDir + "LanguageRule_Python.asset";
	const string CRulePath = "Assets/_UL/Data/LanguageRule_C.asset";
	const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
	const string PythonPrefabPath = PythonDir + "Player_Python.prefab";
	const string SkillTablePath = "Assets/_JW/Resources/SkillTable.asset";
	const string ThrustFlashPath = "Assets/_JW/ThrustFlash.prefab";

	enum EffectKind
	{
		Loop,    // 계속 반복 (구슬, 보호막, 범위 마법, 중독)
		OneShot, // 한 번 재생하고 사라짐 (OneShotEffect)
		Bolt,    // 날아가는 마법탄 (MagicBoltVisual). 반복 재생
		Beam,    // 번개 줄기. 한 번 재생, 왼쪽 끝이 기준점, 길이 1유닛으로 맞춤
	}

	// 이름(= 시트 파일 이름 = 프리팹 이름), 칸 너비·높이(px), 종류, 초당 프레임, 기준점(칸 안 0~1), 정렬 레이어, 정렬 순서
	static readonly (string name, int w, int h, EffectKind kind, float fps, Vector2 pivot, string layer, int order)[] Effects =
	{
		("mana_orb", 16, 16, EffectKind.Loop, 8f, Center, "Effect", 6),
		("orb_create_cast", 32, 32, EffectKind.OneShot, 14f, Center, "Effect", 7),
		("orb_shot_bolt", 32, 32, EffectKind.Bolt, 14f, Center, "Projectile", 5),
		("orb_shot_hit", 32, 32, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("orb_burst_cast", 128, 128, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("orb_shield_zone", 64, 64, EffectKind.Loop, 8f, Center, "Effect", 4),
		("orb_shield_hit", 64, 64, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("magic_missile_bolt", 32, 32, EffectKind.Bolt, 14f, Center, "Projectile", 5),
		("magic_missile_hit", 32, 32, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("fireball_bolt", 32, 32, EffectKind.Bolt, 14f, Center, "Projectile", 5),
		("fireball_hit", 64, 64, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("ice_spear_bolt", 48, 32, EffectKind.Bolt, 14f, Center, "Projectile", 5),
		("ice_spear_hit", 32, 32, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("chain_lightning_beam", 64, 32, EffectKind.Beam, 20f, new Vector2(0f, 0.5f), "Effect", 7),
		("chain_lightning_hit", 32, 32, EffectKind.OneShot, 16f, Center, "Effect", 8),
		// 운석: 칸 아래 가운데가 떨어지는 자리. 앞 5장(12fps, 약 0.4초)이 떨어지는 장면이라 PyMeteor.ImpactTick(20틱)과 맞는다
		("meteor_cast", 96, 144, EffectKind.OneShot, 12f, new Vector2(0.5f, 0.14f), "Effect", 7),
		("blizzard_zone", 96, 96, EffectKind.Loop, 10f, Center, "Effect", 6),
		("venom_mist_zone", 96, 96, EffectKind.Loop, 8f, Center, "Effect", 6),
		("hex_bolt", 32, 32, EffectKind.Bolt, 12f, Center, "Projectile", 5),
		("hex_hit", 32, 32, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("snake_bind_bolt", 32, 32, EffectKind.Bolt, 12f, Center, "Projectile", 5),
		("snake_bind_hit", 32, 32, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("flash_cast", 160, 160, EffectKind.OneShot, 18f, Center, "Effect", 8),
		("blink_cast", 48, 48, EffectKind.OneShot, 16f, Center, "Effect", 7),
		("levitate_cast", 48, 48, EffectKind.OneShot, 14f, Center, "Effect", 3),
	};

	static Vector2 Center => new Vector2(0.5f, 0.5f);

	// 캐릭터 모션: Animator 상태 이름, 시트 이름, 반복 여부
	static readonly (string state, string sheet, bool loop)[] Motions =
	{
		("Idle", "Py_Idle", true),
		("Run", "Py_Run", true),
		("Jump", "Py_Jump", false),
		("Cast", "Py_Cast", false),
		("Hit", "Py_Hit", false),
		("Death", "Py_Death", false),
	};

	// SkillTable에 더할 처음 값 (전부 임시). 계열·대상 지정은 enum 이름으로 적는다
	// id, 이름, 계열, 대상 지정, 데미지 배율, 틱 수, 쿨타임 틱, heap, 반경, 설명
	static readonly (string id, string name, SkillFamily family, SkillTargeting targeting, float damage, int ticks, int cooldown, int heap, float radius, string note)[] Skills =
	{
		(PredefinedFunctionIds.PyMagicMissile, "매직 미사일", SkillFamily.Element, SkillTargeting.Single, 1f, 30, 0, 1, 0f, "마법탄을 쏴 처음 닿은 적 한 명"),
		(PredefinedFunctionIds.PyFireball, "파이어볼", SkillFamily.Element, SkillTargeting.Single, 1.2f, 30, 50, 1, 2f, "처음 닿은 적 + 반경 안 다른 적에게 절반 (폭발)"),
		(PredefinedFunctionIds.PyIceSpear, "얼음 창", SkillFamily.Element, SkillTargeting.Pierce, 0.8f, 30, 50, 1, 0f, "관통, 맞은 적 이속 감소"),
		(PredefinedFunctionIds.PyChainLightning, "체인 라이트닝", SkillFamily.Element, SkillTargeting.Single, 1f, 30, 100, 1, 0f, "바로 닿음, 가까운 적으로 2번 튐 (0.6배)"),
		(PredefinedFunctionIds.PyMeteor, "메테오", SkillFamily.Zone, SkillTargeting.Single, 2f, 40, 150, 2, 3f, "대상 자리에 0.4초 뒤 떨어져 반경 안 모두"),
		(PredefinedFunctionIds.PyBlizzard, "블리자드", SkillFamily.Zone, SkillTargeting.Single, 0.4f, 50, 200, 2, 3f, "대상 자리에 깔림, 10틱마다 반경 안 모두 + 이속 감소"),
		(PredefinedFunctionIds.PyVenomMist, "독 안개", SkillFamily.Zone, SkillTargeting.Single, 0.2f, 40, 200, 2, 3f, "대상 자리에 깔림, 10틱마다 약한 데미지 + 중독 한 겹"),
		(PredefinedFunctionIds.PyOrbCreate, "구슬 생성", SkillFamily.Orb, SkillTargeting.None, 1f, 30, 0, 2, 0f, "마나 구슬 하나를 heap에 만든다 (heap 크기 = 구슬 크기, 최대 5개)"),
		(PredefinedFunctionIds.PyOrbShot, "구슬 발사", SkillFamily.Orb, SkillTargeting.Single, 1.8f, 30, 0, 0, 0f, "구슬 1개를 써서 쏜다. 매직 미사일보다 셈"),
		(PredefinedFunctionIds.PyOrbBarrage, "구슬 난사", SkillFamily.Orb, SkillTargeting.MultiSelect, 1.2f, 40, 100, 0, 0f, "구슬을 모두 써서 고른 대상들에게 하나씩"),
		(PredefinedFunctionIds.PyOrbBurst, "구슬 폭발", SkillFamily.Orb, SkillTargeting.Area, 1.2f, 30, 100, 0, 4f, "구슬을 모두 터뜨려 둘레 모두. 배율 × 구슬 수"),
		(PredefinedFunctionIds.PyOrbShield, "구슬 보호막", SkillFamily.Orb, SkillTargeting.None, 1f, 30, 100, 0, 0f, "구슬 1개를 써서 10초 동안 다음 피해 한 번을 막음"),
		(PredefinedFunctionIds.PyHex, "저주", SkillFamily.Curse, SkillTargeting.Single, 0.3f, 30, 100, 1, 0f, "약한 데미지 + 표식"),
		(PredefinedFunctionIds.PySnakeBind, "뱀 속박", SkillFamily.Curse, SkillTargeting.Single, 0.3f, 30, 150, 1, 0f, "약한 데미지 + 기절"),
		(PredefinedFunctionIds.PyFlash, "섬광", SkillFamily.Curse, SkillTargeting.Area, 0f, 30, 200, 1, 5f, "둘레 모두 실명. 데미지 없음"),
		(PredefinedFunctionIds.PyBlink, "블링크", SkillFamily.Movement, SkillTargeting.None, 1f, 15, 100, 1, 0f, "바라보는 쪽으로 5칸 순간이동"),
		(PredefinedFunctionIds.PyLevitate, "공중 부양", SkillFamily.Movement, SkillTargeting.None, 1f, 20, 300, 1, 0f, "6초 동안 떠다님"),
	};

	[MenuItem("Invoke/JW/파이썬 마법사 만들기")]
	public static void Build()
	{
		EnsureFolder("Assets/_JW/Resources");
		EnsureFolder("Assets/_JW/Resources/Magic");
		EnsureFolder("Assets/_JW/Resources/StatusEffects");
		EnsureFolder("Assets/_JW/Python");
		EnsureFolder("Assets/_JW/Python/Animations");

		int effects = BuildEffects();
		AnimatorController controller = BuildMotions(out Sprite idleSprite);
		LanguageRuleData rule = BuildLanguageRule();
		int rows = AddSkillRows();
		bool prefab = BuildPlayerPrefab(controller, idleSprite, rule);
		bool registered = prefab && RegisterInNetworkManager();

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log($"[PythonWizardBuilder] 이펙트 프리팹 {effects}개, 모션 컨트롤러 {(controller != null ? "만듦" : "없음(그림 없음)")}, " +
			$"언어 규칙 {(rule != null ? "있음" : "없음")}, SkillTable에 더한 줄 {rows}개, 플레이어 프리팹 {(prefab ? "만듦" : "못 만듦")}, NetworkManager 등록 {(registered ? "됨" : "안 됨")}");
	}

	static void EnsureFolder(string path)
	{
		if (AssetDatabase.IsValidFolder(path)) return;
		int slash = path.LastIndexOf('/');
		AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
	}

	// 1. 이펙트 ---------------------------------------------------------------

	static int BuildEffects()
	{
		int made = 0;
		foreach (var e in Effects)
		{
			Sprite[] frames = SliceSheet(EffectSheetDir + e.name + ".png", e.w, e.h, e.pivot, 16);
			if (frames == null) continue;
			MakeEffectPrefab(EffectPrefabDir + e.name + ".prefab", e.name, frames, e.kind, e.fps, e.layer, e.order, e.w / 16f);
			made++;
		}
		// 중독 몸 효과 (StatusEffectView가 Resources/StatusEffects/Poison으로 찾는다)
		Sprite[] poison = SliceSheet(PoisonSheet, 32, 32, Center, 16);
		if (poison != null)
		{
			MakeEffectPrefab(PoisonPrefab, "Poison", poison, EffectKind.Loop, 10f, "Effect", 5, 2f);
			made++;
		}
		return made;
	}

	// 시트를 가져오기 설정(Sprite Multiple, Point, 압축 없음)하고 w × h 칸으로 가로 한 줄 자른다. 없는 시트는 알리고 null
	static Sprite[] SliceSheet(string path, int w, int h, Vector2 pivot, int pixelsPerUnit)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
		{
			Debug.LogWarning($"[PythonWizardBuilder] {path}가 없어서 건너뜀");
			return null;
		}
		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Multiple;
		importer.spritePixelsPerUnit = pixelsPerUnit;
		importer.filterMode = FilterMode.Point;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.mipmapEnabled = false;
		importer.alphaIsTransparency = true;
		importer.SaveAndReimport();

		var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		int count = texture.width / w;
		string baseName = System.IO.Path.GetFileNameWithoutExtension(path);

		var factory = new SpriteDataProviderFactories();
		factory.Init();
		ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
		provider.InitSpriteEditorDataProvider();
		var rects = new List<SpriteRect>();
		for (int i = 0; i < count; i++)
		{
			rects.Add(new SpriteRect
			{
				name = $"{baseName}_{i}",
				rect = new Rect(i * w, 0, w, h),
				alignment = SpriteAlignment.Custom,
				pivot = pivot,
				spriteID = GUID.Generate(),
			});
		}
		provider.SetSpriteRects(rects.ToArray());
		var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
		names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
		provider.Apply();
		importer.SaveAndReimport();

		return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
			.OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
			.ToArray();
	}

	// widthUnits: 한 칸의 가로 길이(유닛). 번개 줄기를 길이 1유닛으로 맞출 때 쓴다
	static void MakeEffectPrefab(string path, string name, Sprite[] frames, EffectKind kind, float fps, string layer, int order, float widthUnits)
	{
		var obj = new GameObject(name);
		var sr = obj.AddComponent<SpriteRenderer>();
		sr.sprite = frames[0];
		GameObject thrust = AssetDatabase.LoadAssetAtPath<GameObject>(ThrustFlashPath);
		SpriteRenderer thrustSr = thrust != null ? thrust.GetComponentInChildren<SpriteRenderer>() : null;
		if (thrustSr != null) sr.sharedMaterial = thrustSr.sharedMaterial;
		sr.sortingLayerName = layer;
		sr.sortingOrder = order;

		var loop = obj.AddComponent<SpriteFrameLoop>();
		var so = new SerializedObject(loop);
		SerializedProperty list = so.FindProperty("frames");
		list.arraySize = frames.Length;
		for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
		so.FindProperty("framesPerSecond").floatValue = fps;
		so.FindProperty("playOnceCount").intValue = 0;
		so.ApplyModifiedPropertiesWithoutUndo();

		if (kind == EffectKind.OneShot || kind == EffectKind.Beam)
		{
			// 한 바퀴가 끝나기 직전에 없앤다 (첫 장면으로 돌아가 깜빡이지 않게)
			var oneShot = obj.AddComponent<OneShotEffect>();
			var oneShotSo = new SerializedObject(oneShot);
			oneShotSo.FindProperty("lifetime").floatValue = Mathf.Max(0.05f, frames.Length / fps - 0.01f);
			oneShotSo.ApplyModifiedPropertiesWithoutUndo();
		}
		if (kind == EffectKind.Bolt) obj.AddComponent<MagicBoltVisual>();
		if (kind == EffectKind.Beam) obj.transform.localScale = new Vector3(1f / widthUnits, 1f, 1f);

		PrefabUtility.SaveAsPrefabAsset(obj, path);
		Object.DestroyImmediate(obj);
	}

	// 2. 캐릭터 모션 ------------------------------------------------------------

	static AnimatorController BuildMotions(out Sprite idleSprite)
	{
		idleSprite = null;
		var clips = new Dictionary<string, AnimationClip>();
		foreach (var m in Motions)
		{
			// C 캐릭터와 같은 설정: 64×64 칸, 24 PPU, 발밑 가운데가 기준점
			Sprite[] frames = SliceSheet(PythonDir + m.sheet + ".png", 64, 64, new Vector2(0.5f, 0f), 24);
			if (frames == null || frames.Length == 0) continue;
			if (m.state == "Idle") idleSprite = frames[0];
			clips[m.state] = MakeClip(m.sheet, frames, m.loop);
		}
		if (clips.Count == 0) return null;

		var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
		if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
		AnimatorStateMachine machine = controller.layers[0].stateMachine;
		foreach (var m in Motions)
		{
			if (!clips.TryGetValue(m.state, out AnimationClip clip)) continue;
			AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == m.state);
			// Transition은 만들지 않는다. 어떤 상태를 틀지는 PlayerAnimator가 정한다 (player-animation.md)
			if (state == null) state = machine.AddState(m.state);
			state.motion = clip;
			state.speed = 1f;
			state.writeDefaultValues = true;
			if (m.state == "Idle") machine.defaultState = state;
		}
		EditorUtility.SetDirty(controller);
		return controller;
	}

	// 12fps 클립을 만든다 (있으면 내용만 바꾼다)
	static AnimationClip MakeClip(string name, Sprite[] frames, bool loop)
	{
		var clip = new AnimationClip { name = name, frameRate = 12 };
		var keys = new ObjectReferenceKeyframe[frames.Length];
		for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = frames[i] };
		AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
		AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
		settings.loopTime = loop;
		AnimationUtility.SetAnimationClipSettings(clip, settings);

		string path = AnimationDir + name + ".anim";
		AnimationClip old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
		if (old != null)
		{
			EditorUtility.CopySerialized(clip, old);
			return old;
		}
		AssetDatabase.CreateAsset(clip, path);
		return clip;
	}

	// 3. 언어 규칙 --------------------------------------------------------------

	// LanguageRule_C를 복사해 Python 규칙으로 바꾼다. 이미 있으면 사람이 고친 값을 지키기 위해 건드리지 않는다
	static LanguageRuleData BuildLanguageRule()
	{
		var rule = AssetDatabase.LoadAssetAtPath<LanguageRuleData>(RulePath);
		if (rule != null) return rule;
		if (!AssetDatabase.CopyAsset(CRulePath, RulePath))
		{
			Debug.LogError($"[PythonWizardBuilder] {CRulePath}를 복사하지 못함");
			return null;
		}
		rule = AssetDatabase.LoadAssetAtPath<LanguageRuleData>(RulePath);
		var so = new SerializedObject(rule);
		// Python: 가비지 컬렉션(누수 없음), 문자열 == 비교는 내용으로, 0은 None이 아님, float %도 됨, int → float만 자동 변환
		so.FindProperty("garbageCollected").boolValue = true;
		so.FindProperty("stringEquality").enumValueIndex = (int)StringEquality.ByContent;
		so.FindProperty("zeroIsNull").boolValue = false;
		so.FindProperty("allowFloatModulo").boolValue = true;
		so.FindProperty("numericConversion").enumValueIndex = (int)NumericConversion.WideningOnly;
		// 동적 타입: 선언하지 않은 변수·타입 불일치는 실행해 봐야 드러나므로 runtime error로 분류한다
		SerializedProperty rules = so.FindProperty("rules");
		for (int i = 0; i < rules.arraySize; i++)
		{
			SerializedProperty row = rules.GetArrayElementAtIndex(i);
			var failure = (FunctionFailure)row.FindPropertyRelative("failure").enumValueIndex;
			if (failure == FunctionFailure.UndeclaredVariable || failure == FunctionFailure.TypeMismatch)
				row.FindPropertyRelative("category").enumValueIndex = (int)FailureCategory.RuntimeError;
		}
		so.ApplyModifiedPropertiesWithoutUndo();
		EditorUtility.SetDirty(rule);
		return rule;
	}

	// 4. 스킬 수치 --------------------------------------------------------------

	// SkillTable에 없는 py.* 줄만 더한다. 있는 줄은 Inspector에서 고친 값을 지키기 위해 건드리지 않는다
	static int AddSkillRows()
	{
		var table = AssetDatabase.LoadAssetAtPath<SkillTable>(SkillTablePath);
		if (table == null)
		{
			Debug.LogError($"[PythonWizardBuilder] {SkillTablePath}가 없음");
			return 0;
		}
		var so = new SerializedObject(table);
		SerializedProperty list = so.FindProperty("skills");
		var existing = new HashSet<string>();
		for (int i = 0; i < list.arraySize; i++) existing.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue);

		int added = 0;
		foreach (var s in Skills)
		{
			if (existing.Contains(s.id)) continue;
			// 한 칸 늘리면 마지막 줄을 복사한 칸이 생긴다. 아래에서 모든 값을 새로 넣는다
			list.arraySize++;
			SerializedProperty row = list.GetArrayElementAtIndex(list.arraySize - 1);
			row.FindPropertyRelative("id").stringValue = s.id;
			row.FindPropertyRelative("displayName").stringValue = s.name;
			row.FindPropertyRelative("family").enumValueIndex = (int)s.family;
			row.FindPropertyRelative("targeting").enumValueIndex = (int)s.targeting;
			row.FindPropertyRelative("damageMultiplier").floatValue = s.damage;
			row.FindPropertyRelative("tickCount").intValue = s.ticks;
			row.FindPropertyRelative("cooldownTicks").intValue = s.cooldown;
			row.FindPropertyRelative("localVariableCount").intValue = 0;
			row.FindPropertyRelative("heapSize").intValue = s.heap;
			row.FindPropertyRelative("areaRadius").floatValue = s.radius;
			row.FindPropertyRelative("needsSummonedSword").boolValue = false;
			row.FindPropertyRelative("usableUnarmed").boolValue = true;
			row.FindPropertyRelative("note").stringValue = s.note + " (수치 전부 임시)";
			added++;
		}
		if (added > 0)
		{
			so.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(table);
		}
		return added;
	}

	// 5. 플레이어 프리팹 ---------------------------------------------------------

	// Player 프리팹의 Variant로 파이썬 플레이어 프리팹을 만든다. 이미 있으면 빠진 컴포넌트와 연결만 채운다
	static bool BuildPlayerPrefab(AnimatorController controller, Sprite idleSprite, LanguageRuleData rule)
	{
		bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PythonPrefabPath) != null;
		GameObject root;
		if (exists)
		{
			root = PrefabUtility.LoadPrefabContents(PythonPrefabPath);
		}
		else
		{
			var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
			if (basePrefab == null)
			{
				Debug.LogError($"[PythonWizardBuilder] {PlayerPrefabPath}가 없음");
				return false;
			}
			root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
			root.name = "Player_Python";
		}

		// 마법사 컴포넌트 (없는 것만 붙인다)
		Ensure<ManaOrbs>(root);
		Ensure<ManaOrbView>(root);
		Ensure<OrbShield>(root);
		Ensure<OrbShieldView>(root);
		Ensure<MagicEffectView>(root);
		PlayerCharacter character = Ensure<PlayerCharacter>(root);
		var characterSo = new SerializedObject(character);
		characterSo.FindProperty("language").stringValue = CharacterLanguages.Python;
		characterSo.ApplyModifiedPropertiesWithoutUndo();

		// 그림·모션·언어 규칙을 마법사 것으로 바꾼다
		if (controller != null && root.TryGetComponent(out Animator animator)) animator.runtimeAnimatorController = controller;
		if (idleSprite != null && root.TryGetComponent(out SpriteRenderer renderer)) renderer.sprite = idleSprite;
		if (rule != null && root.TryGetComponent(out LanguageRule language))
		{
			var languageSo = new SerializedObject(language);
			languageSo.FindProperty("ruleData").objectReferenceValue = rule;
			languageSo.ApplyModifiedPropertiesWithoutUndo();
		}

		PrefabUtility.SaveAsPrefabAsset(root, PythonPrefabPath);
		if (exists) PrefabUtility.UnloadPrefabContents(root);
		else Object.DestroyImmediate(root);
		return true;
	}

	// 6. 등록 ----------------------------------------------------------------

	const string BaseScenePath = "Assets/Scenes/Base.unity";

	// Base 씬의 NetworkManager에 파이썬 플레이어 프리팹을 등록한다. 씬이 열려 있지 않으면 잠깐 열어서 고치고 닫는다
	static bool RegisterInNetworkManager()
	{
		var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PythonPrefabPath);
		if (prefab == null) return false;
		if (EditorApplication.isPlaying)
		{
			Debug.LogWarning("[PythonWizardBuilder] Play 중에는 NetworkManager에 등록하지 않음. Play를 끄고 다시 눌러 주세요.");
			return false;
		}

		Scene scene = SceneManager.GetSceneByPath(BaseScenePath);
		bool opened = false;
		if (!scene.isLoaded)
		{
			scene = EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Additive);
			opened = true;
		}

		InvokeNetworkManager manager = null;
		foreach (GameObject root in scene.GetRootGameObjects())
		{
			manager = root.GetComponentInChildren<InvokeNetworkManager>(true);
			if (manager != null) break;
		}
		if (manager == null)
		{
			Debug.LogError($"[PythonWizardBuilder] {BaseScenePath}에서 InvokeNetworkManager를 찾지 못함");
			if (opened) EditorSceneManager.CloseScene(scene, true);
			return false;
		}

		var so = new SerializedObject(manager);
		bool changed = false;

		SerializedProperty spawn = so.FindProperty("spawnPrefabs");
		bool inSpawn = false;
		for (int i = 0; i < spawn.arraySize; i++) inSpawn |= spawn.GetArrayElementAtIndex(i).objectReferenceValue == prefab;
		if (!inSpawn)
		{
			spawn.arraySize++;
			spawn.GetArrayElementAtIndex(spawn.arraySize - 1).objectReferenceValue = prefab;
			changed = true;
		}

		SerializedProperty characters = so.FindProperty("characterPrefabs");
		SerializedProperty entry = null;
		for (int i = 0; i < characters.arraySize; i++)
		{
			SerializedProperty e = characters.GetArrayElementAtIndex(i);
			if (e.FindPropertyRelative("language").stringValue == CharacterLanguages.Python) entry = e;
		}
		if (entry == null)
		{
			characters.arraySize++;
			entry = characters.GetArrayElementAtIndex(characters.arraySize - 1);
			entry.FindPropertyRelative("language").stringValue = CharacterLanguages.Python;
			changed = true;
		}
		SerializedProperty entryPrefab = entry.FindPropertyRelative("prefab");
		if (entryPrefab.objectReferenceValue != prefab)
		{
			entryPrefab.objectReferenceValue = prefab;
			changed = true;
		}

		if (changed)
		{
			so.ApplyModifiedPropertiesWithoutUndo();
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
		}
		if (opened) EditorSceneManager.CloseScene(scene, true);
		return true;
	}

	static T Ensure<T>(GameObject obj) where T : Component =>
		obj.TryGetComponent(out T found) ? found : obj.AddComponent<T>();
}
