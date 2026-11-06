/// <summary>
/// 진행도 저장의 규칙 숫자. 서버와 클라이언트가 함께 읽는다.
/// <para>
/// 편집 화면은 저장 요청을 보내기 전에 이 값으로 먼저 안내하고, 서버도 같은 값으로 다시 검사한다.
/// </para>
/// </summary>
public static class ProgressRules
{
	/// <summary>
	/// 캐릭터 하나가 가질 수 있는 페이지 함수(슬롯에 걸리지 않은 함수)의 최대 개수 (임시).<br/>
	/// 슬롯 함수 8개(slot1~slot8)는 서버가 캐릭터를 만들 때 함께 만들고, 이 개수에 들어가지 않는다.
	/// </summary>
	public const int MaxPageFunctions = 5;

	/// <summary>
	/// 캐릭터의 코드 영역 최대 크기 (임시). 슬롯 함수 8개와 그 함수들이 호출로 닿는 사용자 정의 함수들의 블록 수 합(CodeSizeCalculator).<br/>
	/// 서버는 함수를 저장할 때마다 저장한 뒤의 크기를 세어 이 값을 넘으면 거부한다.<br/>
	/// 메모리 작업에서 코드 영역 한도의 위치가 정해지면 그곳으로 옮긴다.
	/// </summary>
	public const int MaxCodeSize = 1000;

	/// <summary>슬롯 함수의 고정 이름. 슬롯 번호 0~7 → slot1~slot8.</summary>
	public static string SlotFunctionName(int slot) => $"slot{slot + 1}";
}

/// <summary>
/// 스킬 트리 노드 해금 요청의 결과. PlayerProgress.TryUnlock이 돌려주고, 소유 클라이언트에는 PlayerProgress.UnlockAnswered로 알린다.<br/>
/// 서버가 아래 순서(UnknownNode → AlreadyUnlocked → PrerequisiteMissing → MaterialShort)로 검사하고, 먼저 걸린 이유 하나만 돌려준다.
/// </summary>
public enum UnlockResult
{
	Unlocked,            // 해금함 (같이 열리는 노드도 함께)
	UnknownNode,         // SkillTree에 없는 노드 ID (null 포함)
	AlreadyUnlocked,     // 이미 해금한 노드
	PrerequisiteMissing, // 선행 노드를 아직 해금하지 않음 (SkillTree.CanUnlock)
	MaterialShort,       // 노드의 Cost보다 가진 재료가 모자람
	NotAllowed,          // 서버가 아닌 곳에서 TryUnlock을 부름. 값을 바꾸지 않음
	// 새 값은 맨 끝에만 더한다. 이 값은 네트워크로 보내므로 중간에 끼우면 뒤 값들의 번호가 바뀐다 (CastRejection과 같은 규칙)
}

/// <summary>소유 클라이언트가 서버에 보내는 플레이어 데이터 요청의 종류.</summary>
public enum PlayerDataRequest
{
	SaveFunction,
	SaveGlobals,
}

/// <summary>플레이어 데이터 요청의 결과.</summary>
public enum PlayerDataRequestResult
{
	Saved,
	NotAllowed,       // 서버 DB를 쓰지 않는 플레이어(임시 계정, 네트워크를 켜지 않은 테스트)
	InvalidData,      // JSON을 읽을 수 없음, GUID가 없음
	TooManyFunctions, // 새 함수(페이지 함수)인데 이미 ProgressRules.MaxPageFunctions개
	SlotFunctionLocked, // 슬롯 함수의 이름(slot1~slot8)·파라미터(없음)·반환 타입(Void)·비동기 여부(아님)를 바꾸려 함. 슬롯 함수는 function body만 바꿀 수 있음
	BlockLocked,      // 해금하지 않은 문법의 블록을 씀 (SkillTree.IsBlockUnlocked)
	SkillLocked,      // 해금하지 않은 C 스킬을 부름 (스킬 트리에 노드가 있는 사전 정의 함수만 해당)
	CodeSizeExceeded, // 저장하면 슬롯 8개의 코드 영역 크기가 ProgressRules.MaxCodeSize를 넘음
	GlobalsRejected,  // 전역 변수 검사에 걸림 (칸 부족 등. 이유는 서버 콘솔 오류)
}
