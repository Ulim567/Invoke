// 소환 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 효과(판정, 데미지, 상태 이상)는 각 스킬 이슈에서 채운다. 지금은 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>검 소환 (malloc). 최대 5개, 검마다 포인터 생성 (대상 없음)</summary>
public class CSummonSword : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonSword;
}

/// <summary>해제 (free). 소환검만 해제 가능, 거리 상관없음 (대상 없음)</summary>
public class CFreeSword : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.FreeSword;
}

/// <summary>패링. 성공 시 적 공격을 검 이펙트로 1회 되돌려줌 (대상 없음)</summary>
public class CParry : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.Parry;
}

/// <summary>표식 남기기. 검이 적 좌우로 찔러 표식 + 1.0초 기절 (단일)</summary>
public class CMark : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.Mark;
}

/// <summary>검무. 검 개수만큼 적 근처에 두고 휘두름, 데미지는 낮지만 출혈 부여 (범위)</summary>
public class CSwordDance : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SwordDance;
}
