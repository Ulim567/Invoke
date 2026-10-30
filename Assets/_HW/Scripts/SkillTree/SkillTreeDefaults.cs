using System.Collections.Generic;

/// <summary>
/// 기획서대로 만든 스킬 트리 처음 값. SkillTree 에셋의 「기획서대로 채우기」에서만 쓴다.
/// 에셋을 채운 뒤에는 에셋 값이 기준이다. 여기를 고쳐도 이미 만든 에셋은 바뀌지 않는다.
/// <para>
/// - 문법 노드: 1차 기획서 4.3 해금 요소. 순차 실행 → 변수 → 연산 → if문 → 논리 연산 → for문 → while문 → 함수 → list → Type 정의<br/>
/// - 스킬 노드: 「C 캐릭터 - 1차」 2. 해금 구조, 3. 스킬트리<br/>
/// - 재료 수는 전부 임시. bug_fragment(버그 조각, 잡몹), field_boss_core(사냥터 보스 코어), boss_core(레이드 보스 코어)<br/>
/// - 박진우와 맞춤 (#91): 패링은 검 소환 없이 재료만으로, 검 소환은 사냥터 보스 재료가 있어야 열림
/// </para>
/// </summary>
public static class SkillTreeDefaults
{
	/// <summary>잡몹에게서 얻는 재료 (임시 ID)</summary>
	public const string BugFragment = "bug_fragment";

	/// <summary>사냥터 보스에게서 얻는 재료 (임시 ID). 검 소환처럼 게임이 좀 진행된 뒤에 열 노드에 쓴다. 어느 보스인지는 기획에서 정한다</summary>
	public const string FieldBossCore = "field_boss_core";

	/// <summary>레이드 보스에게서 얻는 재료 (임시 ID). 마지막 단계 스킬에 쓴다</summary>
	public const string BossCore = "boss_core";

	// 문법 노드 ID
	public const string Sequence = "syntax.sequence";
	public const string Variable = "syntax.variable";
	public const string Arithmetic = "syntax.arithmetic";
	public const string If = "syntax.if";
	public const string Logic = "syntax.logic";
	public const string For = "syntax.for";
	public const string While = "syntax.while";
	public const string Function = "syntax.function";
	public const string List = "syntax.list";
	public const string TypeDefinition = "syntax.type";

