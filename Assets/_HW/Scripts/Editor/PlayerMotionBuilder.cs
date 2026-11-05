using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// C 캐릭터의 스킬 계열별 모션(Slash, Throw, Summon, Lunge)을 Animator Controller에 붙이는 에디터 도구. 게임에는 들어가지 않는다.
/// <para>
/// 메뉴 Invoke → 플레이어 → 스킬 모션 붙이기. 여러 번 눌러도 된다.<br/>
/// - C_Slash, C_Throw, C_Summon 그림으로 12fps 클립(.anim)을 만든다 (있으면 다시 만든다). Lunge는 있는 C_Lunge 클립을 쓴다.<br/>
/// - C_Player.controller에 같은 이름의 상태를 더한다 (Transition 없음, PlayerAnimator가 고른다. player-animation.md).<br/>
/// - 스킬마다 상태(예: c_wide_slash)를 하나씩 더하고, SkillMotionTable의 임시 모션 클립을 넣는다 (#288).
///   이미 Motion이 들어 있는 스킬 상태는 건드리지 않는다. 사람이 전용 클립으로 바꿔 둔 것을 지키기 위해서다.
/// </para>
/// </summary>
public static class PlayerMotionBuilder
{
	const string Dir = "Assets/_HW/Player/";
	const string ControllerPath = Dir + "Animations/C_Player.controller";

	[MenuItem("Invoke/플레이어/스킬 모션 붙이기")]
	public static void Build()
	{
		var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
		if (controller == null)
		{
			Debug.LogError($"[PlayerMotionBuilder] {ControllerPath}를 찾지 못함");
			return;
		}

		AddState(controller, "Slash", MakeClip("C_Slash"));
		AddState(controller, "Throw", MakeClip("C_Throw"));
		AddState(controller, "Summon", MakeClip("C_Summon"));
		AddState(controller, "Lunge", AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "Animations/C_Lunge.anim"));

		int added = AddSkillStates(controller);

		EditorUtility.SetDirty(controller);
		AssetDatabase.SaveAssets();
		Debug.Log($"[PlayerMotionBuilder] Slash, Throw, Summon, Lunge 모션과 스킬 상태 {added}개를 C_Player에 붙임");
	}

	// 스킬 상태를 오른쪽에 4줄씩 늘어놓는다. 임시 모션 상태의 클립과 Speed를 그대로 쓴다
	static int AddSkillStates(AnimatorController controller)
	{
		AnimatorStateMachine machine = controller.layers[0].stateMachine;
		int added = 0, index = 0;
		foreach (SkillMotionTable.Entry entry in SkillMotionTable.All)
		{
			if (entry.Base == null) continue;
			AnimatorState baseState = Find(machine, entry.Base);
			if (baseState == null || baseState.motion == null)
			{
				Debug.LogWarning($"[PlayerMotionBuilder] {entry.Name}: 임시 모션 상태 {entry.Base}가 없어 건너뜀");
				continue;
			}

			AnimatorState state = Find(machine, entry.StateName);
			if (state == null)
			{
				var position = new Vector3(700 + (index / 6) * 230, -60 + (index % 6) * 60, 0);
				state = machine.AddState(entry.StateName, position);
				state.writeDefaultValues = true;
				added++;
			}
			if (state.motion == null)
			{
				state.motion = baseState.motion;
				state.speed = baseState.speed;
			}
			index++;
		}
		return added;
	}

	static AnimatorState Find(AnimatorStateMachine machine, string name) =>
		machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

	// 그림 한 장(여러 장면)으로 12fps, 반복 없는 클립을 만든다
	static AnimationClip MakeClip(string sheet)
	{
		Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Dir + sheet + ".png").OfType<Sprite>()
			.OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
		if (frames.Length == 0)
		{
			Debug.LogError($"[PlayerMotionBuilder] {sheet}.png의 장면을 찾지 못함");
			return null;
		}

		var clip = new AnimationClip { frameRate = 12 };
		var keys = new ObjectReferenceKeyframe[frames.Length];
		for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = frames[i] };
		AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
		AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
		settings.loopTime = false;
		AnimationUtility.SetAnimationClipSettings(clip, settings);

		string path = Dir + "Animations/" + sheet + ".anim";
		AnimationClip old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
		if (old != null)
		{
			EditorUtility.CopySerialized(clip, old);
			return old;
		}
		AssetDatabase.CreateAsset(clip, path);
		return clip;
	}

	static void AddState(AnimatorController controller, string name, Motion motion)
	{
		if (motion == null) return;
		AnimatorStateMachine machine = controller.layers[0].stateMachine;
		AnimatorState state = Find(machine, name);
		if (state == null) state = machine.AddState(name);
		state.motion = motion;
		state.speed = 1f;
		state.writeDefaultValues = true;
	}
}
