/// <summary>
/// 값 칸 하나를 읽어 값(RuntimeValue) 하나를 내놓는 블록의 기반 클래스.<br/>
/// 직접 입력한 값(LiteralValue), 변수(VariableValue), 연산 블록, malloc 블록, 값을 돌려주는 함수 호출이 이 클래스를 상속한다.
/// <para>
/// 사용법 (값 칸을 가진 블록이 쓰는 법)<br/>
/// - 실행을 시작할 때 값 칸의 원본을 Clone으로 복사한다.<br/>
/// - 틱마다 복사본의 Execute를 부르고, Done이 나오면 Result를 읽는다. Running이면 다음 틱에 다시 부르고, Failed이면 바깥에도 Failed를 돌려준다.<br/>
/// - 바깥 블록이 끊기면 복사본의 Cancel도 부른다.
/// </para>
/// <para>
/// 틱 규칙: 플레이어가 놓은 블록 하나 = 1틱<br/>
/// - 연산 블록처럼 플레이어가 값 칸에 끼운 블록은 자기 틱을 쓴다(UsesTick = true). 바깥 블록은 그 다음 틱에 자기 일을 한다.<br/>
/// - 직접 입력한 값과 변수는 블록이 아니므로 틱을 쓰지 않는다(UsesTick = false). 바깥 블록은 같은 틱 안에서 계속 진행한다.<br/>
/// - 값 칸이 여럿이면 왼쪽부터 차례로 실행한다. 바깥 블록은 ValueSlots로 이 규칙을 지킨다.
/// </para>
/// </summary>
// Block을 상속하는 이유: 값 칸에 끼운 스킬은 여러 틱 동안 실행되고 도중에 끊기면 OnCancel도 받아야 한다. Block이 이미 틱 단위 실행과 끊기를 가지고 있기 때문
public abstract class ValueBlock : Block
{
	/// <summary>
	/// 읽은 값. Execute가 Done을 돌려준 뒤에만 쓴다.
	/// </summary>
	public RuntimeValue Result { get; protected set; }

	// 플레이어가 놓은 블록이면 true. Done을 돌려준 틱을 이 블록이 쓴 것으로 보고, 바깥 블록은 다음 틱에 진행한다
	internal virtual bool UsesTick => true;
}
