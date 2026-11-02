# 사전 정의 함수 (C 스킬 작성 규칙)

- 담당자: 이우림

## 하는 일
- 게임이 통째로 제공하는 function(C 스킬, 공통 function, 특수 이동 function, 입력 function)을 만드는 형식
- 고정 ID로 사전 정의 함수 찾기
- 스킬 코드에서 에러로 끝내기(`Fail`), 일찍 끝내기(`Stop`)와 스킬 에러(`SkillError`) 형식
- 스킬 코드에서 heap 쓰기(`Malloc`, `Free`, `TryGetHeapObject`)
- 스킬 코드에서 파라미터 값 읽기(`TryGetArgument`), 반환 값 정하기(`Return`) (#90)
- 입력 함수(`InputFunction`) 만드는 형식, 대상 입력 함수 `GetBug` (#131)
- 하지 않는 일
  - 에러가 났을 때의 처리(그 작업만 끝냄, 페널티 등): 캐릭터 언어의 언어 규칙 파일 (`function-executor.md` 「실패 처리」)
  - 실행, 입력 대기: `FunctionExecutor` (`function-executor.md` 「입력 대기」)
  - 플레이어가 대상을 고르는 화면 흐름: 입력을 받는 쪽(`SlotCaster`)
  - heap 칸 계산, 누수 판단: `ProcessMemory`, `HeapStorage` (`function-executor.md` 「heap」)

## 규칙

### 고정 ID
- 형식: `분류.영어_이름` (소문자, 밑줄)
  - 분류: `c`(C 스킬), `common`(세 캐릭터 공통 function), `input`(입력 function), `test`(테스트용 function)
  - 예: `c.thrust`, `common.dash`, `input.get_position`, `test.function_a`
- 한 번 정한 ID: 바꾸기, 지우기 금지 (예전에 저장한 함수 데이터가 그 function을 찾지 못함)
- 같은 ID 두 개: 금지
- 적는 곳: `PredefinedFunctionIds` 한 파일에 상수로 모음. 스킬의 `Id`는 이 상수를 씀 (예: `Id => PredefinedFunctionIds.Thrust`)
  - 모든 ID를 한 파일에서 보고, ID를 바꾸거나 지운 것을 PR에서 이 파일의 변경으로 확인하기 위함
- 자동 검사: 에디터에서 컴파일이 끝날 때마다 `PredefinedFunctionRegistry`가 검사. Play 불필요
  - 등록: 고정 ID가 유일하고 형식에 맞는 function만
  - 형식 위반: 콘솔 오류, 등록하지 않음
  - 같은 ID: 콘솔 오류, 그 ID를 쓰는 클래스 모두 등록하지 않음 (어느 쪽이 맞는지 알 수 없음)

### 메모리
- stack 프레임 크기(`FrameSize`): 1 + 파라미터 수 + `LocalVariableCount`. 실행기가 계산
- `LocalVariableCount`: 스킬마다 stack 부담을 조정하는 값. 무거운 스킬일수록 크게 정함
  - 스킬 작성자가 정하는 이유: C 스킬의 내부는 C# 코드라서 실제 지역 변수를 셀 수 없음
- heap: 함수가 끝나도 남아야 하는 객체(예: 소환검)를 올릴 때 사용. 아래 「heap 쓰기」

### 대상 지정 방식
- 대상: 파라미터로 받음

| 방식 | 파라미터 | 예 |
| --- | --- | --- |
| 단일 | `Target` 1개 | 검 찌르기, 꼬챙이(첫 대상), 검 다중베기 |
| 여러 명 선택 | `Target` list 1개 | 전장 휩쓸기 |
| 범위 | 없음. 캐릭터 주변에 바로 발동 | 검 크게베기, 검무 |
| 대상 없음 | 없음 | 검 소환, 해제, 패링, 이동 계열 |

- 파라미터로 받는 이유: 다른 function 안에서 불릴 때는 argument로, 슬롯에서 바로 cast할 때는 플레이어 입력으로 대상을 받음. 두 경우를 같은 파라미터 하나로 받기 위함

### 동기·비동기
- 동기(`IsAsync = false`): 부른 쪽이 이 function이 끝날 때까지 기다림. 같은 `FunctionTask` 안에서 stack 프레임이 위로 쌓임
- 비동기(`IsAsync = true`): 새 `FunctionTask` 하나 생성. 부른 쪽은 기다리지 않고 다음 `Block`으로 넘어감. 새 `FunctionTask`는 다음 tick부터 진행
- 슬롯에서 바로 cast: 동기·비동기와 상관없이 새 `FunctionTask`

### 실행 중단 공격, 슬롯 취소, Overflow 재시작 (#143)
- 끊기는 경우 (자세한 규칙: `function-executor.md` 「끊기는 경우」)
  - 실행 중단 공격: 그 캐릭터의 모든 `FunctionTask`가 끊김 (`FunctionExecutor.InterruptAll`)
  - 슬롯 취소: 그 슬롯에서 시전한 `FunctionTask`만 끊김 (`FunctionExecutor.Cancel`)
  - Overflow 재시작(C): 모든 `FunctionTask`가 끊긴 뒤 heap과 전역 변수가 처음으로 돌아감 (`FunctionExecutor.RestartProcess`)
- 실행 중이던 function마다 `OnEnd` 대신 `OnCancel`이 한 번 불림. 세 경우 모두 같음. heap에 올리지 않은 오브젝트(예: 이펙트) 정리는 `OnCancel`에서
- heap에 올린 오브젝트(예: 소환검): `OnCancel`에서 없애지 않음
  - 실행 중단 공격, 슬롯 취소: 해제하지 않은 객체는 누수가 됨 (`function-executor.md` 「heap」)
  - Overflow 재시작: heap이 비워지므로 객체마다 `ObjectCleared`가 불림. 소환검 오브젝트 삭제는 그 이벤트를 받은 쪽
- stack: 실행기가 돌려줌
- 쿨타임: 끊긴 스킬도 쿨타임 전체

## 다른 시스템이 쓰는 것

### `PredefinedFunction` 만들기

`PredefinedFunction`을 상속한 클래스 하나가 function 하나다.

- 등록되지 않는 클래스: 값을 받는 생성자(예: `public Thrust(int damage)`)만 있는 클래스. 콘솔 오류 (아래 `PredefinedFunctionRegistry`의 「원본 객체」)

#### 정하는 값

| 이름 | 타입 | 정하지 않으면 | 설명 |
| --- | --- | --- | --- |
| `Id` | `string` | 컴파일 오류 (반드시 정함) | 고정 ID. 함수 데이터의 함수 블록이 이 값으로 function을 가리킴 (`BlockData.FunctionId`) |
| `Name` | `string` | 컴파일 오류 (반드시 정함) | 화면 이름. 바꿔도 됨 |
| `TickCount` | `int` | 컴파일 오류 (반드시 정함) | 동작 시간(tick 수). 1 이하이면 1 tick |
| `CodeSize` | `int` | 1 | 코드 영역 크기. 함수 안의 함수 블록, 슬롯에 바로 넣은 경우 모두 이 값 |
| `Parameters` | `ParameterData` 목록 | 파라미터 없음 | 파라미터 이름과 타입. 함수 블록의 argument 수와 순서 |
| `ReturnType` | `VariableType` | `Void` | `Void`가 아니면 함수 블록을 expression으로 쓸 수 있음 |
| `LocalVariableCount` | `int` | 0 | 실행 중에 쓰는 지역 변수 수. stack 프레임 크기 계산에 씀. 스킬마다 stack 부담을 조정하는 값 |
| `IsAsync` | `bool` | `false` (동기) | 다른 function 안에서 불렸을 때 기다리지 않으면 `true` |
| `CooldownTicks` | `int` | 0 | 쿨타임(tick 수). 동작 시간(`TickCount`)과 따로 정함. 이 스킬이 끝난 tick부터 흐름(실패·실행 중단 포함). 쿨타임 동안 같은 고정 ID의 스킬은 슬롯 직접 시전이 거부되고, 사용자 정의 함수 안에서는 끝날 때까지 기다림. 규칙: [function-executor.md](function-executor.md)의 「쿨타임」 (#122) |

- `Parameters`, `ReturnType`, `IsAsync`의 위치: `FunctionBlock`. 사용자 정의 함수와 함께 가지는 값. 덮어쓰는 방법은 그대로(`public override`) (#132)

#### 효과 실행 (몇 번째 tick에 무엇을 하는지)

| 이름 | 덮어쓰지 않으면 | 설명 |
| --- | --- | --- |
| `OnStart(FunctionContext context)` | 아무것도 하지 않음 | 첫 tick에 `OnTick`보다 먼저 한 번 불린다. |
| `OnTick(FunctionContext context, int tick)` | 아무것도 하지 않음 | 매 tick 불린다. `tick`은 1부터 `TickCount`까지 센다. 몇 번째 tick에 무엇을 할지(예: 자세잡기 → 차징 → 발사) 여기서 정한다. |
| `OnEnd(FunctionContext context)` | 아무것도 하지 않음 | 마지막 tick에 `OnTick` 다음으로 한 번 불린다. `Completed`일 때만 불린다. |
| `OnCancel(FunctionContext context)` | 아무것도 하지 않음 | 도중에 cancel됐을 때(실패, `Interrupted`) `OnEnd` 대신 한 번 불린다. heap에 올리지 않은 오브젝트를 없애는 등의 정리를 한다. heap에 올린 오브젝트는 없애지 않는다. |
| `OnFail(FunctionContext context, SkillError error)` | 아무것도 하지 않음 | 이 스킬이 `Fail`로 에러를 냈을 때 `OnCancel`보다 먼저 한 번 불린다. 이 스킬만의 결과(예: 시전자가 피해를 입음)를 적는다. |

- 한 tick에서 부르는 순서: `OnStart`(첫 tick만) → `OnTick` → `OnEnd`(마지막 tick만)
- 시전한 캐릭터의 정보: `context.Caster`의 컴포넌트 (예: `context.Caster.GetComponent<PlayerStats>()`). 값을 바꿀 때는 그 컴포넌트의 함수를 부른다(`CLAUDE.md` 코드 작성 규칙 2).
- 애니메이션 길이: `TickCount` × `FunctionExecutor.SecondsPerTick`초. `OnStart`에서 재생 속도를 맞추고, `OnEnd`·`OnCancel`에서 되돌림
- 실행되는 것은 언제나 복사본이므로, 실행 중에 쓰는 값(예: 차징한 양)을 필드에 저장해도 된다.
  - 다만 `List` 같은 참조 형식 필드는 원본과 복사본이 함께 쓰게 된다. 실행 중에 쓰는 참조 형식 값은 `OnStart`에서 새로 만든다.
- `OnStart` 등에서 `base`를 부르지 않아도 된다. stack memory는 `FunctionExecutor`가 따로 처리한다.
- 값을 바꾸는 일(데미지, 이동)과 연출(이펙트, 소리, 애니메이션)은 다른 함수로 나눈다(`CLAUDE.md` 코드 작성 규칙 1). 예: 10 tick째에 판정 함수와 이펙트 함수를 각각 부른다.

```csharp
// 예시. 실제로 있는 function이 아니다
public class Thrust : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.Thrust;
	public override string Name => "Thrust";
	public override int TickCount => 20;

	protected override void OnTick(FunctionContext context, int tick)
	{
		// 10 tick째에 cast한 캐릭터 앞쪽으로 판정을 낸다
		if (tick == 10) { /* context.Caster를 기준으로 판정 */ }
	}
}
```

#### 에러로 끝내기, 일찍 끝내기

`OnStart`, `OnTick` 안에서 부른다.

| 이름 | 하는 일 |
| --- | --- |
| `Fail(SkillError error)` | 이 function을 에러로 끝냄. 그 tick 끝에 `OnFail` → `OnCancel` 순서로 불림. 처리는 언어 규칙 파일의 `SkillFailed` 줄을 따름. 여러 번 부르면 처음 에러만 씀 |
| `Stop()` | 이 function을 에러 없이 일찍 끝냄. 그 tick 끝에 `OnEnd`가 불림. 사용자 정의 함수 안이었다면 다음 `Block`으로 넘어감 |

- `Fail`과 `Stop`을 둘 다 부름: `Fail`을 따름
- `OnEnd`, `OnCancel` 안에서 부름: 콘솔 오류, 무시
- `OnFail` 안에서 부름: 무시 (이미 에러로 끝나는 중)

```csharp
// 예시. TempSwordState는 물리 검 관리가 생기기 전까지 쓰는 테스트용 임시 컴포넌트(맨손 여부만 가짐)
protected override void OnStart(FunctionContext context)
{
	if (context.Caster.GetComponent<TempSwordState>().IsUnarmed) Fail(new UnarmedError());
}
```

#### heap 쓰기

`OnStart`, `OnTick`, `OnEnd` 안에서 부른다.

| 이름 | 돌려주는 값 | 하는 일 |
| --- | --- | --- |
| `Malloc(FunctionContext context, object obj, int size)` | `int` | `obj`를 시전한 캐릭터의 heap `size`칸에 올리고 포인터를 돌려줌. heap이 모자라면 `HeapStorage.NullPointer`, 에러 없음 |
| `Free(FunctionContext context, int pointer)` | `bool` | 포인터의 객체를 해제하고 heap 칸 반환. 쓸 수 없는 포인터이면 `Fail(InvalidPointerError)` 후 `false`. 오브젝트는 없애지 않음 |
| `TryGetHeapObject<T>(FunctionContext context, int pointer, out T obj)` | `bool` | 포인터의 객체를 `T` 타입으로 꺼냄. 쓸 수 없는 포인터이면 `Fail(InvalidPointerError)` 후 `false` (C의 null 역참조) |
| `GetGlobalPointers<T>(FunctionContext context)` | `List<int>` | 숨은 시스템 전역의 `T` 객체 포인터. 다른 작업이 올려 둔 객체(예: 먼저 시전한 검 소환의 소환검)를 찾을 때 |
| `CountHeapObjects<T>(FunctionContext context)` | `int` | heap의 `T` 객체 수. 누수 객체 포함 (소환검 최대 개수 검사) |

- 쓸 수 없는 포인터: `NullPointer`, 해제한 포인터, 누수된 포인터, 타입이 다른 객체의 포인터
- 숨은 시스템 전역 (#139): C 라이브러리의 `static` 전역 변수처럼 플레이어에게 보이지 않는 캐릭터의 포인터 목록
- 포인터의 주인 (#139에서 바뀜)
  - 이 함수가 정상으로 끝나면: 이 함수가 malloc한 포인터가 숨은 시스템 전역으로 옮겨짐. 사용자 정의 함수 안에서 불렸어도 이 함수가 끝나는 순간 옮겨지므로, 부른 함수가 나중에 실패해도 남음
  - 실행 중단이나 실패로 끝나면: 해제하지 않은 객체는 누수
- 진행 중인 다른 함수가 malloc한 객체: `GetGlobalPointers`에 나오지 않음
- 만든 객체를 플레이어도 쓰게 하려면: 포인터를 반환 값으로 넘김(`ReturnType`을 그 struct의 포인터로, `Return`). 플레이어가 지역 변수에 받아도 숨은 시스템 전역이 붙잡고 있어 누수가 아님 (#139, 검 소환에 적용은 박진우에게 요청)
- `OnCancel` 안에서 `Malloc`, `Free`를 부름: 콘솔 오류, 무시 (`OnCancel`에서 해제하면 누수가 생기지 않기 때문)
- `size`가 음수이거나 `obj`가 `null`: 예외 (스킬 코드의 실수)

```csharp
// 예시. Sword, SpawnSword는 실제로 있는 것이 아니다
protected override void OnTick(FunctionContext context, int tick)
{
	if (tick != 5) return;
	Sword sword = SpawnSword(context.Caster);
	if (Malloc(context, sword, 2) == HeapStorage.NullPointer) DespawnSword(sword); // heap 부족: 소환하지 못함
}
```

#### 파라미터 값 읽기, 반환 값 정하기 (#90)

`OnStart`, `OnTick`, `OnEnd` 안에서 부른다.

| 이름 | 돌려주는 값 | 하는 일 |
| --- | --- | --- |
| `TryGetArgument(string name, out RuntimeValue value)` | `bool` | 이름이 `name`인 파라미터의 값을 꺼냄. 값이 없으면(argument를 비움, 슬롯에서 바로 시전) `false`. 없는 이름이면 콘솔 오류 후 `false` |
| `Return(RuntimeValue value)` | - | 반환 값을 정함. 함수를 끝내지는 않음(끝내려면 `Stop`). 여러 번 부르면 마지막 값 |

- 파라미터 값을 넘기는 곳: 함수 블록의 argument, 슬롯 직접 시전의 `FunctionExecutor.Cast(스킬, argument)` (#131). 값은 `Parameters`의 타입으로 맞춰서 넘어옴(캐릭터 언어의 숫자 타입 변환 규칙)
  - 지금 넘어오는 타입: `int`, `float`, `bool`, `string`, `char`, 포인터, 대상(`Target`, #131). 위치, 방향 등은 아직 언제나 「값 없음」
  - 대상: 몬스터는 `value.AsBug()`. 「대상 없음」, 죽은 대상, 없어진 대상은 실행기가 넘기기 전에 `InvalidTarget`으로 막으므로 스킬은 쓸 수 있는 대상만 받음
  - 값이 없을 때: 슬롯에서 바로 시전했을 때처럼 동작
- 이름: `Parameters`에 적은 `ParameterData`의 이름
- 반환 값이 쓰이는 곳: expression으로 쓴 함수 블록(예: `int n = CountSwords();`). statement로 놓으면 버림
  - 반환 타입이 `Void`가 아닌데 `Return` 없이 끝남, `ReturnType`으로 맞출 수 없는 값을 넘김: 콘솔 오류 + 작업을 `InternalError`(개발자 책임)로 끝냄. statement로 놓아도, 슬롯에서 바로 시전해도 같음(#132)
    - 확인 시점: 스킬이 끝날 때(`OnEnd` 다음)
  - 반환 타입이 `Void`인데 `Return`을 부름, `OnCancel`에서 부름: 콘솔 오류, 무시
- 값 만들기, 꺼내기: `RuntimeValue.FromInt(3)`, `value.AsInt()` 등

```csharp
// 예시. CountSwords는 실제로 있는 function이 아니다
public override VariableType ReturnType => new VariableType(VariableTypeKind.Int);

protected override void OnEnd(FunctionContext context)
{
	Return(RuntimeValue.FromInt(CountHeapObjects<Sword>(context)));
}
```

#### `SkillError` (스킬 에러)

스킬 에러 종류 하나가 `SkillError`를 상속한 클래스 하나다.
- 추가 정보(예: 필요한 소환검 수): 그 에러 클래스의 필드
- 에러끼리 묶기: 상속 (이후 Java의 try가 묶음 단위로 잡을 수 있게 함)
- 에러 객체: `Fail`을 부를 때마다 `new`로 새로 만듦 (`SourceName`이 덮어써지지 않게 하기 위함)
- 처리 방법: 에러 클래스가 정하지 않음. 같은 에러라도 언어마다 다르게 처리하기 위함

| 이름 | 타입 | 설명 |
| --- | --- | --- |
| `Message` | `string` | 로그와 화면에 보여 줄 설명. 덮어쓰지 않으면 클래스 이름 |
| `SourceName` | `string` | 에러를 낸 스킬의 이름. `Fail`이 채움 |

- 있는 에러
  - `Assets/_UL/Scripts/FunctionExecutor/Predefined/SwordErrors.cs`
    - `UnarmedError`: 맨손
    - `NoSummonedSwordError`: 소환검 없음
  - `Assets/_UL/Scripts/FunctionExecutor/Predefined/PointerErrors.cs`
    - `InvalidPointerError`: 쓸 수 없는 포인터. `Pointer`에 그 포인터 값. heap 함수(`Free`, `TryGetHeapObject`)가 냄. 플레이어의 `free` 블록은 이 에러 대신 실패 종류 `InvalidPointer`
    - 「해제한 포인터 사용 시 패널티」가 정해지면 이 클래스를 상속해 종류를 나눔

### `InputFunction` (입력 함수 만들기, #131)

플레이어에게 값을 입력받아 돌려주는 사전 정의 함수의 기반 클래스다. `PredefinedFunction`을 상속한다.
- 고정 ID: `input.`으로 시작. 편집 화면의 블록 목록에 자동으로 나옴
- 입력 요청: `InputFunction`만 함. C 스킬은 대상을 파라미터로만 받음
- tick: 기다리는 동안은 `TickCount`에 넣지 않음. 기다리는 동안의 규칙은 `function-executor.md` 「입력 대기」

| 이름 | 돌려주는 값 | 하는 일 |
| --- | --- | --- |
| `RequestInput(FunctionContext context, VariableType type)` | - | 플레이어에게 `type` 타입의 값을 입력받음. `OnStart`, `OnTick`에서 부름. 이미 요청했거나 끝난 뒤에 부르면 콘솔 오류, 무시 |
| `TryTakeInput(FunctionContext context, out RuntimeValue value)` | `bool` | 들어온 값을 꺼냄. 값이 들어온 뒤의 `OnTick`, `OnEnd`에서 부름. 아직 없으면 `false` |

- 있는 입력 함수

| 고정 ID | 이름 | 반환 타입 | 하는 일 |
| --- | --- | --- | --- |
| `input.get_bug` | `GetBug` | `Target`(종류 `Bug`) | 플레이어가 고른 몬스터를 돌려줌. 고를 몬스터가 없으면 「대상 없음」. `TickCount` 1 |

```csharp
// GetBug의 실행 단계
protected override void OnStart(FunctionContext context) => RequestInput(context, VariableType.TargetOf(TargetKind.Bug));

protected override void OnTick(FunctionContext context, int tick)
{
	if (TryTakeInput(context, out RuntimeValue value)) Return(value);
}
```

### `PredefinedFunctionRegistry` (고정 ID로 function 찾기)

- 필요한 이유: 함수 데이터에는 고정 ID 글자(예: `"c.thrust"`)만 저장됨. 이 글자로 실제 function을 찾는 곳

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TryGet(string id, out PredefinedFunction function)` | 고정 ID | `bool` | 없는 ID면 `false`. 받은 function은 원본이므로 실행할 때는 `FunctionExecutor.Cast`에 넘긴다. |
| `All` | - | `IReadOnlyList<PredefinedFunction>` | 모든 `PredefinedFunction`. 편집 화면의 블록 목록에 쓴다. |

- 등록: `PredefinedFunction`을 상속한 클래스는 자동 등록
  - 자동인 이유: 스킬을 만들 때마다 목록에 손으로 추가하면, 추가를 잊은 스킬은 찾을 수 없음
- 원본 객체: 등록할 때 스킬 클래스마다 객체 하나를 만들어 고정 ID와 짝지어 보관
  - 시점: 에디터에서는 컴파일이 끝날 때마다, 빌드한 게임에서는 `TryGet`이나 `All`을 처음 부를 때
  - 이유: `Id`, `CodeSize`, `Parameters` 같은 값은 객체가 있어야 읽을 수 있음. 보관해 두면 조회할 때마다 새로 만들지 않음
  - 실행: `Cast`가 이 원본의 복사본을 실행. 원본 자체는 실행되지 않음
  - 만드는 방법: 스킬 클래스의 값을 받지 않는 생성자를 부름. 생성자를 작성하지 않은 클래스는 C#이 이 생성자를 자동으로 만들어 줌
  - 값을 받는 생성자만 있는 클래스: 콘솔 오류, 등록되지 않음
- 쓰는 곳: 코드 영역 크기 계산(`CodeSize`), 편집 화면(`Parameters`, `ReturnType`), 블록 실행

## 맞춘 결과
- 박진우와 확인할 것
  - 스킬마다 다른 조건(맨손, 소환검)을 스킬 코드에서 `Fail`로 처리 (#38)
  - 에러 클래스(`UnarmedError` 등)의 위치: 지금은 `_UL`. 박진우 폴더로 옮겨도 실행기 코드는 고치지 않음 (#38)
  - 쿨타임 `CooldownTicks`를 스킬마다 정함 (#38)
  - heap 쓰기 함수(`Malloc`, `Free`, `TryGetHeapObject`)와 `OnCancel`에서 heap 오브젝트를 없애지 않는 규칙 (#66)
  - 파라미터 값을 이름으로 읽는 `TryGetArgument`, 반환 값을 정하는 `Return` (#90)
  - 쿨타임 기준: 사용자 정의 함수 → 스킬(고정 ID). 스킬이 끝날 때 시작하고, 실패·실행 중단도 쿨타임 전체 (#122)
  - 대상 파라미터 값: 이제 함수 블록의 argument로 넘어옴. 몬스터는 `TryGetArgument("target", ...)` 뒤 `AsBug()`. 쓸 수 없는 대상이면 실행기가 `InvalidTarget`으로 끝내고 스킬은 시작하지 않음 (#131)
  - 스킬의 동기·비동기: 모든 스킬 동기(`IsAsync` 기본값) (#131)
  - `Parameters`, `ReturnType`, `IsAsync` 위치: `PredefinedFunction` → `FunctionBlock`. 스킬의 `override`는 그대로 (#132)
  - 슬롯 직접 시전에서도 `Return` 누락·반환 타입 불일치가 콘솔 오류 + `InternalError` (#132)
- 박진우·임혜원과 확인할 것
  - 스킬 애니메이션 길이를 `TickCount`에 맞추는 방식 (#38)
  - 누수된 소환검 연출: `ObjectLeaked`를 받아 그 자리에 멈추고 흐리게 (#66)
