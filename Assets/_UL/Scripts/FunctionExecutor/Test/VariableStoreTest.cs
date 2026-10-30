using UnityEngine;

/// <summary>
/// 변수 저장소(VariableStore)와 기본값(LanguageRule) 테스트용 컴포넌트. 플레이를 시작하면 테스트를 돌리고 결과를 콘솔 로그로 남긴다.<br/>
/// 기본값을 그 캐릭터의 언어 규칙 파일에서 읽으므로, 실행기(FunctionExecutor)가 붙은 오브젝트에 붙인다.<br/>
/// 메모리를 쓰지 않으므로 다른 테스트 컴포넌트와 함께 붙여도 된다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class VariableStoreTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);
	static readonly VariableType SwordPointer = VariableType.PointerTo(VariableType.StructOf("Sword"));
	static readonly VariableType ShieldPointer = VariableType.PointerTo(VariableType.StructOf("Shield"));

	FunctionExecutor executor;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		TestDefaultValues();
		TestDeclareGetSet();
		TestRejected();
		TestValueCopy();
	}

	// 테스트 1: 언어 규칙 파일의 기본값. C 파일이면 아래 값이 나와야 한다
	void TestDefaultValues()
	{
		CheckDefault(Int, "0");
		CheckDefault(Float, "0");
		CheckDefault(new VariableType(VariableTypeKind.Bool), "false");
		CheckDefault(new VariableType(VariableTypeKind.Char), "'\\0'");
		CheckDefault(new VariableType(VariableTypeKind.String), "\"\"");
		CheckDefault(SwordPointer, "포인터 0");

		// 아직 다루지 않는 타입. 이 줄 바로 앞에 「Position 타입의 기본값은 아직 없습니다」 콘솔 오류가 하나 나와야 한다
		bool found = executor.TryGetDefaultValue(new VariableType(VariableTypeKind.Position), out _);
		Check("Position 기본값 없음 (콘솔 오류 1개가 정상)", !found);
	}

	// 테스트 2: int x = 3 → x = 5
	void TestDeclareGetSet()
	{
		VariableStore store = new VariableStore();

		Check("int x = 3 선언", store.TryDeclare("x", Int, RuntimeValue.FromInt(3)));
		CheckValue(store, "x", "3");

		Check("x = 5 쓰기", store.TrySet("x", RuntimeValue.FromInt(5)));
		CheckValue(store, "x", "5");
	}

	// 테스트 3: 저장소가 거부해야 하는 경우. 거부하면 값이 바뀌지 않아야 한다
	void TestRejected()
	{
		VariableStore store = new VariableStore();
		store.TryDeclare("x", Int, RuntimeValue.FromInt(5));

		Check("같은 이름 x 다시 선언 거부", !store.TryDeclare("x", Int, RuntimeValue.FromInt(1)));
		Check("없는 변수 y 읽기 거부", !store.TryGet("y", out _));
		Check("없는 변수 y 쓰기 거부", !store.TrySet("y", RuntimeValue.FromInt(1)));
		Check("int 변수 x에 float 1.5 쓰기 거부", !store.TrySet("x", RuntimeValue.FromFloat(1.5f)));
		CheckValue(store, "x", "5");

		Check("float f에 int 값으로 선언 거부", !store.TryDeclare("f", Float, RuntimeValue.FromInt(1)));
		Check("Sword* 변수에 Shield* 값으로 선언 거부",
			!store.TryDeclare("s", SwordPointer, RuntimeValue.FromPointer(ShieldPointer, HeapStorage.NullPointer)));
	}

	// 테스트 4: y = x 다음 x를 바꿔도 y는 그대로 (C의 값 복사)
	void TestValueCopy()
	{
		VariableStore store = new VariableStore();
		store.TryDeclare("x", Int, RuntimeValue.FromInt(1));
		store.TryGet("x", out RuntimeValue x);
		store.TryDeclare("y", Int, x);
		store.TrySet("x", RuntimeValue.FromInt(2));

		CheckValue(store, "x", "2");
		CheckValue(store, "y", "1");
	}

	void CheckDefault(VariableType type, string expected)
	{
		bool found = executor.TryGetDefaultValue(type, out RuntimeValue value);
		Check($"{type.Kind} 기본값 {expected}", found && value.ToString() == expected, found ? $"실제 {value}" : "기본값 없음");
	}

	static void CheckValue(VariableStore store, string name, string expected)
	{
		bool found = store.TryGet(name, out RuntimeValue value);
		Check($"{name} 값 {expected}", found && value.ToString() == expected, found ? $"실제 {value}" : "변수 없음");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[VariableStoreTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
