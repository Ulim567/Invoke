/// <summary>
/// array 하나의 원소들. 배열 만들기 블록이 만들고, array 변수에는 이 저장소를 가리키는 값(RuntimeValue.FromArray)이 담긴다.
/// <para>
/// - 길이는 만들 때 정하고 바뀌지 않는다.<br/>
/// - 원소를 바꿔도 변수의 값(이 저장소를 가리키는 값)은 그대로다. 그래서 array를 함수에 넘기면 불린 함수와 부른 함수가 같은 원소를 본다(C에서 array가 포인터로 넘어가는 것과 같음).<br/>
/// - 메모리 칸은 이 저장소가 아니라, 이 array를 만든 함수의 스택 프레임이 길이만큼 가진다(FunctionDataConverter가 프레임 크기에 더함).
/// </para>
/// </summary>
// RuntimeValue는 바꿀 수 없는 값이라 원소를 담지 못한다. arr[i] = 값으로 원소가 바뀌어야 하므로 원소는 따로 둔다
internal class ArrayStorage
{
	readonly RuntimeValue[] elements;

	public VariableType ElementType { get; }

	public int Length => elements.Length;

	// 모든 원소를 initialValue로 채운다. 배열 만들기 블록은 언어 규칙의 기본값을 넘긴다
	public ArrayStorage(VariableType elementType, int length, RuntimeValue initialValue)
	{
		ElementType = elementType;
		elements = new RuntimeValue[length];
		for (int i = 0; i < length; i++) elements[i] = initialValue;
	}

	public bool IsInRange(int index) => index >= 0 && index < elements.Length;

	// 범위는 부르는 블록이 IsInRange로 먼저 확인한다. 범위 밖이면 IndexOutOfRange로 실패시켜야 하기 때문
	public RuntimeValue Get(int index) => elements[index];

	public void Set(int index, RuntimeValue value) => elements[index] = value;

	/// <summary>
	/// 지금 함수의 지역 변수에서 이름이 name인 array의 원소를 찾는다. 원소 읽기·쓰기 블록이 쓴다.<br/>
	/// 변수가 없으면 UndeclaredVariable, array가 아니면(예: int x에 x[0]) TypeMismatch를 기록하고 false를 돌려준다.
	/// </summary>
	public static bool TryFind(FunctionContext context, string name, out ArrayStorage storage)
	{
		storage = null;
		// 원소 블록은 사용자 정의 함수 안에만 있으므로 지역 변수가 언제나 있다
		if (!context.CurrentVariables.TryGet(name, out RuntimeValue value))
		{
			context.RecordFailure(FunctionFailure.UndeclaredVariable);
			return false;
		}
		if (value.Type?.Kind != VariableTypeKind.Array)
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return false;
		}

		storage = value.AsArray();
		return true;
	}

	/// <summary>
	/// index 값을 원소 번호로 바꾼다. 정수 타입(int, char, C의 bool)만 받는다. 그 밖의 타입이면 false(TypeMismatch).
	/// </summary>
	// float은 받지 않는다. C에서도 arr[0.5]는 compile error이기 때문. 계산에서 float을 int로 바꾸는 ImplicitAll 규칙을 그대로 쓰지 않는 이유다
	// bool은 bool을 int로 올리는 언어(C)에서만 받는다. 계산 규칙(NumericRules)과 같다
	public static bool TryGetIndex(RuntimeValue index, LanguageRuleData rule, out int number)
	{
		number = 0;
		switch (index.Type?.Kind)
		{
			case VariableTypeKind.Int:
				number = index.AsInt();
				return true;
			case VariableTypeKind.Char:
				number = index.AsChar();
				return true;
			case VariableTypeKind.Bool when rule.NumericConversion == NumericConversion.ImplicitAll:
				number = index.AsBool() ? 1 : 0;
				return true;
		}
		return false;
	}
}
