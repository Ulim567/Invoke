using Mirror;

/// <summary>
/// 내 화면 → 서버: [테스트용] 그 언어 캐릭터의 진행도를 json(CharacterProgressData)으로 모두 바꿔 달라는 요청.
/// 캐릭터 화면(로그인 뒤, 들어가기 전)에서만 받는다. ProgressTestData가 InvokeAuthenticator.RequestProgressTestData로 보낸다.
/// </summary>
public struct ProgressTestDataRequest : NetworkMessage
{
	public string language;
	public string json;
}

/// <summary>
/// 서버 → 내 화면: 테스트용 진행도 넣기의 결과. 실패면 reason에 이유가 있다.
/// </summary>
public struct ProgressTestDataResult : NetworkMessage
{
	public bool success;
	public string reason;
}
