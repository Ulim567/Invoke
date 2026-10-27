# 사전 정의 함수 (C 스킬 작성 규칙)

- 담당자: 이우림

## 하는 일
- 게임이 통째로 제공하는 function(C 스킬, 공통 function, 특수 이동 function, 입력 function)을 만드는 형식
- 고정 ID로 사전 정의 함수 찾기
- 하지 않는 일
  - 실행: `FunctionExecutor` (`function-executor.md`)
  - 실제 대상 입력, 파라미터 값 읽기: 「함수 실행 중 대상 입력 기다리기」 이슈
  - 반환 값을 실제로 넘기기: 블록 실행 이슈
  - heap 계산: 메모리 작업 이슈

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
- heap: 실행 중에 객체를 만들 때 사용. 계산 방식은 메모리 작업 이슈

### 대상 지정 방식
- 대상: 파라미터로 받음

| 방식 | 파라미터 | 예 |
| --- | --- | --- |
| 단일 | `Target` 1개 | 검 찌르기, 꼬챙이(첫 대상), 검 다중베기 |
| 여러 명 선택 | `Target` list 1개 | 전장 휩쓸기 |
| 범위 | 없음. 캐릭터 주변에 바로 발동 | 검 크게베기, 검무 |
| 대상 없음 | 없음 | 검 소환, 해제, 패링, 이동 계열 |

- 파라미터로 받는 이유: 다른 function 안에서 불릴 때는 값 칸으로, 슬롯에서 바로 cast할 때는 플레이어 입력으로 대상을 받음. 두 경우를 같은 파라미터 하나로 받기 위함

### 동기·비동기
- 동기(`IsAsync = false`): 부른 쪽이 이 function이 끝날 때까지 기다림. 같은 `FunctionTask` 안에서 stack 프레임이 위로 쌓임
- 비동기(`IsAsync = true`): 새 `FunctionTask` 하나 생성. 부른 쪽은 기다리지 않고 다음 `Block`으로 넘어감. 새 `FunctionTask`는 다음 tick부터 진행
- 슬롯에서 바로 cast: 동기·비동기와 상관없이 새 `FunctionTask`

### 실행 중단 공격
- 맞았을 때: 그 캐릭터의 모든 `FunctionTask`가 끊김 (`FunctionExecutor.InterruptAll`)
- 실행 중이던 function마다 `OnEnd` 대신 `OnCancel`이 한 번 불림. 만들어 둔 오브젝트 정리는 `OnCancel`에서
- stack: 실행기가 돌려줌

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
| `Parameters` | `ParameterData` 목록 | 파라미터 없음 | 파라미터 이름과 타입. 함수 블록의 값 칸 수와 순서 |
| `ReturnType` | `VariableType` | `Void` | `Void`가 아니면 함수 블록을 값 칸에 끼울 수 있음 |
| `LocalVariableCount` | `int` | 0 | 실행 중에 쓰는 지역 변수 수. stack 프레임 크기 계산에 씀. 스킬마다 stack 부담을 조정하는 값 |
| `IsAsync` | `bool` | `false` (동기) | 다른 function 안에서 불렸을 때 기다리지 않으면 `true` |

#### 효과 실행 (몇 번째 tick에 무엇을 하는지)

| 이름 | 덮어쓰지 않으면 | 설명 |
| --- | --- | --- |
| `OnStart(FunctionTask task)` | 아무것도 하지 않음 | 첫 tick에 `OnTick`보다 먼저 한 번 불린다. |
| `OnTick(FunctionTask task, int tick)` | 아무것도 하지 않음 | 매 tick 불린다. `tick`은 1부터 `TickCount`까지 센다. 몇 번째 tick에 무엇을 할지(예: 자세잡기 → 차징 → 발사) 여기서 정한다. |
| `OnEnd(FunctionTask task)` | 아무것도 하지 않음 | 마지막 tick에 `OnTick` 다음으로 한 번 불린다. `Completed`일 때만 불린다. |
| `OnCancel(FunctionTask task)` | 아무것도 하지 않음 | 도중에 cancel됐을 때(`Overflow`, `Interrupted`) `OnEnd` 대신 한 번 불린다. 만들어 둔 오브젝트를 없애는 등의 정리를 한다. |

- 한 tick에서 부르는 순서: `OnStart`(첫 tick만) → `OnTick` → `OnEnd`(마지막 tick만)
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

	protected override void OnTick(FunctionTask task, int tick)
	{
		// 10 tick째에 cast한 캐릭터 앞쪽으로 판정을 낸다
		if (tick == 10) { /* task.Caster를 기준으로 판정 */ }
	}
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
- 아직 다른 사람과 정한 것 없음
