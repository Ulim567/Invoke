using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Mirror;
using UnityEngine;

/// <summary>
/// 자세 시험 (#538, Python #546). 자세 규칙의 효과 값, 계열 데미지, 쿨타임 증감, 받는 피해, 회복, 자해, 상태 이상 지속 시간, init_stance·get_stance 시전을 확인한다.
/// <para>
/// - 사용법: 네트워크를 켜지 않은 Play 중 Spawn(baseStatsPath, python)을 부른다. 콘솔에 「[StanceTest C] … 통과/실패」(Python은 [StanceTest Python]), 마지막에 「전체 통과」.<br/>
/// - 몬스터를 실제로 때리지는 않는다. 스킬이 데미지를 준 뒤 부르는 알림(DataDrivenSkill.NotifyDamageDealt)을 직접 불러 자해를 본다.
/// </para>
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class StanceTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	bool python;
	string Tag => python ? "[StanceTest Python]" : "[StanceTest C]";
	FunctionExecutor executor;
	PlayerStance stance;
	PlayerStats stats;
	PlayerHealth health;
	int failures;
	readonly List<string> outputs = new List<string>();
	FunctionTask endedTask;

#if UNITY_EDITOR
	/// <summary>
	/// 시험 오브젝트를 만든다. baseStatsPath는 PlayerStats의 기본 능력치 에셋 경로(C_BaseStats).
	/// python이면 Python 캐릭터(PlayerCharacter의 언어 Python, Python 문법 규칙)로 Python 자세를 시험한다 (#546).
	/// </summary>
	public static GameObject Spawn(string baseStatsPath, bool python = false)
	{
		GameObject go = new GameObject(python ? "StanceTest_Python" : "StanceTest_C");
		go.SetActive(false);
		PlayerStats stats = go.AddComponent<PlayerStats>();
		FieldInfo baseStats = typeof(PlayerStats).GetField("baseStats", BindingFlags.NonPublic | BindingFlags.Instance);
		baseStats?.SetValue(stats, UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(baseStatsPath));
		LanguageRule language = go.AddComponent<LanguageRule>();
		if (python)
		{
			language.SetRuleDataForTest(LanguageRuleData.CreatePythonForTest());
			language.SetFamilyForTest(LanguageFamily.Python);
			// 자세 규칙은 PlayerCharacter의 언어로 고른다. 이 컴포넌트가 없으면 C로 본다
			PlayerCharacter character = go.AddComponent<PlayerCharacter>();
			typeof(PlayerCharacter).GetField("language", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(character, CharacterLanguages.Python);
		}
		else
		{
			language.SetRuleDataForTest(UnityEditor.AssetDatabase.LoadAssetAtPath<LanguageRuleData>("Assets/_UL/Data/LanguageRule_C.asset"));
			language.SetFamilyForTest(LanguageFamily.C);
		}
		go.AddComponent<NetworkIdentity>();
		go.AddComponent<PlayerHealth>();
		go.AddComponent<PlayerStance>();
		go.AddComponent<FunctionExecutor>();
		StanceTest test = go.AddComponent<StanceTest>();
		test.python = python;
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

		if (python)
		{
			TestPythonRule();
			TestPythonRuleText();
			TestPythonDamageBonus();
			TestPythonCooldown();
			yield return TestStatusDuration();
			TestPythonFunctionSetup();
			yield return TestGetStance();
			yield return TestInitStance();
		}
		else
		{
			TestRule();
			TestCRuleText();
			TestDamageBonus();
			TestCooldown();
			yield return TestReceivedDamage();
			yield return TestLifesteal();
			yield return TestSelfDamage();
			yield return TestGetStance();
			yield return TestInitStance();
		}

		if (failures == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} 실패 {failures}개");
	}

	// 규칙 판별: 겹치는 숫자의 효과가 더해지는지, 0은 효과가 없는지 (Notion 예시 5, 6, 55 포함)
	void TestRule()
	{
		StanceRule c = StanceRule.For(CharacterLanguages.C);
		Check("C 캐릭터는 C 규칙, Python 캐릭터는 Python 규칙 (#546)",
			c is CStanceRule && StanceRule.For(CharacterLanguages.Python) is PythonStanceRule, "");

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
			&& Near(e.ElementDamageBonus, expected.ElementDamageBonus) && Near(e.ZoneDamageBonus, expected.ZoneDamageBonus)
			&& Near(e.OrbDamageBonus, expected.OrbDamageBonus)
			&& Near(e.ReceivedDamageRate, expected.ReceivedDamageRate) && Near(e.CooldownRate, expected.CooldownRate)
			&& Near(e.CurseCooldownRate, expected.CurseCooldownRate) && Near(e.CurseDurationRate, expected.CurseDurationRate)
			&& Near(e.SelfDamageRate, expected.SelfDamageRate) && Near(e.LifestealRate, expected.LifestealRate);
		Check(name, ok, $"데미지 {e.DamageBonus}, 찌르기 {e.ThrustDamageBonus}, 휘두르기 {e.SlashDamageBonus}, 소환 {e.SummonDamageBonus}, "
			+ $"원소 {e.ElementDamageBonus}, 범위 {e.ZoneDamageBonus}, 구슬 {e.OrbDamageBonus}, "
			+ $"받는 피해 {e.ReceivedDamageRate}, 쿨타임 {e.CooldownRate}, 보조 쿨타임 {e.CurseCooldownRate}, 보조 지속 시간 {e.CurseDurationRate}, "
			+ $"자해 {e.SelfDamageRate}, 회복 {e.LifestealRate}");
	}

	// Python 규칙 판별 (#546, Notion 예시 1, 4, 8, 64 포함)
	void TestPythonRule()
	{
		StanceRule py = StanceRule.For(CharacterLanguages.Python);
		Check("Python 캐릭터의 자세 효과는 Python 규칙", py is PythonStanceRule && PlayerCharacter.LanguageOf(gameObject) == CharacterLanguages.Python, "");

		int palindromes = 0, squares = 0, cubes = 0;
		for (int n = 1; n <= 100; n++)
		{
			if (StanceRule.IsPalindrome(n)) palindromes++;
			if (StanceRule.IsSquare(n)) squares++;
			if (StanceRule.IsCube(n)) cubes++;
		}
		Check("1~100에 회문수 18개, 완전제곱수 10개, 세제곱수 4개", palindromes == 18 && squares == 10 && cubes == 4,
			$"회문수 {palindromes}, 완전제곱수 {squares}, 세제곱수 {cubes}");
		Check("회문수 7·11·99, 아님 10·12·100 / 완전제곱수 1·100, 아님 2 / 세제곱수 1·64, 아님 9",
			StanceRule.IsPalindrome(7) && StanceRule.IsPalindrome(11) && StanceRule.IsPalindrome(99)
			&& !StanceRule.IsPalindrome(10) && !StanceRule.IsPalindrome(12) && !StanceRule.IsPalindrome(100)
			&& StanceRule.IsSquare(1) && StanceRule.IsSquare(100) && !StanceRule.IsSquare(2)
			&& StanceRule.IsCube(1) && StanceRule.IsCube(64) && !StanceRule.IsCube(9), "");

		ExpectEffect(py, 0, new StanceEffect(), "0: 자세 없음, 효과 없음");
		ExpectEffect(py, 1, new StanceEffect(zone: 0.15f, element: -0.15f, cooldown: -0.15f, received: -0.05f, damage: 0.2f),
			"1: 작은 수 + 회문수 + 완전제곱수 + 세제곱수 (Notion 예시)");
		ExpectEffect(py, 4, new StanceEffect(zone: 0.15f, element: -0.25f, orb: 0.2f, cooldown: -0.15f, received: -0.05f, damage: -0.05f),
			"4: 작은 수 + 4의 배수 + 회문수 + 완전제곱수 (Notion 예시)");
		ExpectEffect(py, 8, new StanceEffect(zone: 0.15f, element: -0.25f, orb: 0.2f, cooldown: -0.15f, received: 0.1f, damage: 0.25f),
			"8: 작은 수 + 4의 배수 + 회문수 + 세제곱수 (Notion 예시)");
		ExpectEffect(py, 64, new StanceEffect(element: 0.05f, zone: -0.15f, orb: 0.2f, received: -0.15f, damage: 0.2f),
			"64: 큰 수 + 4의 배수 + 완전제곱수 + 세제곱수 (Notion 예시)");
		ExpectEffect(py, 77, new StanceEffect(element: 0.15f, zone: -0.15f, cooldown: -0.15f, received: 0.1f, curseCooldown: 0.2f, curseDuration: 0.3f),
			"77: 큰 수 + 회문수 + 7의 배수");
		ExpectEffect(py, 50, new StanceEffect(zone: 0.15f, element: -0.15f), "50: 작은 수만 (51부터 큰 수)");
		ExpectEffect(py, 51, new StanceEffect(element: 0.15f, zone: -0.15f), "51: 큰 수만");
	}

	// Python 계열 데미지: 자세 64는 데미지 +20%, 원소 +5%, 범위 -15%, 구슬 +20%
	void TestPythonDamageBonus()
	{
		stance.SetStance(64);
		float element = PlayerStance.DamageBonusOf(gameObject, SkillFamily.Element);
		float zone = PlayerStance.DamageBonusOf(gameObject, SkillFamily.Zone);
		float orb = PlayerStance.DamageBonusOf(gameObject, SkillFamily.Orb);
		float curse = PlayerStance.DamageBonusOf(gameObject, SkillFamily.Curse);
		float dot = PlayerStance.DamageBonusOf(gameObject);
		Check("자세 64의 데미지 추가분: 원소 +25%, 범위 +5%, 구슬 +40%, 보조 +20%, 도트 +20%",
			Near(element, 0.25f) && Near(zone, 0.05f) && Near(orb, 0.4f) && Near(curse, 0.2f) && Near(dot, 0.2f),
			$"원소 {element}, 범위 {zone}, 구슬 {orb}, 보조 {curse}, 도트 {dot}");

		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.PyMagicMissile, out PredefinedFunction missile);
		float fromSkill = NewCast(missile).StanceDamageBonus(gameObject);
		Check("매직 미사일은 원소 계열 추가분을 받음", Near(fromSkill, 0.25f), $"실제 {fromSkill}");
		stance.Clear();
	}

	// 계열별 쿨타임: 자세 7(작은 수 + 회문수 + 7의 배수)은 모든 스킬 -15%, 보조 계열은 -15% + 20% = +5%
	void TestPythonCooldown()
	{
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.PyHex, out PredefinedFunction hex);
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.PyMagicMissile, out PredefinedFunction missile);
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.PyInitStance, out PredefinedFunction init);
		stance.SetStance(7);
		float hexRate = PlayerStance.CooldownRateOf(gameObject, hex);
		float missileRate = PlayerStance.CooldownRateOf(gameObject, missile);
		float initRate = PlayerStance.CooldownRateOf(gameObject, init);
		Check("자세 7의 쿨타임 증감: 저주(보조) +5%, 매직 미사일(원소) -15%, init_stance(스킬 아님) -15%",
			Near(hexRate, 0.05f) && Near(missileRate, -0.15f) && Near(initRate, -0.15f),
			$"저주 {hexRate}, 매직 미사일 {missileRate}, init_stance {initRate}");

		// CooldownTracker가 끝난 스킬마다 그 스킬의 증감을 곱하는지
		foreach (PredefinedFunction skill in new[] { hex, init })
		{
			if (skill.CooldownTicks <= 0) continue;
			int ended = -1;
			var tracker = new CooldownTracker(stats, () => 0, s => PlayerStance.CooldownRateOf(gameObject, s));
			tracker.CooldownStarted += (id, endTick) => ended = endTick;
			tracker.SkillEnded(skill);
			int expected = Mathf.RoundToInt(stats.ApplyCooldownReduction(skill.CooldownTicks) * (1f + PlayerStance.CooldownRateOf(gameObject, skill)));
			Check($"{skill.Name} 쿨타임 {skill.CooldownTicks} tick, 자세 7 → {expected} tick", ended == expected, $"실제 {ended}");
		}
		stance.Clear();
	}

	// 상태 이상 지속 시간: 7의 배수이면 보조 계열 스킬이 거는 상태가 30% 길다. 표식 5초 → 6.5초. 범위 계열(독 안개)과 다른 자세는 그대로
	IEnumerator TestStatusDuration()
	{
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.PyHex, out PredefinedFunction hex);
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.PyVenomMist, out PredefinedFunction mist);
		stance.SetStance(14);
		float hexRate = NewCast(hex).StanceStatusDurationRate(gameObject);
		float mistRate = NewCast(mist).StanceStatusDurationRate(gameObject);
		stance.SetStance(15);
		float otherRate = NewCast(hex).StanceStatusDurationRate(gameObject);
		Check("지속 시간 증감: 자세 14의 저주 +30%, 자세 14의 독 안개(범위) 0, 자세 15의 저주 0",
			Near(hexRate, 0.3f) && Near(mistRate, 0f) && Near(otherRate, 0f), $"저주 {hexRate}, 독 안개 {mistRate}, 자세 15 저주 {otherRate}");

		// 실제로 상태를 걸어 남은 시간을 본다. 몬스터 없이 StatusEffects만 붙인 대상
		GameObject target = new GameObject("StanceTest_StatusTarget");
		target.AddComponent<NetworkIdentity>();
		StatusEffects effects = target.AddComponent<StatusEffects>();
		yield return null;
		StatusEffects.TryApply(target, StatusType.Mark, gameObject, hexRate);
		StatusEffects.TryApply(target, StatusType.Stun, gameObject, 0f);
		float mark = effects.RemainingTime(StatusType.Mark);
		float stun = effects.RemainingTime(StatusType.Stun);
		Check($"표식 {StatusDefaults.MarkDuration}초 × 1.3 = {StatusDefaults.MarkDuration * 1.3f}초, 증감 0인 기절은 {StatusDefaults.StunDuration}초 그대로",
			Mathf.Abs(mark - StatusDefaults.MarkDuration * 1.3f) < 0.05f && Mathf.Abs(stun - StatusDefaults.StunDuration) < 0.05f,
			$"표식 {mark}, 기절 {stun}");
		Destroy(target);
		stance.Clear();
	}

	// Python 자세 함수가 등록되고, 슬롯에 넣을 수 없고, 새 캐릭터가 1개씩 받는지
	void TestPythonFunctionSetup()
	{
		bool registered = PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.PyInitStance, out PredefinedFunction init)
			& PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.PyGetStance, out PredefinedFunction get);
		Check("py.init_stance·py.get_stance 등록, 이름 init_stance·get_stance, init_stance 시전 125 tick·쿨타임 150 tick",
			registered && init.Name == "init_stance" && get.Name == "get_stance"
			&& init.TickCount == InitStanceFunction.CastTicks && init.CooldownTicks == InitStanceFunction.Cooldown, "");
		Check("자세 함수는 슬롯에 넣을 수 없음",
			!SlotLoadout.IsEquippableSkill(PredefinedFunctionIds.PyInitStance) && !SlotLoadout.IsEquippableSkill(PredefinedFunctionIds.PyGetStance), "");

		Dictionary<string, int> counts = ItemCountRules.Initial(CharacterLanguages.Python);
		counts.TryGetValue(PredefinedFunctionIds.PyInitStance, out int initCount);
		counts.TryGetValue(PredefinedFunctionIds.PyGetStance, out int getCount);
		// 자세 함수는 스킬 트리에서 해금해야 받는다 (#545)
		Check("새 Python 캐릭터의 자세 함수 처음 개수는 0, C 자세 함수도 없음 (#545)",
			initCount == 0 && getCount == 0 && !counts.ContainsKey(PredefinedFunctionIds.InitStance), $"init {initCount}, get {getCount}");
	}

	// 규칙 표 (#545): 노드 ID·이름·조건, 이득·손해 글이 효과 값에서 나오는지
	void TestRuleTable(StanceRule rule, string[] expectedNames, string prefix)
	{
		var names = new List<string>();
		bool idsOk = true;
		foreach (StanceRuleEntry entry in rule.Rules)
		{
			names.Add(entry.Name);
			idsOk &= entry.NodeId.StartsWith(prefix, StringComparison.Ordinal)
				&& StanceRule.TryGetRule(entry.NodeId, out StanceRuleEntry found) && found == entry;
		}
		Check($"규칙 표 이름 = {string.Join("·", expectedNames)}", string.Join("·", names) == string.Join("·", expectedNames), string.Join("·", names));
		Check($"규칙 노드 ID는 {prefix}로 시작하고 TryGetRule로 찾음", idsOk, "");
		Check("없는 노드 ID는 못 찾음", !StanceRule.TryGetRule("c.stance.none", out _) && !StanceRule.TryGetRule(PredefinedFunctionIds.Thrust, out _), "");
	}

	void TestCRuleText()
	{
		TestRuleTable(StanceRule.For(CharacterLanguages.C), new[] { "적중", "일망타진", "조급함", "신중함", "흡혈", "혹사", "지휘", "완벽" }, "c.stance.");
		StanceRuleEntry odd = Rule(CStanceRule.OddNode), middle = Rule(CStanceRule.MiddleNode), prime = Rule(CStanceRule.PrimeNode),
			three = Rule(CStanceRule.ThreeNode), perfect = Rule(CStanceRule.PerfectNode);
		ExpectText(odd, "2로 나누어떨어지지 않는 수", "찌르기 계열 데미지 +15%", "휘두르기 계열 데미지 -15%");
		ExpectText(three, "3으로 나누어떨어지는 수", "쿨타임 -15%", "받는 피해 +10%");
		ExpectText(middle, "40 이상 60 이하", "스킬이 준 데미지의 5% 회복", "데미지 -5%");
		ExpectText(prime, "1을 제외한, 1과 자기 자신으로만 나누어떨어지는 수", "데미지 +20%", "데미지를 준 스킬마다 자해 (최대 체력의 1%)");
		ExpectText(perfect, "완전수(자기 자신을 제외한 약수의 합이 자기 자신과 같은 수)", "데미지 +30%", "");

		// 스킬 트리 규칙 카드 글 (#545): 문장 한 줄 + 「효과:」 + 좋은 효과(청록)·나쁜 효과(빨강) 목록
		BlockReferenceEntry oddCard = BlockReference.RuleEntryOf(CStanceRule.OddNode, true);
		string oddExpected = "자세 숫자가 2로 나누어떨어지지 않는 수인 경우, 아래 효과가 적용됩니다.\n\n효과:\n"
			+ "<color=#5EF2D6>- 찌르기 계열 데미지 +15%</color>\n<color=#E07070>- 휘두르기 계열 데미지 -15%</color>";
		Check("적중 카드: 이름 적중, 문장 + 효과 목록, 색 태그 사용", oddCard != null && oddCard.Name == "적중" && oddCard.RichSummary && oddCard.Summary == oddExpected,
			oddCard?.Summary);
		BlockReferenceEntry perfectCard = BlockReference.RuleEntryOf(CStanceRule.PerfectNode, true);
		Check("완벽 카드: 나쁜 효과가 없으면 빨간 줄 없음", perfectCard != null && perfectCard.Summary.EndsWith("<color=#5EF2D6>- 데미지 +30%</color>")
			&& !perfectCard.Summary.Contains("#E07070"), perfectCard?.Summary);
		BlockReferenceEntry hiddenCard = BlockReference.RuleEntryOf(CStanceRule.OddNode, false);
		Check("해금 전 카드: 이름·내용 ???", hiddenCard != null && hiddenCard.Name == "???" && hiddenCard.Summary == "???", hiddenCard?.Summary);
	}

	void TestPythonRuleText()
	{
		TestRuleTable(StanceRule.For(CharacterLanguages.Python), new[] { "직격", "포화", "응축", "경솔", "집요함", "비늘", "각성" }, "py.stance.");
		ExpectText(Rule(PythonStanceRule.BigNode), "51 이상 100 이하", "원소 계열 데미지 +15%", "범위 계열 데미지 -15%");
		ExpectText(Rule(PythonStanceRule.SevenNode), "7로 나누어떨어지는 수", "보조 계열 상태 이상 지속 시간 +30%", "보조 계열 쿨타임 +20%");
		ExpectText(Rule(PythonStanceRule.SquareNode), "제곱수(같은 수를 두 번 곱한 수)", "받는 피해 -15%", "데미지 -5%");
	}

	static StanceRuleEntry Rule(string nodeId) => StanceRule.TryGetRule(nodeId, out StanceRuleEntry rule) ? rule : null;

	// gains·losses는 「, 」로 이은 글. 손해가 없으면 ""
	void ExpectText(StanceRuleEntry rule, string condition, string gains, string losses)
	{
		bool ok = rule != null && rule.Condition == condition && string.Join(", ", rule.Gains) == gains && string.Join(", ", rule.Losses) == losses;
		Check($"{rule?.Name} 글: 조건 {condition} / 이득 {gains} / 손해 {(losses.Length > 0 ? losses : "없음")}", ok,
			rule == null ? "규칙 없음" : $"조건 {rule.Condition} / 이득 {string.Join(", ", rule.Gains)} / 손해 {string.Join(", ", rule.Losses)}");
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
			var tracker = new CooldownTracker(stats, () => 0, skill => PlayerStance.CooldownRateOf(gameObject, skill));
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

	// 언어마다 고정 ID와 문법만 다르다. C: int s = get_stance(); printf(s);  Python: s = get_stance(); print(s)
	string GetStanceId => python ? PredefinedFunctionIds.PyGetStance : PredefinedFunctionIds.GetStance;
	string InitStanceId => python ? PredefinedFunctionIds.PyInitStance : PredefinedFunctionIds.InitStance;
	string PrintId => python ? PredefinedFunctionIds.PyPrint : PredefinedFunctionIds.Printf;
	BlockData Store(string name, string functionId) => python
		? BlockData.Assign(name, SlotData.FromBlock(Call(functionId)))
		: BlockData.Declare(Int, name, SlotData.FromBlock(Call(functionId)));

	// get_stance: s = get_stance() 후 출력
	IEnumerator TestGetStance()
	{
		stance.SetStance(37);
		yield return Run("get_stance 37", Store("s", GetStanceId), Call(PrintId, SlotData.FromVariable("s")));
		Check("get_stance는 지금 자세(37)", Completed() && Output() == "s = 37", Detail());

		stance.Clear();
		yield return Run("get_stance 0", Store("s", GetStanceId), Call(PrintId, SlotData.FromVariable("s")));
		Check("자세가 없으면 get_stance는 0", Completed() && Output() == "s = 0", Detail());
	}

	// init_stance: n = init_stance() 후 출력 → 2.5초(125 tick) 뒤 1~100, 자세도 그 숫자, 쿨타임 3초(모든 스킬 쿨타임 증감만 받음)
	IEnumerator TestInitStance()
	{
		int startTick = executor.CurrentTick;
		yield return Run("init_stance", Store("n", InitStanceId), Call(PrintId, SlotData.FromVariable("n")));
		int elapsed = executor.CurrentTick - startTick;
		string text = Output();
		int n = -1;
		if (text.StartsWith("n = ")) int.TryParse(text.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
		Check("init_stance는 1~100을 돌려주고 자세도 그 숫자", Completed() && n >= 1 && n <= 100 && stance.Stance == n, $"{Detail()}, 자세 {stance.Stance}");
		Check($"init_stance는 {InitStanceFunction.CastTicks} tick 넘게 걸림", elapsed >= InitStanceFunction.CastTicks, $"{elapsed} tick");

		PredefinedFunctionRegistry.TryGet(InitStanceId, out PredefinedFunction init);
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
		LanguageFamily family = python ? LanguageFamily.Python : LanguageFamily.C;
		if (!FunctionDataConverter.TryConvert(data, Array.Empty<FunctionData>(), family, out UserFunction function))
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
