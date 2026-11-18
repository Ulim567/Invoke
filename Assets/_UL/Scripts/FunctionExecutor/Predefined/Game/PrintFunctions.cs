using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// 출력 함수의 공통 부분 (#536). 값 하나를 글자로 바꿔 시전한 플레이어의 화면 왼쪽 아래에 한 줄로 보인다.
/// <para>
/// - argument 칸에 변수나 expression을 넣었으면 「n = 3」, 값을 직접 적었으면 「3」만 보인다(글은 FunctionDataConverter가 넘긴다).<br/>
/// - 출력 글은 실행 문맥에 기록만 하고, 실행기가 FunctionExecutor.Output으로 알린다. 화면에 보내는 일은 FunctionExecutorNetwork가 한다.<br/>
/// - 값을 글자로 바꾸는 방식만 언어마다 다르다(Format). C는 CPrintf, Python은 PyPrint.
/// </para>
/// </summary>
// 스킬이 아니라 기본 함수다. 스킬 트리 노드가 없고, 슬롯에 직접 넣지 않는다(SlotLoadout.IsEquippableSkill)
public abstract class PrintFunction : PredefinedFunction
{
	// 어떤 타입의 값이든 받는다. 출력할 값의 타입을 플레이어가 고르지 않게 하기 위함
	static readonly IReadOnlyList<ParameterData> ValueParameter =
		new[] { new ParameterData("value", VariableType.AnyType) };

	// 리스트·array 안의 리스트를 몇 겹까지 펼칠지. 자기 자신을 담은 리스트를 끝없이 펼치지 않게 하기 위함
	const int MaxDepth = 4;

	/// <summary>출력 글의 값 부분 최대 글자 수 (#536). 넘으면 잘라 끝에 「...」를 붙인다.</summary>
	// 서버에서 자르는 이유: 아주 긴 문자열·리스트를 출력해도 네트워크로 보내는 글과 HUD 상자가 끝없이 커지지 않게 하기 위함
	// 「…」 대신 「...」: HUD 글꼴(Galmuri)에 없는 글자일 수 있어 ASCII만 쓴다
	public const int MaxValueLength = 100;

	public override int TickCount => 1;
	public override IReadOnlyList<ParameterData> Parameters => ValueParameter;

	// 죽었거나 없어진 대상도 출력할 수 있어야 한다. 기본 검사는 시작 전에 NoTarget·InvalidTarget으로 멈추게 하기 때문
	protected override bool ValidatesTargets => false;

	protected override void OnTick(FunctionContext context, int tick)
	{
		string text = TryGetArgument("value", out RuntimeValue value) ? Format(context, value, 0) : "";
		if (text.Length > MaxValueLength) text = text.Substring(0, MaxValueLength) + "...";
		string source = ArgumentTexts != null && ArgumentTexts.Length > 0 ? ArgumentTexts[0] : null;
		context.RequestOutput(string.IsNullOrEmpty(source) ? text : $"{source} = {text}");
	}

	/// <summary>
	/// 값 하나를 그 언어의 출력 모양으로. depth는 리스트·array 안으로 들어간 겹 수(맨 바깥 0)다.
	/// </summary>
	protected abstract string Format(FunctionContext context, RuntimeValue value, int depth);

	// 원소들을 앞뒤 글자로 감싸 쉼표로 잇는다. 너무 깊으면 ...로 줄인다
	protected string JoinElements(FunctionContext context, IReadOnlyList<RuntimeValue> elements, string open, string close, int depth)
	{
		if (depth >= MaxDepth) return $"{open}...{close}";

		StringBuilder builder = new StringBuilder(open);
		for (int i = 0; i < elements.Count; i++)
		{
			if (i > 0) builder.Append(", ");
			builder.Append(Format(context, elements[i], depth + 1));
		}
		return builder.Append(close).ToString();
	}

	// 소수를 컴퓨터 설정과 상관없이 점으로 적는다
	protected static string FloatText(float value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// C의 출력 함수 printf(값) (#536). 예: int n = 3; printf(n); → 「n = 3」
/// <para>
/// 값 모양: bool은 true·false, char·문자열은 따옴표 없이, 포인터는 번호(0이면 NULL), array는 {1, 2, 3}, 대상은 몬스터 이름.
/// </para>
/// </summary>
// 진짜 C의 printf("%d", n)와 달리 형식 글자 없이 값 하나만 받는다 (이우림 결정 11/18). 플레이어가 형식 글자를 몰라도 값을 보게 하기 위함
public class CPrintf : PrintFunction
{
	public override string Id => PredefinedFunctionIds.Printf;
	public override string Name => "printf";

	protected override string Format(FunctionContext context, RuntimeValue value, int depth)
	{
		if (value.Type == null) return "";
		switch (value.Type.Kind)
		{
			case VariableTypeKind.Float: return FloatText(value.AsFloat());
			case VariableTypeKind.Bool: return value.AsBool() ? "true" : "false";
			// 글자 번호 0은 화면에 보이지 않으므로 C 표기로 적는다
			case VariableTypeKind.Char: return value.AsChar() == '\0' ? "\\0" : value.AsChar().ToString();
			// 맨 바깥 문자열은 printf("%s")처럼 따옴표 없이, array 안의 문자열은 C 초기화 모양처럼 따옴표를 붙인다
			case VariableTypeKind.String: return depth == 0 ? value.AsString() : $"\"{value.AsString()}\"";
			case VariableTypeKind.Pointer:
				return value.AsPointer() == HeapStorage.NullPointer ? "NULL" : value.AsPointer().ToString(CultureInfo.InvariantCulture);
			// C의 array 초기화 모양 {1, 2, 3}
			case VariableTypeKind.Array: return JoinElements(context, value.AsArray().Elements, "{", "}", depth);
		}
		return value.ToString();
	}
}

/// <summary>
/// Python의 출력 함수 print(값) (#536). 예: n = 3, print(n) → 「n = 3」
/// <para>
/// 값 모양: True·False, None, 맨 바깥 문자열은 따옴표 없이, 소수는 3.0처럼 점 아래를 남기고, 리스트는 [1, 'a', None]처럼 원소까지.
/// </para>
/// </summary>
public class PyPrint : PrintFunction
{
	public override string Id => PredefinedFunctionIds.PyPrint;
	public override string Name => "print";

	protected override string Format(FunctionContext context, RuntimeValue value, int depth)
	{
		if (value.Type == null) return "";
		switch (value.Type.Kind)
		{
			case VariableTypeKind.None: return "None";
			case VariableTypeKind.Bool: return value.AsBool() ? "True" : "False";
			// Python은 소수 3.0을 3.0으로 보인다. C#은 3으로 적으므로 점 아래를 붙인다
			case VariableTypeKind.Float:
			{
				string text = FloatText(value.AsFloat());
				bool plain = text.IndexOf('.') < 0 && text.IndexOf('E') < 0 && text.IndexOf('N') < 0 && text.IndexOf('I') < 0;
				return plain ? text + ".0" : text;
			}
			// 맨 바깥 문자열은 print처럼 따옴표 없이, 리스트 안의 문자열은 Python처럼 작은따옴표를 붙인다
			case VariableTypeKind.String: return depth == 0 ? value.AsString() : $"'{value.AsString()}'";
			case VariableTypeKind.List:
				return TryGetListElements(context, value, out IReadOnlyList<RuntimeValue> elements)
					? JoinElements(context, elements, "[", "]", depth)
					: value.ToString();
			case VariableTypeKind.Array: return JoinElements(context, value.AsArray().Elements, "[", "]", depth);
		}
		return value.ToString();
	}
}
