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
/// - 상점 창: 로비의 ShopPanel이 열림.<br/>
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
	bool shopWasOpen;
	ShopPanel shopPanel;
	float nextPanelSearch;
	// HUD와 함께 꺼질 때 열려 있던 창의 일. 다시 켜진 뒤 첫 Update에서 알린다
	readonly List<TutorialTask> pending = new List<TutorialTask>();

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
		// HUD를 숨기는 창(편집·장착 등)이 열리면 HudView가 HUD를 끄면서 이 오브젝트도 꺼진다.
		// HudView가 이번 프레임에 먼저 돌면 Update가 창이 열린 것을 못 본 채 꺼지므로, 꺼지는 이 때 열린 창을 적어 두었다가 다시 켜지면 알린다.
		// (enabled = false로 끄는 것은 튜토리얼을 끝낸 것이라 적지 않는다)
		if (!gameObject.activeInHierarchy) RememberOpenPanels();
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

		// 꺼져 있던 동안 열렸던 창 (OnDisable에서 적어 둠)
		for (int i = 0; i < pending.Count; i++) Signaled?.Invoke(pending[i]);
		pending.Clear();

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
			shopPanel = null;
			shopWasOpen = false;
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

		// 상점 창: 로비에서 열렸는지 본다
		if (place == PlaceScenes.Lobby && shopPanel == null && Time.unscaledTime >= nextPanelSearch)
		{
			nextPanelSearch = Time.unscaledTime + PanelSearchInterval;
			shopPanel = FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include);
		}
		bool shopOpen = place == PlaceScenes.Lobby && shopPanel != null && shopPanel.IsOpen;
		if (shopOpen != shopWasOpen)
		{
			shopWasOpen = shopOpen;
			if (shopOpen) Signaled?.Invoke(TutorialTask.OpenShop);
		}
	}

	// 지금 열려 있는 편집·장착·상점 창을 pending에 적는다. 아직 못 찾은 창은 바로 찾는다
	void RememberOpenPanels()
	{
		if (player == null) return;
		string place = PlayerPlace.Mine;
		if (place == PlaceScenes.TrainingRoom)
		{
			if (editorPanel == null) editorPanel = InPlace(FindObjectsByType<BlockEditorPanel>(FindObjectsInactive.Include), place);
			if (equipPanel == null) equipPanel = InPlace(FindObjectsByType<SlotEquipPanel>(FindObjectsInactive.Include), place);
			if (editorPanel != null && editorPanel.IsOpen) pending.Add(TutorialTask.OpenEditor);
			if (equipPanel != null && equipPanel.IsOpen) pending.Add(TutorialTask.OpenEquip);
		}
		else if (place == PlaceScenes.Lobby)
		{
			if (shopPanel == null) shopPanel = FindFirstObjectByType<ShopPanel>(FindObjectsInactive.Include);
			if (shopPanel != null && shopPanel.IsOpen) pending.Add(TutorialTask.OpenShop);
		}
	}

	// 내 장소에 있는 창 (호스트는 다른 장소 씬도 열려 있음)
	static T InPlace<T>(T[] list, string place) where T : Component
	{
		for (int i = 0; i < list.Length; i++)
			if (InMyPlace(list[i].gameObject, place)) return list[i];
		return null;
	}

	/// <summary>
	/// 그 오브젝트가 내 장소(place)에 있는지. TutorialGuide도 쓴다.<br/>
	/// - 호스트: 같은 이름의 장소 씬이 여럿 열려 있을 수 있다 (연습장은 플레이어마다 따로). 내 캐릭터가 있는 씬과 같은지 본다.<br/>
	/// - 클라이언트: 내 장소 씬만 열려 있다. 서버가 만든 네트워크 오브젝트(허수아비 등)는 장소 씬이 아닌 씬에 생기므로, NetworkIdentity가 있으면 내 장소의 것으로 친다 (다른 장소의 것은 받지 않음).
	/// </summary>
	internal static bool InMyPlace(GameObject go, string place)
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (NetworkServer.active && me != null && me.scene.name == place) return go.scene == me.scene;
		if (go.scene.name == place) return true;
		return !NetworkServer.active && go.TryGetComponent(out NetworkIdentity _);
	}

	void Attach(GameObject me)
	{
		player = me;
		lastPlace = "";
		editorWasOpen = false;
		equipWasOpen = false;
		editorPanel = null;
		equipPanel = null;
		shopPanel = null;
		shopWasOpen = false;
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

	// 세 번째 값은 받은 블록·함수의 항목 ID 목록 (#589에서 블록 수 int에서 바뀜). 튜토리얼은 받았다는 것만 본다
	void OnReward(int exp, IReadOnlyList<MonsterReward.MaterialDrop> drops, IReadOnlyList<string> itemIds) => Signaled?.Invoke(TutorialTask.GetReward);
	void OnSkillTree() => Signaled?.Invoke(TutorialTask.OpenSkillTree);
	void OnLevelUp() => Signaled?.Invoke(TutorialTask.OpenLevelUp);
}
