// 던지기 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 효과(판정, 데미지, 상태 이상)는 각 스킬 이슈에서 채운다. 지금은 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>검 던지기. 물리 검을 던짐, 체력 2% 이하 적 처형 (보스 제외) (단일)</summary>
public class CSwordThrow : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SwordThrow;
}

/// <summary>소환검 던지기. 더 먼 적까지 명중 (단일)</summary>
public class CSummonedThrow : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonedThrow;
}

/// <summary>검 돌아오기. 던진 검을 던진 순서대로 하나씩 회수, 경로의 적에게 데미지 (경로)</summary>
public class CSwordReturn : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SwordReturn;
}
