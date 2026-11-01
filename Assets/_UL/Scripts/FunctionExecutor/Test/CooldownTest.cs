using UnityEngine;

/// <summary>
/// 쿨타임 테스트용 컴포넌트. 사용자 정의 함수와 스킬 하나를 정해진 틱마다 다시 시전해, 실행 중·쿨타임 중에는 거부되는지 확인한다.<br/>
/// 함수 안의 같은 스킬과 반복문 안의 스킬이 쿨타임 동안 실패하지 않고 기다리는지도 확인한다.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다. 다른 테스트의 작업이 섞이면 로그를 알아보기 어렵기 때문
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class CooldownTest : MonoBehaviour
{
	// 슬롯처럼 같은 객체를 계속 시전한다. 사용자 정의 함수가 실행 중인지는 원본 객체로 구분하기 때문
	// 실행 10틱, 쿨타임 20틱인 같은 스킬 두 개. 둘째 스킬은 첫째 스킬의 쿨타임이 끝날 때까지 기다린다. 함수는 자기 쿨타임이 없다
	readonly UserFunction combo = new UserFunction("CooldownCombo",
		new TestCooldownFunction(),
		new TestCooldownFunction());
	// 슬롯에 스킬 하나를 바로 넣은 경우. 쿨타임은 고정 ID로 구분하므로 combo 안의 스킬과 쿨타임을 함께 쓴다
	readonly TestCooldownFunction skill = new TestCooldownFunction();
	// 반복문 안의 스킬. 반복할 때마다 쿨타임이 끝날 때까지 기다려야 한다
	readonly UserFunction repeat = new UserFunction("CooldownRepeat",
		new TestRepeatBlock(2, new TestCooldownFunction()));

	FunctionExecutor executor;
	// 지금 진행할 단계. 그 단계의 틱이 되면 FixedUpdate가 실행하고 다음 단계로 넘어간다
	int step;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();

		// 틱 1~10 첫 스킬(쿨타임 틱 30까지), 틱 11~29 둘째 스킬이 기다림, 틱 30~39 둘째 스킬(쿨타임 틱 59까지)
		// 틱 39에 Completed로 끝나고, 끝 로그의 쿨타임은 0틱이어야 한다(사용자 정의 함수는 자기 쿨타임이 없음)
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
				// 기대: 첫 스킬이 틱 10에 끝나 쿨타임 중이므로 거부 (OnCooldown, 남은 쿨타임 5틱)
				// 예전 규칙(함수가 끝나면 함수 쿨타임만 시작)이면 스킬을 따로 시전한 것이라 거부되지 않았다
				executor.Cast(skill);
				break;

			case 2 when tick >= 41:
				// 기대: 시전되어 틱 42에 시작한다. 예전 규칙(함수 쿨타임 = 스킬 쿨타임 합 40틱)이면 거부되었다
				// 첫 스킬은 틱 59까지 기다린 뒤 틱 59~68(쿨타임 틱 88까지), 둘째 스킬은 틱 88~97, 틱 97에 Completed
				executor.Cast(combo);
				break;

			case 3 when tick >= 98:
				// combo가 끝난 뒤에 시전한다. 같은 스킬을 쓰는 두 함수가 함께 기다리면 로그를 알아보기 어렵기 때문
				// 기대: 틱 99 「반복 1회째 / 스택 1」(스킬이 프레임을 받기 전), 틱 117까지 기다린 뒤 틱 117~126 스킬
				// 틱 127 「반복 2회째 / 스택 1」, 틱 146까지 기다린 뒤 틱 146~155 스킬, 틱 155에 Completed
				// 예전 규칙이면 기다리지 않고 틱 99~118에 두 번 연달아 실행되었다
				executor.Cast(repeat);
				break;

			default:
				return;
		}
		step++;
	}
}
