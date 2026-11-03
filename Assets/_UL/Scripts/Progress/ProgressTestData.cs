using UnityEngine;

/// <summary>
/// [테스트용] 캐릭터 진행도를 JSON 하나로 서버 DB에 한 번에 넣는 컴포넌트. Base 씬의 오브젝트에 붙인다.
/// <para>
/// 서버 DB를 자주 지우는 개발 중에, 원하는 진행도(레벨, 재료, 해금, 슬롯, 함수, 전역 변수)를 가진 캐릭터를 바로 만들기 위함이다.
/// </para>
/// <para>
/// 사용법<br/>
/// - 에디터에서 Play → 로그인 → 캐릭터 화면(들어가기 전)에서 이 컴포넌트의 ⋮ → 「진행도 넣기」 → 「들어가기」.<br/>
/// - 그 언어(Language)의 캐릭터가 먼저 있어야 한다. 서버가 그 캐릭터의 진행도 테이블 6개를 JSON으로 모두 바꾼다.<br/>
/// - 상태별로 JSON이 다른 컴포넌트를 여러 개 붙여 두고 골라 쓸 수 있다.<br/>
/// - 지금 진행도의 JSON 모양은 ⋮ → 「처음 값 JSON 채우기」로 볼 수 있다.<br/>
/// - 서버는 에디터이거나, 실행 인자 -allowTestProgressData로 켠 서버일 때만 받는다 (InvokeAuthenticator.AllowTestProgressDataArg).
/// </para>
/// </summary>
public class ProgressTestData : MonoBehaviour
{
	[Tooltip("진행도를 바꿀 캐릭터의 언어 (CharacterLanguages). 예: C")]
	[SerializeField] string language = CharacterLanguages.C;

	[Tooltip("CharacterProgressData의 JSON. Level, Exp, Money, Materials, Unlocked, Slots(8개, Kind: UserFunction, Id: 슬롯 함수 GUID), Functions(슬롯 함수 + 페이지 함수), Globals. 슬롯의 함수가 Functions에 없으면 서버가 새 캐릭터와 같은 슬롯 함수를 만든다")]
	[SerializeField, TextArea(6, 30)] string json =
		"{\"Level\":10,\"Exp\":0,\"Money\":1000," +
		"\"Materials\":{\"bug_fragment\":50,\"field_boss_core\":2,\"boss_core\":1}," +
		"\"Unlocked\":[\"syntax.sequence\",\"c.thrust\",\"c.sword_throw\"]," +
		"\"Slots\":[],\"Functions\":[],\"Globals\":{\"Declarations\":[]}}";

	void OnEnable()
	{
		InvokeAuthenticator authenticator = InvokeAuthenticator.Current;
		if (authenticator != null) authenticator.ProgressTestDataResultReceived += OnResult;
	}

	void OnDisable()
	{
		InvokeAuthenticator authenticator = InvokeAuthenticator.Current;
		if (authenticator != null) authenticator.ProgressTestDataResultReceived -= OnResult;
	}

	[ContextMenu("진행도 넣기")]
	void SendProgress()
	{
		InvokeAuthenticator authenticator = InvokeAuthenticator.Current;
		if (authenticator == null || !authenticator.RequestProgressTestData(language, json))
		{
			Debug.LogWarning("[ProgressTestData] 로그인한 뒤 캐릭터 화면에서 씁니다.", this);
			return;
		}
		Debug.Log($"[ProgressTestData] {name}: [{language}] 캐릭터의 진행도 넣기를 요청했습니다.", this);
	}

	// JSON을 처음부터 쓰지 않아도 되도록, 새 캐릭터의 처음 값과 같은 모양을 채운다(슬롯 함수 8개 포함)
	[ContextMenu("처음 값 JSON 채우기")]
	void FillWithEmptyProgress()
	{
		var data = new CharacterProgressData();
		data.FillInitialSlotFunctions();
		json = FunctionDataJson.ToJson(data);
	}

	void OnResult(ProgressTestDataResult result)
	{
		if (result.success) Debug.Log($"[ProgressTestData] {name}: 진행도를 넣었습니다. 「들어가기」를 누르면 바뀐 값으로 시작합니다.", this);
		else Debug.LogWarning($"[ProgressTestData] {name}: 서버가 거부했습니다: {result.reason}", this);
	}
}
