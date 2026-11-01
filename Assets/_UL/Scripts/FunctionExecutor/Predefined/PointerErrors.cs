// 포인터 관련 스킬 에러. PredefinedFunction의 heap 함수(Free, TryGetHeapObject)가 낸다

/// <summary>
/// 쓸 수 없는 포인터로 객체를 꺼내거나 해제했다.<br/>
/// NullPointer, 해제한 포인터, 누수된 포인터, 타입이 다른 객체의 포인터일 때 난다.
/// </summary>
// 「해제한 포인터 사용 시 패널티」가 아직 정해지지 않아 한 종류로 둔다. 종류를 나눌 때는 이 클래스를 상속한다
public class InvalidPointerError : SkillError
{
	/// <summary>
	/// 쓸 수 없었던 포인터 값.
	/// </summary>
	public int Pointer { get; }

	public InvalidPointerError(int pointer)
	{
		Pointer = pointer;
	}

	public override string Message => $"잘못된 포인터 {Pointer}";
}
