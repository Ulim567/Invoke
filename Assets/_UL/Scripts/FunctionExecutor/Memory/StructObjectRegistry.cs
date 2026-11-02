using System;
using System.Collections.Generic;

/// <summary>
/// 캐릭터 하나의 struct 객체 등록 자리. 미리 정한 struct(PredefinedStructs)마다, malloc 블록이 heap에 올릴 실제 객체를 만드는 함수를 등록받는다.
/// <para>
/// 사용법<br/>
/// - ProcessMemory가 만들어 가지고 있다. 다른 스크립트는 ProcessMemory.StructObjects로 가져온다.<br/>
/// - 그 struct의 객체를 맡은 컴포넌트가 Start에서 Register를 부른다. 예: SummonedSwords가 Sword에 소환검을 만드는 함수를 등록한다.<br/>
/// - 등록한 struct를 malloc하면 malloc 블록이 이 순서로 부른다: heap 남은 칸 확인 → 만들기 → heap에 올리기 → 포인터 알리기.<br/>
/// - heap이 모자라면 만들기 함수를 부르지 않고 NULL을 내놓는다. 만들기 함수가 null을 돌려줘도(예: 소환검이 이미 5개) NULL이다. 둘 다 실패로 끝내지 않는다.<br/>
/// - 등록하지 않은 struct는 malloc 블록이 지금처럼 StructInstance를 올린다.
/// </para>
/// <para>
/// heap에 올린 뒤의 해제는 이 등록 자리가 다루지 않는다. 해제된 객체를 없애는 일은 HeapStorage.ObjectFreed를 받은 쪽이 한다.
/// </para>
/// </summary>
// 실행기가 객체를 맡은 코드(예: 박진우의 SummonedSwords)를 직접 모르게 하기 위해 둔다. 다른 struct도 등록만 하면 같은 방식으로 붙는다
// 캐릭터마다 하나씩 두는 이유: 등록하는 함수가 자기 캐릭터의 컴포넌트를 그대로 쓰게 하기 위함. 모든 캐릭터가 함께 쓰면 함수마다 시전한 캐릭터를 받아 컴포넌트를 찾아야 한다
public class StructObjectRegistry
{
	// struct 하나에 등록한 함수 두 개. 등록할 때의 타입(T)을 object로 받는다. 꺼내 쓰는 malloc 블록은 struct마다 다른 타입을 모르기 때문
	internal class Entry
	{
		public Func<object> Create;
		public Action<object, int> NotifyAllocated;
	}

	// struct 이름 → 등록한 함수
	readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

	// ProcessMemory만 만든다
	internal StructObjectRegistry() { }

	/// <summary>
	/// structName(PredefinedStructs의 이름) struct의 객체를 만드는 함수를 등록한다.<br/>
	/// - create: 객체를 만든다. 만들 수 없으면 null을 돌려준다. heap이 모자라면 불리지 않는다.<br/>
	/// - notifyAllocated: heap에 올린 객체와 그 포인터를 받는다. 예: SummonedSwords.Register<br/>
	/// 목록에 없는 struct 이름, 비운 함수, 같은 struct를 두 번 등록하면 예외가 난다.
	/// </summary>
	// 잘못 등록하면 malloc이 조용히 StructInstance로 돌아가 원인을 찾기 어려우므로, 등록하는 순간 바로 드러나게 한다
	public void Register<T>(string structName, Func<T> create, Action<T, int> notifyAllocated) where T : class
	{
		if (!PredefinedStructs.TryGet(structName, out _))
			throw new ArgumentException($"미리 정한 struct 목록에 없는 이름입니다: {structName}", nameof(structName));
		if (create == null) throw new ArgumentNullException(nameof(create));
		if (notifyAllocated == null) throw new ArgumentNullException(nameof(notifyAllocated));
		if (entries.ContainsKey(structName))
			throw new InvalidOperationException($"{structName}은(는) 이미 등록되어 있습니다. 한 캐릭터에서 한 컴포넌트만 등록합니다.");

		entries.Add(structName, new Entry
		{
			// T가 class이므로 Func<T>를 그대로 Func<object>로 쓴다
			Create = create,
			NotifyAllocated = (obj, pointer) => notifyAllocated((T)obj, pointer),
		});
	}

	/// <summary>
	/// structName에 등록한 함수를 지운다. 등록한 컴포넌트가 사라질 때 부른다. 등록이 없으면 아무것도 하지 않는다.
	/// </summary>
	public void Unregister(string structName) => entries.Remove(structName);

	// malloc 블록이 부른다. 등록이 없으면 false
	internal bool TryGet(string structName, out Entry entry) => entries.TryGetValue(structName, out entry);
}
