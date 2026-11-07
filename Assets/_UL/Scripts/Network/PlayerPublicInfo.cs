using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어의 현재 장소와 닉네임처럼, 모든 플레이어의 화면이 알아야 하는 값을 가진 컴포넌트. 플레이어 프리팹 루트에 붙인다.
/// <para>
/// - 값은 서버만 바꾼다(코드 작성 규칙 2). InvokeNetworkManager.PutPlayerInScene가 ServerSetPlace를, AccountServer가 ServerSetNickname을 부른다.<br/>
/// - 바뀐 값은 Mirror가 이 플레이어를 보는 모든 클라이언트에 보낸다(Sync Mode: Observers, 프리팹 Inspector). 소유 클라이언트에만 보내는 PlayerProgress와 다르다.<br/>
/// - 화면은 값을 읽기만 한다. 예: 내 캐릭터가 레이드 던전에 있는지는 NetworkClient.localPlayer의 PlayerPublicInfo.Place가 PlaceScenes.RaidDungeon인지로 본다.
/// </para>
/// <para>
/// Place가 필요한 이유: 클라이언트에서는 Mirror가 플레이어를 늘 기준 씬(Base)에 만들어서 player.scene.name으로는 장소를 알 수 없다.
/// </para>
/// </summary>
public class PlayerPublicInfo : NetworkBehaviour
{
	[SyncVar] string place = "";
	[SyncVar] string nickname = "";

	/// <summary>
	/// 이 플레이어가 지금 있는 장소의 씬 이름(PlaceScenes.Lobby 등). 아직 서버가 정하지 않았으면 빈 문자열이다.
	/// 연습장·레이드 던전은 씬이 여러 개 열려도 이름은 같다.
	/// </summary>
	public string Place => place;

	/// <summary>로그인한 계정의 닉네임. 아직 서버가 정하지 않았거나 계정 없이 만든 플레이어(시험용)는 빈 문자열이다.</summary>
	public string Nickname => nickname;

	/// <summary>[서버] 현재 장소를 정한다. PutPlayerInScene만 부른다.</summary>
	[Server]
	public void ServerSetPlace(string sceneName)
	{
		place = sceneName ?? "";
	}

	/// <summary>[서버] 닉네임을 정한다. 접속 중에는 바뀌지 않으므로 플레이어를 만들 때 한 번만 부른다.</summary>
	[Server]
	public void ServerSetNickname(string value)
	{
		nickname = value ?? "";
	}
}
