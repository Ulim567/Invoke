using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 내 캐릭터가 튜토리얼에서 해 볼 일을 했는지 지켜보고 Signaled로 알린다. 값은 읽기만 하고 바꾸지 않는다.
/// <para>
/// - 이동·점프·달리기: PlayerInputReader (키를 직접 읽지 않는다, 코드 규칙 5).<br/>
/// - 스킬 쓰기: FunctionExecutorNetwork.CastAccepted (서버가 받아 준 시전만, 슬롯 0 = Q).<br/>
/// - 장소: PlayerPlace.Mine (연습장·사냥터에 들어옴, 로비에 있음).<br/>
/// - 편집 창·장착 창: 내 장소 씬의 BlockEditorPanel·SlotEquipPanel이 열렸다가 닫힘(InputBlockingPanel.IsOpen). 두 창을 따로 센다.<br/>
/// - 몬스터 잡기: MonsterReward.LocalRewardReceived (내가 보상을 받았을 때). 1·2번 키: HudView.SkillTreeRequested, LevelUpRequested.
/// </para>
/// </summary>
public class TutorialTracker : MonoBehaviour
{
	const int QSlot = 0;
	// 장소 씬의 창은 장소에 들어온 뒤 열리므로, 못 찾았으면 이 간격(초)마다 다시 찾는다
	const float PanelSearchInterval = 1f;

	/// <summary>일을 했을 때. 이미 한 일도 다시 불릴 수 있다 (받는 쪽이 걸러낸다)</summary>
	public event Action<TutorialTask> Signaled;

	GameObject player;
	PlayerInputReader input;
	FunctionExecutorNetwork executor;
	string lastPlace = "";
	bool editorWasOpen;
	bool equipWasOpen;
	BlockEditorPanel editorPanel;
	SlotEquipPanel equipPanel;
	float nextPanelSearch;

	void OnEnable()
	{
		MonsterReward.LocalRewardReceived += OnReward;
		HudView.SkillTreeRequested += OnSkillTree;
		HudView.LevelUpRequested += OnLevelUp;
	}

	void OnDisable()
	{
		MonsterReward.LocalRewardReceived -= OnReward;
		HudView.SkillTreeRequested -= OnSkillTree;
		HudView.LevelUpRequested -= OnLevelUp;
		Detach();
	}

	void Update()
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (me != player)
		{
			Detach();
			Attach(me);
		}
		if (player == null) return;

		if (input != null)
		{
			bool moving = Mathf.Abs(input.MoveX) > 0.1f;
			if (moving) Signaled?.Invoke(TutorialTask.Move);
			if (moving && input.RunHeld) Signaled?.Invoke(TutorialTask.Run);
			if (input.JumpPressed) Signaled?.Invoke(TutorialTask.Jump);
		}

		string place = PlayerPlace.Mine;
		if (place != lastPlace)
		{
			lastPlace = place;
			editorPanel = null;
			equipPanel = null;
			nextPanelSearch = 0f;
			if (place == PlaceScenes.TrainingRoom) Signaled?.Invoke(TutorialTask.EnterTraining);
			else if (place == PlaceScenes.HuntingGround) Signaled?.Invoke(TutorialTask.EnterHunting);
		}
		// 로비에 있는 동안 계속 알린다 (받는 쪽이 그 쪽에 와 있을 때만 센다)
		if (place == PlaceScenes.Lobby) Signaled?.Invoke(TutorialTask.EnterLobby);

		// 편집 창·장착 창: 연습장에서 어느 창이 열렸는지 따로 본다
		if (place == PlaceScenes.TrainingRoom && (editorPanel == null || equipPanel == null) && Time.unscaledTime >= nextPanelSearch)
		{
			nextPanelSearch = Time.unscaledTime + PanelSearchInterval;
			if (editorPanel == null) editorPanel = InPlace(FindObjectsByType<BlockEditorPanel>(FindObjectsInactive.Include), place);
			if (equipPanel == null) equipPanel = InPlace(FindObjectsByType<SlotEquipPanel>(FindObjectsInactive.Include), place);
		}
		bool editorOpen = place == PlaceScenes.TrainingRoom && editorPanel != null && editorPanel.IsOpen;
		if (editorOpen != editorWasOpen)
		{
			editorWasOpen = editorOpen;
			Signaled?.Invoke(editorOpen ? TutorialTask.OpenEditor : TutorialTask.CloseEditor);
		}
		bool equipOpen = place == PlaceScenes.TrainingRoom && equipPanel != null && equipPanel.IsOpen;
		if (equipOpen != equipWasOpen)
		{
			equipWasOpen = equipOpen;
			if (equipOpen) Signaled?.Invoke(TutorialTask.OpenEquip);
		}
	}

	// 내 장소와 같은 이름의 씬에 있는 창 (호스트는 다른 장소 씬도 열려 있음)
	static T InPlace<T>(T[] list, string place) where T : Component
	{
		for (int i = 0; i < list.Length; i++)
			if (list[i].gameObject.scene.name == place) return list[i];
		return null;
	}

	void Attach(GameObject me)
	{
		player = me;
		lastPlace = "";
		editorWasOpen = false;
		equipWasOpen = false;
		editorPanel = null;
		equipPanel = null;
		if (me == null) return;
		input = me.GetComponent<PlayerInputReader>();
		executor = me.GetComponent<FunctionExecutorNetwork>();
		if (executor != null) executor.CastAccepted += OnCast;
	}

	void Detach()
	{
		if (executor != null) executor.CastAccepted -= OnCast;
		player = null;
		input = null;
		executor = null;
	}

	void OnCast(int slot, int task)
	{
		if (slot == QSlot) Signaled?.Invoke(TutorialTask.CastQ);
	}

	void OnReward(int exp, IReadOnlyList<MonsterReward.MaterialDrop> drops, int items) => Signaled?.Invoke(TutorialTask.GetReward);
	void OnSkillTree() => Signaled?.Invoke(TutorialTask.OpenSkillTree);
	void OnLevelUp() => Signaled?.Invoke(TutorialTask.OpenLevelUp);
}
