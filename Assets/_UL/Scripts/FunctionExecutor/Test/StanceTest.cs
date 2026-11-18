using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Mirror;
using UnityEngine;

/// <summary>
/// 자세 시험 (#538). 자세 규칙의 효과 값, 계열 데미지, 쿨타임 증감, 받는 피해, 회복, 자해, init_stance·get_stance 시전을 확인한다.
/// <para>
/// - 사용법: 네트워크를 켜지 않은 Play 중 Spawn(baseStatsPath)을 부른다. 콘솔에 「[StanceTest] … 통과/실패」, 마지막에 「전체 통과」.<br/>
/// - 몬스터를 실제로 때리지는 않는다. 스킬이 데미지를 준 뒤 부르는 알림(DataDrivenSkill.NotifyDamageDealt)을 직접 불러 자해를 본다.
/// </para>
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class StanceTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	const string Tag = "[StanceTest]";
	FunctionExecutor executor;
	PlayerStance stance;
	PlayerStats stats;
	PlayerHealth health;
	int failures;
	readonly List<string> outputs = new List<string>();
	FunctionTask endedTask;

#if UNITY_EDITOR
	/// <summary>시험 오브젝트를 만든다. baseStatsPath는 PlayerStats의 기본 능력치 에셋 경로(C_BaseStats).</summary>
	public static GameObject Spawn(string baseStatsPath)
	{
		GameObject go = new GameObject("StanceTest");
		go.SetActive(false);
		PlayerStats stats = go.AddComponent<PlayerStats>();
		FieldInfo baseStats = typeof(PlayerStats).GetField("baseStats", BindingFlags.NonPublic | BindingFlags.Instance);
		baseStats?.SetValue(stats, UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(baseStatsPath));
		LanguageRule language = go.AddComponent<LanguageRule>();
		language.SetRuleDataForTest(UnityEditor.AssetDatabase.LoadAssetAtPath<LanguageRuleData>("Assets/_UL/Data/LanguageRule_C.asset"));
		language.SetFamilyForTest(LanguageFamily.C);
		go.AddComponent<NetworkIdentity>();
		go.AddComponent<PlayerHealth>();
		go.AddComponent<PlayerStance>();
		go.AddComponent<FunctionExecutor>();
		go.AddComponent<StanceTest>();
		go.SetActive(true);
		return go;
	}
