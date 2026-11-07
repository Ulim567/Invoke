using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 장소 씬 이름 모음. 씬 이름을 문자열로 직접 쓰지 않고 이 상수를 쓴다.
/// (씬 이름을 바꿀 때 고칠 곳을 하나로 만들기 위함)
/// <para>
/// 씬 파일은 Assets/Scenes에 있고, Build Profiles의 Scene List에 등록되어 있어야 불러올 수 있다.
/// </para>
/// </summary>
public static class PlaceScenes
{
	public const string Lobby = "Lobby";
	public const string HuntingGround = "HuntingGround";
	public const string TrainingRoom = "TrainingRoom";
	public const string RaidDungeon = "RaidDungeon";

	/// <summary>
	/// 서버가 시작할 때 한 번씩 불러오는 장소.
	/// 연습장과 레이드 던전은 입장할 때마다 따로 불러오므로 여기에 넣지 않는다.
	/// </summary>
	public static readonly string[] FixedPlaces = { Lobby, HuntingGround };

	/// <summary>
	/// 공격할 수 없는 장소(스킬을 시전할 수 없는 장소). 서버가 이 장소에서 온 시전 요청을 거부한다 (CastRejection.InNoCastPlace).
	/// 장소를 더하거나 빼려면 여기만 고친다.
	/// </summary>
	public static readonly string[] NoCastPlaces = { Lobby };

	/// <summary>
	/// 씬 이름이 공격할 수 없는 장소(NoCastPlaces)이면 true.
	/// </summary>
	public static bool IsNoCastPlace(string sceneName)
	{
		return System.Array.IndexOf(NoCastPlaces, sceneName) >= 0;
	}

	/// <summary>
	/// 이 플레이어가 지금 공격할 수 없는 장소에 있으면 true. 서버·호스트·클라이언트 어디에서 불러도 된다.
	/// <para>
	/// - 서버(호스트 포함)와 네트워크를 켜지 않은 테스트: 플레이어 오브젝트가 속한 씬의 이름으로 판단한다.<br/>
	/// - 클라이언트: Mirror가 플레이어를 기준 씬(Base)에 만들어 플레이어의 씬 이름으로는 장소를 알 수 없다. 서버가 알려 준 장소 씬만 불러오므로, 불러온 씬 중에 공격할 수 없는 장소가 있는지로 판단한다.
	/// </para>
	/// </summary>
	public static bool IsPlayerInNoCastPlace(GameObject player)
	{
		if (NetworkServer.active || !NetworkClient.active) return IsNoCastPlace(player.scene.name);

		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene scene = SceneManager.GetSceneAt(i);
			if (scene.isLoaded && IsNoCastPlace(scene.name)) return true;
		}
		return false;
	}
}
