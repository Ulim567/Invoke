# FunctionExecutor

- 담당자: 이우림

## 하는 일
- 캐릭터마다 `FunctionExecutor`가 하나씩 붙어, cast한 function을 tick마다 진행한다. function을 한 번 cast할 때마다 `FunctionTask`가 하나 생기고, memory가 허락하는 만큼 여러 `FunctionTask`가 동시에 진행된다.
- function이 실행되는 동안 stack memory를 쓰고, function이 끝나거나 cancel되는 즉시 돌려준다.
- 하지 않는 일
  - 키 입력을 읽지 않는다. slot을 뗐을 때 `Cast`를 부르는 것은 cast 담당 스크립트가 한다.
  - 이펙트, 소리, 애니메이션을 하지 않는다. 연출은 `TaskStarted`, `TaskEnded` event를 받은 쪽이 한다.
  - heap, code area, Overflow 시 재부팅, Java의 try는 아직 다루지 않는다. 이후 이슈에서 추가한다.
  - 빈 슬롯을 거르지 않는다. 빈 슬롯이면 cast 담당 스크립트가 `Cast`를 부르지 않는다.

## 동작 규칙
- 1 tick = `FixedUpdate` N회. N은 `FunctionExecutor`의 Inspector 값 `Fixed Updates Per Tick`이다(처음 값 1, 즉 0.02초).
- cast한 function은 다음 tick부터 진행한다.
- 같은 tick에서는 먼저 cast한 `FunctionTask`부터 진행한다.
- 모든 `Block`은 최소 1 tick을 쓴다.
  - `PredefinedFunction`: `TickCount` tick
  - `UserFunction`: 실제로 실행된 `Block`들의 tick 합
  - `ReturnBlock`: 1 tick
- `FunctionBlock`은 실행을 시작할 때 `FrameSize`칸의 stack을 쓰고, 끝나거나 cancel되는 즉시 돌려준다. `ReturnBlock` 같은 문법 `Block`은 stack을 쓰지 않는다.
  - 반복문도 문법 `Block`이므로 stack을 쓰지 않는다. 반복문 안의 `FunctionBlock`은 한 번 끝날 때마다 stack을 돌려주므로, 반복 횟수와 상관없이 그 `FunctionBlock`의 `FrameSize`만큼만 쓴다.
  - `PredefinedFunction`의 `FrameSize`: 1 + 파라미터 수 + `LocalVariableCount`
- stack이 모자라면 실패 `Overflow`가 난다. 이미 실행 중이던 바깥 `FunctionBlock`들은 cancel되며 stack을 돌려준다. 그다음은 아래 「실패 처리」를 따른다.
- memory 전체 칸 수는 `PlayerStats.MemorySize`다.
  - 레벨업, 버프 등으로 `MemorySize`가 바뀌면 다음 `FunctionBlock`이 stack을 쓸 때부터 바로 반영된다.
  - `MemorySize`가 `StackUsed`보다 작아져도 실행 중인 `FunctionTask`는 cancel되지 않는다. `Free`가 0이 되어, 새로 실행을 시작하는 `FunctionBlock`만 `Overflow`로 끝난다.
- stack과 heap은 memory 한 영역을 함께 쓴다. 남은 칸(`Free`)은 `Capacity - StackUsed - HeapUsed`다.
  - 끝나는 순서 때문에 생기는 빈칸은 따지지 않으므로, stack과 heap은 각각 칸 수만 센다.
  - memory 게이지는 stack을 위에서부터, heap을 아래에서부터 그린다.
  - heap은 아직 없으므로 `HeapUsed`는 항상 0이다.
- `UserFunction` 안에서 `IsAsync`가 `true`인 `PredefinedFunction`을 만나면 끝나기를 기다리지 않는다. 그 function을 새 `FunctionTask`로 cast하고, 1 tick 뒤 다음 `Block`으로 넘어간다. 새 `FunctionTask`는 다음 tick부터 진행한다.
- `FunctionTask`는 function을 끝까지 실행하면 `Completed`로 끝난다. `UserFunction` 안에서 `ReturnBlock`을 만나면, 그 `ReturnBlock`을 담은 `UserFunction`만 끝나고 바깥 `UserFunction`은 다음 `Block`으로 넘어간다.

