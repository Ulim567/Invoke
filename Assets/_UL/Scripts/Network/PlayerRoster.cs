using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 이 화면(이 컴퓨터)에 있는 플레이어 목록과, 플레이어가 나타나거나 사라질 때의 이벤트 (#310). 파티원 체력 UI처럼 「지금 누가 있는지」가 필요한 화면이 쓴다.
/// <para>
/// - 클라이언트에서는 Mirror가 같은 장소의 플레이어만 만들어 주므로, 이 목록이 곧 같은 장소의 플레이어다. 레이드 던전은 파티마다 따로 열리므로 던전 안에서는 파티원이다.<br/>
/// - 장소를 옮기거나 다른 플레이어가 들어오고 나가면 Mirror가 플레이어 오브젝트를 만들고 없애므로, 그때 PlayerAdded·PlayerRemoved가 불린다.<br/>
/// - 호스트는 서버이기도 해서 다른 장소의 플레이어도 갖고 있다. 호스트 화면에서 같은 장소만 보려면 IsInMyPlace로 거른다 (장소를 옮겨도 이벤트는 불리지 않으므로, 호스트 화면의 UI는 그릴 때마다 거른다).<br/>
/// - 목록을 채우는 일은 PlayerHealth가 한다 (Start에 넣고 OnDestroy에 뺀다). 다른 스크립트는 읽기만 한다.
/// </para>
/// <para>
/// 사용법: Players를 한 번 읽어 그리고, PlayerAdded·PlayerRemoved를 받아 고친다. 각 플레이어의 체력은 PlayerHealth.HpChanged로 받는다.
/// 플레이어가 나타난 순간에는 체력 값이 이미 서버의 값으로 맞춰져 있다.
/// </para>
/// </summary>
public static class PlayerRoster
{
	static readonly List<PlayerHealth> players = new List<PlayerHealth>(8);

	/// <summary>이 화면에 있는 플레이어 (내 캐릭터 포함). 읽기만 한다.</summary>
	public static IReadOnlyList<PlayerHealth> Players => players;

	/// <summary>[표시] 플레이어가 이 화면에 나타났다 (장소에 들어옴, 접속함, 내가 그 장소로 옴).</summary>
	public static event Action<PlayerHealth> PlayerAdded;

	/// <summary>[표시] 플레이어가 이 화면에서 사라졌다 (장소를 떠남, 접속이 끊김, 내가 그 장소를 떠남).</summary>
	public static event Action<PlayerHealth> PlayerRemoved;

	// 에디터에서 Play를 다시 눌러도 이전 Play의 목록과 구독이 남지 않게 한다
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics()
	{
		players.Clear();
		PlayerAdded = null;
		PlayerRemoved = null;
	}

	/// <summary>내 캐릭터면 true. 「나를 뺀 파티원」만 그릴 때 쓴다. 네트워크를 켜지 않은 테스트에서는 늘 false.</summary>
	public static bool IsLocal(PlayerHealth player) => player != null && player.isLocalPlayer;

	/// <summary>
	/// 내 캐릭터와 같은 장소에 있으면 true. 클라이언트에서는 목록의 플레이어가 모두 같은 장소이므로 늘 true다.
	/// 호스트에서는 다른 장소의 플레이어도 목록에 있으므로 씬으로 비교한다 (호스트의 플레이어 오브젝트는 서버의 장소 씬에 있다).
	/// </summary>
	public static bool IsInMyPlace(PlayerHealth player)
	{
		if (player == null) return false;
		NetworkIdentity me = NetworkClient.localPlayer;
		if (me == null || !NetworkServer.active) return true;
		return player.gameObject.scene == me.gameObject.scene;
	}

	/// <summary>[PlayerHealth만 부른다] 플레이어를 목록에 넣는다.</summary>
	public static void Register(PlayerHealth player)
	{
		if (player == null || players.Contains(player)) return;
		players.Add(player);
		PlayerAdded?.Invoke(player);
	}

	/// <summary>[PlayerHealth만 부른다] 플레이어를 목록에서 뺀다.</summary>
	public static void Unregister(PlayerHealth player)
	{
		if (!players.Remove(player)) return;
		PlayerRemoved?.Invoke(player);
	}
}
