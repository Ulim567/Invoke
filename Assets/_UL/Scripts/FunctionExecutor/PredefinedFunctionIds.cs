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
	// C 스킬
	public const string Thrust = "c.thrust";

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
}
