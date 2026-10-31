// 검 관련 스킬 에러. 박진우의 C 스킬이 쓴다
// 스킬 구현자가 자기 폴더로 옮기거나 새 에러를 추가해도 실행기 코드는 고치지 않는다

/// <summary>
/// 맨손이라서 공격 스킬을 쓸 수 없다.
/// </summary>
public class UnarmedError : SkillError
{
	public override string Message => "맨손";
}

/// <summary>
/// 쓸 수 있는 소환검이 없어서 소환검이 필요한 스킬을 쓸 수 없다.
/// </summary>
public class NoSummonedSwordError : SkillError
{
	public override string Message => "소환검 없음";
}