#endif

	IEnumerator Start()
	{
		yield return null;
		executor = GetComponent<FunctionExecutor>();
		stance = GetComponent<PlayerStance>();
		stats = GetComponent<PlayerStats>();
		health = GetComponent<PlayerHealth>();
		executor.Output += (task, text) => outputs.Add(text);
		executor.TaskEnded += task => endedTask = task;

		TestRule();
		TestDamageBonus();
		TestCooldown();
		yield return TestReceivedDamage();
		yield return TestLifesteal();
		yield return TestSelfDamage();
		yield return TestGetStance();
		yield return TestInitStance();

		if (failures == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} 실패 {failures}개");
	}

	// 규칙 판별: 겹치는 숫자의 효과가 더해지는지, 0은 효과가 없는지 (Notion 예시 5, 6, 55 포함)
	void TestRule()
	{
		StanceRule c = StanceRule.For(CharacterLanguages.C);
		Check("Python은 자세 규칙 없음", StanceRule.For(CharacterLanguages.Python) == null, "");

		int primes = 0, fibonacci = 0, perfect = 0;
		for (int n = 1; n <= 100; n++)
		{
			if (StanceRule.IsPrime(n)) primes++;
			if (StanceRule.IsFibonacci(n)) fibonacci++;
			if (StanceRule.IsPerfect(n)) perfect++;
		}
		Check("1~100에 소수 25개, 피보나치 수 10개, 완전수 2개", primes == 25 && fibonacci == 10 && perfect == 2, $"소수 {primes}, 피보나치 {fibonacci}, 완전수 {perfect}");
		Check("피보나치 수 1·2·89, 아님 4·100 / 완전수 6·28, 아님 1·12",
			StanceRule.IsFibonacci(1) && StanceRule.IsFibonacci(2) && StanceRule.IsFibonacci(89) && !StanceRule.IsFibonacci(4) && !StanceRule.IsFibonacci(100)
			&& StanceRule.IsPerfect(6) && StanceRule.IsPerfect(28) && !StanceRule.IsPerfect(1) && !StanceRule.IsPerfect(12), "");

		ExpectEffect(c, 0, new StanceEffect(), "0: 자세 없음, 효과 없음");
		ExpectEffect(c, 1, new StanceEffect(thrust: 0.05f, slash: -0.15f, summon: 0.2f), "1: 홀수 + 피보나치 (찌르기 +15-10%)");
		ExpectEffect(c, 5, new StanceEffect(damage: 0.3f, thrust: 0.05f, slash: -0.15f, summon: 0.2f, cooldown: 0.1f, selfDamage: 0.01f),
			"5: 홀수 + 5의 배수 + 소수 + 피보나치 (Notion 예시)");
		ExpectEffect(c, 6, new StanceEffect(damage: 0.3f, thrust: -0.15f, slash: 0.15f, cooldown: -0.15f, received: 0.1f),
			"6: 짝수 + 3의 배수 + 완전수 (Notion 예시)");
		ExpectEffect(c, 55, new StanceEffect(damage: 0.05f, thrust: 0.05f, slash: -0.15f, summon: 0.2f, cooldown: 0.1f, lifesteal: 0.05f),
			"55: 홀수 + 5의 배수 + 40~60 + 피보나치 (Notion 예시, 받는 피해 대신 회복)");
		ExpectEffect(c, 45, new StanceEffect(damage: 0.05f, thrust: 0.15f, slash: -0.15f, cooldown: -0.05f, received: 0.1f, lifesteal: 0.05f),
			"45: 홀수 + 3의 배수 + 5의 배수 + 40~60 (쿨타임 -15+10%)");
		ExpectEffect(c, 2, new StanceEffect(damage: 0.2f, thrust: -0.25f, slash: 0.15f, summon: 0.2f, selfDamage: 0.01f),
			"2: 짝수 + 소수 + 피보나치");
	}

	void ExpectEffect(StanceRule rule, int n, StanceEffect expected, string name)
	{
		StanceEffect e = rule.EffectOf(n);
		bool ok = Near(e.DamageBonus, expected.DamageBonus) && Near(e.ThrustDamageBonus, expected.ThrustDamageBonus)
			&& Near(e.SlashDamageBonus, expected.SlashDamageBonus) && Near(e.SummonDamageBonus, expected.SummonDamageBonus)
			&& Near(e.ReceivedDamageRate, expected.ReceivedDamageRate) && Near(e.CooldownRate, expected.CooldownRate)
			&& Near(e.SelfDamageRate, expected.SelfDamageRate) && Near(e.LifestealRate, expected.LifestealRate);
		Check(name, ok, $"데미지 {e.DamageBonus}, 찌르기 {e.ThrustDamageBonus}, 휘두르기 {e.SlashDamageBonus}, 소환 {e.SummonDamageBonus}, "
			+ $"받는 피해 {e.ReceivedDamageRate}, 쿨타임 {e.CooldownRate}, 자해 {e.SelfDamageRate}, 회복 {e.LifestealRate}");
	}

	// 주는 데미지: 계열을 넘기면 모든 스킬 추가분 + 계열 추가분, 계열 없이(도트) 부르면 모든 스킬 추가분만
	void TestDamageBonus()
	{
		stance.SetStance(5);
		float thrust = PlayerStance.DamageBonusOf(gameObject, SkillFamily.Thrust);
		float slash = PlayerStance.DamageBonusOf(gameObject, SkillFamily.Slash);
		float summon = PlayerStance.DamageBonusOf(gameObject, SkillFamily.Summon);
		float thrown = PlayerStance.DamageBonusOf(gameObject, SkillFamily.Throw);
		float dot = PlayerStance.DamageBonusOf(gameObject);
		Check("자세 5의 데미지 추가분: 찌르기 +35%, 휘두르기 +15%, 소환 +50%, 던지기 +30%, 도트 +30%",
			Near(thrust, 0.35f) && Near(slash, 0.15f) && Near(summon, 0.5f) && Near(thrown, 0.3f) && Near(dot, 0.3f),
			$"찌르기 {thrust}, 휘두르기 {slash}, 소환 {summon}, 던지기 {thrown}, 도트 {dot}");

		int attack = stats.AttackPower;
		int d = DamageCalculator.Calculate(new DamageRequest(attack, 1f) { BonusRate = thrust }).Damage;
		Check($"자세 5의 찌르기 데미지 (공격력 {attack}) = {Round(attack * 1.35f)}", d == Round(attack * 1.35f), $"실제 {d}");

		// 스킬 클래스가 자기 계열(표의 Family)을 넘기는지: 찌르기(thrust)는 Thrust 계열
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.Thrust, out PredefinedFunction original);
		float fromSkill = NewCast(original).StanceDamageBonus(gameObject);
		Check("thrust 스킬은 찌르기 계열 추가분을 받음", Near(fromSkill, 0.35f), $"실제 {fromSkill}");

		stance.Clear();
		Check("자세 없음·컴포넌트 없음(null)의 추가분은 0",
			PlayerStance.DamageBonusOf(gameObject, SkillFamily.Thrust) == 0f && PlayerStance.DamageBonusOf(null) == 0f, "");
	}

	// 쿨타임: 능력치 쿨타임 감소를 적용한 뒤 자세 증감을 곱한다. 150 tick → 자세 5(+10%) 165, 자세 6(-15%) 128(127.5 반올림), 자세 15(-5%) 142
	void TestCooldown()
	{
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.InitStance, out PredefinedFunction init);
		int[] stances = { 5, 6, 15, 0 };
		foreach (int n in stances)
		{
			stance.SetStance(n);
			int ended = -1;
			var tracker = new CooldownTracker(stats, () => 0, () => PlayerStance.CooldownRateOf(gameObject));
			tracker.CooldownStarted += (id, endTick) => ended = endTick;
			tracker.SkillEnded(init);
			int expected = Mathf.RoundToInt(stats.ApplyCooldownReduction(init.CooldownTicks) * (1f + stance.Effect.CooldownRate));
			Check($"쿨타임 150 tick, 자세 {n} → {expected} tick", ended == expected, $"실제 {ended}");
		}
		stance.Clear();
	}

	// 받는 피해: 10을 받으면 3의 배수 11, 그 밖(짝수 4, 40~60인 55)과 자세 없음은 10
	IEnumerator TestReceivedDamage()
	{
		int[] stances = { 9, 4, 55, 0 };
		int[] expected = { 11, 10, 10, 10 };
		for (int i = 0; i < stances.Length; i++)
		{
			stance.SetStance(stances[i]);
			int before = health.Hp;
			health.TakeDamage(10);
			int taken = before - health.Hp;
			Check($"받는 피해 10, 자세 {stances[i]} → {expected[i]}", taken == expected[i], $"실제 {taken}");
			// 다음 피격을 받을 수 있게 경직·무적이 끝날 때까지 기다린다
			yield return new WaitForSeconds(health.HitStunTime + health.InvincibleTime + 0.1f);
		}
		stance.Clear();
	}

	// 회복: 40~60이면 스킬이 준 데미지의 5%. 1 미만은 모아 두었다가 1이 되면 회복. 40~60이 아니면 없음
	IEnumerator TestLifesteal()
	{
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.Thrust, out PredefinedFunction original);
		// 회복할 자리를 만든다
		health.TakeSelfDamage(30);

		stance.SetStance(4);
		int before = health.Hp;
		NewCast(original).NotifyDamageDealt(gameObject, 40);
		yield return null;
		Check("40~60이 아니면(4) 회복 없음", health.Hp == before, $"{before} → {health.Hp}");

		stance.SetStance(50);
		DataDrivenSkill cast = NewCast(original);
		before = health.Hp;
		cast.NotifyDamageDealt(gameObject, 10);
		yield return null;
		int afterFirst = health.Hp;
		cast.NotifyDamageDealt(gameObject, 10);
		yield return null;
		int afterSecond = health.Hp;
		Check("자세 50: 데미지 10의 5%(0.5)는 모아 두고, 두 번째에 1 회복", afterFirst == before && afterSecond == before + 1,
			$"{before} → {afterFirst} → {afterSecond}");

		before = health.Hp;
		NewCast(original).NotifyDamageDealt(gameObject, 40);
		yield return null;
		Check("다른 시전이 준 데미지 40의 5% = 2 회복", health.Hp == before + 2, $"{before} → {health.Hp}");

		health.Heal(health.MaxHp);
		Check("회복은 최대 체력을 넘지 않음", health.Hp == health.MaxHp, $"{health.Hp}/{health.MaxHp}");
		stance.Clear();
	}

	// 자해: 소수면 시전 한 번에 한 번만 최대 체력의 1%, 경직·무적 없이, 무적 중에도 깎는다. 소수가 아니면 없다
	IEnumerator TestSelfDamage()
	{
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.Thrust, out PredefinedFunction original);
		int maxHp = health.MaxHp;
		int expected = Round(maxHp * 0.01f);

		// 소수가 아닌 자세: 자해 없음
		stance.SetStance(4);
		int before = health.Hp;
		NewCast(original).NotifyDamageDealt(gameObject, 5);
		yield return null;
		Check("짝수(4)는 자해 없음", health.Hp == before, $"{before} → {health.Hp}");

		// 소수 자세: 같은 시전에서 두 번 맞혀도 한 번만
		stance.SetStance(97);
		before = health.Hp;
		DataDrivenSkill cast = NewCast(original);
		cast.NotifyDamageDealt(gameObject, 5);
		cast.NotifyDamageDealt(gameObject, 5);
		yield return null;
		Check($"소수(97): 시전 한 번에 한 번만 최대 체력의 1%({expected})", before - health.Hp == expected, $"{before} → {health.Hp}");
		Check("자해는 경직·무적을 걸지 않음", !health.IsStunned && !health.IsInvincible, $"경직 {health.IsStunned}, 무적 {health.IsInvincible}");

		// 데미지 0은 데미지를 준 것으로 세지 않는다
		before = health.Hp;
		NewCast(original).NotifyDamageDealt(gameObject, 0);
		yield return null;
		Check("데미지 0이면 자해 없음", health.Hp == before, $"{before} → {health.Hp}");

		// 무적 중에도 깎는다: 맞아서 무적이 된 뒤 다른 시전의 자해
		health.TakeDamage(1);
		bool invincible = health.IsInvincible;
		before = health.Hp;
		NewCast(original).NotifyDamageDealt(gameObject, 5);
		yield return null;
		Check("무적 중에도 자해로 깎임", invincible && before - health.Hp == expected, $"무적 {invincible}, {before} → {health.Hp}");

		// 자해로 체력이 0이 되면 죽는다
		health.TakeSelfDamage(health.Hp - 1);
		NewCast(original).NotifyDamageDealt(gameObject, 5);
		yield return null;
		Check("자해로 체력이 0이 되면 사망", health.Hp == 0 && health.IsDead, $"체력 {health.Hp}, 사망 {health.IsDead}");
		stance.Clear();
	}

	// 실행기가 시전할 때처럼 원본과 같은 종류의 새 스킬을 만든다. 원본에 「데미지를 줬음」이 남지 않게 하기 위함
	static DataDrivenSkill NewCast(PredefinedFunction original) => (DataDrivenSkill)Activator.CreateInstance(original.GetType());

	// get_stance: int s = get_stance(); printf(s);
	IEnumerator TestGetStance()
	{
		stance.SetStance(37);
		yield return Run("get_stance 37", BlockData.Declare(Int, "s", SlotData.FromBlock(Call(PredefinedFunctionIds.GetStance))),
			Call(PredefinedFunctionIds.Printf, SlotData.FromVariable("s")));
		Check("get_stance는 지금 자세(37)", Completed() && Output() == "s = 37", Detail());

		stance.Clear();
		yield return Run("get_stance 0", BlockData.Declare(Int, "s", SlotData.FromBlock(Call(PredefinedFunctionIds.GetStance))),
			Call(PredefinedFunctionIds.Printf, SlotData.FromVariable("s")));
		Check("자세가 없으면 get_stance는 0", Completed() && Output() == "s = 0", Detail());
	}

	// init_stance: int n = init_stance(); printf(n); → 2.5초(125 tick) 뒤 1~100, 자세도 그 숫자, 쿨타임 3초(3의 배수면 감소)
	IEnumerator TestInitStance()
	{
		int startTick = executor.CurrentTick;
		yield return Run("init_stance", BlockData.Declare(Int, "n", SlotData.FromBlock(Call(PredefinedFunctionIds.InitStance))),
			Call(PredefinedFunctionIds.Printf, SlotData.FromVariable("n")));
		int elapsed = executor.CurrentTick - startTick;
		string text = Output();
		int n = -1;
		if (text.StartsWith("n = ")) int.TryParse(text.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
		Check("init_stance는 1~100을 돌려주고 자세도 그 숫자", Completed() && n >= 1 && n <= 100 && stance.Stance == n, $"{Detail()}, 자세 {stance.Stance}");
		Check($"init_stance는 {InitStanceFunction.CastTicks} tick 넘게 걸림", elapsed >= InitStanceFunction.CastTicks, $"{elapsed} tick");

		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.InitStance, out PredefinedFunction init);
		int remaining = executor.GetRemainingCooldownTicks(init);
		int expectedCooldown = Mathf.RoundToInt(stats.ApplyCooldownReduction(InitStanceFunction.Cooldown) * (1f + stance.Effect.CooldownRate));
		// 끝난 뒤 이 코루틴이 돌기까지 몇 tick 지났을 수 있다
		Check($"init_stance 쿨타임 약 {expectedCooldown} tick (자세 {n}, 쿨타임 증감 {stance.Effect.CooldownRate})",
			remaining <= expectedCooldown && remaining >= expectedCooldown - 10, $"남은 {remaining} tick");
	}

	IEnumerator Run(string name, params BlockData[] blocks)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.AddRange(blocks);
		outputs.Clear();
		endedTask = null;
		if (!FunctionDataConverter.TryConvert(data, Array.Empty<FunctionData>(), LanguageFamily.C, out UserFunction function))
		{
			Check($"{name}: 변환", false, "");
			yield break;
		}
		if (executor.Cast(function) == null)
		{
			Check($"{name}: 시전", false, "");
			yield break;
		}
		float limit = Time.time + 10f;
		while ((endedTask == null || endedTask.Name != name) && Time.time < limit) yield return null;
	}

	bool Completed() => endedTask != null && endedTask.EndReason == FunctionTaskEndReason.Completed;
	string Output() => string.Join(" | ", outputs);
	string Detail() => $"끝난 이유 {(endedTask != null ? endedTask.EndReason.ToString() : "없음")}, 출력 {Output()}";

	static BlockData Call(string id, params SlotData[] arguments) => BlockData.CallPredefinedFunction(id, arguments);
	static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0001f;
	static int Round(float value) => Mathf.FloorToInt(value + 0.5f);

	void Check(string name, bool ok, string detail)
	{
		if (ok) Debug.Log($"{Tag} {name} 통과");
		else
		{
			failures++;
			Debug.LogError($"{Tag} {name} 실패: {detail}");
		}
	}
}
