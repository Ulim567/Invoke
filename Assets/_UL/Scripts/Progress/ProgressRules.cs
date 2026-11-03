/// <summary>
/// 진행도 저장의 규칙 숫자. 서버와 클라이언트가 함께 읽는다.
/// <para>
/// 편집 화면은 저장 요청을 보내기 전에 이 값으로 먼저 안내하고, 서버도 같은 값으로 다시 검사한다.
/// </para>
/// </summary>
public static class ProgressRules
{
	/// <summary>캐릭터 하나가 가질 수 있는 사용자 정의 함수의 최대 개수 (임시).</summary>
	public const int MaxFunctions = 5;
}

/// <summary>소유 클라이언트가 서버에 보내는 플레이어 데이터 요청의 종류.</summary>
public enum PlayerDataRequest
{
	SaveFunction,
	ChangeSlots,
	SaveGlobals,
}

/// <summary>플레이어 데이터 요청의 결과.</summary>
public enum PlayerDataRequestResult
{
	Saved,
	NotAllowed,       // 서버 DB를 쓰지 않는 플레이어(임시 계정, 네트워크를 켜지 않은 테스트)
	InvalidData,      // JSON을 읽을 수 없음, GUID가 없음
	TooManyFunctions, // 새 함수인데 이미 ProgressRules.MaxFunctions개
	InvalidSlot,      // 슬롯 번호가 0~7이 아님
	UnknownFunction,  // 이 캐릭터에 없는 함수를 장착하려 함
	NotEquippable,    // 장착할 수 없는 스킬 (없는 ID, 입력 함수)
	GlobalsRejected,  // 전역 변수 검사에 걸림 (칸 부족 등. 이유는 서버 콘솔 오류)
}
