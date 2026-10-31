/// <summary>
/// 모든 사전 정의 함수의 고정 ID. 스킬의 Id는 글자를 직접 적지 않고 여기의 상수를 쓴다.
/// <para>
/// 규칙 (docs/systems/predefined-function.md 「고정 ID」)<br/>
/// - 형식: 분류.영어_이름 (소문자, 밑줄). 분류는 c, common, input, test<br/>
/// - 한 번 정한 ID는 바꾸거나 지우지 않는다. 예전에 저장한 함수 데이터가 그 함수를 찾지 못하게 되기 때문이다.
/// </para>
/// <para>
/// ID를 한 파일에 모으는 이유: 모든 ID를 한눈에 보고, ID를 바꾸거나 지운 것을 PR에서 이 파일의 변경으로 확인하기 위함
/// </para>
/// </summary>
public static class PredefinedFunctionIds
{
	// C 스킬 (박진우, #67). 수치는 Assets/_JW/Resources/SkillTable.asset
	// 찌르기
	public const string Thrust = "c.thrust";
	public const string FocusedThrust = "c.focused_thrust";
	public const string VitalStrike = "c.vital_strike";
	public const string Skewer = "c.skewer";
	public const string SummonedThrust = "c.summoned_thrust";
	public const string WoundExploit = "c.wound_exploit";
	public const string DeepThrust = "c.deep_thrust";
	// 던지기
	public const string SwordThrow = "c.sword_throw";
	public const string SummonedThrow = "c.summoned_throw";
	public const string SwordReturn = "c.sword_return";
	// 휘두르기
	public const string SwordSlash = "c.sword_slash";
	public const string WideSlash = "c.wide_slash";
	public const string SummonedSlash = "c.summoned_slash";
	public const string WoundSlash = "c.wound_slash";
	public const string MultiSlash = "c.multi_slash";
	public const string BattlefieldSweep = "c.battlefield_sweep";
	// 소환
	public const string SummonSword = "c.summon_sword";
	public const string FreeSword = "c.free_sword";
	public const string Parry = "c.parry";
	public const string Mark = "c.mark";
	public const string SwordDance = "c.sword_dance";
	// 이동
	public const string SwordRide = "c.sword_ride";

	// 세 캐릭터 공통 (특수 이동 함수)
	public const string Dash = "common.dash";
	public const string DoubleJump = "common.double_jump";

	// 테스트용 함수
	public const string TestFunctionA = "test.function_a";
	public const string TestFunctionB = "test.function_b";
	public const string TestAsyncFunction = "test.async_function";
	public const string TestHeavyFunction = "test.heavy_function";
	public const string TestAttackFunction = "test.attack_function";
	public const string TestSummonedSwordFunction = "test.summoned_sword_function";
	public const string TestStopFunction = "test.stop_function";
	public const string TestCooldownFunction = "test.cooldown_function";
	public const string TestMallocFunction = "test.malloc_function";
	public const string TestLongMallocFunction = "test.long_malloc_function";
	public const string TestFreeFunction = "test.free_function";
	public const string TestArgumentFunction = "test.argument_function";
	public const string TestReturnFunction = "test.return_function";
	public const string TestMissingReturnFunction = "test.missing_return_function";
}
