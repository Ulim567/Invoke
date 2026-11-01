using UnityEngine;

/// <summary>
/// [임시 시험용, #143] 실행 중단 시험 키(F9)를 누르면 이 캐릭터의 실행 중인 작업을 모두 끊는다(FunctionExecutor.InterruptAll).<br/>
/// 몬스터의 실행 중단 공격이 아직 없어서 둔다. 시험할 때만 Player_UL 프리팹에 붙이고 Host로 Play한 뒤, Game 창을 클릭하고 F9를 누른다.<br/>
/// 시험이 끝나면 뗀다. 붙여 두면 Host로 Play할 때마다 작업 3개가 시전되어 다른 시험의 memory 값이 달라지기 때문이다.
/// <para>
/// 내 캐릭터가 생기면 끊을 작업을 스스로 시전한다. Player_UL에는 슬롯 시전(SlotCaster)이 없어 스킬을 쓸 수 없기 때문이다.<br/>
/// - InterruptKeyNested: 사용자 정의 함수 안의 TestHoldMallocFunction(약 1분, 힙 2칸 malloc). 끊으면 그 객체는 누수<br/>
/// - InterruptKeyLoop: while (true) { } 사용자 정의 함수. 끝나지 않는다<br/>
/// - TestLongMallocFunction: 20틱 만에 정상 종료해 객체가 숨은 시스템 전역에 남는다. 끊어도 누수가 아님<br/>
/// 끊기 전 스택 3칸·힙 4칸, 끊은 뒤 스택 0칸·힙 4칸(누수 2칸)이 memory 게이지와 FunctionExecutorLogger의 「끝 (Interrupted)」 로그로 보인다.<br/>
/// 다시 시험하려면 Play를 다시 시작한다.
/// </para>
/// 몬스터의 실행 중단 공격이 생기면 이 컴포넌트, TestHoldMallocFunction, PlayerInputReader.InterruptTestPressed, Debug 액션 맵을 함께 지운다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor), typeof(PlayerInputReader))]
public class InterruptKeyTest : MonoBehaviour
{
	FunctionExecutor executor;
	PlayerInputReader input;
	// 끊을 작업을 이미 시전했는지. 내 캐릭터가 된 뒤 한 번만 시전한다
	bool casted;

	void Awake()
	{
		executor = GetComponent<FunctionExecutor>();
		input = GetComponent<PlayerInputReader>();
	}

	void Update()
	{
		// 다른 플레이어의 캐릭터에서는 입력이 연결되지 않아 값을 읽으면 오류가 나므로, 내 캐릭터에서만 읽는다
		// isLocalPlayer는 Start 때 아직 정해지지 않았을 수 있어, Update에서 처음 true가 될 때 시전한다
		if (!input.isLocalPlayer) return;

		if (!casted)
		{
			casted = true;
			CastTasks();
		}

		if (!input.InterruptTestPressed) return;

		Debug.Log($"[InterruptKeyTest] {name}: 실행 중단 (틱 {executor.CurrentTick})");
		// 작업을 끊는 일은 값을 바꾸는 일이다. 서버와 클라이언트가 나뉜 플레이에서는 서버에서 불러야 하지만, 시험용이라 Host에서만 쓴다
		executor.InterruptAll();
	}

	// 서로 다른 스킬만 쓴다. 같은 스킬은 동시에 실행되지 않고 기다려서, 끊을 때 진행 중이 아닐 수 있기 때문
	void CastTasks()
	{
		executor.Cast(new UserFunction("InterruptKeyNested", new TestHoldMallocFunction()));
		executor.Cast(new UserFunction("InterruptKeyLoop",
			new WhileBlock(new LiteralValue(new VariableType(VariableTypeKind.Bool), "true"))));
		executor.Cast(new TestLongMallocFunction());
		Debug.Log($"[InterruptKeyTest] {name}: 끊을 작업 3개를 시전했습니다. Game 창을 클릭하고 F9를 누르십시오.");
	}
}
