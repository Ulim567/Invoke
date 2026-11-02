# FunctionExecutor

- 담당자: 이우림

## 하는 일
- 캐릭터마다 `FunctionExecutor`가 하나씩 붙어, cast한 function을 tick마다 진행한다. function을 한 번 cast할 때마다 `FunctionTask`가 하나 생기고, memory가 허락하는 만큼 여러 `FunctionTask`가 동시에 진행된다.
- function이 실행되는 동안 stack memory를 쓰고, function이 끝나거나 cancel되는 즉시 돌려준다.
- 하지 않는 일
  - 키 입력을 읽지 않는다. slot을 뗐을 때 `Cast`를 부르는 것은 cast 담당 스크립트가 한다.
  - 이펙트, 소리, 애니메이션을 하지 않는다. 연출은 `TaskStarted`, `TaskEnded` event를 받은 쪽이 한다.
  - code area, Overflow 시 재부팅 시간(디버프), Java의 try, 버그 공격의 메모리 효과(`MemoryOccupy`, `ReclaimBlock`)는 아직 다루지 않는다. 이후 이슈에서 추가한다. Overflow 때 프로세스를 새로 시작하는 것은 한다(#143).
  - 빈 슬롯을 거르지 않는다. 빈 슬롯이면 cast 담당 스크립트가 `Cast`를 부르지 않는다.
- 멀티플레이 (#173): 함수는 서버의 실행기만 실행. 클라이언트 화면의 실행기는 멈추고(`TickEnabled` false) 서버가 알려 준 메모리·쿨타임 값으로 답함. 시전 요청·알림은 `FunctionExecutorNetwork`([network.md](network.md))

## 파일 위치
- `Assets/_UL/Scripts/FunctionExecutor/` 아래 (#118)

| 폴더 | 파일 | 맡는 일 |
| --- | --- | --- |
| `Core` | `FunctionExecutor`, `FunctionTask`, `FunctionContext`, `CooldownTracker`, `FunctionExecutorLogger` | 시전, tick 진행, 작업 상태, 실행 문맥, 쿨타임 |
| `Conditions` | `CastCondition`, `CooldownCondition`, `MemoryCondition` | 시전 전 검사 |
| `Blocks` | `Block`, `FunctionBlock`, `UserFunction`, `ValueBlock`, `ValueSlots`, `Statements`, `CallBlock`, `LiteralValue`, `VariableValue`, `ArithmeticBlock`, `CompareBlock`, `LogicBlock`, `NotBlock`, `ConditionalBlock`, `LoopBlock`, `ForBlock`, `WhileBlock`, `AssignBlock`, `DeclareBlock`, `MallocBlock`, `FreeBlock`, `ArrayDeclareBlock`, `ArrayGetBlock`, `ArraySetBlock` | 블록 코딩의 블록 |
| `Predefined` | `PredefinedFunction`, `InputFunction`, `PredefinedFunctionIds`, `PredefinedFunctionRegistry`, `SkillError`, `SwordErrors`, `PointerErrors`, `Input/GetBug` | 스킬 작성자가 쓰는 것, 입력 함수 |
| `Language` | `LanguageRule`, `LanguageRuleData`, `NumericRules` | 언어 규칙 |
| `Memory` | `ProcessMemory`, `HeapStorage`, `StructInstance` | stack, heap |
| `Values` | `RuntimeValue`, `VariableStore`, `ArrayStorage` | 실행 중의 값, 지역 변수, array 원소 |
| `Conversion` | `FunctionDataConverter` | 함수 데이터 → `UserFunction` |
| `Test` | 테스트 컴포넌트, 테스트용 함수·블록 | 테스트 |

## 동작 규칙
- 1 tick = `FixedUpdate` N회. N은 `FunctionExecutor`의 Inspector 값 `Fixed Updates Per Tick`이다(처음 값 1, 즉 0.02초).
- cast한 function은 다음 tick부터 진행한다.
- 같은 tick에서는 먼저 cast한 `FunctionTask`부터 진행한다.
- 모든 `Block`은 최소 1 tick을 쓴다. 플레이어가 놓은 블록 하나 = 1 tick (#90)
  - `PredefinedFunction`: `TickCount` tick
  - `UserFunction`: 실제로 실행된 `Block`들의 tick 합
  - `ReturnBlock`, 선언, 대입, 연산, 비교, 논리(`&& || !`), `malloc`, `free`, 배열 만들기(길이와 상관없음), 원소 읽기·쓰기: 1 tick
  - expression으로 쓴 블록(연산, 비교, 논리, `malloc`, 값을 돌려주는 함수, 원소 읽기): 자기 tick을 쓰고, 바깥 블록은 그 다음 tick에 진행. 예: `x = x + 2` → 더하기 1 tick, 대입 1 tick
  - value slot에 직접 입력한 값, 넣은 변수: 블록이 아니므로 tick을 쓰지 않음
  - value slot이 여럿: 왼쪽부터 차례로 실행
  - 논리 블록의 short-circuit으로 실행하지 않은 오른쪽 value slot: tick을 쓰지 않음 (#120)
  - 조건문 (#120): 실행이 닿은 갈래(`if`, `else if`, `else`)마다 1 tick. 조건의 expression은 그 앞에 자기 tick. 고른 갈래의 `{ }`는 다음 tick부터. 예: `x`가 1일 때 `if (x > 3) {...} else { x = 2; }` → 비교, `if`, `else`, 대입 4 tick
    - 고른 갈래가 없음, 고른 갈래의 `{ }`가 비어 있음: 그 갈래의 tick에 끝남
  - 반복문 (#120): 검사 한 번마다 1 tick. 검사의 expression은 그 앞에 자기 tick. `{ }`는 검사 다음 tick부터. 마지막 거짓 검사도 1 tick
    - `for`: `i` 만들기와 `i++`는 검사와 같은 tick. 같은 뜻의 `while`보다 바퀴당 약 3 tick 적음(`while`은 비교·더하기·대입 블록을 따로 놓음)
    - 빈 `{ }`: 바로 다음 tick에 다음 검사. 한 바퀴는 최소 1 tick이므로 `while (true) { }`도 게임을 멈추지 않음
    - `break`, `continue`: 1 tick. `break`는 그 tick에 반복문이 끝남(마지막 검사 없음), `continue`는 다음 tick에 다음 검사
- `FunctionBlock`은 실행을 시작할 때 `FrameSize`칸의 stack을 쓰고, 끝나거나 cancel되는 즉시 돌려준다. `ReturnBlock` 같은 문법 `Block`은 stack을 쓰지 않는다.
  - 반복문도 문법 `Block`이므로 stack을 쓰지 않는다. 반복문 안의 `FunctionBlock`은 한 번 끝날 때마다 stack을 돌려주므로, 반복 횟수와 상관없이 그 `FunctionBlock`의 `FrameSize`만큼만 쓴다.
  - `PredefinedFunction`의 `FrameSize`: 1 + 파라미터 수 + `LocalVariableCount`
  - function 안에서 다른 function을 부름(동기): 불린 function의 프레임이 위에 쌓이고, 끝나는 즉시 내려감. 예: 프레임 2칸인 A가 3칸인 B를 부르면 B 실행 중 5칸, B가 끝나면 2칸 (#132)
  - 재귀 호출: 부를 때마다 프레임이 쌓임. 끝나지 않는 재귀는 stack이 모자라 `Overflow` (#132)
- stack이 모자라면 실패 `Overflow`가 난다. 이미 실행 중이던 바깥 `FunctionBlock`들은 cancel되며 stack을 돌려준다. 그다음은 아래 「실패 처리」를 따른다.
- memory 전체 칸 수는 `PlayerStats.MemorySize`다.
  - 레벨업, 버프 등으로 `MemorySize`가 바뀌면 다음 `FunctionBlock`이 stack을 쓸 때부터 바로 반영된다.
  - `MemorySize`가 `StackUsed`보다 작아져도 실행 중인 `FunctionTask`는 cancel되지 않는다. `Free`가 0이 되어, 새로 실행을 시작하는 `FunctionBlock`만 `Overflow`로 끝난다.
- 데이터 영역(전역 변수), stack, heap은 memory 한 영역을 함께 쓴다. 남은 칸(`Free`)은 `Capacity - DataUsed - StackUsed - HeapUsed`다. (#139)
  - 끝나는 순서 때문에 생기는 빈칸은 따지지 않으므로, 세 영역은 각각 칸 수만 센다.
  - memory 게이지는 stack을 위에서부터, heap을 아래에서부터 그린다.
  - 데이터 영역: 아직 게이지에 그리지 않음. 맨 아래에 따로 그리고 heap을 그 위부터 그리는 것을 임혜원에게 요청할 예정 (#139)
- `UserFunction` 안에서 `IsAsync`가 `true`인 function(사전 정의 함수, 사용자 정의 함수 #132)을 만나면 끝나기를 기다리지 않는다. 그 function을 새 `FunctionTask`로 cast하고, 1 tick 뒤 다음 `Block`으로 넘어간다. 새 `FunctionTask`는 다음 tick부터 진행한다. 바깥 function이 그 tick에 끝나도(마지막 줄의 비동기 호출) 새 `FunctionTask`는 만들어진다.
- `FunctionTask`는 function을 끝까지 실행하면 `Completed`로 끝난다. `UserFunction` 안에서 `ReturnBlock`을 만나면, 그 `ReturnBlock`을 담은 `UserFunction`만 끝나고 바깥 `UserFunction`은 다음 `Block`으로 넘어간다.
- 스킬의 동기·비동기 (#131): 모든 스킬이 동기(`IsAsync` 기본값 `false`). 비동기 스킬이 생기면 그 스킬의 `IsAsync`만 `true`로 바꿈
- 사용자 정의 함수의 동기·비동기 (#132): 함수 데이터의 `FunctionData.IsAsync`. 기본값 `false`(동기). 편집 화면에서 고르는 UI는 아직 없음

### 입력 대기 (#131)
- 입력 함수(`InputFunction`을 상속한 사전 정의 함수, 예: `GetBug`)가 입력을 요청하면 그 `FunctionTask`만 `WaitingInput`으로 멈춤. 다른 `FunctionTask`는 그대로 진행
- 기다리는 동안
  - tick: 세지 않음. 입력을 요청한 tick은 1 tick으로 셈
  - stack: 프레임을 돌려주지 않음. 기다리는 `FunctionTask`의 프레임도 memory 게이지에 남음
  - 캐릭터 이동 막기: 입력을 받는 쪽이 함(`SlotCaster`가 대상을 고르는 동안 Player 액션 맵 비활성화)
- 순서
  1. `FunctionExecutor`가 그 tick의 `Step` 다음에 `InputRequested`를 부름
  2. 입력을 받는 쪽이 요청한 입력 함수의 고정 ID로 자기 입력인지 확인하고, 값을 정해 `ProvideInput`을 부름. `InputRequested` 안에서 바로 불러도 됨
  3. `FunctionTask`가 다음 tick부터 이어서 진행. 입력 함수는 그 tick에 값을 돌려줌
- `GetBug`의 tick: 요청 1 tick + 값을 돌려주는 1 tick. 예: `Target b = GetBug(); Thrust(b);`에서 대기 tick을 빼면 `GetBug` 2, 선언 1, `Thrust`의 `TickCount`
- 기다리는 중에 실행 중단 공격(`InterruptAll`): `Interrupted`로 끝나고 프레임을 돌려줌. 끝난 `FunctionTask`에 `ProvideInput`을 부르면 `false`
- 입력 함수가 값을 받기 전에 끝남(`Stop`, `Fail`, 실행 중단): 남은 입력 요청을 지움
- 입력 요청: 입력 함수(`InputFunction`)만 함. C 스킬은 대상을 파라미터로만 받음

### 함수 데이터 실행 (#90)
- 실행 순서: 함수 데이터(`FunctionData`)를 `FunctionDataConverter`가 `UserFunction`으로 바꾼 뒤 `Cast`. 블록 목록을 위에서부터 차례로 실행
- 지원 블록: 선언, 대입, 연산(`+ - * / %`), 비교(`> < == != >= <=`, #120), 논리(`&& || !`, #120), 조건문(`if`, `else if`, `else`, #120), `for`, `while`, `break`, `continue`(#120), 사전 정의 함수 블록, `malloc`, `free`, 사용자 정의 함수 블록, `return`, 배열 만들기, 원소 읽기·쓰기(#132)
  - list: 없음. list가 문법에 있는 언어(Java, Python)의 캐릭터가 생길 때 (#132)
- 지원 타입: `int`, `float`, `bool`, `string`, `char`, 미리 정한 struct의 포인터(예: `Sword*`), 대상(`Target`, 종류 `Bug`, #131), 그 타입들의 array(#132)
- 프레임 크기: 1 + 파라미터 수 + 지역 변수 수(`FunctionData.LocalVariables()`)
  - array 지역 변수: 길이만큼. 예: `int x; int arr[5];` → 1 + 1 + 5 = 7칸 (#132)
  - array 파라미터: 1칸. C처럼 원소가 아니라 array를 가리키는 값 하나만 넘어옴 (#132)
- 대상(`Target`) (#131)
  - 타입: `VariableType.TargetOf(TargetKind.Bug)`. 종류(`TargetKind`)는 지금 `Bug`(몬스터)만
  - 종류를 정하지 않은 `Target`(예: `DataDrivenSkill`의 파라미터): 어떤 종류의 대상이든 받음. 값은 언제나 자기 종류를 가짐
  - 값: `RuntimeValue.FromBug(몬스터)`. 「대상 없음」은 `FromBug(null)`
  - 초기값을 비운 `Target` 변수: 「대상 없음」(언어와 상관없음)
  - 비교: `==`, `!=`만. 같은 종류의 대상끼리는 같은 대상인지, `NULL`과는 「대상 없음」인지. 죽었거나 없어진 대상은 `NULL`과 같지 않음. 크기 비교, 다른 종류끼리 비교: `TypeMismatch`
  - 직접 입력한 `0`: `NULL`로 보지 않음(`zeroIsNull`은 포인터 자리의 규칙)
  - 계산, 조건으로 쓰기(`if (b)`): `TypeMismatch`
  - 스킬에 argument로 넘길 때: 「대상 없음」, 죽은 대상(`Monster.IsDead`), 오브젝트가 없어진 대상이면 `InvalidTarget`. 변수에 담거나 비교할 때는 검사하지 않음(C의 해제한 포인터처럼 쓰는 순간에 에러)
- 지역 변수
  - 저장: 실행 중인 `UserFunction` 복사본마다 변수 저장소(`VariableStore`) 하나. 같은 function을 두 번 시전하면 저장소 두 개
  - 찾기: 블록은 실행 문맥(`FunctionContext`)이 쌓아 둔 저장소 목록의 맨 위(지금 실행 중인 `UserFunction`의 것)에서 찾음. 함수 안에서 다른 함수를 부르면 위에 쌓이고, 끝나면 내려감(C의 호출 스택)
  - 블록 범위 (#120): C처럼 `{ }` 하나가 범위 하나
    - `{ }` 안에서 선언한 변수: `}`에서 사라짐. 반복문은 바퀴마다 새 변수
    - 찾기: 안쪽 범위부터. 바깥과 같은 이름이면 안쪽 변수
    - function body의 맨 바깥 범위: 파라미터와 같은 범위(따로 열지 않음)
    - 같은 범위에 같은 이름 선언: `DuplicateVariable`
    - 바깥 범위와 같은 이름 선언: 언어 규칙 파일의 `allowShadowing`. C는 허용, 허용하지 않으면 `DuplicateVariable`
    - 프레임 크기: 범위와 상관없이 function body의 모든 선언을 셈(지금과 같음)
    - 블록 목록 실행: `Statements`가 위에서부터 차례로 실행하고, `{ }` 하나마다 범위를 열고 닫음. `UserFunction`의 function body와 조건문·반복문의 `{ }`가 함께 씀
- 선언의 initializer를 비움: 언어 규칙 파일의 기본값
- value slot: 직접 입력한 값(`"3"`, `"0.5"`, `"true"`, `"a"`, `NULL`), 변수, 값 블록(연산, 비교, 논리, `malloc`, 값을 돌려주는 사전 정의 함수)
- 숫자 계산과 타입 변환: 언어 규칙 파일의 「숫자 타입 변환」. C는 `ImplicitAll`
  - `int / int`: 정수 나눗셈(`7 / 2` = 3). `%`의 부호는 왼쪽 값을 따름(`-7 % 2` = -1)
  - `int` 범위를 넘음: 반대쪽 끝으로 돌아감
  - 0으로 나누기, `% 0`: `int`, `float` 모두 `DivideByZero`
  - 계산할 수 없는 타입(`string`, 포인터): `TypeMismatch`
- 비교 (#120): 결과는 `bool`
  - 타입이 섞임: 아래 표의 「연산에서 타입이 섞임」 규칙으로 맞춘 뒤 비교(`3 == 3.0` → `true`). `int`끼리는 `float`으로 바꾸지 않음
  - 같은 타입끼리: 언어와 상관없이 비교 가능. 단 `bool`의 크기 비교(`true > false`)는 `ImplicitAll`만
  - 포인터: `==`, `!=`만. 같은 타입의 포인터끼리, 또는 한쪽이 `NULL`이면 주소 비교. 크기 비교, 다른 타입과의 비교는 `TypeMismatch`
  - `string`: 언어 규칙 파일의 `stringEquality`. C는 `Disallowed`(`TypeMismatch`), `ByContent`이면 `==`, `!=`로 글자 내용 비교. 크기 비교는 언제나 `TypeMismatch`
  - 그 밖의 타입이 섞임(예: `string`과 `int`): `TypeMismatch`
- `NULL` (#120)
  - 직접 입력: 함수 데이터에 `Type = PointerTo(null)`, `Literal = "Null"`(`SlotData.FromNull()`). 실행기는 `NullPointer`로 읽음
  - 어떤 포인터와도 비교하고, 어떤 포인터 변수에도 넣음(`Sword* p = NULL`)
  - 직접 입력한 `0`: 언어 규칙 파일의 `zeroIsNull`이 켜져 있으면(C) 포인터 자리(포인터와 비교, 포인터 변수에 넣기, `free`)에서 `NULL`. 계산하거나 변수에서 읽은 0은 `NULL`이 아님
- 논리 (#120): 결과는 `bool`
  - short-circuit: `&&`의 왼쪽이 거짓, `||`의 왼쪽이 참이면 오른쪽 value slot을 실행하지 않음(C와 같음)
  - 조건 값 변환(`NumericRules.TryConvertToCondition`): 논리 블록, 조건문, `while`이 함께 씀
    - `bool`: 모든 언어에서 그대로
    - `ImplicitAll`(C): 숫자는 0이 아니면 참, 포인터는 `NULL`이 아니면 참(`!5` → `false`)
    - `Strict`, `WideningOnly`: `bool`만
    - 조건으로 쓸 수 없는 값(`string` 등): `TypeMismatch`
- 조건문 (#120)
  - 함수 데이터: `if`, `else if`, `else`가 따로 놓인 블록. `FunctionDataConverter`가 연달아 놓인 `If` + `ElseIf`들 + `Else`를 `ConditionalBlock` 하나로 묶음
  - 변환 거부: 앞에 `If`가 없는 `ElseIf`, `Else`(`else` 바로 뒤의 갈래 포함), 비어 있는 조건 칸
  - `{ }`: 범위 하나. `{ }` 안의 실패, `return`, `break`, `continue`는 바깥에 그대로 전달(`if` 안의 `break`도 바깥 반복문에 걸림)
- 반복문 (#120)
  - `for (int i = start; i < end; i++)`: `start`를 `int`로 바꿔 `i` 선언. `end`는 검사마다 다시 읽음. `{ }`가 `i`를 바꾸면 바뀐 값으로 계속. `i`가 `int` 범위를 넘으면 반대쪽 끝으로
    - `i`: `for`만의 범위. `for`가 끝나면 사라짐. 바깥과 같은 이름은 `allowShadowing`을 따름
    - `start`를 `int`로 바꿀 수 없음, `i < end`를 비교할 수 없음: `TypeMismatch`
  - `while (조건)`: 조건 값 변환은 논리 블록과 같음
  - `{ }`: 바퀴마다 새 범위. `{ }` 안의 선언은 바퀴마다 새 변수
  - `break`, `continue`: 가장 안쪽 반복문에만 적용. 반복문 밖에서 만나면 `NotInLoop`(함수를 넘어 바깥 함수의 반복문에 걸리지 않음)
  - 반복문 밖의 `break`, `continue` 찾기: `BlockData.HasJumpOutsideLoop(blocks)`. 저장할 때 검사하는 이슈가 씀. 실행기는 쓰지 않음

| 규칙 | 연산에서 타입이 섞임 | 대입·선언에서 다른 타입 | 쓰는 언어 |
| --- | --- | --- | --- |
| `ImplicitAll` | `char`, `bool`은 `int`로. `int`와 `float`이 섞이면 `float` | 모두 바꿈. `float` → `int`는 소수점 버림(`int x = 0.9` → 0), 0이 아니면 `true` | C |
| `WideningOnly` | `char`는 `int`로, `bool` 계산 불가. `int`와 `float`이 섞이면 `float` | `int` → `float`, `char` → `int`·`float`만 | (Java, C#) |
| `Strict` | 같은 타입끼리만 | 같은 타입끼리만 | - |

- 사전 정의 함수 블록
  - 찾기: 고정 ID로 `PredefinedFunctionRegistry`
  - argument: 왼쪽부터 실행 후 스킬 복사본에 넘김. 비운 칸은 「값 없음」. 스킬은 `TryGetArgument`로 읽음([predefined-function.md](predefined-function.md))
    - 쓸 수 없는 대상(「대상 없음」, 죽음, 없어짐): 스킬이 시작하기 직전(쿨타임 기다림 뒤)에 `InvalidTarget`. 스킬은 시작하지 않고 쿨타임도 걸리지 않음. 슬롯 직접 시전(`Cast(스킬, 대상)`)도 같음 (#131)
  - tick: argument에 expression으로 쓴 블록이 없으면 그 스킬의 `TickCount`(슬롯에서 바로 시전했을 때와 같음)
  - 클래스: statement와 expression, 사전 정의 함수와 사용자 정의 함수 모두 `CallBlock` (#132)
  - statement로 놓음: 반환 값은 버림. `Return` 누락·반환 타입 불일치는 expression으로 썼을 때와 같이 실패
  - expression으로 씀: 스킬이 `Return`으로 정한 값을 반환 타입으로 맞춰 내놓음
  - `Return` 없이 끝남, 반환 타입으로 맞출 수 없는 값: 콘솔 오류 + 작업을 `InternalError`로 끝냄 (개발자 책임)
    - 확인 시점: 스킬이 끝날 때(`FunctionBlock.TryValidateEnd`). 슬롯에서 바로 시전한 스킬도 같음 (#132)
  - expression으로 쓸 수 없음(변환 거부): 반환 타입 `Void`, 비동기 함수, 아직 다루지 않는 반환 타입
- 사용자 정의 함수 블록 (#132)
  - 찾기: 함수 블록의 GUID(`BlockData.FunctionId`)로, `TryConvert`에 넘긴 캐릭터의 함수 목록에서
  - 변환: 한 번의 `TryConvert`에서 같은 GUID는 한 번만. 같은 함수를 부르는 블록들이 바꾼 함수 하나를 함께 씀
  - 재귀 호출(자기 자신, 서로 부름): 허용
  - argument: 왼쪽부터 실행 후 파라미터 타입으로 맞춰 복사본에 넘김. 맞출 수 없으면 `TypeMismatch`
    - 받는 쪽: 실행을 시작할 때 파라미터를 function body의 맨 바깥 범위에 지역 변수로 선언. 같은 이름의 지역 변수를 선언하면 `DuplicateVariable`(C와 같음)
    - 넘기는 방식: 값 복사. 포인터는 주소, array는 같은 array
    - 슬롯에서 바로 시전(argument 없음): 언어 규칙 파일의 기본값. array 파라미터는 길이 0인 array
  - 비운 argument, argument 수가 파라미터 수와 다름: 변환 거부(C는 argument를 빠뜨릴 수 없음)
  - 비동기 함수(`FunctionData.IsAsync`): 새 `FunctionTask`로 시전. expression으로 쓸 수 없음(변환 거부)
- `return` 블록 (#132)
  - 칸: value slot 하나(비워도 됨). 값에 쓴 블록이 있으면 그 블록의 tick 다음 tick에 함수를 끝냄
  - 반환 값 확인: 그 `return`을 담은 함수가 끝날 때. 슬롯에서 바로 시전해 반환 값을 쓰지 않아도 같음

| 끝난 방식 | `void` 함수 | 반환 타입이 있는 함수 |
| --- | --- | --- |
| 마지막 블록까지 실행함 | 정상 종료 | `MissingReturn` |
| `return;` | 정상 종료 | `MissingReturn` |
| `return 값;` | `TypeMismatch` | 반환 타입으로 맞춤. 맞출 수 없으면 `TypeMismatch` |

  - return 누락의 compile error: 원래는 편집 화면에서 저장할 때 찾아낼 잘못(MSVC 방식). 저장 검사가 생기기 전까지는 실행 중에 `MissingReturn`
  - 저장 검사용 판정: `BlockData.AlwaysReturnsValue(blocks)`. 값을 넣은 `return`, 또는 `else`까지 있고 모든 갈래가 반드시 `return`하는 조건문이 있으면 `true`. 반복문 안의 `return`은 세지 않음. 실행기는 쓰지 않음
- array (#132)
  - 만들기: 배열 만들기 블록(`int arr[5]`). 일반 선언 블록의 array 타입은 변환 거부
  - 길이: 직접 입력한 1 이상의 정수만(C89). 변수, 값 블록, 0 이하는 변환 거부
  - 처음 값: 모든 원소가 언어 규칙 파일의 기본값. 초기값 목록(`{1, 2, 3}`) 없음
  - 원소 타입: 변수로 선언할 수 있는 타입. 2차원 array는 변환 거부
  - memory: 그 array를 만든 function의 stack 프레임. function이 시작할 때 프레임과 함께 받고, 끝날 때 함께 돌려줌. `free` 필요 없음
  - 범위: 일반 변수와 같음. `{ }` 안의 array는 `}`에서 사라지고, 반복문 안이면 바퀴마다 새 array
  - 원소 읽기 `arr[i]`, 원소 쓰기 `arr[i] = 값`
    - index: 정수 타입(`int`, `char`, C의 `bool`)만. `float` 등은 `TypeMismatch`(C의 `arr[0.5]`도 compile error)
    - 쓸 값: 원소 타입으로 맞춤(대입과 같은 규칙)
    - 범위 밖, 음수 index: `IndexOutOfRange`. 실제 C는 검사하지 않고 이웃 메모리를 건드림
    - 없는 변수: `UndeclaredVariable`. array가 아닌 변수(`int x`에 `x[0]`): `TypeMismatch`
  - array 통째로 쓰기(`arr = other`, 계산, 비교, 조건): `TypeMismatch`
  - 함수에 넘기기: 같은 array를 가리킴. 불린 함수가 원소를 바꾸면 부른 쪽도 바뀜
    - 파라미터 타입: `VariableType.ArrayOf(원소 타입)`. 길이를 적지 않음(C의 `int a[]`)
    - 원소 타입이 다른 array: `TypeMismatch`
    - 반환 타입 array, 비동기 함수의 array 파라미터에 값 넘기기: 변환 거부. 비동기 작업이 부른 function보다 오래 남으면, 프레임과 함께 돌아간 array를 memory 없이 계속 쓰게 되기 때문(C의 dangling pointer)
- 변환 거부: 지금 실행기가 실행할 수 없는 기능을 쓴 함수. 콘솔 오류, 시전하지 않음
  - 예: 지원하지 않는 블록·타입, 목록에 없는 struct, 없는 고정 ID, 없는 GUID(지운 함수), 비운 value slot(사전 정의 함수의 argument는 비워도 됨)
  - 플레이어 코드의 잘못(없는 변수, 같은 이름 두 번 선언 등): 거부하지 않음. 실행 중 실패로 처리

### 전역 변수 (#139)
- 선언: 캐릭터마다 전역 변수 파일 1개(`GlobalVariablesData`, [function-data.md](function-data.md)). 함수 안의 선언은 언제나 지역 변수
- 쓰기: 모든 사용자 정의 함수가 함께 읽고 대입
  - 찾는 순서: 지금 함수의 지역 변수 → 캐릭터의 전역 변수. 변수 읽기, 대입, array 원소 읽기·쓰기 모두 같음
  - 전역 변수에 원소 읽기·쓰기(전역 `int x`에 `x[0]`): `TypeMismatch` (전역 array는 없음)
  - 이름이 같을 때: 지역 변수가 전역 변수를 가림 (C 규칙)
- 초기값: 직접 입력한 값만. 비우면 언어 규칙 파일의 기본값. 포인터는 `NULL`만
- memory: 전역 변수 하나 = 데이터 영역 1칸(`DataUsed`). 초기화 때 자동으로 잡히고 다음 초기화까지 유지. `malloc` 아님
  - 포인터 전역 변수: 변수 자체는 데이터 영역 1칸, 가리키는 객체는 heap
  - 실행 중에 `Capacity`가 줄어도 잡힌 칸은 유지. `Free`가 0이 됨 (stack 규칙과 같음)
- 저장 검사(`FunctionExecutor.TrySaveGlobalVariables`): 걸리면 저장하지 않음. 콘솔 오류
  - 선언 수 > `Capacity`
  - 대상(Target) 타입, array, 다룰 수 없는 타입
  - 빈 이름, 같은 이름 두 번
  - 직접 입력한 값이 아닌 초기값(연산, 변수, `malloc`), 읽을 수 없는 글자열, 변수 타입으로 바꿀 수 없는 값
- 초기화(`FunctionExecutor.RestartProcess`): 프로세스를 새로 시작
  - 순서: 진행 중인 작업 모두 끊기(`Interrupted`) → heap 비우기(`Clear`, 숨은 시스템 전역 포함) → 전역 변수 파일의 초기값으로 전역 변수 다시 만들기
  - 부르는 곳: 전역 변수 파일 저장, 사망(박진우), 재부팅(재부팅 이슈), 장소 이동(사냥터 ↔ 로비, 장소 이동 이슈), 연습장 초기화
  - 접속: `FunctionExecutor.Start`가 전역 변수만 만듦. 같은 오브젝트의 다른 컴포넌트가 `Start`에서 먼저 시전한 작업을 끊지 않기 위함
- 전역 변수 파일 저장 위치: 이 컴퓨터의 `GlobalVariables.json` 하나(`GlobalVariablesStorage`). 서버 저장 전까지의 임시 저장소
- 초기화 때 읽는 파일 (#173): `SetGlobalVariablesFile`로 정한 파일. 정하지 않았으면 이 컴퓨터의 파일
  - 서버: 플레이어가 접속할 때 보낸 파일(`FunctionExecutorNetwork`, [network.md](network.md))
- 아직 없음: 전역 array, 전역 변수 파일 편집 화면, 서버 저장, Mirror 동기화
- 지금 한계 (서버 저장 이슈에서 해결)
  - 전역 변수 파일이 컴퓨터마다 하나: 같은 컴퓨터의 다른 계정·캐릭터도 같은 파일을 보냄. 캐릭터 ID로 나눠 저장해야 함
  - 접속 직후 같은 오브젝트의 다른 컴포넌트가 `Start`에서 먼저 시전하면, 그 순간의 `Free`에 데이터 영역이 빠져 있음. 서버에서 불러오기를 기다린 뒤 전역 변수를 만드는 흐름으로 바꿀 때 함께 정리

### heap
- malloc: 스킬 코드가 객체를 heap에 올리고 포인터를 받음. `Free`가 모자라면 `NullPointer`(0), 에러 없음
- free: 스킬 코드가 포인터로 객체를 해제. heap 칸은 즉시 반환. 오브젝트 삭제는 스킬 코드가 따로 함
- `malloc` 블록 (#90): 미리 정한 struct(`PredefinedStructs`)의 객체(`StructInstance`)를 heap에 올리고 포인터를 내놓음. 칸 수는 그 struct의 `HeapSize`. 모자라면 `NullPointer`, 에러 없음
  - 화면의 오브젝트(예: 소환검): 만들지 않음. 연결 방법은 박진우와 정할 것
- `free` 블록 (#90): value slot의 포인터를 해제. 쓸 수 없는 포인터(해제한 포인터, 누수된 포인터)이면 `InvalidPointer`(플레이어 코드 에러)
  - `NULL`(`NullPointer`, 직접 입력한 `NULL`, C에서 직접 입력한 `0`): C처럼 아무 일도 하지 않고 1 tick 뒤 끝남 (#120). 스킬이 쓰는 `PredefinedFunction.Free`는 그대로 `NullPointer`를 에러로 봄
- 포인터 값: 1부터 하나씩 커짐. 해제한 값은 다시 쓰지 않음
  - `int` 최댓값에 닿을 때: 1로 되돌리고, 아직 쓰는 값은 건너뜀
- 루트 집합: 포인터를 보관하는 곳들 (#139에서 바뀜)

| 루트 | 들어가는 포인터 | C에서 |
| --- | --- | --- |
| 작업의 지역 루트 | 진행 중인 작업이 malloc한 포인터, 실행 중인 작업들의 지역 변수·파라미터·지역 array 원소에 담긴 포인터. 작업이 끝나면 사라짐 | 지역 포인터 |
| 숨은 시스템 전역 (캐릭터 하나에 하나) | 사전 정의 함수가 정상으로 끝날 때 옮겨진, 그 함수가 malloc한 포인터(예: 소환검). 플레이어에게 보이지 않음 | C 라이브러리의 `static` 전역 변수 |
| 플레이어의 전역 변수 | 전역 변수에 담긴 포인터 값 | 전역 포인터 |

- 사전 정의 함수가 정상으로 끝날 때: 그 함수가 malloc한 포인터를 숨은 시스템 전역으로 옮김
  - 작업이 아니라 그 함수가 끝나는 순간. 사용자 정의 함수 안에서 불린 검 소환도, 그 함수가 나중에 실패해도 소환검이 남음
  - 실행 중단·실패로 끝난 사전 정의 함수: 옮기지 않음 (정리 블록이 실행되지 못함)
- 작업이 끝날 때(끝난 이유와 상관없이): 그 작업의 지역 루트가 사라짐 → 루트 집합 어디에도 포인터가 없는 객체는 누수
  - 다른 작업이 malloc한 객체를 지역 변수에만 쥐고 있는 동안(예: `Sword* s = g; g = NULL;`): 다른 작업이 끝나도 누수가 아님 (#141 검토)
  - 사용자 정의 함수가 malloc해서 지역 변수에만 담은 객체: 함수가 끝나면 누수 (C와 같음)
  - malloc하지 않은 작업도 누수를 판단함. 전역 변수의 포인터를 바꿔(`g = NULL`) 예전 객체를 놓쳤을 수 있기 때문
- 누수 객체
  - heap 칸: 계속 차지
  - 객체 수(`CountObjects`): 포함. 소환검 최대 개수에 들어감
  - 포인터로 꺼내기·해제: 불가. 포인터 값을 알아도 같음
  - 반환: 영역 비우기(`Clear`)만
- 영역 비우기(`Clear`): heap 전부 비움. 아직 쓰던 객체까지 포함
  - 부르는 곳: 초기화(`RestartProcess`, #139), 메모리 비우기 스킬 (이후 이슈)
- 주인별 heap 칸(`GetUsageByOwner`, #139): 플레이어 전역 / 숨은 시스템 전역 / 작업의 지역 루트 / 누수. 한 객체는 한 번만 셈(우선순위 플레이어 전역 → 시스템 전역 → 지역). 합은 `HeapUsed`
  - 누수: 누수로 표시된 객체 + 아무도 쥐지 않아 다음 누수 판단 때 누수가 될 객체

### 시전 전 검사
- `Cast`가 `FunctionTask`를 만들기 전에 공통 조건을 차례로 검사한다. 하나라도 걸리면 `FunctionTask`를 만들지 않고 `CastRejected`로 거부 이유(`CastRejection`)를 알린다.
  1. 쿨타임: `OnCooldown`
     - 스킬: 같은 스킬(고정 ID)이 어느 function 안에서든 실행 중이거나 쿨타임 중
     - 사용자 정의 함수: 같은 function(원본)이 실행 중
  2. 메모리: `Free`가 그 function의 `FrameSize`보다 작으면 `NotEnoughMemory`
- 시전 거부는 책임 없음(게임 상황): 언어 규칙 표를 거치지 않음
- 메모리 검사 범위: 맨 바깥 function의 프레임만. 안쪽 function의 stack은 실행 중에 `Overflow`로 처리
- 같은 tick에 연달아 시전: 둘 다 같은 `Free`를 보고 통과할 수 있음. 나중에 시작한 쪽은 실행 중에 `Overflow`
- `UserFunction` 안의 비동기 시전: 시전 전 검사 없음. 쿨타임은 아래 「쿨타임」의 기다림 규칙
- 공통 조건 추가: `CastCondition`을 상속한 클래스 하나를 만들고, `FunctionExecutor.Awake`의 조건 목록에 추가
- `Cast(null)`: `ArgumentNullException`
- 스킬마다 다른 조건(맨손, 소환검 등)은 공통 조건이 아니다. 스킬 코드가 `Fail`로 직접 처리한다([predefined-function.md](predefined-function.md)).

### 실패 처리
- 책임으로 나눔

| 책임 | 예 | 실행기가 하는 일 | 화면 |
| --- | --- | --- | --- |
| 없음 | 쿨타임 중, 시전 전 메모리 부족 | `FunctionTask`를 만들지 않고 `CastRejected` | 아직 쓸 수 없음 |
| 플레이어 | 선언하지 않은 변수, 0으로 나누기, 잘못된 포인터 `free`, 맨손으로 찌르기 호출 | 언어 규칙 표의 처리. C는 그 `FunctionTask`만 `Failed`로 끝냄. Overflow는 프로세스를 새로 시작(#143) | compile error 또는 runtime error |
| 개발자 | 스킬의 `Return` 누락·반환 타입 불일치, 언어 규칙 표에 그 실패 종류의 줄이 없음, 언어 규칙 파일 미연결 | 콘솔 오류, 그 `FunctionTask`를 `InternalError`로 끝냄 | 플레이어의 에러로 보여 주지 않음 |

- 플레이어 책임인 실패 종류(`FunctionFailure`)

| 실패 종류 | 나는 때 |
| --- | --- |
| `Overflow` | 실행 중에 stack이 모자람 |
| `SkillFailed` | 스킬 코드가 `Fail`로 에러를 냄(예: 맨손으로 찌르기). 어떤 에러인지는 `task.Error`. 스킬을 함수로 부르는 컨셉이므로 잘못된 때에 스킬 함수를 부른 플레이어의 에러 |
| `UndeclaredVariable` | value slot이나 대입 블록의 변수가 지금 함수의 지역 변수와 캐릭터의 전역 변수 어디에도 없음 (#90, #139) |
| `InvalidLiteral` | 직접 입력한 글자열을 그 타입의 값으로 바꿀 수 없음. 예: `int` 칸의 `"abc"` (#90) |
| `DuplicateVariable` | 같은 이름의 변수를 두 번 선언함 (#90) |
| `TypeMismatch` | 값을 변수·파라미터 타입으로 바꿀 수 없음, 그 타입에 쓸 수 없는 연산. 예: `int x = "abc"`, C의 `float %` (#90) |
| `DivideByZero` | 0으로 나누기, `% 0` (#90) |
| `InvalidPointer` | `free` 블록에 쓸 수 없는 포인터(해제한 포인터, 누수된 포인터)를 넣음. `NULL`은 실패가 아님 (#120) |
| `NotInLoop` | 반복문 밖에서 `break`, `continue`를 만남 (#120) |
| `InvalidTarget` | 스킬에 넘긴 대상이 「대상 없음」이거나, 죽었거나, 오브젝트가 없어짐 (#131) |
| `MissingReturn` | 반환 타입이 있는 사용자 정의 함수가 return 없이 끝나거나 `return;`을 만남 (#132) |
| `IndexOutOfRange` | array의 범위 밖 원소를 읽거나 씀(음수 포함) (#132) |

- 실패 종류마다 할 처리와 분류: 캐릭터의 `LanguageRule` 컴포넌트에 연결한 언어 규칙 파일(`LanguageRuleData`)이 정함
- 분류(`FailureCategory`): 화면이 compile error와 runtime error를 나누어 보여 줄 때 씀
  - `CompileError`: 그 언어에서 실행 전에 찾아내는 잘못
  - `RuntimeError`: 그 언어에서 실행 중에 찾아내는 잘못
  - 언어마다 다름. 예: 선언하지 않은 변수는 C에서 `CompileError`, Python에서 `RuntimeError`
  - 찾아내는 시점: 분류와 상관없이 지금은 모두 실행 중. compile error를 편집 화면에서 저장할 때 검사하는 일은 별도 이슈
  - `SkillFailed`는 한 줄이므로 스킬 에러는 종류와 상관없이 같은 분류
- 처리 종류(`FailureHandling`)
  - `EndTask`: 그 `FunctionTask`만 `Failed`로 끝남
  - `Reject`: 실행 중에는 `EndTask`와 같음. 시전 거부가 표를 거치지 않게 된 뒤로 쓰는 줄 없음
  - `RestartProcess` (#143): 실패한 `FunctionTask`를 `Failed`로 끝낸 뒤 프로세스를 새로 시작(`FunctionExecutor.RestartProcess`). 다른 작업은 `Interrupted`, heap과 전역 변수는 처음으로. 아래 「끊기는 경우」
- 표에 없는 실패 종류: Play를 시작할 때 콘솔 오류. 실제로 나면 콘솔 오류 후 `InternalError`
- 언어 규칙 파일 미연결: Play를 시작할 때 콘솔 오류. 실패가 나면 콘솔 오류 후 `InternalError`
- `FunctionFailure`, `FailureHandling`, `FailureCategory`, `NumericConversion`의 새 값: 맨 끝에만 추가. 언어 규칙 파일이 값을 숫자로 저장하므로, 중간에 끼우면 이미 만든 파일의 값이 바뀜

### 끊기는 경우 (#143)
- 실행 중인 작업이 끝까지 실행되지 못하고 끊기는 세 경우

| 끊는 방법 | 부르는 곳 | 끊는 대상 | 끝난 이유 | heap | 전역 변수 |
| --- | --- | --- | --- | --- | --- |
| 실행 중단 공격 `InterruptAll` | 몬스터 공격의 실행 중단 효과 | 그 캐릭터의 모든 작업 | `Interrupted` | 그대로. 끊긴 작업이 쥐던 객체는 누수 | 그대로 |
| 슬롯 취소 `Cancel` | 슬롯 입력(`SlotCaster`) | 그 슬롯의 원본으로 시전한 작업만 | `Cancelled` | 그대로. 끊긴 작업이 쥐던 객체는 누수 | 그대로 |
| Overflow (C, `RestartProcess` 처리) | 실행기(언어 규칙 표) | 그 캐릭터의 모든 작업 | Overflow를 낸 작업 `Failed`, 나머지 `Interrupted` | 비움(소환검 포함) | 파일의 초기값 |

- 세 경우 공통
  - 대상: 진행 중인 작업과 다음 tick을 기다리는 작업. 진행을 시작하기 전에 끊긴 작업은 `TaskStarted` 없이 `TaskEnded`만
  - 실행 중이던 function마다 `OnEnd` 대신 `OnCancel`. 프레임은 끊는 즉시 반환
  - 끊긴 사전 정의 함수: 정상 종료가 아니므로 malloc한 포인터가 숨은 시스템 전역으로 옮겨지지 않음. 끊기기 전에 이미 끝난 사전 정의 함수의 객체는 숨은 시스템 전역에 남음
  - 쿨타임: 끊긴 스킬도 쿨타임 전체(「쿨타임」). 시작하기 전에 끊긴 스킬은 쿨타임 없음
  - 목록에서 빠지는 때: 끊은 뒤 처음 끝나는 tick. `TaskEnded`와 누수 표시도 그때
    - tick 밖에서 끊음(몬스터 공격, 슬롯 입력 등에서 `InterruptAll`·`Cancel`): 다음 tick이 끝날 때. 그사이 `State`는 이미 `Ended`
    - tick 안에서 끊음(Overflow 재시작): 그 tick이 끝날 때
- 실제 언어와의 관계
  - Overflow: C의 stack overflow는 프로세스를 죽이고, 운영체제가 그 프로세스의 메모리를 모두 회수함. 그래서 heap과 전역 변수까지 처음으로
  - 재부팅 시간(디버프): 아직 없음. 재부팅 이슈에서 `RestartProcess`에 더함
- 슬롯 취소 `Cancel`
  - 찾는 기준: 슬롯에서 넘긴 원본(`Cast`에 넘긴 객체). 같은 스킬이 다른 function 안에서 실행 중이어도 끊지 않음
  - `IsRunning`과의 차이: 스킬 슬롯은 같은 스킬이 다른 function 안에서 실행 중이기만 해도 `IsRunning`이 `true`. 그때 `Cancel`은 `false`. 부르는 쪽은 `Cancel`이 돌려준 값을 보고 처리
  - 끊은 작업이 비동기 호출로 만든 작업: 따로 시전된 작업이므로 끊지 않음
  - 언제 부를지(실행 중인 슬롯을 다시 누름, 연타 대책 등): `SlotCaster`가 정함. 아래 「맞춘 결과」
- 끝나지 않는 작업(`while (true) { }` 등): 제한 없음. 실제 C와 같음
  - 이유: 공격 속도는 스킬별 쿨타임이 막음(#122). 끝나지 않는 작업은 stack을 계속 차지하고, 실행 중단 공격·재시작에 끊김
  - 플레이어가 끝내는 방법: 슬롯 취소

### 쿨타임 (#122)
- 붙는 곳: 스킬(`PredefinedFunction`의 고정 ID)마다 하나. 슬롯 직접 시전, function 안의 호출, 비동기 시전이 같은 쿨타임을 씀
- 길이: 그 스킬의 `CooldownTicks`(tick 수). 스킬 구현자가 정함. 기본 0
- 시작: 그 스킬이 끝나는 tick부터. 정상 종료, 실패, 실행 중단 모두 쿨타임 전체
  - 이유: 스킬을 일부러 끊어 쿨타임을 줄이지 못하게 함
  - 시작하지 못한 스킬(stack 부족 `Overflow`, 시작 전 실행 중단): 쿨타임 없음
  - 예: `{ A(); B(); }`에서 A가 끝난 뒤 B를 실행하기 전에 function이 끝나면 A만 쿨타임
- 쿨타임 감소: 스킬이 끝날 때 `PlayerStats.ApplyCooldownReduction`을 한 번 적용하고 tick 수로 반올림. 스킬의 `CooldownTicks` 값은 그대로
- 같은 스킬이 실행 중이거나 쿨타임 중일 때
  - 슬롯에서 직접 시전: 시전 거부(`OnCooldown`)
  - function 안의 스킬: 실패 없이 쓸 수 있을 때까지 기다림. 기다린 tick도 function의 진행 tick에 들어감
    - 기다리는 동안의 stack: 그 스킬의 프레임은 없음. 바깥 function들의 프레임은 그대로
    - 기다리는 동안 실행 중단: 시작하지 않았으므로 쿨타임 없음
    - argument: 먼저 계산한 뒤 기다림
  - function 안의 비동기 스킬: 부른 function이 기다렸다가 새 `FunctionTask`를 시전. 그동안 다음 블록으로 넘어가지 않음
    - 이유: 바로 시전하면 반복문이 tick마다 기다리는 `FunctionTask`를 쌓아, 쿨타임이 끝날 때마다 차례로 실행됨(시전 예약)
    - 한계: 새 `FunctionTask`는 다음 tick에 시작하므로, 그 사이에 같은 비동기 스킬을 한 번 더 요청할 수 있음. 그 `FunctionTask`도 시작할 때 기다리므로 쿨타임은 피하지 못함
  - 같은 tick에 두 `FunctionTask`가 같은 스킬을 시작: 먼저 진행하는 쪽이 시작, 나머지는 기다림
- 사용자 정의 함수: 자기 쿨타임 없음. 실행 중이면 다시 시전할 수 없음(`OnCooldown`)
  - 실행 중 판단: 슬롯에서 시전한 원본 객체마다. 같은 객체를 다시 `Cast`에 넘겨야 이어짐
- 알려진 단점: 같은 function이라도 직전에 스킬을 쓴 시점에 따라 걸리는 시간이 다름

## 다른 시스템이 쓰는 것

캐릭터(Player 프리팹)에 `FunctionExecutor`, `ProcessMemory`, `LanguageRule`이 붙어 있다. `GetComponent`로 가져온다.
- `ProcessMemory`는 같은 오브젝트에 `PlayerStats`가 있어야 한다(`RequireComponent`). memory 전체 칸 수를 `PlayerStats`에서 읽기 때문이다.

### `FunctionExecutor`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Cast(FunctionBlock function)` | cast할 function (`PredefinedFunction` 또는 `UserFunction`) | `FunctionTask` | function을 cast한다. 넘긴 객체는 원본으로 남고 복사본이 실행되므로, 같은 객체를 여러 번 넘겨도 된다. `FunctionTask`는 다음 tick부터 진행한다. 시전 전 검사에 걸리면 `FunctionTask`를 만들지 않고 `CastRejected`를 알린 뒤 `null`을 돌려준다. |
| `Cast(PredefinedFunction function, params RuntimeValue[] arguments)` | 사전 정의 함수, argument(`Parameters` 순서) | `FunctionTask` | argument를 넘겨 시전한다. 슬롯에서 바로 시전할 때 고른 대상을 넘긴다. 예: `Cast(thrust, RuntimeValue.FromBug(몬스터))`. 스킬은 `TryGetArgument`로 읽는다. 쓸 수 없는 대상이면 작업이 시작할 때 `InvalidTarget`. 파라미터 타입으로 맞출 수 없거나 파라미터보다 많으면 예외 (#131) |
| `CastRejected` (event) | `FunctionBlock`, `CastRejection` | - | 시전 전 검사에 걸려 거부했을 때 불린다. 거부한 function과 거부 이유(`OnCooldown`, `NotEnoughMemory`)를 넘긴다. 거부 이유를 화면에 보여 줄 때 쓴다. 멀티플레이 화면은 `FunctionExecutorNetwork.CastRejected`를 받는다(`NoFunction` 포함, #173) |
| `TaskStarted` (event) | `FunctionTask` | - | `FunctionTask`가 첫 tick을 진행하기 직전에 불린다. 연출을 시작할 때 쓴다. |
| `TaskEnded` (event) | `FunctionTask` | - | `FunctionTask`가 끝나서 목록에서 빠질 때 불린다. 끝난 이유는 `task.EndReason`, 플레이어 책임으로 실패(`Failed`)했으면 `task.Failure`, `task.Error`로 확인한다. |
| `InputRequested` (event) | `FunctionTask`, `PredefinedFunction` | - | `FunctionTask`가 입력 함수를 만나 입력을 기다리기 시작할 때 한 번 불린다. 기다리는 작업과 요청한 입력 함수를 넘긴다. 받는 쪽은 입력 함수의 `Id`(예: `input.get_bug`)로 자기가 맡은 입력인지 확인한다. (#131) |
| `ProvideInput(FunctionTask task, RuntimeValue value)` | 기다리는 작업, 넣을 값 | `bool` | 입력을 기다리는 작업에 값을 넣는다. 작업은 다음 tick부터 이어서 진행한다. 대상 입력이면 `RuntimeValue.FromBug(몬스터)`, 고를 대상이 없으면 `FromBug(null)`. 작업이 이미 끝났거나 입력을 기다리지 않으면 `false`. 값의 타입이 요청한 타입과 다르면 예외. (#131) |
| `CurrentTick` | - | `int` | 지금까지 지난 tick 수. 첫 tick이 1이다. |
| `SecondsPerTick` | - | `float` | 1 tick의 길이(초). 스킬 애니메이션 길이를 `TickCount`에 맞추거나, 쿨타임을 초로 보여 줄 때 쓴다. |
| `GetRemainingCooldownTicks(FunctionBlock function)` | 슬롯의 function (원본) | `int` | 남은 쿨타임(tick 수). 쿨타임 중이 아니면 0. 스킬이면 그 스킬(고정 ID)의 쿨타임, 사용자 정의 함수는 자기 쿨타임이 없으므로 0. 쿨타임 UI에서 쓴다. `TickEnabled`가 false인 실행기(클라이언트 화면)는 `SetDisplayedCooldown`으로 받은 끝 시각으로 답한다 (#173) |
| `CooldownStarted` (event) | `string` 스킬 고정 ID, `int` 지금부터 남은 tick 수 | - | 스킬의 쿨타임이 시작되거나 늘어났을 때. 서버가 시전한 사람 화면에 쿨타임을 알릴 때 쓴다 (#173) |
| `SetDisplayedCooldown(string skillId, float endTime)` | 스킬 고정 ID, 끝 시각(`Time.time` 기준) | - | 클라이언트 화면의 실행기에 서버가 알려 준 쿨타임 끝 시각을 넣는다. `FunctionExecutorNetwork`만 부른다 (#173) |
| `TickEnabled` | - | `bool` (읽기·쓰기) | tick을 진행하는지. false이면 tick을 세지 않고 작업도 진행하지 않는다. 처음 값 true. 클라이언트 화면의 실행기를 멈출 때 `FunctionExecutorNetwork`가 쓴다 (#173) |
| `TryGetTask(int id, out FunctionTask task)` | 작업 번호(`FunctionTask.Id`) | `bool` | 진행 중인 작업과 다음 tick을 기다리는 작업에서 번호로 찾는다. 목록에서 빠졌거나 없는 번호면 `false`. 클라이언트가 보낸 작업 번호를 서버의 작업으로 바꿀 때 쓴다 (#173) |
| `IsRunning(FunctionBlock function)` | 슬롯의 function (원본) | `bool` | 그 function의 `FunctionTask`가 진행 중이거나 다음 tick을 기다리면 `true`. 스킬이면 같은 스킬이 다른 function 안에서 실행 중일 때도 `true` |
| `InterruptAll()` | - | - | 실행 중단 공격에 맞았을 때 부른다. 진행 중인 `FunctionTask`와 다음 tick을 기다리는 `FunctionTask`를 모두 `Interrupted`로 끝낸다. 끊긴 `FunctionTask`도 tick이 끝날 때 목록에서 빠지며 `TaskEnded`가 불린다. 진행을 시작하기 전에 끊긴 `FunctionTask`는 `TaskStarted` 없이 `TaskEnded`만 불린다. |
| `Cancel(FunctionBlock function)` | 슬롯의 function (원본) | `bool` | 그 원본으로 시전한 `FunctionTask`만 `Cancelled`로 끊는다(진행 중, 다음 tick을 기다리는 것 모두). 끊은 작업이 있으면 `true`, 없으면 `false`. 플레이어가 그 슬롯의 작업을 취소할 때 부른다. 스킬 슬롯은 같은 스킬이 다른 function 안에서 실행 중이어도 끊지 않으므로, `IsRunning`이 `true`여도 `false`일 수 있다. 처리는 「끊기는 경우」 (#143) |
| `RestartProcess()` | - | - | 초기화. 진행 중인 `FunctionTask`를 모두 끊고(`InterruptAll`과 같음), heap(소환검 포함)을 비운 뒤, 전역 변수 파일(`SetGlobalVariablesFile`, 정하지 않았으면 저장한 파일)의 초기값으로 전역 변수를 다시 만든다. 사망, 재부팅, 장소 이동(사냥터 ↔ 로비), 연습장 초기화에서 부른다. (#139) |
| `SetGlobalVariablesFile(GlobalVariablesData file)` | 전역 변수 파일 (`null`이면 이 컴퓨터의 파일) | - | 초기화 때 읽을 전역 변수 파일을 정한다. 정하기만 하고 초기화하지 않는다. 서버가 플레이어가 보낸 파일로 전역 변수를 만들 때 쓴다 (#173) |
| `TrySaveGlobalVariables(GlobalVariablesData file)` | 전역 변수 파일 | `bool` | 검사해서 저장한 뒤 `RestartProcess`를 부른다. 검사에 걸리면 저장하지 않고 `false`, 이유는 콘솔 오류(「전역 변수」의 저장 검사). 전역 변수 편집 화면이 저장할 때 부른다. (#139) |

### `FunctionTask`

`FunctionTask`는 `FunctionExecutor`만 만든다. 다른 스크립트는 값을 읽기만 한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Id` | - | `int` | 작업 번호. 실행기가 1부터 붙이고, 캐릭터(실행기)마다 따로 센다. 서버와 클라이언트가 같은 작업을 가리킬 때 쓴다 (#173) |
| `Caster` | - | `GameObject` | 이 `FunctionTask`를 cast한 캐릭터 |
| `Name` | - | `string` | cast한 function의 이름 |
| `State` | - | `FunctionTaskState` | `Running`(진행 중), `WaitingInput`(입력 함수가 입력을 기다리는 중, #131), `Ended`(끝남) |
| `EndReason` | - | `FunctionTaskEndReason` | `None`(진행 중), `Completed`(정상 종료), `Interrupted`(실행 중단 공격), `Failed`(플레이어 책임인 실패. 실패 종류는 `Failure`), `InternalError`(개발자 책임. 플레이어의 에러로 보여 주지 않음), `Cancelled`(플레이어가 슬롯 취소, #143). Overflow로 프로세스를 새로 시작하면 Overflow를 낸 작업은 `Failed`, 함께 끊긴 작업은 `Interrupted` |
| `IsEnded` | - | `bool` | 끝났으면 `true` |
| `IsFailed` | - | `bool` | `Failed`(플레이어 책임)로 끝났으면 `true`. `InternalError`는 `false`. 실패 여부는 `Failure`가 아니라 이 값으로 판단한다. |
| `Failure` | - | `FunctionFailure?` | `Failed`로 끝났을 때 실패 종류. 그 밖에는 `null` |
| `Error` | - | `SkillError` | `Failure`가 `SkillFailed`일 때 스킬이 낸 에러. 그 밖에는 `null` |

### `FunctionContext` (실행 문맥)

작업 하나의 실행 문맥. 블록과 `PredefinedFunction`이 `Step`, `OnStart`, `OnTick`, `OnEnd`, `OnCancel`, `OnFail`에서 받는다. `FunctionTask`가 만들어 가진다.
- 작업마다 다른 것(지역 변수 목록, 실패 기록, malloc한 포인터의 주인)은 실행 문맥이 직접 가짐
- 캐릭터에 하나인 것(메모리, heap, 언어 규칙)은 원래 주인이 가지고, 실행 문맥은 닿는 길만 줌. heap 규칙(루트 집합, 누수)은 `HeapStorage`
- 블록에 실행기와 작업을 보여 주지 않음. 실행 문맥도 실행기를 모름: 실패와 비동기 시전 요청은 기록만 하고, 실행기가 `Step` 다음에 읽어 처리

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Caster` | - | `GameObject` | 이 작업을 cast한 캐릭터. `PredefinedFunction`은 이것으로 캐릭터의 위치, 방향, 컴포넌트를 찾는다. |

- 그 밖의 값(`Memory`, `Rule`, 지역 변수 목록, 실패 기록, 비동기 시전 요청, `Malloc`)은 실행기 안에서만 씀(`internal`). 스킬은 `PredefinedFunction`의 함수로 씀([predefined-function.md](predefined-function.md))

### `LanguageRule` (캐릭터의 언어 컴포넌트)

캐릭터 하나의 언어. 연결한 언어 규칙 파일(`LanguageRuleData`)로 실패의 처리와 분류, 기본값, 숫자 타입 변환 규칙을 알려 준다.
- 붙이는 곳: `FunctionExecutor`와 같은 오브젝트(`RequireComponent`)
- 없을 때: `FunctionExecutor`가 콘솔 오류를 내고 붙여서 실행. 언어 규칙 파일 미연결과 같은 처리

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `GetFailureCategory(FunctionFailure failure)` | 실패 종류 | `FailureCategory` | 이 캐릭터 언어에서 그 실패가 `CompileError`인지 `RuntimeError`인지. `TaskEnded`에서 `Failed`로 끝난 `task.Failure`를 화면에 보여 줄 때 쓴다. `Failed`로 끝난 작업은 언제나 표에 줄이 있음. 줄이 없는 실패 종류를 넘기면 `RuntimeError` (#90) |

### `LanguageRuleData` (언어 규칙 파일)

캐릭터 언어 하나의 규칙 파일이다.
- 만들기: Project 창 Create → Invoke → Language Rule
- 연결: 캐릭터의 `LanguageRule` Inspector `Rule Data` 칸
- 실패 처리 표: 실패 종류마다 처리, 분류 한 줄
- 기본값: 초기값을 비운 변수의 값. `int`, `float`, `bool`, `char`(글자 번호), `string` 칸. 포인터는 언제나 `NullPointer` (#90)
- 숫자 타입 변환: `ImplicitAll`, `Strict`, `WideningOnly` 중 하나(「함수 데이터 실행」의 표) (#90)
- `float %` 허용 여부 (#90)
- `NULL`과 비교 (#120): `zeroIsNull`(직접 입력한 0을 `NULL`로 볼지, C는 켬), `stringEquality`(string 비교 방법 `Disallowed`·`ByContent`, C는 `Disallowed`)
- 변수 범위 (#120): `allowShadowing`(`{ }` 안에서 바깥과 같은 이름을 선언할 수 있는지, C는 켬)
- 연결하지 않음: 콘솔 오류. 플레이어 함수가 실패하면 `InternalError`, 기본값, 숫자 타입 변환, `NULL`과 비교는 처음 값(C 규칙)
- C 파일(`Assets/_UL/Data/LanguageRule_C`)

| 실패 종류 | 처리 | 분류 |
| --- | --- | --- |
| `Overflow` | `RestartProcess` (#143) | `RuntimeError` |
| `SkillFailed` | `EndTask` | `RuntimeError` |
| `UndeclaredVariable` | `EndTask` | `CompileError` |
| `InvalidLiteral` | `EndTask` | `CompileError` |
| `DuplicateVariable` | `EndTask` | `CompileError` |
| `TypeMismatch` | `EndTask` | `CompileError` |
| `DivideByZero` | `EndTask` | `RuntimeError` |
| `InvalidPointer` | `EndTask` | `RuntimeError` |
| `NotInLoop` | `EndTask` | `CompileError` |
| `InvalidTarget` | `EndTask` | `RuntimeError` |
| `MissingReturn` | `EndTask` | `CompileError` |
| `IndexOutOfRange` | `EndTask` | `RuntimeError` |

- C 파일의 그 밖의 값: 기본값 0, 0, `false`, 글자 번호 0, 빈 글자열. 숫자 타입 변환 `ImplicitAll`, `float %` 허용하지 않음

### `ProcessMemory`

값은 `ProcessMemory`만 바꾼다. 다른 시스템은 읽기만 한다(예: memory 게이지 UI). heap 객체는 `Heap`의 함수로 다룬다.
- 클라이언트 화면 (#173): `Capacity`, `DataUsed`, `StackUsed`, `HeapUsed`, `Free`가 서버가 알려 준 값으로 답한다(`FunctionExecutorNetwork`). 읽는 쪽은 그대로 읽는다. `Heap`은 비어 있다

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Capacity` | - | `int` | memory 전체 칸 수. `PlayerStats.MemorySize`와 같다. |
| `DataUsed` | - | `int` | 지금 데이터 영역이 쓰는 칸 수. 선언한 전역 변수 수 (#139) |
| `StackUsed` | - | `int` | 지금 stack이 쓰는 칸 수. 실행 중인 모든 `FunctionBlock`의 `FrameSize` 합 |
| `HeapUsed` | - | `int` | 지금 heap이 쓰는 칸 수. 해제하지 않은 객체와 누수 객체의 크기 합 |
| `Free` | - | `int` | 남은 칸 수. `Capacity - DataUsed - StackUsed - HeapUsed`이고, 0보다 작아지지 않는다. (#139) |
| `Heap` | - | `HeapStorage` | 이 캐릭터의 heap 객체 저장소 |

### `HeapStorage` (heap 객체 저장소)

- 가져오기: `ProcessMemory.Heap`. 컴포넌트가 아니므로 프리팹에 따로 붙이지 않음
- 스킬 코드: 이 표의 함수 대신 `PredefinedFunction`의 heap 함수를 씀 ([predefined-function.md](predefined-function.md))
- 오브젝트 생성·삭제: 하지 않음. 메모리 기록만 맡음

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `NullPointer` | - | `int` | null 포인터(0). malloc 실패 값. 이 값으로는 어떤 객체도 찾을 수 없음 |
| `Malloc(FunctionTask owner, object obj, int size)` | malloc하는 작업, 올릴 객체, 칸 수 | `int` | 포인터. 그 포인터는 `owner`의 포인터 목록에 들어감(`owner`가 `null`이면 숨은 시스템 전역). heap이 모자라면 `NullPointer` |
| `Free(int pointer)` | 포인터 | `bool` | 해제하고 heap 칸 반환. `NullPointer`, 없는·해제한·누수된 포인터이면 `false` |
| `TryGet<T>(int pointer, out T obj)` | 포인터 | `bool` | 객체를 `T` 타입으로 꺼냄. 쓸 수 없는 포인터, 타입이 다른 객체, `Destroy`된 Unity 오브젝트이면 `false` |
| `GetGlobalPointers<T>()` | - | `List<int>` | 숨은 시스템 전역의 `T` 객체 포인터. malloc한 순서. 이름은 다른 시스템이 쓰고 있어 그대로 둠 |
| `CountObjects<T>()` | - | `int` | heap의 `T` 객체 수. 누수 객체 포함 |
| `GetUsageByOwner()` | - | `HeapUsage` | 주인별 heap 칸 수(`PlayerGlobals`, `SystemGlobals`, `TaskLocals`, `Leaked`). 합은 `HeapUsed`. memory 화면에서 읽는다 (#139) |
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
| `new ReturnBlock()` | - | `ReturnBlock` | 남은 `Block`을 실행하지 않고 이 `ReturnBlock`을 담은 `UserFunction`을 끝낸다(`return;`). 1 tick을 쓴다. |
| `new ReturnBlock(ValueBlock value)` | 돌려줄 값의 value slot | `ReturnBlock` | `return 값;`. 값은 `ReturnBlock`을 담은 `UserFunction`이 끝날 때 반환 타입으로 맞춘다 (#132) |

- 코드로 만든 `UserFunction`: 파라미터 없음, `Void`, 동기. 파라미터·반환 타입·비동기 여부가 있는 함수는 `FunctionDataConverter`가 함수 데이터에서 만듦 (#132)
- `Parameters`, `ReturnType`, `IsAsync`: `FunctionBlock`의 값. 사전 정의 함수와 사용자 정의 함수가 함께 가짐 (#132)

```csharp
// 예시. Dash, Thrust는 실제로 있는 function이 아니다
UserFunction dashThrust = new UserFunction("DashThrust", new Dash(), new Thrust());
executor.Cast(dashThrust);
```

### `FunctionDataConverter` (함수 데이터 → `UserFunction`, #90)

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TryConvert(FunctionData data, IEnumerable<FunctionData> ownedFunctions, out UserFunction function)` | 함수 데이터, 캐릭터가 가진 모든 함수 | `bool` | 바꾼 `UserFunction`(원본)을 `function`에 담는다. 프레임 크기도 계산해 넣는다. 지금 실행할 수 없는 함수이면 콘솔 오류를 내고 `false` (#132에서 `ownedFunctions` 추가) |

- `ownedFunctions`: 함수 블록이 부르는 사용자 정의 함수를 GUID로 찾는 곳. 다른 사용자 정의 함수를 부르지 않으면 빈 목록. `CodeSizeCalculator.Calculate`의 `ownedFunctions`와 같은 목록 (#132)
  - static 저장소가 아닌 이유: 사용자 정의 함수는 캐릭터마다 다르므로, 서버 한 곳에서 여러 캐릭터의 함수를 바꿀 때 섞이지 않게 하기 위함
- 바꾼 함수: 원본. 슬롯은 같은 객체를 계속 `Cast`에 넘겨야 실행 중 검사가 이어짐
- 부른 함수를 고쳤을 때: 그 함수를 부르는 함수도 다시 바꿔야 함. 바꾼 함수는 바꾼 순간의 함수 데이터로 만들어짐 (#132)

```csharp
// 예시. 슬롯에 넣은 함수 데이터를 바꿔서 시전
if (FunctionDataConverter.TryConvert(functionData, ownedFunctions, out UserFunction function)) executor.Cast(function);
```

### `GlobalVariablesConverter` (전역 변수 파일 검사, #139)

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TryConvert(GlobalVariablesData data, LanguageRuleData rule, int capacity, out List<(string Name, VariableType Type, RuntimeValue Value)> variables)` | 전역 변수 파일, 캐릭터의 언어 규칙 파일, memory 전체 칸 수 | `bool` | 검사하고 선언할 목록(이름, 타입, 초기값)을 돌려준다. 걸리면 콘솔 오류를 내고 `false` |

- 보통은 직접 부르지 않고 `FunctionExecutor.TrySaveGlobalVariables`를 부른다

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
- 박진우와 확인할 것 (#118)
  - 스킬 함수(`OnStart`, `OnTick`, `OnEnd`, `OnCancel`, `OnFail`)와 heap 함수(`Malloc`, `Free` 등)의 첫 파라미터 타입: `FunctionTask` → `FunctionContext`. 이름과 사용법은 그대로 ([predefined-function.md](predefined-function.md))
  - `Return` 누락·반환 타입 불일치: statement로 불러도 콘솔 오류 + `InternalError`(개발자 책임)
  - `Player_JW` 프리팹: `LanguageRule` 컴포넌트(없으면 붙이기)의 `Rule Data` 칸에 `LanguageRule_C` 연결. 연결하지 않으면 플레이어 함수의 실패가 `InternalError`로 끝남
- 임혜원과 확인할 것 (#118)
  - 실패를 화면에 보여 줄 때 `EndReason`으로 책임 구분(`Failed`: 플레이어, `InternalError`: 개발자). `Failed`이면 `LanguageRule.GetFailureCategory`로 compile error / runtime error 구분. `FailureCategory`는 `CompileError`, `RuntimeError` 두 값
  - `GetFailureCategory` 위치: `FunctionExecutor` → `LanguageRule` 컴포넌트
  - `CastRejected`의 거부 이유: `FunctionFailure` → `CastRejection`(`OnCooldown`, `NotEnoughMemory`)
  - `Player_HW` 프리팹: `LanguageRule` 컴포넌트(없으면 붙이기)의 `Rule Data` 칸에 `LanguageRule_C` 연결. 연결하지 않으면 플레이어 함수의 실패가 `InternalError`로 끝남
- 정할 것 (#90)
  - tick 비용 규칙 재검토: 지금은 블록 하나 = 1 tick. 반복문을 만드는 블록 실행 2 이슈에서 「문법 블록은 tick 없음, 반복 한 바퀴에 1 tick」 등과 비교
    - #120에서 정함: 블록 하나 = 1 tick 유지. 조건문은 닿은 갈래마다 1 tick, 반복문은 검사 한 번마다 1 tick
  - 플레이어가 만든 함수의 `return` 누락 규칙: 블록 실행 3 이슈. C는 컴파일러에 따라 경고(GCC) 또는 오류(MSVC C4716), 실행되면 값은 정의되지 않음
    - #132에서 정함: 저장할 때 compile error(MSVC 방식). 저장 검사가 생기기 전까지는 실행 중 `MissingReturn`(C는 `CompileError`)
  - 슬롯에서 함수 데이터를 바꿔 시전하는 연결: `FunctionDataConverter`를 부르는 곳([slot-caster.md](slot-caster.md))
- 임혜원과 확인할 것 (#120)
  - 반복문 밖의 `break`, `continue`: 편집 화면에서 놓지 못하게 막을지, 저장할 때 `BlockData.HasJumpOutsideLoop`로 compile error를 보여 줄지. 지금은 실행 중에 `NotInLoop`(`CompileError`)로 끝남
- 정할 것 (#120)
  - 반복문으로 쿨타임 피하기: 쿨타임이 슬롯의 function에 붙고 function이 끝나야 시작하므로, 반복문 안의 스킬은 몇 번 실행해도 쿨타임을 한 번만 셈. `while (true) { Thrust(); }`는 function이 끝나지 않아 쿨타임이 시작되지 않음
    - 고칠 방법(정함): 스킬별 쿨타임. 실행된 스킬에만 그 스킬의 쿨타임을 붙이고, 함수 안에서 쿨타임 중인 스킬에 닿으면 끝날 때까지 기다림. 사용자 정의 함수의 쿨타임은 없앰
    - #122에서 함 (위 「쿨타임」)
- 박진우와 확인할 것 (#122)
  - `CooldownTicks` 위치: `FunctionBlock` → `PredefinedFunction`. 덮어쓰는 방법은 그대로
  - 슬롯 직접 시전 거부: 같은 스킬이 다른 function 안에서 실행 중이거나 쿨타임 중이어도 `OnCooldown`
- 김도훈과 확인할 것 (#122)
  - 쿨타임 감소 적용 시점: function이 끝날 때 → 스킬이 끝날 때
- 임혜원과 확인할 것 (#122)
  - 사용자 정의 함수 슬롯의 쿨타임 UI에 보여 줄 내용. 지금 `GetRemainingCooldownTicks`는 0
  - function이 스킬의 쿨타임을 기다리는 동안의 표시. 필요한 함수는 정한 뒤 이 문서에 추가
- 박진우와 정할 것 (#131)
  - `SlotCaster`의 입력 대기 연결
    - `InputRequested`에서 입력 함수의 `Id`가 `input.get_bug`이면 그 작업의 대상 입력
    - 그 작업을 시전한 슬롯 키가 눌려 있으면: 지금의 대상 고르기(누른 채 ← →, Player 액션 맵 비활성화)를 시작하고, 키를 떼면 그때 선택된 몬스터로 `ProvideInput`
    - 슬롯 키가 눌려 있지 않으면(함수 안의 비동기 호출로 생긴 작업 포함): 가장 가까운 몬스터로 바로 `ProvideInput`
    - 후보 몬스터가 없음(고르는 중에 모두 죽음 포함): `RuntimeValue.FromBug(null)`로 `ProvideInput`
    - 작업과 슬롯 짝짓기: `Cast`가 돌려준 `FunctionTask`
    - 기다리는 중에 작업이 끝남(`TaskEnded`): 고르기를 끝냄
  - 슬롯에서 스킬을 바로 시전할 때(단일): 키를 뗄 때 `CastRequested` 대신 `executor.Cast(function, RuntimeValue.FromBug(target.GetComponent<Monster>()))`. 여러 명 선택(`Target` list)은 list 값이 생기는 블록 실행 3 이슈에서
    - #132에서 list 대신 array로 정함. 아래 「박진우와 확인할 것 (#132)」
  - 스킬의 `Target` 파라미터: 종류 없이 둬도 `Bug` 값을 받음. 몬스터는 `TryGetArgument("target", out RuntimeValue v)` 뒤 `v.AsBug()`. 쓸 수 없는 대상은 실행기가 `InvalidTarget`으로 막으므로 스킬이 시작하지 않음
- 임혜원과 정할 것 (#131)
  - 선언 블록의 타입 목록(`BlockView.DeclareTypes`)에 `Target`(종류 `Bug`) 추가, 화면 이름(`BlockLabels.TypeName`)은 `Bug`. 그 전까지 플레이어는 `Thrust(GetBug())`처럼 값 칸에 바로 끼워서만 씀
  - `GetBug` 블록: 고정 ID가 `input.`으로 시작하므로 블록 목록에 이미 나옴
- 박진우와 확인할 것 (#132)
  - `Parameters`, `ReturnType`, `IsAsync` 위치: `PredefinedFunction` → `FunctionBlock`. 스킬의 `override`는 그대로
  - 슬롯에서 스킬을 바로 시전해도 `Return` 누락·반환 타입 불일치가 콘솔 오류 + `InternalError`
  - 스킬 호출 블록 클래스 이름: `PredefinedCallBlock` → `CallBlock`
  - 여러 몬스터 고르기(#131에서 미룸): list 대신 `Target` array. 스킬이 array 파라미터의 원소를 읽는 함수는 그 기능을 만들 때 정함
- 임혜원과 확인할 것 (#132)
  - 새 블록 UI: 원소 읽기 `ArrayGet`(value slot에 끼움), 원소 쓰기 `ArraySet` ([function-data.md](function-data.md))
  - 배열 만들기 블록의 길이 칸: 1 이상의 숫자를 직접 입력하는 것만. 다른 값이 든 함수는 변환 거부
  - `FunctionData.IsAsync`: 칸만 있고 편집 화면 UI는 아직 없음
  - return 누락 저장 검사: `BlockData.AlwaysReturnsValue`
- 정할 것 (#132)
  - 실행 중 실패한 블록을 편집 화면에 표시: 별도 이슈. 실행기가 실패한 `BlockData`와 함수 GUID를 작업에 담고, 편집 화면은 같은 `BlockData`를 든 블록 UI를 칠함
  - 범위 밖 index에서 C처럼 이웃 변수를 덮어쓰는 연출: 하려면 별도 이슈(프레임 안의 변수 위치 규칙이 새로 필요)
- 김도훈과 정할 것 (#143)
  - 실행 중단 효과(`AttackEffectType.Interrupt`)를 받는 곳: `MonsterAttackResolver`가 맞은 대상에서 `GetComponentInParent<FunctionExecutor>()`를 찾아 `InterruptAll()` 호출 (제안). 없으면(플레이어가 아님) 아무것도 하지 않음- 박진우와 정할 것 (#143)
  - `SlotCaster`에서 `Cancel`을 부르는 때: 실행 중인 슬롯을 다시 누름(제안). 스킬 슬롯에도 넣을지, 연타로 자기 작업을 끊지 않게 하는 방법(사용자 정의 함수 슬롯만, 길게 누르기, 시전 직후 잠깐 무시 등)
  - `Cancel`이 `false`일 때(그 슬롯에서 시전한 작업이 없음): 지금처럼 `Cast`
  - 그 전까지: 플레이어가 자기 작업을 끊을 방법이 없음. 끝나지 않는 작업은 실행 중단 공격·재시작으로만 끝남
  - 지금 `SlotCaster`가 `Cast`의 반환 값(`null`이면 시전 거부)을 보지 않고 「바로 시전」 로그와 `CastRequested`를 냄
- 박진우·임혜원에게 요청 (#173): 시전·입력 대기·시전 모션을 `FunctionExecutorNetwork`로. 목록은 [network.md](network.md)의 「맞춘 결과」
- 박진우와 확인할 것 (#143)
  - `OnCancel`이 불리는 경우에 슬롯 취소와 Overflow 재시작이 더해짐. Overflow 재시작 때는 heap이 비워져 소환검도 `ObjectCleared`로 지워짐 ([predefined-function.md](predefined-function.md))
- 임혜원과 확인할 것 (#143)
  - 끝난 이유 `Cancelled`(플레이어 취소)와 `Interrupted`(실행 중단 공격, 재시작에 함께 끊김)를 나누어 보여 줄지
  - Overflow 재시작: `Failed`(실패 종류 `Overflow`)로 끝난 작업 하나가 원인. 함께 끊긴 작업은 `Interrupted`