### 시전 전 검사
- `Cast`가 `FunctionTask`를 만들기 전에 공통 조건을 차례로 검사한다. 하나라도 걸리면 `FunctionTask`를 만들지 않고 거부한다.
  1. 쿨타임: 같은 function이 실행 중이거나 쿨타임 중이면 `OnCooldown`
  2. 메모리: `Free`가 그 function의 `FrameSize`보다 작으면 `NotEnoughMemory`
- 메모리 검사 범위: 맨 바깥 function의 프레임만. 안쪽 function의 stack은 실행 중에 `Overflow`로 처리
- 같은 tick에 연달아 시전: 둘 다 같은 `Free`를 보고 통과할 수 있음. 나중에 시작한 쪽은 실행 중에 `Overflow`
- `UserFunction` 안의 비동기 시전: 검사하지 않음
- 공통 조건 추가: `CastCondition`을 상속한 클래스 하나를 만들고, `FunctionExecutor.Awake`의 조건 목록에 추가
- `Cast(null)`: `ArgumentNullException`
- 스킬마다 다른 조건(맨손, 소환검 등)은 공통 조건이 아니다. 스킬 코드가 `Fail`로 직접 처리한다([predefined-function.md](predefined-function.md)).

### 실패 처리
- 실패 종류(`FunctionFailure`)

| 실패 종류 | 나는 때 |
| --- | --- |
| `Overflow` | 실행 중에 stack이 모자람 |
| `NotEnoughMemory` | 시전 전 검사에서 메모리가 모자람 |
| `SkillFailed` | 스킬 코드가 `Fail`로 에러를 냄. 어떤 에러인지는 `task.Error` |
| `OnCooldown` | 시전 전 검사에서 실행 중이거나 쿨타임 중 |

- 실패 종류마다 할 처리: 캐릭터 언어의 언어 규칙 파일(`LanguageRule`)이 정함
- 처리 종류(`FailureHandling`)와 시점별 동작

| 처리 | 시전 전 | 실행 중 |
| --- | --- | --- |
| `Reject` | 시전하지 않음, `CastRejected` | 그 `FunctionTask`만 `Failed`로 끝남 |
| `EndTask` | 시전하지 않음, `CastRejected` (끝낼 `FunctionTask`가 아직 없음) | 그 `FunctionTask`만 `Failed`로 끝남 |

- 표에 없는 실패 종류: Play를 시작할 때 콘솔 오류. 실제로 나면 콘솔 오류 후 `EndTask`
- 언어 규칙 파일 미연결: 콘솔 오류, 모든 실패를 `EndTask`
- `FunctionFailure`, `FailureHandling`의 새 값: 맨 끝에만 추가. 언어 규칙 파일이 값을 숫자로 저장하므로, 중간에 끼우면 이미 만든 파일의 값이 바뀜

### 쿨타임
- 붙는 곳: 슬롯에서 시전한 function(원본 객체)마다 하나. 같은 객체를 다시 `Cast`에 넘겨야 쿨타임이 이어짐
- 길이: 그 function의 `CooldownTicks`(tick 수)
  - `PredefinedFunction`: 스킬 구현자가 정함. 기본 0
  - `UserFunction`: 안에 든 `FunctionBlock`들의 `CooldownTicks` 합. 반복문 같은 문법 `Block` 안의 function은 아직 세지 않음(블록 실행 이슈)
