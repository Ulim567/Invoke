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

		foreach (SkillTreeNode node in nodes)
		{
			SkillTreeNodeDescription description = DescriptionOf(node.Id);
			if (description != null) node.SetDescription(description);
		}
		return nodes;
	}

	/// <summary>
	/// 노드의 처음 설명. 없으면 null. 「기획서대로 채우기」와 「비어 있는 설명만 채우기」(SkillTree 에셋 메뉴)가 쓴다.
	/// </summary>
	public static SkillTreeNodeDescription DescriptionOf(string id) =>
		id != null && Descriptions.TryGetValue(id, out (string summary, string detail) d) ? new SkillTreeNodeDescription(d.summary, d.detail) : null;

	// 노드 설명 (짧은 설명: 문법 탭의 노드 옆 한 문장, 자세한 설명: 오른쪽 설명 칸)
	// - 문법: 블록 편집기의 블록 모양(BlockLabels)과 실행 규칙(배열 칸 번호는 0부터, ArrayStorage.IsInRange) 기준
	// - 스킬: SkillTable(박진우)의 note 기준. 임시 배율 숫자는 적지 않는다 (바뀌면 설명이 틀려지므로)
	// - 스킬 설명에는 스킬이 하는 일만 적는다. 해금·같이 열리는 노드·여는 블록은 설명 칸의 다른 줄이 보여 준다
	static readonly Dictionary<string, (string summary, string detail)> Descriptions = new Dictionary<string, (string, string)>
	{
		// 문법 ---------------------------------------------------------------
		[Sequence] = ("블록을 위에서 아래로 하나씩 차례대로 실행합니다.",
			"함수 body에 놓인 블록은 위에서부터 한 개씩 실행됩니다. C 스킬 블록을 이어 놓으면 차례대로 시전하고, wait 블록으로 다음 블록까지 기다릴 수 있습니다."),
		[Variable] = ("값에 이름을 붙여 담아 두고 다시 씁니다.",
			"선언 블록(int x = 3;)으로 변수를 만들고, 대입 블록(x = 5;)으로 담긴 값을 바꿉니다. 타입은 int, float, bool, char, string 중에서 고릅니다."),
		[Arithmetic] = ("+ - * / % 로 값을 계산합니다.",
			"연산 블록은 값 칸에 끼워 씁니다. 두 값을 더하고(+), 빼고(-), 곱하고(*), 나누고(/), 나머지(%)를 구합니다."),
		[If] = ("조건이 참일 때만 안쪽 블록을 실행합니다.",
			"if 블록의 조건 칸에 비교 블록(> < == != >= <=)을 끼워 씁니다. 조건이 거짓이면 안쪽을 건너뛰고, 바로 아래에 else if나 else를 붙여 다른 경우를 이어서 고를 수 있습니다."),
		[Logic] = ("&& || ! 로 여러 조건을 하나로 합칩니다.",
			"&&는 두 조건이 모두 참일 때, ||는 하나라도 참일 때 참입니다. Not(!)은 참과 거짓을 뒤집습니다."),
		[For] = ("정해진 횟수만큼 안쪽 블록을 반복합니다.",
			"for (int i = 0; i < 10; i++)처럼 i가 끝 값에 닿을 때까지 반복합니다. break는 반복을 바로 끝내고, continue는 이번 차례를 건너뛰고 다음 반복으로 넘어갑니다."),
		[While] = ("조건이 참인 동안 안쪽 블록을 계속 반복합니다.",
			"반복할 횟수를 미리 모를 때 씁니다. 조건이 거짓이 되지 않으면 반복이 끝나지 않으니, 안쪽에서 조건을 바꾸거나 break로 빠져나오세요."),
		[Function] = ("블록 묶음에 이름을 붙여 두고 필요할 때 부릅니다.",
			"페이지에 만든 함수는 다른 함수 안에서 함수 블록으로 부를 수 있습니다. return 블록으로 함수를 끝내고, 부른 쪽에 값을 돌려줍니다."),
		[List] = ("같은 타입의 값 여러 개를 한 줄로 담습니다.",
			"배열 만들기 블록(int arr[3];)으로 칸 수를 정해 만들고, arr[i]로 i번째 칸을 읽거나 바꿉니다. 칸 번호는 0부터 셉니다."),
		[TypeDefinition] = ("여러 변수를 하나로 묶은 나만의 타입(struct)을 만듭니다.",
			"서로 관련된 값을 한 이름으로 묶어 다룹니다. 이 문법으로 쓰는 블록은 아직 준비 중입니다."),

		// 찌르기 ---------------------------------------------------------------
		[PredefinedFunctionIds.Thrust] = ("대상 한 명을 검으로 찌릅니다.",
			"가장 기본이 되는 찌르기입니다. 대상 한 명을 골라 찌릅니다."),
		[PredefinedFunctionIds.FocusedThrust] = ("출혈 중인 대상을 더 세게 찌릅니다.",
			"대상 한 명을 찌릅니다. 대상이 출혈 상태면 더 큰 피해를 줍니다."),
		[PredefinedFunctionIds.VitalStrike] = ("미니게임으로 급소를 노려 큰 피해를 줍니다.",
			"미니게임에 성공하면 큰 피해를 줍니다. 눈을 맞히면 대상이 실명하고, 발목을 맞히면 대상의 이동 속도가 느려집니다."),
		[PredefinedFunctionIds.Skewer] = ("여러 적을 한꺼번에 꿰뚫어 찌릅니다.",
			"찌르기가 대상을 관통해 여러 명을 동시에 찌릅니다. 한 명이 받는 피해는 약합니다."),
		[PredefinedFunctionIds.SummonedThrust] = ("소환검에게 명령해 대상을 찌르게 합니다.",
			"소환해 둔 검이 대상 한 명을 찌릅니다."),
		[PredefinedFunctionIds.WoundExploit] = ("출혈 중인 대상의 상처를 노려 찌릅니다.",
			"대상 한 명을 찌릅니다. 대상이 출혈 상태면 검 집중찌르기보다 더 큰 피해를 줍니다."),
		[PredefinedFunctionIds.DeepThrust] = ("깊숙이 찔러 치명타 피해를 줍니다.",
			"대상 한 명을 깊이 찔러 치명타로 큰 피해를 줍니다."),

		// 던지기 ---------------------------------------------------------------
		[PredefinedFunctionIds.SwordThrow] = ("검을 던져 대상 한 명을 맞춥니다.",
			"들고 있는 검을 던집니다. 체력이 아주 조금 남은 적은 바로 처치합니다(보스 제외). 던진 검을 다시 주울 때까지는 손에 검이 없어 공격 스킬을 쓸 수 없습니다."),
		[PredefinedFunctionIds.SummonedThrow] = ("소환검을 던져 더 먼 적까지 맞춥니다.",
			"소환검을 대상 한 명에게 던집니다. 검 던지기보다 더 멀리 있는 적도 맞힙니다."),
		[PredefinedFunctionIds.SwordReturn] = ("던진 소환검을 차례대로 되돌려 받습니다.",
			"던진 소환검을 먼저 던진 것부터 하나씩 되돌려 받습니다. 돌아오는 길에 있는 적에게 피해를 줍니다. 손에 검이 없을 때도 쓸 수 있습니다."),

		// 휘두르기 --------------------------------------------------------------
		[PredefinedFunctionIds.SwordSlash] = ("검을 휘둘러 검기를 날립니다.",
			"대상 한 명에게 검기를 날립니다. 검기는 heap 메모리를 씁니다."),
		[PredefinedFunctionIds.WideSlash] = ("넓게 베어 주변 적에게 출혈을 겁니다.",
			"넓은 범위에 검기를 날립니다. 피해는 낮지만 맞은 적에게 출혈을 겁니다."),
		[PredefinedFunctionIds.SummonedSlash] = ("소환된 검의 수만큼 검기를 날립니다.",
			"소환해 둔 검 하나마다 검기를 하나씩 대상에게 날립니다."),
		[PredefinedFunctionIds.WoundSlash] = ("상처를 갈라 출혈 피해를 키웁니다.",
			"대상 한 명을 벱니다. 대상이 출혈 상태면 출혈로 받는 피해가 커집니다."),
		[PredefinedFunctionIds.MultiSlash] = ("소환된 검들이 대상을 한 번씩 벱니다.",
			"소환해 둔 검이 각각 대상 한 명을 한 번씩 벱니다. 검이 많을수록 더 여러 번 벱니다."),
		[PredefinedFunctionIds.BattlefieldSweep] = ("소환된 검들이 여러 적에게 흩어져 벱니다.",
			"여러 대상을 고르면, 소환해 둔 검들이 각 대상에게 날아가 벱니다."),

		// 소환 ----------------------------------------------------------------
		[PredefinedFunctionIds.SummonSword] = ("검을 불러내 소환검으로 만듭니다.",
			"malloc처럼 검 하나를 만들고 그 검을 가리키는 포인터를 돌려줍니다. 소환검은 최대 5개까지 둘 수 있습니다."),
		[PredefinedFunctionIds.FreeSword] = ("소환검 하나를 해제해 거둡니다.",
			"free처럼 소환검을 없앱니다. 거리에 상관없이 해제할 수 있지만, 들고 있는 물리 검은 해제할 수 없습니다."),
		[PredefinedFunctionIds.Parry] = ("적의 공격을 받아쳐 되돌려 줍니다.",
			"막는 데 성공하면 적의 공격을 검 이펙트로 한 번 되돌려 줍니다. 손에 검이 있어야 쓸 수 있습니다."),
		[PredefinedFunctionIds.Mark] = ("적에게 표식을 남기고 잠시 기절시킵니다.",
			"검이 적의 좌우를 찔러 표식을 남기고 1초 동안 기절시킵니다. 소환검이 있어야 쓸 수 있습니다."),
		[PredefinedFunctionIds.SwordDance] = ("소환검들이 적 곁에서 휘둘러 출혈을 겁니다.",
			"검 개수만큼 적 근처에 검을 두고 휘두릅니다. 피해는 낮지만 출혈을 겁니다."),

		// 이동 ----------------------------------------------------------------
		[PredefinedFunctionIds.Dash] = ("짧은 거리를 빠르게 이동합니다.",
			"짧은 거리를 빠르게 이동합니다. 모든 캐릭터가 함께 쓰는 이동 기술입니다."),
		[PredefinedFunctionIds.DoubleJump] = ("공중에서 한 번 더 점프합니다.",
			"점프한 뒤 공중에서 한 번 더 점프합니다. 모든 캐릭터가 함께 쓰는 이동 기술입니다."),
		[PredefinedFunctionIds.SwordRide] = ("소환검을 타고 이동합니다.",
			"소환검 위에 올라타 이동합니다. 쓸 수 있는 소환검이 없으면 쓸 수 없습니다."),
	};

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
