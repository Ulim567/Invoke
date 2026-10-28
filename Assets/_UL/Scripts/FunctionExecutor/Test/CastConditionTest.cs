using UnityEngine;

/// <summary>
/// 시전 전 검사 테스트용 컴포넌트. 플레이를 시작하면 메모리가 모자란 함수와 충분한 함수를 하나씩 시전한다.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다. 다른 테스트가 메모리를 먼저 쓰면 결과가 달라지기 때문
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class CastConditionTest : MonoBehaviour
{
	void Start()
	{
		FunctionExecutor executor = GetComponent<FunctionExecutor>();

		// 메모리 부족: 프레임 21칸 > 남은 칸 10칸이므로 시전 거부 (NotEnoughMemory) 로그가 찍히고, 작업은 만들어지지 않아야 한다
		executor.Cast(new TestHeavyFunction());

		// 통과: 프레임 1칸이므로 시전되어 틱 30에 Completed로 끝나야 한다
		executor.Cast(new TestFunctionA());
	}
}
