using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

/// <summary>
/// 함수 데이터와 JSON 글자열을 서로 바꾼다. 서버 전송과 DB 저장에 쓴다.<br/>
/// Newtonsoft Json을 직접 부르는 곳은 이 클래스뿐이다. (설정을 바꿀 때 이곳만 고치면 되게 하기 위함)
/// <para>
/// 사용법<br/>
/// - 저장: string json = FunctionDataJson.ToJson(function);<br/>
/// - 불러오기: if (FunctionDataJson.TryFromJson(json, out FunctionData function)) { ... }<br/>
///   서버는 조작되거나 깨진 글자열을 받을 수 있으므로, 실패하면 오류를 던지지 않고 false를 돌려준다.
/// </para>
/// </summary>
public static class FunctionDataJson
{
	static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
	{
		// JSON에 적힌 클래스 이름으로 객체를 만들지 않는다. 켜 두면 조작된 데이터가 엉뚱한 클래스를 만들게 할 수 있다
		TypeNameHandling = TypeNameHandling.None,
		// SlotData처럼 칸 종류에 따라 쓰지 않는 칸이 많으므로, 비어 있는 칸은 적지 않아 JSON을 짧게 한다
		NullValueHandling = NullValueHandling.Ignore,
		// 서버로 보낼 때 크기를 줄이기 위해 줄바꿈을 넣지 않는다
		Formatting = Formatting.None,
		// 블록 안에 블록을 끝없이 겹친 조작 데이터를 막는다
		MaxDepth = 64,
		Converters =
		{
			// enum을 번호가 아니라 이름으로 적는다. 번호로 적으면 enum 중간에 값을 더할 때 예전 데이터가 다른 값으로 읽힌다
			// 번호로 적힌 데이터도 받지 않는다. 없는 번호가 들어와도 enum으로 읽혀 버리기 때문이다
			new StringEnumConverter { AllowIntegerValues = false },
		},
	};

	/// <summary>
	/// 함수 데이터를 JSON 글자열로 바꾼다.
	/// </summary>
	public static string ToJson(FunctionData function) => JsonConvert.SerializeObject(function, Settings);

	/// <summary>
	/// JSON 글자열을 함수 데이터로 되돌린다. 실패하면 false를 돌려주고 function은 null이다.
	/// </summary>
	public static bool TryFromJson(string json, out FunctionData function)
	{
		function = null;
		if (string.IsNullOrEmpty(json)) return false;

		try
		{
			function = JsonConvert.DeserializeObject<FunctionData>(json, Settings);
		}
		catch (JsonException)
		{
			return false;
		}

		// "null"처럼 객체가 아닌 JSON은 오류 없이 null이 되므로 따로 확인한다
		return function != null;
	}
}
