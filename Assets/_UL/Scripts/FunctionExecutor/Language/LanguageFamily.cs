/// <summary>
/// 언어 방식. 변수를 만드는 방법, 변수 범위, 연산·조건 규칙, None 처리, 편집 화면의 블록 모양이 이 값으로 나뉜다 (#532).
/// <para>
/// - 값은 캐릭터 언어(CharacterLanguages, PlayerCharacter의 언어)에서 LanguageFamilies.Of로 정한다. 언어를 정하는 곳은 캐릭터 언어 하나다.<br/>
/// - 언어마다 다른 코드는 이 값으로 갈래를 나눈다(switch). 새 언어(예: Java)를 만들면 값을 맨 끝에 더하고, 갈래가 없는 곳은 C로 동작한다.
/// </para>
/// </summary>
// bool(Python인가) 대신 enum으로 두는 이유: 세 번째 언어가 생겨도 함수의 받는 값을 바꾸지 않고 갈래만 더하기 위함
public enum LanguageFamily
{
	// 선언으로 변수를 만들고 타입이 고정된다. { } 하나가 범위 하나다
	C,
	// 대입이 곧 변수 만들기이고 타입은 값이 가진다. 범위는 함수 하나다. 함수 안에서 대입하는 이름은 global로 적지 않으면 지역 변수다
	Python,
}

/// <summary>
/// 캐릭터 언어 이름(CharacterLanguages)을 언어 방식(LanguageFamily)으로 바꾼다 (#532).
/// </summary>
public static class LanguageFamilies
{
	/// <summary>
	/// 캐릭터 언어 language의 언어 방식. 모르는 언어와 아직 만들 수 없는 언어(Java)는 C.
	/// </summary>
	// Java 캐릭터를 만들 때 LanguageFamily에 Java를 더하고 여기에 줄을 더한다
	public static LanguageFamily Of(string language) => language switch
	{
		CharacterLanguages.Python => LanguageFamily.Python,
		_ => LanguageFamily.C,
	};
}
