/// <summary>
/// 레벨 규칙 (경험치 표, 최대 레벨). 서버와 클라이언트가 함께 읽는다. 숫자는 모두 임시다.
/// <para>
/// - 다음 레벨까지 필요한 경험치 = 100 × 지금 레벨 (1→2: 100, 2→3: 200 …)<br/>
/// - 최대 레벨 30. 최대 레벨에서는 경험치가 쌓이지 않는다.<br/>
/// - 레벨을 실제로 올리는 일은 PlayerProgress.AddExp(이우림)가 이 규칙으로 한다. 레벨업 능력치는 PlayerLevelStats가 준다.
/// </para>
/// </summary>
public static class LevelRules
{
	/// <summary>최대 레벨</summary>
	public const int MaxLevel = 30;

	// 레벨 1에서 다음 레벨까지 필요한 경험치. 레벨마다 이만큼씩 늘어난다
	const int ExpPerLevel = 100;

	/// <summary>
	/// level에서 다음 레벨로 가는 데 필요한 경험치. 최대 레벨이면 0.
	/// </summary>
	public static int ExpToNext(int level) => level >= MaxLevel ? 0 : ExpPerLevel * level;
}
