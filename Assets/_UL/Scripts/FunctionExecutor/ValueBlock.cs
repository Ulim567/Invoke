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
/// 같은 틱 안에서 Execute를 불러 Done이 나오면 바깥 블록은 그 틱 안에서 계속 진행한다.<br/>
/// 그래서 바로 Done을 돌려주는 값 블록(직접 입력한 값, 변수)은 틱을 따로 쓰지 않는다.
/// </para>
/// </summary>
// Block을 상속하는 이유: 값 칸에 끼운 스킬은 여러 틱 동안 실행되고 도중에 끊기면 OnCancel도 받아야 한다. Block이 이미 틱 단위 실행과 끊기를 가지고 있기 때문
public abstract class ValueBlock : Block
{
	/// <summary>
	/// 읽은 값. Execute가 Done을 돌려준 뒤에만 쓴다.
	/// </summary>
	public RuntimeValue Result { get; protected set; }
}