- 시작: `FunctionTask`가 끝나는 tick부터. `Completed`, `Failed`, `Interrupted` 모두 같음
- 쿨타임 감소: 시작할 때 `PlayerStats.ApplyCooldownReduction`을 한 번 적용하고 tick 수로 반올림
- 실행 중인 function: 쿨타임이 아직 시작되지 않았지만 다시 시전할 수 없음(`OnCooldown`)
- `UserFunction` 안의 비동기 시전으로 만든 `FunctionTask`: 쿨타임을 시작하지 않음. 그 스킬의 쿨타임은 바깥 function의 쿨타임에 이미 더해져 있음

## 다른 시스템이 쓰는 것

캐릭터(Player 프리팹)에 `FunctionExecutor`, `ProcessMemory`가 붙어 있다. `GetComponent`로 가져온다.
- `ProcessMemory`는 같은 오브젝트에 `PlayerStats`가 있어야 한다(`RequireComponent`). memory 전체 칸 수를 `PlayerStats`에서 읽기 때문이다.

### `FunctionExecutor`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Cast(FunctionBlock function)` | cast할 function (`PredefinedFunction` 또는 `UserFunction`) | `FunctionTask` | function을 cast한다. 넘긴 객체는 원본으로 남고 복사본이 실행되므로, 같은 객체를 여러 번 넘겨도 된다. `FunctionTask`는 다음 tick부터 진행한다. 시전 전 검사에 걸리면 `FunctionTask`를 만들지 않고 `CastRejected`를 알린 뒤 `null`을 돌려준다. |
| `CastRejected` (event) | `FunctionBlock`, `FunctionFailure` | - | 시전 전 검사에 걸려 거부했을 때 불린다. 거부한 function과 실패 종류를 넘긴다. 거부 이유를 화면에 보여 줄 때 쓴다. |
| `TaskStarted` (event) | `FunctionTask` | - | `FunctionTask`가 첫 tick을 진행하기 직전에 불린다. 연출을 시작할 때 쓴다. |
| `TaskEnded` (event) | `FunctionTask` | - | `FunctionTask`가 끝나서 목록에서 빠질 때 불린다. 끝난 이유는 `task.EndReason`, 실패했으면 `task.Failure`, `task.Error`로 확인한다. |
| `CurrentTick` | - | `int` | 지금까지 지난 tick 수. 첫 tick이 1이다. |
| `SecondsPerTick` | - | `float` | 1 tick의 길이(초). 스킬 애니메이션 길이를 `TickCount`에 맞추거나, 쿨타임을 초로 보여 줄 때 쓴다. |
| `GetRemainingCooldownTicks(FunctionBlock function)` | 슬롯에서 cast한 function (원본) | `int` | 남은 쿨타임(tick 수). 쿨타임 중이 아니면 0. 쿨타임 UI에서 쓴다. |
| `IsRunning(FunctionBlock function)` | 슬롯에서 cast한 function (원본) | `bool` | 그 function의 `FunctionTask`가 진행 중이거나 다음 tick을 기다리면 `true` |
| `InterruptAll()` | - | - | 실행 중단 공격에 맞았을 때 부른다. 진행 중인 `FunctionTask`와 다음 tick을 기다리는 `FunctionTask`를 모두 `Interrupted`로 끝낸다. 끊긴 `FunctionTask`도 tick이 끝날 때 목록에서 빠지며 `TaskEnded`가 불린다. 진행을 시작하기 전에 끊긴 `FunctionTask`는 `TaskStarted` 없이 `TaskEnded`만 불린다. |
### `FunctionTask`

