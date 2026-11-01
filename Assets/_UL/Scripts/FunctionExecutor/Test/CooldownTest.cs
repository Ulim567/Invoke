using UnityEngine;

/// <summary>
/// 쿨타임 테스트용 컴포넌트. 사용자 정의 함수와 스킬 하나를 정해진 틱마다 다시 시전해, 실행 중·쿨타임 중에는 거부되는지 확인한다.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다. 다른 테스트의 작업이 섞이면 로그를 알아보기 어렵기 때문
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class CooldownTest : MonoBehaviour
{
	// 슬롯처럼 같은 객체를 계속 시전한다. 사용자 정의 함수가 실행 중인지는 원본 객체로 구분하기 때문
	// 실행 10틱, 쿨타임 20틱인 스킬 두 개. 함수는 실행 20틱이고 자기 쿨타임이 없다
	readonly UserFunction combo = new UserFunction("CooldownCombo",
		new TestCooldownFunction(),
		new TestCooldownFunction());
	// 슬롯에 스킬 하나를 바로 넣은 경우. 쿨타임은 고정 ID로 구분하므로 combo 안의 스킬과 쿨타임을 함께 쓴다
	readonly TestCooldownFunction skill = new TestCooldownFunction();

	FunctionExecutor executor;
	// 지금 진행할 단계. 그 단계의 틱이 되면 FixedUpdate가 실행하고 다음 단계로 넘어간다
	int step;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();

		// 틱 1~10 첫 스킬(틱 10에 끝나 쿨타임 틱 30까지), 틱 11~20 둘째 스킬(틱 20에 끝나 쿨타임 틱 40까지)
		// 틱 20에 Completed로 끝나고, 끝 로그의 쿨타임은 0틱이어야 한다(사용자 정의 함수는 자기 쿨타임이 없음)
		executor.Cast(combo);
	}

	void FixedUpdate()
	{
		int tick = executor.CurrentTick;
		switch (step)
		{
			case 0 when tick >= 5:
				// 기대: combo가 실행 중이므로 거부 (OnCooldown, 남은 쿨타임 0틱)
				executor.Cast(combo);
				// 기대: 같은 스킬이 combo 안에서 실행 중이므로 거부 (OnCooldown, 남은 쿨타임 0틱)
				executor.Cast(skill);
				break;

			case 1 when tick >= 25:
				// 기대: 둘째 스킬이 틱 20에 끝나 쿨타임 중이므로 거부 (OnCooldown, 남은 쿨타임 15틱 무렵)
				// 예전 규칙(함수가 끝나면 함수 쿨타임만 시작)이면 스킬을 따로 시전한 것이라 거부되지 않았다
				executor.Cast(skill);
				break;

			case 2 when tick >= 41:
				// 기대: 시전되어 틱 61 무렵 Completed로 끝나야 한다
				// 예전 규칙(함수 쿨타임 = 스킬 쿨타임 합 40틱)이면 틱 60까지 거부되었다
				executor.Cast(combo);
				break;

			default:
				return;
		}
		step++;
	}
}
