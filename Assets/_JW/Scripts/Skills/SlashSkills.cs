// 휘두르기 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 효과(판정, 데미지, 상태 이상)는 각 스킬 이슈에서 채운다. 지금은 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>검 휘두르기. 검기를 날림 (단일)</summary>
public class CSwordSlash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SwordSlash;
}

/// <summary>검 크게베기. 광역 검기, 데미지는 낮지만 출혈 부여 (범위)</summary>
public class CWideSlash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.WideSlash;
}

/// <summary>소환검 휘두르기. 소환된 검 개수만큼 검기 발사 (단일)</summary>
public class CSummonedSlash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonedSlash;
}

/// <summary>상처 가르기. 출혈 상태의 적에게 출혈 도트 데미지 2.0배 (단일)</summary>
public class CWoundSlash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.WoundSlash;
}

/// <summary>검 다중베기. 소환된 검들이 각각 한 번씩 대상을 벰 (단일 + 다단 히트)</summary>
public class CMultiSlash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.MultiSlash;
}

/// <summary>전장 휩쓸기. 소환된 검들이 여러 적에게 각각 가서 벰 (여러 명 선택)</summary>
public class CBattlefieldSweep : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.BattlefieldSweep;
}
