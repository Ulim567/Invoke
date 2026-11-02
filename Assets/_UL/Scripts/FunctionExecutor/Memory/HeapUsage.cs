/// <summary>
/// 힙 칸 수를 주인별로 나눈 값. HeapStorage.GetUsageByOwner가 돌려준다.<br/>
/// 네 값의 합은 ProcessMemory.HeapUsed와 같다.
/// </summary>
public readonly struct HeapUsage
{
	/// <summary>플레이어의 전역 변수가 가리키는 객체의 칸 수</summary>
	public int PlayerGlobals { get; }

	/// <summary>숨은 시스템 전역(예: 소환검)이 가진 객체의 칸 수</summary>
	public int SystemGlobals { get; }

	/// <summary>진행 중인 작업이 malloc했거나 지역 변수에 담은 객체의 칸 수</summary>
	public int TaskLocals { get; }

	/// <summary>누수된 객체와, 아무도 쥐지 않아 다음 누수 판단 때 누수가 될 객체의 칸 수</summary>
	public int Leaked { get; }

	public HeapUsage(int playerGlobals, int systemGlobals, int taskLocals, int leaked)
	{
		PlayerGlobals = playerGlobals;
		SystemGlobals = systemGlobals;
		TaskLocals = taskLocals;
		Leaked = leaked;
	}

	public override string ToString() => $"플레이어 전역 {PlayerGlobals}, 시스템 전역 {SystemGlobals}, 지역 {TaskLocals}, 누수 {Leaked}";
}
