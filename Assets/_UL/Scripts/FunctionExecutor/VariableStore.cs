using System.Collections.Generic;

/// <summary>
/// 변수 저장소. 변수 이름마다 타입과 현재 값(RuntimeValue)을 보관한다.
/// <para>
/// 사용법<br/>
/// - 지역 변수: 실행 중인 UserFunction 복사본이 하나씩 가진다(UserFunction.Variables).<br/>
/// - TryDeclare로 변수를 만들고, TryGet으로 읽고, TrySet으로 바꾼다.<br/>
/// - 없는 이름, 이미 있는 이름, 타입이 다른 값이면 아무것도 바꾸지 않고 false를 돌려준다. 그때 무엇을 할지는 부른 블록이 정한다.
/// </para>
/// </summary>
// 전역 변수 이슈에서는 캐릭터 하나가 이 클래스를 하나 더 가진다. 지역 변수와 전역 변수를 같은 함수로 읽고 쓰기 위함
// 변수 값은 이 클래스의 함수로만 바꾼다. 나중에 Mirror로 값을 맞출 때 바꾸는 곳이 한 군데여야 하기 때문
public class VariableStore
{
	class Variable
	{
		// 선언한 타입. 값을 바꿀 때 새 값의 타입이 이 타입과 같은지 확인한다
		public VariableType Type;
		public RuntimeValue Value;
	}

	readonly Dictionary<string, Variable> variables = new Dictionary<string, Variable>();

	/// <summary>
	/// type 타입의 변수 name을 만들고 value를 넣는다.<br/>
	/// 이미 같은 이름이 있거나 value의 타입이 type과 다르면 만들지 않고 false를 돌려준다.
	/// </summary>
	public bool TryDeclare(string name, VariableType type, RuntimeValue value)
	{
		if (string.IsNullOrEmpty(name) || type == null) return false;
		if (variables.ContainsKey(name)) return false;
		if (!IsSameType(type, value.Type)) return false;

		variables.Add(name, new Variable { Type = type, Value = value });
		return true;
	}

	/// <summary>
	/// 변수 name이 있으면 true. TryDeclare, TrySet이 false를 돌려준 이유(이름 때문인지 타입 때문인지)를 가릴 때 쓴다.
	/// </summary>
	public bool Contains(string name) => name != null && variables.ContainsKey(name);

	/// <summary>
	/// 변수 name의 현재 값을 꺼낸다. 없는 이름이면 false를 돌려준다.
	/// </summary>
	public bool TryGet(string name, out RuntimeValue value)
	{
		if (name != null && variables.TryGetValue(name, out Variable variable))
		{
			value = variable.Value;
			return true;
		}

		value = default;
		return false;
	}

	/// <summary>
	/// 변수 name의 값을 value로 바꾼다. 없는 이름이거나 value의 타입이 선언한 타입과 다르면 바꾸지 않고 false를 돌려준다.
	/// </summary>
	// 타입을 바꿔 넣는 일(예: int 값을 float 변수에)은 여기서 하지 않는다. 바꾸는 규칙이 언어마다 다르므로 대입 블록이 정한다
	public bool TrySet(string name, RuntimeValue value)
	{
		if (name == null || !variables.TryGetValue(name, out Variable variable)) return false;
		if (!IsSameType(variable.Type, value.Type)) return false;

		variable.Value = value;
		return true;
	}

	// 두 타입이 같은지. 포인터, 배열, list는 원소 타입까지, struct는 이름까지 같아야 한다
	// VariableType은 클래스라서 ==로 비교하면 같은 객체인지만 보므로 따로 비교한다
	static bool IsSameType(VariableType a, VariableType b)
	{
		if (a == null || b == null) return a == b;
		if (a.Kind != b.Kind) return false;
		if (a.Kind == VariableTypeKind.Struct) return a.StructName == b.StructName;
		if (a.ElementType != null || b.ElementType != null) return IsSameType(a.ElementType, b.ElementType);
		return true;
	}
}
