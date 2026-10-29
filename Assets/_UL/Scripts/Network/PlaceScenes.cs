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
	/// 레이드 던전은 파티마다 따로 불러오므로 여기에 넣지 않는다.
	/// </summary>
	public static readonly string[] FixedPlaces = { Lobby, HuntingGround, TrainingRoom };
}
