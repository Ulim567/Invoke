using System.Collections.Generic;

/// <summary>
/// 변수 저장소. 변수 이름마다 타입과 현재 값(RuntimeValue)을 보관한다.
/// <para>
/// 사용법<br/>
/// - 지역 변수: 실행 중인 UserFunction 복사본이 하나씩 가진다(UserFunction.Variables).<br/>
/// - TryDeclare로 변수를 만들고, TryGet으로 읽고, TrySet으로 바꾼다.<br/>
/// - 없는 이름, 이미 있는 이름, 타입이 다른 값이면 아무것도 바꾸지 않고 false를 돌려준다. 그때 무엇을 할지는 부른 블록이 정한다.
/// </para>
/// <para>
/// 블록 범위(C의 { })<br/>
/// - OpenScope로 범위를 열고 CloseScope로 닫는다. 닫으면 그 범위에서 선언한 변수가 사라진다. Statements가 { } 하나마다 부른다.<br/>
/// - TryDeclare는 가장 안쪽 범위에 만든다. TryGet, TrySet 등은 안쪽 범위부터 찾는다(바깥과 같은 이름이면 안쪽 변수).<br/>
/// - 맨 바깥 범위는 function body의 범위다. 닫을 수 없다.
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

	// 범위마다 이름과 변수. 뒤에 있을수록 안쪽 범위다. 0번은 function body의 범위
	readonly List<Dictionary<string, Variable>> scopes = new List<Dictionary<string, Variable>> { new Dictionary<string, Variable>() };

	/// <summary>
	/// type 타입의 변수 name을 가장 안쪽 범위에 만들고 value를 넣는다.<br/>
	/// 가장 안쪽 범위에 이미 같은 이름이 있거나 value의 타입이 type과 다르면 만들지 않고 false를 돌려준다.<br/>
	/// 바깥 범위와 같은 이름은 만든다. 그것을 허용할지는 언어마다 다르므로, 부른 블록이 CanDeclare로 먼저 확인한다.
	/// </summary>
	public bool TryDeclare(string name, VariableType type, RuntimeValue value)
	{
		if (string.IsNullOrEmpty(name) || type == null) return false;
		Dictionary<string, Variable> innermost = scopes[scopes.Count - 1];
		if (innermost.ContainsKey(name)) return false;
		if (!IsSameType(type, value.Type)) return false;

		innermost.Add(name, new Variable { Type = type, Value = value });
		return true;
	}

	/// <summary>
	/// 이름 name을 가장 안쪽 범위에 선언할 수 있으면 true.<br/>
	/// 가장 안쪽 범위에 같은 이름이 있으면 언제나 false다. 바깥 범위에 같은 이름이 있으면 allowShadowing일 때만 true다(C는 허용, Java·C#은 거부).
	/// </summary>
	public bool CanDeclare(string name, bool allowShadowing)
	{
		if (name == null || scopes[scopes.Count - 1].ContainsKey(name)) return false;
		return allowShadowing || !Contains(name);
	}

	/// <summary>
	/// 어느 범위에든 변수 name이 있으면 true. TryDeclare, TrySet이 false를 돌려준 이유(이름 때문인지 타입 때문인지)를 가릴 때 쓴다.
	/// </summary>
	public bool Contains(string name) => TryFind(name, out _);

	/// <summary>
	/// 모든 범위의 변수 수. 전역 변수 저장소에서는 데이터 영역이 쓰는 칸 수(ProcessMemory.DataUsed)가 된다.
	/// </summary>
	public int Count
	{
		get
		{
			int count = 0;
			foreach (Dictionary<string, Variable> scope in scopes) count += scope.Count;
			return count;
		}
	}

	// 모든 범위의 변수 값. HeapStorage가 전역 변수에 담긴 포인터를 루트 집합으로 볼 때 쓴다
	internal IEnumerable<RuntimeValue> AllValues()
	{
		foreach (Dictionary<string, Variable> scope in scopes)
			foreach (Variable variable in scope.Values)
				yield return variable.Value;
	}

	/// <summary>
	/// 새 범위를 연다. 이 뒤에 선언한 변수는 CloseScope를 부를 때 사라진다.
	/// </summary>
	public void OpenScope() => scopes.Add(new Dictionary<string, Variable>());

	/// <summary>
	/// 가장 안쪽 범위를 닫고, 그 범위에서 선언한 변수를 지운다.
	/// </summary>
	public void CloseScope()
	{
		// 맨 바깥 범위를 닫는 것은 OpenScope와 짝이 맞지 않는 실행기 코드의 실수이므로 바로 드러나게 한다
		if (scopes.Count <= 1) throw new System.InvalidOperationException("function body의 범위는 닫을 수 없습니다.");
		scopes.RemoveAt(scopes.Count - 1);
	}

	/// <summary>
	/// 변수 name을 선언한 타입. 대입 블록이 새 값을 이 타입으로 바꿀 때 쓴다. 없는 이름이면 false를 돌려준다.
	/// </summary>
	public bool TryGetType(string name, out VariableType type)
	{
		if (TryFind(name, out Variable variable))
		{
			type = variable.Type;
			return true;
		}

		type = null;
		return false;
	}

	/// <summary>
	/// 변수 name의 현재 값을 꺼낸다. 없는 이름이면 false를 돌려준다.
	/// </summary>
	public bool TryGet(string name, out RuntimeValue value)
	{
		if (TryFind(name, out Variable variable))
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
		if (!TryFind(name, out Variable variable)) return false;
		if (!IsSameType(variable.Type, value.Type)) return false;

		variable.Value = value;
		return true;
	}

	// 안쪽 범위부터 찾는다. 바깥과 같은 이름이면 안쪽 변수가 보이게 하기 위함
	bool TryFind(string name, out Variable variable)
	{
		variable = null;
		if (name == null) return false;

		for (int i = scopes.Count - 1; i >= 0; i--)
			if (scopes[i].TryGetValue(name, out variable)) return true;
		return false;
	}

	// 두 타입이 같은지. 포인터, 배열, list는 원소 타입까지, struct는 이름까지, 대상은 종류까지 같아야 한다
	// VariableType은 클래스라서 ==로 비교하면 같은 객체인지만 보므로 따로 비교한다
	internal static bool IsSameType(VariableType a, VariableType b)
	{
		if (a == null || b == null) return a == b;
		if (a.Kind != b.Kind) return false;
		if (a.Kind == VariableTypeKind.Struct) return a.StructName == b.StructName;
		// 한쪽이 종류를 정하지 않은 Target이면 어떤 종류의 대상이든 같다고 본다
		// 값은 언제나 자기 종류를 가지므로, 종류 없는 변수에 담았던 Bug를 Player 변수에 옮기면 그때 다르다고 걸린다
		if (a.Kind == VariableTypeKind.Target) return a.TargetKind == null || b.TargetKind == null || a.TargetKind == b.TargetKind;
		if (a.ElementType != null || b.ElementType != null) return IsSameType(a.ElementType, b.ElementType);
		return true;
	}
}
