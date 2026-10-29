// 찌르기 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 효과(판정, 데미지, 상태 이상)는 각 스킬 이슈에서 채운다. 지금은 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>검 찌르기. 기본 찌르기 (단일)</summary>
public class CThrust : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.Thrust;
}

/// <summary>검 집중찌르기. 출혈 상태의 적에게 데미지 1.5배 (단일)</summary>
public class CFocusedThrust : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.FocusedThrust;
}

/// <summary>급소공략. 미니게임 성공 시 3.0배, 눈은 실명·발목은 이속 감소 (단일)</summary>
public class CVitalStrike : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.VitalStrike;
}

/// <summary>꼬챙이. 여러 명 동시에 찌름, 데미지 약함 (관통)</summary>
public class CSkewer : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.Skewer;
}

/// <summary>소환검 찌르기. 소환검에게 명령해 검이 찌름 (단일)</summary>
public class CSummonedThrust : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonedThrust;
}

/// <summary>상처 공략. 출혈 상태의 적에게 데미지 2.0배 (단일)</summary>
public class CWoundExploit : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.WoundExploit;
}

/// <summary>검 깊이찌르기. 치명타 2.5배 (단일)</summary>
public class CDeepThrust : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.DeepThrust;
}
