using UnityEngine;

/// <summary>
/// 쿨타임 테스트용 컴포넌트. 같은 사용자 정의 함수 객체를 정해진 틱마다 다시 시전해, 실행 중·쿨타임 중에는 거부되는지 확인한다.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다. 다른 테스트의 작업이 섞이면 로그를 알아보기 어렵기 때문
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class CooldownTest : MonoBehaviour
{
	// 쿨타임은 원본 객체로 구분하므로, 슬롯처럼 같은 객체를 계속 시전한다
	// 쿨타임 20틱짜리 스킬 두 개 = 함수 쿨타임 40틱, 실행 10 + 10 = 20틱
	readonly UserFunction combo = new UserFunction("CooldownCombo",
		new TestCooldownFunction(),
		new TestCooldownFunction());

	// 다시 시전할 틱과 기대하는 결과
	// 틱 5: 실행 중이므로 거부 (OnCooldown, 남은 쿨타임 0틱)
	// 틱 30: 틱 20에 끝나 쿨타임 40틱 중이므로 거부 (OnCooldown, 남은 쿨타임 30틱 무렵)
	// 틱 65: 틱 60에 쿨타임이 끝났으므로 시전되어 틱 85 무렵 Completed로 끝나야 한다
	readonly int[] recastTicks = { 5, 30, 65 };

	FunctionExecutor executor;
	int nextRecast;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();

		// 틱 20에 Completed로 끝나고, 끝 로그에 쿨타임 40틱이 찍혀야 한다
		executor.Cast(combo);
	}

	void FixedUpdate()
	{
		if (nextRecast >= recastTicks.Length || executor.CurrentTick < recastTicks[nextRecast]) return;

		nextRecast++;
		executor.Cast(combo);
	}
}
