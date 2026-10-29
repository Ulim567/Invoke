// 이동 계열 (특수 이동 함수) 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 효과(판정, 데미지, 상태 이상)는 각 스킬 이슈에서 채운다. 지금은 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>대시. 짧은 거리 빠르게 이동. 세 캐릭터 공통 (대상 없음)</summary>
public class CommonDash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.Dash;
}

/// <summary>더블점프. 공중에서 한 번 더 점프. 세 캐릭터 공통 (대상 없음)</summary>
public class CommonDoubleJump : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.DoubleJump;
}

/// <summary>검 타기. 소환검을 타고 이동, 쓸 수 있는 소환검이 없으면 불가 (대상 없음)</summary>
public class CSwordRide : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SwordRide;
}
