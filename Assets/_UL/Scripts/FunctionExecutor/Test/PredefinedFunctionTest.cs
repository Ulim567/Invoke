using UnityEngine;

/// <summary>
/// 사전 정의 함수 규칙 테스트용 컴포넌트. 플레이를 시작하면 예시 스킬과 비동기 테스트 함수를 시전한다.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// FunctionExecutorTest와 함께 붙이지 않는다. 두 테스트의 작업이 섞이면 로그를 알아보기 어렵기 때문
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class PredefinedFunctionTest : MonoBehaviour
{
	[SerializeField, Min(0), Tooltip("이 틱이 지나면 InterruptAll을 부른다. 0이면 부르지 않는다")]
	int interruptAfterTick;

	FunctionExecutor executor;
	bool interrupted;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();

		// 고정 ID로 원본을 찾아 시전한다. 함수 데이터의 함수 블록도 나중에 이 방법으로 스킬을 찾는다
		if (!PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.Thrust, out PredefinedFunction thrust))
		{
			Debug.LogError($"[PredefinedFunctionTest] {PredefinedFunctionIds.Thrust}를 찾지 못함");
			return;
		}

		// 예시 스킬: 20틱 = 틱 20에 끝나야 하고, 틱 10에 판정 로그가 찍혀야 한다
		// 실행 중 스택은 2칸 (호출 1 + 파라미터 1)
		executor.Cast(thrust);

		// 비동기: 비동기 함수 시전(1틱) + A(30틱) = 틱 31에 끝나야 한다
		// 비동기 함수는 새 작업으로 틱 2부터 20틱 진행 = 틱 21에 끝나야 한다
		executor.Cast(new UserFunction("UserFunction_AsyncA",
			new TestAsyncFunction(),
			new TestFunctionA()));
	}

	void FixedUpdate()
	{
		// 실행 중단: 정한 틱이 지나면 모든 작업을 끊는다
		// 끊긴 작업은 Interrupted로 끝나야 하고, 실행 중이던 검 찌르기는 OnCancel 로그를 남겨야 한다
		if (interrupted || interruptAfterTick <= 0 || executor.CurrentTick < interruptAfterTick) return;

		interrupted = true;
		executor.InterruptAll();
	}
}
