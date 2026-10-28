/// <summary>
/// 스킬이 실행 중에 낸 에러의 기반 클래스. 에러 하나가 클래스 하나다.
/// <para>
/// 사용법<br/>
/// - 스킬 구현자가 이 클래스를 상속해 에러 종류를 만든다. 함께 알릴 정보(예: 필요한 소환검 수)는 필드로 둔다.<br/>
/// - 스킬의 OnStart나 OnTick에서 PredefinedFunction.Fail(new 에러())를 부른다. 에러 객체는 Fail을 부를 때마다 새로 만든다.
/// </para>
/// <para>
/// 에러가 났을 때 무엇을 할지는 이 클래스가 정하지 않는다. 캐릭터 언어의 언어 규칙 표가 SkillFailed 한 줄로 정한다.<br/>
/// 같은 에러라도 언어마다 처리(C는 그 작업만 끝냄, Java는 try로 잡음 등)를 다르게 하기 위함이다.
/// </para>
/// </summary>
public abstract class SkillError
{
	/// <summary>
	/// 로그와 화면에 보여 줄 설명. 덮어쓰지 않으면 클래스 이름을 쓴다.
	/// </summary>
	public virtual string Message => GetType().Name;

	/// <summary>
	/// 이 에러를 낸 스킬의 이름. PredefinedFunction.Fail이 채운다.
	/// </summary>
	// 작업 이름은 맨 바깥 함수의 이름이라, 안쪽의 어느 스킬에서 에러가 났는지 알 수 없기 때문에 따로 기록한다
	// 한 에러 객체를 여러 스킬이 함께 쓰면 이 값이 덮어써지므로, 에러 객체는 Fail마다 새로 만든다
	public string SourceName { get; internal set; }
}
