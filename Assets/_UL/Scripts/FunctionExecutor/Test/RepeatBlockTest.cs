using UnityEngine;

/// <summary>
/// 반복문이 스택을 차지하지 않는지 확인하는 테스트 컴포넌트. 플레이를 시작하면 반복 블록이 든 사용자 정의 함수 하나를 시전한다.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.
/// FunctionExecutorTest와 함께 켜면 스택이 모자라 결과가 섞이므로, 한쪽만 켠다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class RepeatBlockTest : MonoBehaviour
{
	void Start()
	{
		FunctionExecutor executor = GetComponent<FunctionExecutor>();

		// 반복 횟수 12는 메모리 크기 10보다 크다. 반복할 때마다 스택이 쌓인다면 도중에 오버플로가 나야 한다
		// 매 반복 스택 2(UserFunction 1 + TestFunctionA 1)가 찍히고, A(30틱) × 12회 = 틱 360에 Completed로 끝나야 한다
		executor.Cast(new UserFunction("Loop",
			new TestRepeatBlock(12, new TestFunctionA())));
	}
}
