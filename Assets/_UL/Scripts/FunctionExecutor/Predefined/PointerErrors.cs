// 포인터 관련 스킬 에러. PredefinedFunction의 heap 함수(Free, TryGetHeapObject)가 낸다
// HUD 알림 첫 줄에 클래스 이름이, 둘째 줄에 Message가 그대로 보인다(#480). 이름을 바꾸면 플레이어가 보는 글자도 바뀐다

/// <summary>
/// 쓸 수 없는 포인터로 객체를 꺼내거나 해제했다.<br/>
/// 누수된 포인터, 준 적 없는 값, 타입이 다른 객체의 포인터일 때 이 클래스 그대로 난다.
/// NULL은 NullPointerError, 이미 free한 포인터는 FreedPointerError로 난다 (#504).<br/>
/// 스킬이 포인터 에러인지 확인할 때는 is InvalidPointerError로 본다. 두 하위 클래스도 걸린다.
/// </summary>
// 종류를 이 클래스의 하위 클래스로 나누는 이유: 포인터 에러를 한꺼번에 확인하던 코드(박진우 SwordPointerSimTest 등)를 고치지 않기 위함
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

	public override string Message => $"사용할 수 없는 포인터: {Pointer}";

	/// <summary>
	/// heap에서 pointer를 쓰지 못한 이유에 맞는 에러를 만든다. NULL이면 NullPointerError, 이미 free한 포인터면 FreedPointerError, 그 밖에는 InvalidPointerError.
	/// </summary>
	public static InvalidPointerError For(HeapStorage heap, int pointer)
	{
		if (pointer == HeapStorage.NullPointer) return new NullPointerError();
		if (heap.WasFreed(pointer)) return new FreedPointerError(pointer);
		return new InvalidPointerError(pointer);
	}
}

/// <summary>
/// 스킬에 NULL 포인터를 넘겼다 (예: 소환검이 없어 NULL을 받은 Sword* 변수를 소환검 던지기에 넘김).
/// </summary>
public class NullPointerError : InvalidPointerError
{
	public NullPointerError() : base(HeapStorage.NullPointer) { }

	public override string Message => "스킬에 NULL 포인터 전달";
}

/// <summary>
/// 스킬에 이미 free한 포인터를 넘겼다 (예: free(s) 뒤에 s를 소환검 찌르기에 넘김).
/// </summary>
public class FreedPointerError : InvalidPointerError
{
	public FreedPointerError(int pointer) : base(pointer) { }

	public override string Message => "스킬에 이미 free한 포인터 전달";
}