	public static List<SkillTreeNode> Create()
	{
		var nodes = new List<SkillTreeNode>();

		// 문법 ----------------------------------------------------------------
		// 각 문법은 앞 단계 문법이 선행 조건이다
		nodes.Add(Syntax(Sequence, "순차 실행", null, 0, new[] { BlockKind.Function, BlockKind.Wait },
			"처음부터. 블록을 위에서부터 차례로 실행. C 스킬 블록, 기다리기"));
		nodes.Add(Syntax(Variable, "변수", Sequence, 2, new[] { BlockKind.Declare, BlockKind.Assign }));
		nodes.Add(Syntax(Arithmetic, "연산", Variable, 3, new[] { BlockKind.Arithmetic }));
		nodes.Add(Syntax(If, "if문", Arithmetic, 4, new[] { BlockKind.Conditional, BlockKind.Compare },
			"비교(> < ==)는 조건에 쓰므로 if문과 같이 연다 (가정)"));
		nodes.Add(Syntax(Logic, "논리 연산", If, 5, new[] { BlockKind.Logic, BlockKind.Not }));
		nodes.Add(Syntax(For, "for문", Logic, 6, new[] { BlockKind.For, BlockKind.Break, BlockKind.Continue },
			"break, continue는 첫 반복문과 같이 연다 (가정)"));
		nodes.Add(Syntax(While, "while문", For, 7, new[] { BlockKind.While }));
		nodes.Add(Syntax(Function, "함수", While, 8, new[] { BlockKind.Return },
			"사용자 정의 함수 만들기·부르기, return. 함수 블록(BlockKind.Function)은 C 스킬과 같은 종류라 여기서 막지 않는다"));
		nodes.Add(Syntax(List, "list", Function, 10, new[] { BlockKind.ArrayDeclare }, "배열 만들기 블록을 같이 연다 (가정)"));
		nodes.Add(Syntax(TypeDefinition, "Type 정의", List, 12, null, "struct 만들기. 블록이 아직 없음"));

		// 찌르기 --------------------------------------------------------------
		nodes.Add(Start(PredefinedFunctionIds.Thrust));
		nodes.Add(Skill(PredefinedFunctionIds.FocusedThrust, 3, PredefinedFunctionIds.Thrust));
		nodes.Add(Skill(PredefinedFunctionIds.VitalStrike, 6, PredefinedFunctionIds.FocusedThrust));
		nodes.Add(Skill(PredefinedFunctionIds.Skewer, 4, PredefinedFunctionIds.Thrust));
		nodes.Add(Skill(PredefinedFunctionIds.SummonedThrust, 5, PredefinedFunctionIds.Thrust, PredefinedFunctionIds.SummonSword));
		// 상처 공략과 검 깊이찌르기는 같은 층. 둘 다 소환검 찌르기 다음이고, 어느 쪽을 먼저 열지는 플레이어가 고른다
		nodes.Add(Skill(PredefinedFunctionIds.WoundExploit, 6, PredefinedFunctionIds.SummonedThrust));
		nodes.Add(Skill(PredefinedFunctionIds.DeepThrust, 6, PredefinedFunctionIds.SummonedThrust));

		// 던지기 --------------------------------------------------------------
		nodes.Add(Start(PredefinedFunctionIds.SwordThrow));
		nodes.Add(Skill(PredefinedFunctionIds.SummonedThrow, 5, PredefinedFunctionIds.SwordThrow, PredefinedFunctionIds.SummonSword));
		nodes.Add(Skill(PredefinedFunctionIds.SwordReturn, 6, PredefinedFunctionIds.SummonedThrow));

		// 휘두르기 (검 소환 해금 후) ----------------------------------------------
		nodes.Add(Skill(PredefinedFunctionIds.SwordSlash, 4, PredefinedFunctionIds.SummonSword));
		nodes.Add(Skill(PredefinedFunctionIds.WideSlash, 5, PredefinedFunctionIds.SwordSlash));
		nodes.Add(Skill(PredefinedFunctionIds.SummonedSlash, 6, PredefinedFunctionIds.WideSlash));
		nodes.Add(Skill(PredefinedFunctionIds.WoundSlash, 6, PredefinedFunctionIds.WideSlash));
		// 검 다중베기는 소환검 휘두르기와 상처 가르기를 둘 다 해금해야 열린다
		nodes.Add(Skill(PredefinedFunctionIds.MultiSlash, 8, PredefinedFunctionIds.SummonedSlash, PredefinedFunctionIds.WoundSlash));
		nodes.Add(Skill(PredefinedFunctionIds.BattlefieldSweep, 10, 1, PredefinedFunctionIds.MultiSlash));

		// 소환 ----------------------------------------------------------------
		// 검 소환은 초반에 열리지 않게 사냥터 보스 재료로 막는다 (박진우와 맞춤. 레벨·진행도가 생기면 그 조건으로 바꾼다)
		var summonCost = new List<MaterialCost>(Cost(8, 0)) { new MaterialCost(FieldBossCore, 1) };
		nodes.Add(new SkillTreeNode(PredefinedFunctionIds.SummonSword, SkillTreeNodeKind.Skill, null, null, false,
			summonCost.ToArray(), new[] { BlockKind.Malloc, BlockKind.Free },
			"스킬 선행 없음. 사냥터 보스 재료로 막음. malloc·free 블록도 같이 연다 (malloc → 검 소환)"));
		// 검 소환을 해금하면 해제가 같이 열린다
		nodes.Add(Together(PredefinedFunctionIds.FreeSword, PredefinedFunctionIds.SummonSword));
		// 패링은 소환검을 쓰지 않으므로 검 소환 전에도 재료만으로 연다 (박진우와 맞춤)
		nodes.Add(new SkillTreeNode(PredefinedFunctionIds.Parry, SkillTreeNodeKind.Skill, null, null, false, Cost(4, 0), null,
			"되돌리는 검은 소환검과 따로 생기므로 선행 없음")); 
		nodes.Add(Skill(PredefinedFunctionIds.Mark, 6, PredefinedFunctionIds.SummonSword));
		nodes.Add(Skill(PredefinedFunctionIds.SwordDance, 10, 1, PredefinedFunctionIds.Mark));

		// 이동 ----------------------------------------------------------------
		nodes.Add(new SkillTreeNode(PredefinedFunctionIds.Dash, SkillTreeNodeKind.Skill, null, null, false, Cost(2, 0), null,
			"저렙 구간에 연다. 레벨이 아직 없어서 선행 없이 적은 재료로 둠"));
		nodes.Add(Skill(PredefinedFunctionIds.DoubleJump, 4, PredefinedFunctionIds.Dash));
		nodes.Add(Skill(PredefinedFunctionIds.SwordRide, 8, PredefinedFunctionIds.DoubleJump, PredefinedFunctionIds.SummonSword));

		return nodes;
	}

	static SkillTreeNode Syntax(string id, string name, string prerequisite, int fragments, BlockKind[] blocks, string note = null)
	{
		bool start = prerequisite == null;
		return new SkillTreeNode(id, SkillTreeNodeKind.Syntax, name,
			start ? null : new[] { prerequisite }, start, start ? null : Cost(fragments, 0), blocks, note);
	}

	// 처음부터 해금된 스킬
	static SkillTreeNode Start(string id) =>
		new SkillTreeNode(id, SkillTreeNodeKind.Skill, null, null, true, null, null, "처음부터");

	// 선행 노드를 해금하면 재료 없이 같이 열리는 스킬
	static SkillTreeNode Together(string id, string prerequisite) =>
		new SkillTreeNode(id, SkillTreeNodeKind.Skill, null, new[] { prerequisite }, true, null, null, "검 소환 해금 시 같이 열림");

	static SkillTreeNode Skill(string id, int fragments, params string[] prerequisites) =>
		new SkillTreeNode(id, SkillTreeNodeKind.Skill, null, prerequisites, false, Cost(fragments, 0));

	// 마지막 단계 스킬: 보스 재료도 든다
	static SkillTreeNode Skill(string id, int fragments, int cores, params string[] prerequisites) =>
		new SkillTreeNode(id, SkillTreeNodeKind.Skill, null, prerequisites, false, Cost(fragments, cores));

	static MaterialCost[] Cost(int fragments, int cores)
	{
		var cost = new List<MaterialCost>();
		if (fragments > 0) cost.Add(new MaterialCost(BugFragment, fragments));
		if (cores > 0) cost.Add(new MaterialCost(BossCore, cores));
		return cost.ToArray();
	}
}
