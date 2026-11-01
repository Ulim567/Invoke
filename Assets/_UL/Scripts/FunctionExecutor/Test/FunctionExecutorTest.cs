using System.Linq;
using UnityEngine;

/// <summary>
/// 실행기 테스트용 컴포넌트. 플레이를 시작하면 테스트 함수 두 개를 시전하고, 그 뒤 사용자 정의 함수 세 개를 하나씩 차례로 시전한다.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 진행 틱 수는 시작 로그의 틱부터 끝 로그의 틱까지(양 끝 포함) 센다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class FunctionExecutorTest : MonoBehaviour
{
	// 블록으로 다시 쓰는 함수. return에서 끝나 B는 실행되지 않는다
	readonly UserFunction aReturnB = new UserFunction("UserFunction_AReturnB",
		new TestFunctionA(),
		new ReturnBlock(),
		new TestFunctionB());

	FunctionExecutor executor;
	// 앞 단계에서 시전한 작업. 모두 끝나야 다음 단계를 시전한다
	// 같은 스킬은 동시에 실행되지 않고 기다리므로, 함께 시전하면 기다린 틱이 진행 틱에 섞이기 때문
	FunctionTask[] previous;
	int step;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();

		// 슬롯에서 바로 시전: A는 30틱, B는 50틱 진행해야 한다. 서로 다른 스킬이라 함께 진행한다
		previous = new[] { executor.Cast(new TestFunctionA()), executor.Cast(new TestFunctionB()) };
	}

	void FixedUpdate()
	{
		// 시전이 거부되어 작업이 null이면 끝난 것으로 본다
		if (previous == null || previous.Any(task => task != null && !task.IsEnded)) return;

		switch (step)
		{
			case 0:
				// 마지막 블록까지 실행: A(30틱) + B(50틱) = 80틱 진행해야 한다
				previous = new[] { executor.Cast(new UserFunction("UserFunction_AB",
					new TestFunctionA(),
					new TestFunctionB())) };
				break;

			case 1:
				// return에서 끝남: A(30틱) + return(1틱) = 31틱 진행해야 하고, B는 실행되지 않는다
				previous = new[] { executor.Cast(aReturnB) };
				break;

			case 2:
				// 사용자 정의 함수 안의 사용자 정의 함수: AReturnB(31틱) + B(50틱) = 81틱 진행해야 한다
				// 위에서 시전한 aReturnB를 블록으로 다시 써도 복사본이 실행되므로 진행 상황이 섞이지 않는다
				previous = new[] { executor.Cast(new UserFunction("UserFunction_Nested",
					aReturnB,
					new TestFunctionB())) };
				break;

			default:
				previous = null;
				return;
		}
		step++;
	}
}
