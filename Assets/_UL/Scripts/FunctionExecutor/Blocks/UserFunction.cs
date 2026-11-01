using System;
using System.Collections.Generic;

/// <summary>
/// 플레이어가 블록 코딩으로 만든 함수. 안에 든 블록들을 위에서부터 하나씩 끝까지 실행한다.
/// <para>
/// 사용법<br/>
/// - new UserFunction("이름", 블록1, 블록2, ...)로 만들고, 실행기의 Cast에 넘기거나 다른 사용자 정의 함수의 블록으로 넣는다.<br/>
/// - 블록에는 사전 정의 함수, 사용자 정의 함수, return 같은 문법 블록을 넣을 수 있다.<br/>
/// - 파라미터, 반환 타입, 비동기 여부가 있는 함수는 함수 데이터에서 FunctionDataConverter가 만든다. 코드로 만든 함수는 파라미터 없음, Void, 동기다.
/// </para>
/// <para>
/// 동작 시간은 따로 정하지 않는다. 실제로 실행된 블록들의 틱을 더한 만큼 걸린다.<br/>
/// 자기 쿨타임은 없다. 쿨타임은 안에서 실행된 스킬마다 그 스킬이 끝날 때 걸린다.
/// </para>
/// <para>
/// 끝날 때의 반환 값 검사(플레이어 책임)<br/>
/// - Void 함수: 끝까지 실행했거나 return;이면 정상. return 값;이면 TypeMismatch<br/>
/// - 반환 타입이 있는 함수: 끝까지 실행했거나 return;이면 MissingReturn. return 값;이면 반환 타입으로 맞추고, 맞출 수 없으면 TypeMismatch<br/>
/// - 슬롯에서 바로 시전해 반환 값을 쓰지 않아도 같게 검사한다.
/// </para>
/// </summary>
public class UserFunction : FunctionBlock
{
	// 이 실행에서 블록 목록을 실행하는 도우미. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	Statements statements;
	// 이 실행에서 만난 return의 value slot. 끝까지 실행했거나 return;이면 null
	ValueBlock returnSource;
	readonly IReadOnlyList<ParameterData> parameters;
	readonly VariableType returnType;
	readonly bool isAsync;

	public override string Name { get; }

	/// <summary>
	/// 이 실행의 지역 변수. 실행을 시작할 때(OnStart) 새로 만들어지며, 시작하기 전에는 null이다.
	/// </summary>
	// 필드에 미리 만들어 두지 않는다. Clone은 얕은 복사라서, 같은 함수를 두 번 시전하면 두 작업이 같은 저장소를 함께 쓰게 되기 때문
	internal VariableStore Variables { get; private set; }

	/// <summary>
	/// 위에서부터 차례로 실행할 블록들(원본).
	/// </summary>
	public IReadOnlyList<Block> Blocks { get; private set; }

	/// <summary>
	/// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수. 함수 데이터에서 바꾼 함수는 FunctionDataConverter가 계산해 넣는다.
	/// </summary>
	public override int FrameSize { get; }

	public override IReadOnlyList<ParameterData> Parameters => parameters;

	public override VariableType ReturnType => returnType;

	public override bool IsAsync => isAsync;

	/// <summary>
	/// 프레임 크기 1(파라미터, 지역 변수 없음)인 함수를 만든다.
	/// </summary>
	public UserFunction(string name, params Block[] blocks) : this(name, 1, blocks) { }

	/// <summary>
	/// 프레임 크기를 정해 함수를 만든다. 1보다 작으면 1로 본다.
	/// </summary>
	public UserFunction(string name, int frameSize, params Block[] blocks)
		: this(name, frameSize, Array.Empty<ParameterData>(), new VariableType(VariableTypeKind.Void), false)
	{
		Blocks = blocks;
	}

	// 블록 없이 함수 정보만 가진 함수를 만든다. 블록은 SetBlocks로 나중에 넣는다
	// FunctionDataConverter만 쓴다. 재귀 호출에서 함수 안의 호출 블록이 아직 블록을 바꾸는 중인 자기 자신을 가리켜야 하기 때문
	internal UserFunction(string name, int frameSize, IReadOnlyList<ParameterData> parameters, VariableType returnType, bool isAsync)
	{
		Name = name;
		// 호출 자리 1칸은 언제나 쓴다
		FrameSize = frameSize < 1 ? 1 : frameSize;
		this.parameters = parameters;
		this.returnType = returnType;
		this.isAsync = isAsync;
		Blocks = Array.Empty<Block>();
	}

