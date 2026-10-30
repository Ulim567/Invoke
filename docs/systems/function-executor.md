# FunctionExecutor

- 담당자: 이우림

## 하는 일
- 캐릭터마다 `FunctionExecutor`가 하나씩 붙어, cast한 function을 tick마다 진행한다. function을 한 번 cast할 때마다 `FunctionTask`가 하나 생기고, memory가 허락하는 만큼 여러 `FunctionTask`가 동시에 진행된다.
- function이 실행되는 동안 stack memory를 쓰고, function이 끝나거나 cancel되는 즉시 돌려준다.
- 하지 않는 일
  - 키 입력을 읽지 않는다. slot을 뗐을 때 `Cast`를 부르는 것은 cast 담당 스크립트가 한다.
  - 이펙트, 소리, 애니메이션을 하지 않는다. 연출은 `TaskStarted`, `TaskEnded` event를 받은 쪽이 한다.
  - code area, Overflow 시 재부팅, Java의 try, 버그 공격의 메모리 효과(`MemoryOccupy`, `ReclaimBlock`)는 아직 다루지 않는다. 이후 이슈에서 추가한다.
  - 빈 슬롯을 거르지 않는다. 빈 슬롯이면 cast 담당 스크립트가 `Cast`를 부르지 않는다.

## 동작 규칙
- 1 tick = `FixedUpdate` N회. N은 `FunctionExecutor`의 Inspector 값 `Fixed Updates Per Tick`이다(처음 값 1, 즉 0.02초).
- cast한 function은 다음 tick부터 진행한다.
- 같은 tick에서는 먼저 cast한 `FunctionTask`부터 진행한다.
- 모든 `Block`은 최소 1 tick을 쓴다. 플레이어가 놓은 블록 하나 = 1 tick (#90)
  - `PredefinedFunction`: `TickCount` tick
  - `UserFunction`: 실제로 실행된 `Block`들의 tick 합
  - `ReturnBlock`, 선언, 대입, 연산, `malloc`, `free`: 1 tick
  - 값 칸에 끼운 블록(연산, `malloc`, 값을 돌려주는 스킬): 자기 tick을 쓰고, 바깥 블록은 그 다음 tick에 진행. 예: `x = x + 2` → 더하기 1 tick, 대입 1 tick
  - 값 칸에 직접 입력한 값, 넣은 변수: 블록이 아니므로 tick을 쓰지 않음
  - 값 칸이 여럿: 왼쪽부터 차례로 실행
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
- `UserFunction` 안에서 `IsAsync`가 `true`인 `PredefinedFunction`을 만나면 끝나기를 기다리지 않는다. 그 function을 새 `FunctionTask`로 cast하고, 1 tick 뒤 다음 `Block`으로 넘어간다. 새 `FunctionTask`는 다음 tick부터 진행한다.
- `FunctionTask`는 function을 끝까지 실행하면 `Completed`로 끝난다. `UserFunction` 안에서 `ReturnBlock`을 만나면, 그 `ReturnBlock`을 담은 `UserFunction`만 끝나고 바깥 `UserFunction`은 다음 `Block`으로 넘어간다.

### 함수 데이터 실행 (#90)
- 실행 순서: 함수 데이터(`FunctionData`)를 `FunctionDataConverter`가 `UserFunction`으로 바꾼 뒤 `Cast`. 블록 목록을 위에서부터 차례로 실행
- 지원 블록: 선언, 대입, 연산(`+ - * / %`), 사전 정의 함수 블록, `malloc`, `free`
  - 조건문, `for`, `while`, 비교·논리: 블록 실행 2 이슈
  - 사용자 정의 함수 호출, list: 블록 실행 3 이슈
- 지원 타입: `int`, `float`, `bool`, `string`, `char`, 미리 정한 struct의 포인터(예: `Sword*`)
- 프레임 크기: 1 + 파라미터 수 + 지역 변수 수(`FunctionData.LocalVariables()`)
- 지역 변수
  - 저장: 실행 중인 `UserFunction` 복사본마다 변수 저장소(`VariableStore`) 하나. 같은 function을 두 번 시전하면 저장소 두 개
  - 찾기: 블록은 `FunctionTask`가 쌓아 둔 저장소 목록의 맨 위(지금 실행 중인 `UserFunction`의 것)에서 찾음. 함수 안에서 다른 함수를 부르면 위에 쌓이고, 끝나면 내려감(C의 호출 스택)
  - 블록 범위(조건문 안에서만 보이는 변수): 블록 실행 2 이슈
- 선언의 초기값 칸을 비움: 언어 규칙 파일의 기본값
- 값 칸: 직접 입력한 값(`"3"`, `"0.5"`, `"true"`, `"a"`), 변수, 값 블록(연산, `malloc`, 값을 돌려주는 사전 정의 함수)
- 숫자 계산과 타입 변환: 언어 규칙 파일의 「숫자 타입 변환」. C는 `ImplicitAll`
  - `int / int`: 정수 나눗셈(`7 / 2` = 3). `%`의 부호는 왼쪽 값을 따름(`-7 % 2` = -1)
  - `int` 범위를 넘음: 반대쪽 끝으로 돌아감
  - 0으로 나누기, `% 0`: `int`, `float` 모두 `DivideByZero`
  - 계산할 수 없는 타입(`string`, 포인터): `TypeMismatch`

| 규칙 | 연산에서 타입이 섞임 | 대입·선언에서 다른 타입 | 쓰는 언어 |
| --- | --- | --- | --- |
| `ImplicitAll` | `char`, `bool`은 `int`로. `int`와 `float`이 섞이면 `float` | 모두 바꿈. `float` → `int`는 소수점 버림(`int x = 0.9` → 0), 0이 아니면 `true` | C |
| `WideningOnly` | `char`는 `int`로, `bool` 계산 불가. `int`와 `float`이 섞이면 `float` | `int` → `float`, `char` → `int`·`float`만 | (Java, C#) |
| `Strict` | 같은 타입끼리만 | 같은 타입끼리만 | - |

- 사전 정의 함수 블록
  - 찾기: 고정 ID로 `PredefinedFunctionRegistry`
  - 파라미터 값 칸: 왼쪽부터 실행 후 스킬 복사본에 넘김. 비운 칸은 「값 없음」. 스킬은 `TryGetArgument`로 읽음([predefined-function.md](predefined-function.md))
  - tick: 값 칸에 끼운 블록이 없으면 그 스킬의 `TickCount`(슬롯에서 바로 시전했을 때와 같음)
  - 함수 몸통에 놓음: 반환 값은 버림
  - 값 칸에 끼움: 스킬이 `Return`으로 정한 값을 반환 타입으로 맞춰 내놓음. `Return` 없이 끝나면 콘솔 오류 + `MissingReturnValue`
  - 값 칸에 끼울 수 없음(변환 거부): 반환 타입 `Void`, 비동기 함수, 아직 다루지 않는 반환 타입
- 변환 거부: 지금 실행기가 실행할 수 없는 기능을 쓴 함수. 콘솔 오류, 시전하지 않음
  - 예: 지원하지 않는 블록·타입, 목록에 없는 struct, 없는 고정 ID, 비운 값 칸(사전 정의 함수의 파라미터 칸은 비워도 됨)
  - 플레이어 코드의 잘못(없는 변수, 같은 이름 두 번 선언 등): 거부하지 않음. 실행 중 실패로 처리

### heap
- malloc: 스킬 코드가 객체를 heap에 올리고 포인터를 받음. `Free`가 모자라면 `NullPointer`(0), 에러 없음
- free: 스킬 코드가 포인터로 객체를 해제. heap 칸은 즉시 반환. 오브젝트 삭제는 스킬 코드가 따로 함
- `malloc` 블록 (#90): 미리 정한 struct(`PredefinedStructs`)의 객체(`StructInstance`)를 heap에 올리고 포인터를 내놓음. 칸 수는 그 struct의 `HeapSize`. 모자라면 `NullPointer`, 에러 없음
  - 화면의 오브젝트(예: 소환검): 만들지 않음. 연결 방법은 박진우와 정할 것
- `free` 블록 (#90): 값 칸의 포인터를 해제. 쓸 수 없는 포인터이면 `InvalidPointerError`(`SkillFailed`, `SourceName`은 `free`)
- 포인터 값: 1부터 하나씩 커짐. 해제한 값은 다시 쓰지 않음
  - `int` 최댓값에 닿을 때: 1로 되돌리고, 아직 쓰는 값은 건너뜀
- 루트 집합: 포인터를 보관하는 목록들

| 목록 | 들어가는 포인터 | C에서 |
| --- | --- | --- |
| 작업의 포인터 목록 | 그 작업이 malloc한 포인터 | 지역 포인터 |
| 전역 포인터 목록 (캐릭터 하나에 하나) | 정상 종료한 작업에서 옮겨진 포인터. 어느 작업에서든 씀 | 전역 포인터 |

- 작업이 끝날 때
  - `Completed`: 그 작업의 포인터를 전역 포인터 목록으로 옮김
  - `Interrupted`, `Failed`: 그 작업의 포인터를 버림 (정리 블록이 실행되지 못함)
  - 그다음 루트 집합 어디에도 포인터가 없는 객체: 누수
- 누수 객체
  - heap 칸: 계속 차지
  - 객체 수(`CountObjects`): 포함. 소환검 최대 개수에 들어감
  - 포인터로 꺼내기·해제: 불가. 포인터 값을 알아도 같음
  - 반환: 영역 비우기(`Clear`)만
- 영역 비우기(`Clear`): heap 전부 비움. 아직 쓰던 객체까지 포함
  - 부르는 곳: 재부팅, 메모리 비우기 스킬 (이후 이슈)
- 지금 규칙의 한계: 정상 종료한 작업이 해제하지 않은 포인터는 지역 변수에만 담겼어도 모두 전역 포인터 목록으로 옮겨짐
  - 실제 C: 전역 변수에 담지 않은 지역 포인터는 함수가 끝나면 누수
  - 전역 변수 이슈: 전역 변수에 담긴 포인터를 루트 집합에 추가, 옮기는 규칙(`HeapStorage.HandOverPointers`)만 바꿈. 누수 판단은 그대로

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
| `UndeclaredVariable` | 값 칸이나 대입 블록의 변수가 지금 함수의 지역 변수에 없음 (#90) |
| `InvalidLiteral` | 직접 입력한 글자열을 그 타입의 값으로 바꿀 수 없음. 예: `int` 칸의 `"abc"` (#90) |
| `DuplicateVariable` | 같은 이름의 변수를 두 번 선언함 (#90) |
| `TypeMismatch` | 값을 변수·파라미터·반환 타입으로 바꿀 수 없음, 그 타입에 쓸 수 없는 연산. 예: `int x = "abc"`, C의 `float %` (#90) |
| `DivideByZero` | 0으로 나누기, `% 0` (#90) |
| `MissingReturnValue` | 값 칸에 끼운 사전 정의 함수가 `Return` 없이 끝남. 스킬 작성자의 실수 (#90) |

- 실패 종류마다 할 처리와 분류: 캐릭터 언어의 언어 규칙 파일(`LanguageRule`)이 정함
- 분류(`FailureCategory`): 플레이어 코드의 어떤 잘못인지. 화면이 compile error와 runtime error를 나누어 보여 줄 때 씀
  - `NotCodeError`: 코드의 잘못이 아님(쿨타임, 시전 전 메모리 부족 등)
  - `CompileError`: 그 언어에서 실행 전에 찾아내는 잘못
  - `RuntimeError`: 그 언어에서 실행 중에 찾아내는 잘못
  - 언어마다 다름. 예: 선언하지 않은 변수는 C에서 `CompileError`, Python에서 `RuntimeError`
  - 찾아내는 시점: 분류와 상관없이 지금은 모두 실행 중. 실행 전에 거부하는 일은 「만든 함수를 서버로 보내 검사·저장하기」 이슈의 구조 검사
  - `SkillFailed`는 한 줄이므로 스킬 에러는 종류와 상관없이 같은 분류
- 처리 종류(`FailureHandling`)와 시점별 동작

| 처리 | 시전 전 | 실행 중 |
| --- | --- | --- |
| `Reject` | 시전하지 않음, `CastRejected` | 그 `FunctionTask`만 `Failed`로 끝남 |
| `EndTask` | 시전하지 않음, `CastRejected` (끝낼 `FunctionTask`가 아직 없음) | 그 `FunctionTask`만 `Failed`로 끝남 |

- 표에 없는 실패 종류: Play를 시작할 때 콘솔 오류. 실제로 나면 콘솔 오류 후 `EndTask`
- 언어 규칙 파일 미연결: 콘솔 오류, 모든 실패를 `EndTask`
- `FunctionFailure`, `FailureHandling`, `FailureCategory`, `NumericConversion`의 새 값: 맨 끝에만 추가. 언어 규칙 파일이 값을 숫자로 저장하므로, 중간에 끼우면 이미 만든 파일의 값이 바뀜

### 쿨타임
- 붙는 곳: 슬롯에서 시전한 function(원본 객체)마다 하나. 같은 객체를 다시 `Cast`에 넘겨야 쿨타임이 이어짐
- 길이: 그 function의 `CooldownTicks`(tick 수)
  - `PredefinedFunction`: 스킬 구현자가 정함. 기본 0
  - `UserFunction`: 안에 든 `FunctionBlock`들의 `CooldownTicks` 합
    - 함수 데이터에서 바꾼 함수: 값 칸 안까지 모든 사전 정의 함수 블록의 합. 반복문 안의 함수는 한 번만 셈 (#90)
    - 코드로 직접 만든 함수: `Blocks`에 바로 들어 있는 `FunctionBlock`만 셈
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
| `GetFailureCategory(FunctionFailure failure)` | 실패 종류 | `FailureCategory` | 이 캐릭터 언어에서 그 실패의 분류. `TaskEnded`의 `task.Failure`, `CastRejected`의 실패 종류를 화면에 보여 줄 때 쓴다. 언어 규칙 파일이 없거나 표에 없으면 `NotCodeError` (#90) |
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

캐릭터 언어 하나의 규칙 파일이다.
- 만들기: Project 창 Create → Invoke → Language Rule
- 연결: 캐릭터의 `FunctionExecutor` Inspector `Language Rule` 칸
- 실패 처리 표: 실패 종류마다 처리, 분류 한 줄
- 기본값: 초기값을 비운 변수의 값. `int`, `float`, `bool`, `char`(글자 번호), `string` 칸. 포인터는 언제나 `NullPointer` (#90)
- 숫자 타입 변환: `ImplicitAll`, `Strict`, `WideningOnly` 중 하나(「함수 데이터 실행」의 표) (#90)
- `float %` 허용 여부 (#90)
- 연결하지 않음: 콘솔 오류. 실패는 모두 `EndTask`, 기본값과 숫자 타입 변환은 처음 값(C 규칙)
- C 파일(`Assets/_UL/Data/LanguageRule_C`)

| 실패 종류 | 처리 | 분류 |
| --- | --- | --- |
| `Overflow` | `EndTask` | `RuntimeError` |
| `NotEnoughMemory` | `Reject` | `NotCodeError` |
| `SkillFailed` | `EndTask` | `RuntimeError` |
| `OnCooldown` | `Reject` | `NotCodeError` |
| `UndeclaredVariable` | `EndTask` | `CompileError` |
| `InvalidLiteral` | `EndTask` | `CompileError` |
| `DuplicateVariable` | `EndTask` | `CompileError` |
| `TypeMismatch` | `EndTask` | `CompileError` |
| `DivideByZero` | `EndTask` | `RuntimeError` |
| `MissingReturnValue` | `EndTask` | `NotCodeError` |

- C 파일의 그 밖의 값: 기본값 0, 0, `false`, 글자 번호 0, 빈 글자열. 숫자 타입 변환 `ImplicitAll`, `float %` 허용하지 않음

### `ProcessMemory`

값은 `ProcessMemory`만 바꾼다. 다른 시스템은 읽기만 한다(예: memory 게이지 UI). heap 객체는 `Heap`의 함수로 다룬다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Capacity` | - | `int` | memory 전체 칸 수. `PlayerStats.MemorySize`와 같다. |
| `StackUsed` | - | `int` | 지금 stack이 쓰는 칸 수. 실행 중인 모든 `FunctionBlock`의 `FrameSize` 합 |
| `HeapUsed` | - | `int` | 지금 heap이 쓰는 칸 수. 해제하지 않은 객체와 누수 객체의 크기 합 |
| `Free` | - | `int` | 남은 칸 수. `Capacity - StackUsed - HeapUsed`이고, 0보다 작아지지 않는다. |
| `Heap` | - | `HeapStorage` | 이 캐릭터의 heap 객체 저장소 |

### `HeapStorage` (heap 객체 저장소)

- 가져오기: `ProcessMemory.Heap`. 컴포넌트가 아니므로 프리팹에 따로 붙이지 않음
- 스킬 코드: 이 표의 함수 대신 `PredefinedFunction`의 heap 함수를 씀 ([predefined-function.md](predefined-function.md))
- 오브젝트 생성·삭제: 하지 않음. 메모리 기록만 맡음

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `NullPointer` | - | `int` | null 포인터(0). malloc 실패 값. 이 값으로는 어떤 객체도 찾을 수 없음 |
| `Malloc(FunctionTask owner, object obj, int size)` | malloc하는 작업, 올릴 객체, 칸 수 | `int` | 포인터. 그 포인터는 `owner`의 포인터 목록에 들어감(`owner`가 `null`이면 전역 포인터 목록). heap이 모자라면 `NullPointer` |
| `Free(int pointer)` | 포인터 | `bool` | 해제하고 heap 칸 반환. `NullPointer`, 없는·해제한·누수된 포인터이면 `false` |
| `TryGet<T>(int pointer, out T obj)` | 포인터 | `bool` | 객체를 `T` 타입으로 꺼냄. 쓸 수 없는 포인터, 타입이 다른 객체, `Destroy`된 Unity 오브젝트이면 `false` |
| `GetGlobalPointers<T>()` | - | `List<int>` | 전역 포인터 목록의 `T` 객체 포인터. malloc한 순서 |
| `CountObjects<T>()` | - | `int` | heap의 `T` 객체 수. 누수 객체 포함 |
| `Clear()` | - | - | 영역 비우기. heap 전부 비우고 `HeapUsed`를 0으로 |
| `ObjectLeaked` (event) | `object` | - | 객체가 누수될 때. 누수된 소환검을 멈추고 흐리게 그리는 일은 받은 쪽 |
| `ObjectCleared` (event) | `object` | - | `Clear`로 객체를 지울 때 객체마다. 소환검 오브젝트 삭제는 받은 쪽 |

### `PredefinedFunction` (C 스킬, 공통 function, 특수 이동 function, 입력 function)
- 만드는 방법: [predefined-function.md](predefined-function.md)

### `UserFunction`, `ReturnBlock`

블록 코딩으로 만든 function이다. 플레이어가 짠 함수 데이터는 `FunctionDataConverter`가 이 형식으로 바꾼다. 테스트처럼 코드에서 직접 만들 때는 아래 생성자를 쓴다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `new UserFunction(string name, params Block[] blocks)` | 이름, 위에서부터 실행할 `Block`들 | `UserFunction` | `Block`에는 `PredefinedFunction`, `UserFunction`, `ReturnBlock`을 넣을 수 있다. 같은 객체를 여러 `UserFunction`의 `Block`으로 넣어도 된다. 프레임 크기는 1 |
| `new UserFunction(string name, int frameSize, params Block[] blocks)` | 이름, 프레임 크기, `Block`들 | `UserFunction` | 프레임 크기를 정해 만든다. 1보다 작으면 1 |
| `new ReturnBlock()` | - | `ReturnBlock` | 남은 `Block`을 실행하지 않고 이 `ReturnBlock`을 담은 `UserFunction`을 끝낸다. 1 tick을 쓴다. |

```csharp
// 예시. Dash, Thrust는 실제로 있는 function이 아니다
UserFunction dashThrust = new UserFunction("DashThrust", new Dash(), new Thrust());
executor.Cast(dashThrust);
```

### `FunctionDataConverter` (함수 데이터 → `UserFunction`, #90)

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TryConvert(FunctionData data, out UserFunction function)` | 함수 데이터 | `bool` | 바꾼 `UserFunction`(원본)을 `function`에 담는다. 프레임 크기와 쿨타임도 계산해 넣는다. 지금 실행할 수 없는 함수이면 콘솔 오류를 내고 `false` |

- 바꾼 함수: 원본. 슬롯은 같은 객체를 계속 `Cast`에 넘겨야 쿨타임이 이어짐

```csharp
// 예시. 슬롯에 넣은 함수 데이터를 바꿔서 시전
if (FunctionDataConverter.TryConvert(functionData, out UserFunction function)) executor.Cast(function);
```

## 맞춘 결과
- 김도훈과 정한 것
  - `ProcessMemory`가 memory 전체 칸 수로 `PlayerStats.MemorySize`를 읽는다 (#35)
  - `FunctionExecutor`가 쿨타임이 시작될 때 `PlayerStats.ApplyCooldownReduction`을 적용한다 (#38)
- 박진우와 확인할 것
  - 쿨타임을 동작 시간과 따로 `CooldownTicks`로 정한다 (#38)
  - 스킬마다 다른 조건(맨손, 소환검)은 스킬 코드가 `Fail`로 처리한다 (#38)
- 박진우와 확인할 것 (#66)
  - 검 소환·해제: `PredefinedFunction`의 `Malloc`, `Free`로 heap을 씀
  - 누수된 소환검: 소환검 최대 개수에 포함. `ObjectLeaked`를 받아 그 자리에 멈춤 (흐린 연출은 임혜원)
  - 영역 비우기: `ObjectCleared`를 받아 소환검 오브젝트 삭제
- 김도훈과 정할 것 (#66)
  - 버그 공격 `MemoryOccupy`, `ReclaimBlock`을 heap에 적용하는 방식
- 박진우와 확인할 것 (#90)
  - 스킬이 파라미터 값을 읽는 `TryGetArgument(이름)`, 반환 값을 정하는 `Return` ([predefined-function.md](predefined-function.md))
- 박진우와 정할 것 (#90)
  - `malloc` 블록이 heap에 올린 `StructInstance`(예: `Sword`)와 실제 소환검 오브젝트를 잇는 방법. 지금은 화면에 소환검이 나타나지 않음
- 임혜원과 확인할 것 (#90)
  - 실패를 화면에 보여 줄 때 `GetFailureCategory`로 compile error / runtime error 구분
- 정할 것 (#90)
  - tick 비용 규칙 재검토: 지금은 블록 하나 = 1 tick. 반복문을 만드는 블록 실행 2 이슈에서 「문법 블록은 tick 없음, 반복 한 바퀴에 1 tick」 등과 비교
  - 플레이어가 만든 함수의 `return` 누락 규칙: 블록 실행 3 이슈. C는 컴파일러에 따라 경고(GCC) 또는 오류(MSVC C4716), 실행되면 값은 정의되지 않음
  - 스킬 에러마다 분류 나누기: 지금은 `SkillFailed` 한 줄이라 모든 스킬 에러가 `RuntimeError`
  - 슬롯에서 함수 데이터를 바꿔 시전하는 연결: `FunctionDataConverter`를 부르는 곳([slot-caster.md](slot-caster.md))
