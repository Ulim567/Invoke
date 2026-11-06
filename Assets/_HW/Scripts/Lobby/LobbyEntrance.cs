using System;
using UnityEngine;

/// <summary>
/// 로비의 입구 (사냥터, 연습장, 레이드 던전). 가까이 가서 ↑를 누르면 사냥터·연습장은 입구 창을 열고, 「들어가기」를 누르면 이동 요청을 알린다.
/// <para>
/// - 이동 자체, 서버에 요청 보내기, 파티 모으기는 하지 않는다. 이우림(「장소 사이 이동하기」, 레이드 파티)이 아래 이벤트를 받아서 한다.<br/>
/// - 사냥터·연습장: <see cref="TravelRequested"/>에 갈 씬 이름(PlaceScenes)을 넘긴다.<br/>
/// - 레이드 던전: 입구 창 없이 ↑에 <see cref="RaidEntryRequested"/>로 알린다. 이우림의 RaidLobbyOpener가 받아 레이드 방 화면(RaidLobbyUI)을 연다 (#242).
/// </para>
/// </summary>
public class LobbyEntrance : PlaceInteractable
{
	/// <summary>입구가 어디로 가는지</summary>
	public enum Destination { HuntingGround, TrainingRoom, RaidDungeon }

	/// <summary>
	/// 사냥터·연습장 입구에서 「들어가기」를 눌렀을 때. 갈 씬 이름(PlaceScenes.HuntingGround 등)을 넘긴다.
	/// 내 화면(내 캐릭터)에서만 불린다.
	/// </summary>
	public static event Action<string> TravelRequested;

	/// <summary>
	/// 레이드 던전 입구에서 ↑를 눌렀을 때 (입구 창 없음). ↑를 누른 내 캐릭터를 넘긴다.
	/// 레이드 방 화면이 열린 동안 그 캐릭터의 액션 맵을 비활성화하기 위함이다. 내 화면(내 캐릭터)에서만 불린다.
	/// </summary>
	public static event Action<PlayerInputReader> RaidEntryRequested;

	[SerializeField] Destination destination;

	[SerializeField, Tooltip("창 제목. 비워 두면 기본 이름 (사냥터, 연습장, 레이드 던전)")]
	string displayName;

	[SerializeField, Tooltip("Lobby 씬에 하나 놓은 입구 창")]
	LobbyEntrancePanel panel;

	/// <summary>창 제목에 쓰는 이름</summary>
	public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName : DefaultName(destination);

	/// <summary>창의 설명 글</summary>
	public string Description => $"{DisplayName}(으)로 이동합니다.";

	protected override void Interact(PlayerInputReader player)
	{
		// 레이드 방 화면(RaidLobbyUI)이 입구 창 역할을 하므로 입구 창을 열지 않는다
		if (destination == Destination.RaidDungeon)
		{
			Debug.Log("[LobbyEntrance] 레이드 던전으로 가려고 함");
			RaidEntryRequested?.Invoke(player);
			return;
		}

		if (panel == null)
		{
			Debug.LogError($"[LobbyEntrance] {name}: Panel이 비어 있음", this);
			return;
		}
		panel.Open(this, player);
	}

	/// <summary>
	/// 입구 창의 「들어가기」에서 부른다. 이동을 요청한다고 알린다. 레이드 입구는 입구 창을 열지 않으므로 불리지 않는다.
	/// </summary>
	public void RequestEnter()
	{
		string scene = SceneName(destination);
		Debug.Log($"[LobbyEntrance] {scene}(으)로 이동 요청");
		TravelRequested?.Invoke(scene);
	}

	static string SceneName(Destination d) => d switch
	{
		Destination.HuntingGround => PlaceScenes.HuntingGround,
		Destination.TrainingRoom => PlaceScenes.TrainingRoom,
		_ => PlaceScenes.RaidDungeon,
	};

	static string DefaultName(Destination d) => d switch
	{
		Destination.HuntingGround => "사냥터",
		Destination.TrainingRoom => "연습장",
		_ => "레이드 던전",
	};
}
