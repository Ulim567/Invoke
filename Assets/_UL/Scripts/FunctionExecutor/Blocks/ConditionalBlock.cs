using System.Collections.Generic;

/// <summary>
/// 조건문 블록. 연달아 놓인 if, else if, else를 함께 담는다. 예: if (x &gt; 3) { ... } else if (x == 0) { ... } else { ... }
/// <para>
/// - 갈래를 위에서부터 하나씩 검사한다. 실행이 닿은 갈래(if, else if, else)마다 1틱을 쓴다.<br/>
/// - 조건에 expression으로 쓴 블록이 있으면 그 블록이 먼저 자기 틱을 쓴 뒤, 다음 틱에 그 갈래를 검사한다.<br/>
/// - 조건이 참인 갈래(또는 else)를 고르면, 그 갈래의 { }를 다음 틱부터 실행한다. { }가 비어 있거나 고른 갈래가 없으면 검사한 틱에 끝난다.<br/>
/// - { }는 범위를 연다. 안에서 선언한 변수는 }에서 사라진다.<br/>
/// - 조건으로 바꾸는 규칙은 논리 블록과 같다(C: 0이 아니면 참, NULL이 아니면 참). 조건으로 쓸 수 없는 값이면 TypeMismatch로 실패한다.<br/>
/// - { } 안의 실패와 return은 바깥에 그대로 알린다.
/// </para>
/// <para>
/// 함수 데이터에서는 if, else if, else가 따로 놓인 블록이다. FunctionDataConverter가 연달아 놓인 갈래를 이 블록 하나로 묶는다.
/// </para>
/// </summary>
public class ConditionalBlock : Block
{
	/// <summary>
	/// 조건문의 갈래 하나. if, else if는 조건을 가지고, else는 조건이 null이다.
	/// </summary>
	public class Branch
	{
		/// <summary>
		/// 갈래의 조건(원본). else이면 null.
		/// </summary>
		public ValueBlock Condition { get; }

		/// <summary>
		/// 조건이 참일 때 실행할 { } 안의 블록들(원본).
		/// </summary>
		public IReadOnlyList<Block> Body { get; }

		public Branch(ValueBlock condition, params Block[] body)
		{
			Condition = condition;
			Body = body;
		}
	}

	readonly IReadOnlyList<Branch> branches;
	// 이 실행에서 검사 중인 갈래와 그 조건의 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 정한다
	int branchIndex;
	ValueSlots condition;
	// 고른 갈래의 { }. 아직 고르지 않았으면 null이다
	Statements body;

	public ConditionalBlock(params Branch[] branches)
	{
		this.branches = branches;
	}

	protected override void OnStart(FunctionContext context)
	{
		branchIndex = 0;
		condition = null;
		body = null;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		if (body != null) return body.Step(context);
		// 갈래가 없는 조건문은 FunctionDataConverter가 만들지 않는다. 직접 만든 블록이면 바로 끝낸다
		if (branchIndex >= branches.Count) return BlockResult.Done;

		Branch branch = branches[branchIndex];
		bool chosen = true;
		if (branch.Condition != null)
		{
			condition ??= new ValueSlots(branch.Condition);
			BlockResult result = condition.Step(context);
			if (result != BlockResult.Done) return result;
			if (!LogicBlock.TryGetCondition(context, condition[0], out chosen)) return BlockResult.Failed;
		}

		// 여기까지가 이 갈래의 틱이다. 다음 갈래나 { }는 다음 틱에 시작한다
		if (!chosen)
		{
			branchIndex++;
			condition = null;
			return branchIndex >= branches.Count ? BlockResult.Done : BlockResult.Running;
		}

		// 빈 { }는 틱을 더 쓰지 않게 이 틱에 끝낸다
		if (branch.Body.Count == 0) return BlockResult.Done;
		body = new Statements(branch.Body, true);
		return BlockResult.Running;
	}

	protected override void OnCancel(FunctionContext context)
	{
		condition?.Cancel(context);
		body?.Cancel(context);
	}
}