`FunctionTask`는 `FunctionExecutor`만 만든다. 다른 스크립트는 값을 읽기만 한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Caster` | - | `GameObject` | 이 `FunctionTask`를 cast한 캐릭터. `PredefinedFunction`은 이것으로 캐릭터의 위치, 방향, 컴포넌트를 찾는다. |
| `Name` | - | `string` | cast한 function의 이름 |
| `State` | - | `FunctionTaskState` | `Running`(진행 중), `WaitingInput`(입력 대기, 아직 쓰지 않음), `Ended`(끝남) |
| `EndReason` | - | `FunctionTaskEndReason` | `None`(진행 중), `Completed`(정상 종료), `Interrupted`(실행 중단 공격), `Failed`(실패. 실패 종류는 `Failure`) |
| `IsEnded` | - | `bool` | 끝났으면 `true` |
| `IsFailed` | - | `bool` | 실패로 끝났으면 `true`. 실패 여부는 `Failure`가 아니라 이 값으로 판단한다. |
| `Failure` | - | `FunctionFailure?` | 실패로 끝났을 때 실패 종류. 실패로 끝나지 않았으면 `null` |
| `Error` | - | `SkillError` | `Failure`가 `SkillFailed`일 때 스킬이 낸 에러. 그 밖에는 `null` |

### `LanguageRule` (언어 규칙 파일)

캐릭터 언어 하나의 실패 처리 표다.
- 만들기: Project 창 Create → Invoke → Language Rule
- 연결: 캐릭터의 `FunctionExecutor` Inspector `Language Rule` 칸
- 표: 실패 종류마다 처리 한 줄
- C 파일(`Assets/_UL/Data/LanguageRule_C`)

| 실패 종류 | 처리 |
| --- | --- |
| `Overflow` | `EndTask` |
| `NotEnoughMemory` | `Reject` |
| `SkillFailed` | `EndTask` |
| `OnCooldown` | `Reject` |

### `ProcessMemory`

값은 `ProcessMemory`만 바꾼다. 다른 시스템은 읽기만 한다(예: memory 게이지 UI).

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Capacity` | - | `int` | memory 전체 칸 수. `PlayerStats.MemorySize`와 같다. |
| `StackUsed` | - | `int` | 지금 stack이 쓰는 칸 수. 실행 중인 모든 `FunctionBlock`의 `FrameSize` 합 |
| `HeapUsed` | - | `int` | 지금 heap이 쓰는 칸 수. heap은 아직 없으므로 항상 0 |
| `Free` | - | `int` | 남은 칸 수. `Capacity - StackUsed - HeapUsed`이고, 0보다 작아지지 않는다. |

### `PredefinedFunction` (C 스킬, 공통 function, 특수 이동 function, 입력 function)
- 만드는 방법: [predefined-function.md](predefined-function.md)

### `UserFunction`, `ReturnBlock`

블록 코딩으로 만든 function이다. 블록 코딩 화면이 플레이어가 짠 블록들을 이 형식으로 바꿔 `Cast`에 넘긴다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `new UserFunction(string name, params Block[] blocks)` | 이름, 위에서부터 실행할 `Block`들 | `UserFunction` | `Block`에는 `PredefinedFunction`, `UserFunction`, `ReturnBlock`을 넣을 수 있다. 같은 객체를 여러 `UserFunction`의 `Block`으로 넣어도 된다. |
| `new ReturnBlock()` | - | `ReturnBlock` | 남은 `Block`을 실행하지 않고 이 `ReturnBlock`을 담은 `UserFunction`을 끝낸다. 1 tick을 쓴다. |

```csharp
// 예시. Dash, Thrust는 실제로 있는 function이 아니다
UserFunction dashThrust = new UserFunction("DashThrust", new Dash(), new Thrust());
executor.Cast(dashThrust);
```

## 맞춘 결과
- 김도훈과 정한 것
  - `ProcessMemory`가 memory 전체 칸 수로 `PlayerStats.MemorySize`를 읽는다 (#35)
  - `FunctionExecutor`가 쿨타임이 시작될 때 `PlayerStats.ApplyCooldownReduction`을 적용한다 (#38)
- 박진우와 확인할 것
  - 쿨타임을 동작 시간과 따로 `CooldownTicks`로 정한다 (#38)
  - 스킬마다 다른 조건(맨손, 소환검)은 스킬 코드가 `Fail`로 처리한다 (#38)
- 박진우와 정해야 할 것
  - 스킬별로 stack 밖에서 따로 쓰는 memory(예: 검 소환의 heap)를 어떻게 다룰지