	// FunctionDataConverter가 블록을 다 바꾼 뒤 한 번 넣는다
	internal void SetBlocks(IReadOnlyList<Block> blocks)
	{
		Blocks = blocks;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		// 블록이 하나도 없는 함수도 Statements가 바로 Done을 돌려주므로 최소 1틱은 쓴다
		BlockResult result = statements.Step(context);

		// 반복문이 받지 않은 break, continue가 function body까지 올라왔다. 반복문 밖에 놓인 것이다
		// 바깥 함수의 반복문에 걸리게 하지 않는다. C에서도 break, continue는 함수를 넘지 못하기 때문
		if (result == BlockResult.Break || result == BlockResult.Continue)
		{
			context.RecordFailure(FunctionFailure.NotInLoop);
			return BlockResult.Failed;
		}

		// 안쪽에서 return을 만났으면 남은 블록을 실행하지 않는다. return은 이 함수에서 멈추므로 바깥에는 Done으로 알린다
		// 실패는 이 함수도 끝까지 실행하지 못한 것이므로 바깥에 그대로 알린다
		if (result != BlockResult.Returned) return result;

		returnSource = context.TakeReturnValue();
		return BlockResult.Done;
	}

	// 반환 값 검사는 플레이어 책임이다. 반환 값을 정하는 return 블록을 플레이어가 놓기 때문
	internal override bool TryValidateEnd(FunctionContext context)
	{
		if (returnType.Kind == VariableTypeKind.Void)
		{
			if (returnSource == null) return true;

			context.RecordFailure(FunctionFailure.TypeMismatch);
			return false;
		}

		if (returnSource == null)
		{
			context.RecordFailure(FunctionFailure.MissingReturn);
			return false;
		}
		if (!NumericRules.TryConvertForStore(returnSource, returnSource.Result, returnType, context.Rule, out RuntimeValue converted))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return false;
		}

		ReturnValue = converted;
		return true;
	}

	// 안쪽 블록은 실행 문맥만 받으므로, 이 함수의 지역 변수를 실행 문맥에 올려 두어 안쪽 블록이 찾게 한다
	// Block은 OnStart가 불린 뒤 OnEnd, OnCancel 중 정확히 하나를 부르므로, 올리고 내리는 짝이 어긋나지 않는다
	protected override void OnStart(FunctionContext context)
	{
		Variables = new VariableStore();
		context.PushVariables(Variables);
		DeclareParameters(context);
		// function body는 범위를 따로 열지 않는다. C에서 function body의 맨 바깥 범위는 파라미터와 같은 범위이기 때문
		statements = new Statements(Blocks, false);
	}

	protected override void OnEnd(FunctionContext context)
	{
		context.PopVariables();
	}

	protected override void OnCancel(FunctionContext context)
	{
		// 실행 중인 안쪽 블록도 함께 끊어서, 안쪽 블록들도 정리할 기회를 갖게 한다
		// 안쪽을 먼저 끊는다. 안쪽 사용자 정의 함수가 자기 지역 변수를 먼저 내려야 이 함수의 것이 맨 위가 되기 때문
		statements?.Cancel(context);
		context.PopVariables();
	}

	// 파라미터를 function body의 맨 바깥 범위에 지역 변수로 만든다. 값은 부른 쪽이 파라미터 타입으로 맞춰 넘긴 argument다
	// argument가 없으면(슬롯에서 바로 시전) 언어 규칙의 기본값을 넣는다. 초기값을 비운 선언과 같은 규칙이다
	// 이름이 겹치는 파라미터와 다룰 수 없는 타입은 FunctionDataConverter가 미리 거르므로 선언은 실패하지 않는다
	void DeclareParameters(FunctionContext context)
	{
		for (int i = 0; i < parameters.Count; i++)
		{
			ParameterData parameter = parameters[i];
			RuntimeValue? argument = Arguments != null && i < Arguments.Length ? Arguments[i] : null;
			RuntimeValue value = argument ?? DefaultValue(context, parameter.Type);
			Variables.TryDeclare(parameter.Name, parameter.Type, value);
		}
	}

	static RuntimeValue DefaultValue(FunctionContext context, VariableType type)
	{
		context.Rule.TryGetDefaultValue(type, out RuntimeValue value);
		return value;
	}
}
