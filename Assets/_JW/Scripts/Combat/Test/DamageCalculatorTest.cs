using UnityEngine;

/// <summary>
/// 데미지 계산 테스트용 컴포넌트. 플레이를 시작하면 테스트를 돌리고 결과를 콘솔 로그로 남긴다.<br/>
/// 다른 시스템이 필요 없으므로 씬의 아무 오브젝트에나 붙이면 된다.
/// <para>
/// 공격력 10(C 캐릭터 기본값), 스킬 배율 1로 계산해서, 결과가 "10 × 노션 표의 배율"과 같은지 본다 (이슈 #41 완료 조건).
/// </para>
/// </summary>
public class DamageCalculatorTest : MonoBehaviour
{
	const int Attack = 10;

	// 치명타가 나지 않게 하는 굴림 값과, 반드시 나게 하는 굴림 값
	const float NoCrit = 0.999f;
	const float AlwaysCrit = 0f;

	void Start()
	{
		TestSingleBonus();
		TestStackedBonus();
		TestCritical();
		TestRounding();
		TestExecution();
	}

	// 테스트 1: 조건 배율 하나씩 (노션 C 1차 6. 배율 표)
	void TestSingleBonus()
	{
		CheckDamage("기본 (배율 없음) 1.0배", Bonus(0f), 10);
		CheckDamage("출혈 + 집중찌르기 1.5배", Bonus(DamageBonus.BleedFocusedThrust), 15);
		CheckDamage("출혈 + 상처 공략 2.0배", Bonus(DamageBonus.BleedWoundExploit), 20);
		CheckDamage("출혈 + 상처 가르기 2.0배", Bonus(DamageBonus.BleedWoundSlash), 20);
		CheckDamage("미니게임 성공 + 급소공략 3.0배", Bonus(DamageBonus.VitalStrikeMinigame), 30);
		CheckDamage("표식 2.0배", Bonus(DamageBonus.Mark), 20);
	}

	// 테스트 2: 중첩 예시 (추가분끼리 더한다)
	void TestStackedBonus()
	{
		CheckDamage("표식 + 집중찌르기 2.5배", Bonus(DamageBonus.Mark + DamageBonus.BleedFocusedThrust), 25);
		CheckDamage("표식 + 상처 공략 3.0배", Bonus(DamageBonus.Mark + DamageBonus.BleedWoundExploit), 30);
		CheckDamage("표식 + 급소공략 4.0배", Bonus(DamageBonus.Mark + DamageBonus.VitalStrikeMinigame), 40);

		// 깊이찌르기는 치명타 추가분이라 치명타가 나야 붙는다
		DamageRequest deepThrust = Bonus(DamageBonus.Mark);
		deepThrust.CritChance = 1f;
		deepThrust.CritBonusRate = DamageBonus.CritDeepThrust;
		CheckDamage("표식 + 깊이찌르기(치명타) 3.5배", deepThrust, 35, AlwaysCrit);
	}

	// 테스트 3: 치명타가 날 때와 안 날 때
	void TestCritical()
	{
		DamageRequest request = new DamageRequest(Attack, 1f) { CritChance = 0.3f, CritBonusRate = DamageBonus.CritDeepThrust };

		DamageResult crit = DamageCalculator.Calculate(request, DamageTarget.None, 0.1f);
		Check("굴림 0.1 < 확률 0.3이면 치명타", crit.IsCritical && crit.Damage == 25, $"실제 {crit.Damage}, 치명타 {crit.IsCritical}");

		DamageResult noCrit = DamageCalculator.Calculate(request, DamageTarget.None, 0.5f);
		Check("굴림 0.5 ≥ 확률 0.3이면 치명타 아님", !noCrit.IsCritical && noCrit.Damage == 10, $"실제 {noCrit.Damage}, 치명타 {noCrit.IsCritical}");
	}

	// 테스트 4: 정수로 바꾸는 규칙
	void TestRounding()
	{
		CheckDamage("2.5는 3으로 반올림", new DamageRequest(5, 0.5f), 3);
		CheckDamage("2.4는 2로 반올림", new DamageRequest(4, 0.6f), 2);
		CheckDamage("0보다 크면 최소 1", new DamageRequest(1, 0.1f), 1);
		CheckDamage("공격력 0이면 0", new DamageRequest(0, 1f), 0);
		CheckDamage("몬스터 공격 Flat(7)은 그대로 7", DamageRequest.Flat(7), 7);
	}

	// 테스트 5: 처형 (체력 2% 이하, 검 던지기, 보스 제외)
	void TestExecution()
	{
		DamageRequest swordThrow = new DamageRequest(Attack, 1f) { CanExecute = true };
		DamageRequest otherSkill = new DamageRequest(Attack, 1f);

		DamageResult executed = DamageCalculator.Calculate(swordThrow, new DamageTarget(2, 100, false), NoCrit);
		Check("체력 2/100 + 검 던지기 → 처형, 데미지 = 남은 체력", executed.IsExecution && executed.Damage == 2, $"실제 {executed.Damage}, 처형 {executed.IsExecution}");

		DamageResult notLow = DamageCalculator.Calculate(swordThrow, new DamageTarget(3, 100, false), NoCrit);
		Check("체력 3/100 → 처형 아님", !notLow.IsExecution && notLow.Damage == 10, $"실제 {notLow.Damage}, 처형 {notLow.IsExecution}");

		DamageResult boss = DamageCalculator.Calculate(swordThrow, new DamageTarget(1, 100, true), NoCrit);
		Check("보스는 처형 안 됨", !boss.IsExecution && boss.Damage == 10, $"실제 {boss.Damage}, 처형 {boss.IsExecution}");

		DamageResult notThrow = DamageCalculator.Calculate(otherSkill, new DamageTarget(1, 100, false), NoCrit);
		Check("검 던지기가 아니면 처형 안 됨", !notThrow.IsExecution && notThrow.Damage == 10, $"실제 {notThrow.Damage}, 처형 {notThrow.IsExecution}");

		DamageResult noTarget = DamageCalculator.Calculate(swordThrow, DamageTarget.None, NoCrit);
		Check("대상 정보가 없으면 처형 판정 안 함", !noTarget.IsExecution && noTarget.Damage == 10, $"실제 {noTarget.Damage}, 처형 {noTarget.IsExecution}");
	}

	static DamageRequest Bonus(float bonusRate) => new DamageRequest(Attack, 1f) { BonusRate = bonusRate };

	static void CheckDamage(string name, DamageRequest request, int expected, float critRoll = NoCrit)
	{
		DamageResult result = DamageCalculator.Calculate(request, DamageTarget.None, critRoll);
		Check(name, result.Damage == expected, $"기대 {expected}, 실제 {result.Damage}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[DamageCalculatorTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
