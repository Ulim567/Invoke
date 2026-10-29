using UnityEngine;

/// <summary>
/// 스킬 에러(Fail)와 일찍 끝내기(Stop) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 붙이면 TempSwordState도 함께 붙는다.<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다. 다른 테스트의 작업이 섞이면 로그를 알아보기 어렵기 때문
/// </summary>
[RequireComponent(typeof(FunctionExecutor), typeof(TempSwordState))]
public class SkillErrorTest : MonoBehaviour
{
	// 이 틱이 지나면 맨손으로 바꾸고 공격 스킬을 한 번 더 시전한다
	const int UnarmTick = 15;

	FunctionExecutor executor;
	TempSwordState swordState;
	bool unarmed;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		swordState = GetComponent<TempSwordState>();
		swordState.SetUnarmed(false);

		// 검을 든 채 공격: 틱 10에 Completed로 끝나야 한다
		executor.Cast(new TestAttackFunction());

		// 함수 안의 스킬이 시작될 때 맨손: A(30틱)가 끝나고 틱 31에 공격 스킬이 시작될 때는 이미 맨손이므로
		// 틱 31에 Failed: SkillFailed - 맨손으로 끝나야 한다
		executor.Cast(new UserFunction("UserFunction_AAttack",
			new TestFunctionA(),
			new TestAttackFunction()));

		// 소환검 0개(TestSword를 malloc하지 않음): 틱 1에 Failed: SkillFailed - 소환검 없음으로 끝나야 한다
		executor.Cast(new TestSummonedSwordFunction());

		// Stop: 50틱짜리지만 틱 5에 Completed로 끝나야 한다
		executor.Cast(new TestStopFunction());
	}

	void FixedUpdate()
	{
		if (unarmed || executor.CurrentTick < UnarmTick) return;

		unarmed = true;
		swordState.SetUnarmed(true);

		// 맨손으로 바꾼 직후 슬롯에서 공격: 시전은 되지만 시작하는 틱에 Failed: SkillFailed - 맨손으로 끝나야 한다
		executor.Cast(new TestAttackFunction());
	}
}
