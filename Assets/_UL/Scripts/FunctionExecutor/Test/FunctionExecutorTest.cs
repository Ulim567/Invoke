using UnityEngine;

/// <summary>
/// 실행기 테스트용 컴포넌트. 플레이를 시작하면 테스트 함수 두 개와 사용자 정의 함수 세 개를 연달아 시전한다.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class FunctionExecutorTest : MonoBehaviour
{
	void Start()
	{
		FunctionExecutor executor = GetComponent<FunctionExecutor>();
		executor.Cast(new TestFunctionA());
		executor.Cast(new TestFunctionB());

		// 마지막 블록까지 실행: A(30틱) + B(50틱) = 틱 80에 끝나야 한다
		executor.Cast(new UserFunction("UserFunction_AB",
			new TestFunctionA(),
			new TestFunctionB()));

		// return에서 끝남: A(30틱) + return(1틱) = 틱 31에 끝나야 하고, B는 실행되지 않는다
		UserFunction aReturnB = new UserFunction("UserFunction_AReturnB",
			new TestFunctionA(),
			new ReturnBlock(),
			new TestFunctionB());
		executor.Cast(aReturnB);

		// 사용자 정의 함수 안의 사용자 정의 함수: AReturnB(31틱) + B(50틱) = 틱 81에 끝나야 한다
		// 위에서 시전한 aReturnB를 블록으로 다시 써도 복사본이 실행되므로 진행 상황이 섞이지 않는다
		executor.Cast(new UserFunction("UserFunction_Nested",
			aReturnB,
			new TestFunctionB()));
	}
}
