# 함수 데이터 (이슈 #27)

- 담당자: 이우림

## 하는 일
- 블록 코딩으로 만든 사용자 정의 함수를 저장·전송할 수 있는 함수 데이터로 나타냄
- 함수 데이터를 JSON 글자열로 바꾸고, JSON 글자열에서 되돌림
- 슬롯에 넣은 함수들의 코드 영역 크기 계산
- 하지 않는 일
  - 함수 실행: `FunctionExecutor`가 `UserFunction`으로 실행 (`function-executor.md`)
  - 함수 데이터 → `UserFunction` 변환: 「블록 실행 1」 이슈
  - 구조 검사, 해금 검사: 「만든 함수를 서버로 보내 검사·저장하기」 이슈
  - 크기와 한도 비교: 슬롯에 넣는 쪽
  - 블록 UI, 칸을 채우는 화면: 편집 화면(임혜원)

## 정의

### 블록 종류
- 언어별 표시: 실제 언어처럼 언어마다 다르게 보임 (지금은 C만, 나중에 다른 언어 추가)
  - 함수 데이터: 언어와 상관없는 이름으로 저장 (예: 드롭다운 선택지는 `Add`, `ElseIf`)
- 블록 끼우기: 값 칸에 다른 블록 끼우기 가능 (Scratch 방식)
- 변수
  - 선언 블록 (예: `int x`, `int x = 3`)
    - 타입 지정
    - 초기값 칸: 채우기 선택 (채우면 초기화)
  - 타입 (나중에 추가 가능하게 구현)
    - 기본: `int`, `float`, `bool`, `string`, `char`
    - 게임 전용: 위치, 방향, 대상, 함수
    - 자료구조: 배열, list
    - 사용자 정의 타입: struct (게임이 미리 정한 것, 플레이어가 만드는 것(Type 정의))
    - 참조: 포인터
  - 대입 블록 (예: `x = 5`)
- 연산: `+`, `-`, `*`, `/`, `%`
- 비교: `>`, `<`, `==`, `!=`, `>=`, `<=`
- 논리: `And`, `Or`, `Not` (언어별 표기 `&&`, `||`, `!`는 나중에 결정)
- 조건문
  - 조건문 블록 하나: 드롭다운으로 `if`, `else if`, `else` 중 선택
  - 놓을 수 없는 자리의 항목: 선택 불가
- 반복문
  - `for`, `while`
  - 반복 제어: `break`, `continue`
- 배열 만들기 블록 (예: `int arr[5]`)
  - 일반 변수 만들기 블록과 다른 블록
- 동적 할당 (C)
  - `malloc`, `free` 추가: 코드 블록 (C 스킬 「검 소환」·「해제」는 모티브)
  - `malloc`: 게임이 미리 정한 타입의 실제 오브젝트 생성 (나중에 사용자 정의 타입도 생성 가능하게)
    - 값 칸에 끼우는 블록 (예: `Sword* s = malloc(Sword)`)
  - 미리 정한 타입: struct로 취급 (`struct` 표시 / typedef 이름 표시는 나중에 결정)
  - 포인터 변수 타입 추가
- 함수 블록: 놓으면 그 자리에서 그 함수 호출 (별도 호출 블록 없음)
- 플레이어 입력: 정해진 사전 정의 함수로만 받음 (예: `GetPosition`), 별도 입력 블록 없음
- 사전 정의 함수: 반환 타입 가질 수 있음
- return 블록: 값 칸으로 값 반환 가능
- 기다리기 블록 (예: `0.5초 기다리기`)

### 칸 종류
- 값 칸: 키보드로 직접 입력 또는 블록 드래그
  - 직접 입력한 값 (`bool`은 `true` / `false` 토글, C 화면에서도 `true` / `false`)
  - 변수
  - 값 블록: 연산, 비교, 논리, `malloc`
  - 값을 돌려주는 함수 블록
- 변수 칸: 변수 블록만 드래그 (예: 대입 블록의 왼쪽 `x`)
- 드롭다운 칸: 목록에서 하나 선택
- 이름 칸: 키보드로 새 이름 입력
- 블록 영역: 블록 여러 개를 드래그해 위아래로 쌓음

### 블록마다 가진 칸
| 블록 | 칸 | 블록 영역 |
| --- | --- | --- |
| 선언 | 드롭다운 1(타입), 이름 1, 값 1(초기값, 비워도 됨) | 0 |
| 대입 | 변수 1, 값 1 | 0 |
| 연산 | 값 2, 드롭다운 1(`+ - * / %`) | 0 |
| 비교 | 값 2, 드롭다운 1(`> < == != >= <=`) | 0 |
| 논리 `And`, `Or` | 값 2, 드롭다운 1(`And`, `Or`) | 0 |
| 논리 `Not` | 값 1 | 0 |
| 조건문 | 드롭다운 1(`if`, `else if`, `else`), 값 1(조건, `else`는 없음) | 1 |
| `for` (단순형: `for (int i = 0; i < 10; i++)`) | 이름 1(`i`), 값 2(시작, 끝) | 1 |
| `while` | 값 1(조건) | 1 |
| `break`, `continue` | 없음 | 0 |
| 배열 만들기 | 드롭다운 1(타입), 이름 1, 값 1(길이) | 0 |
| `malloc` | 드롭다운 1(struct 타입) | 0 |
| `free` | 값 1(포인터) | 0 |
| 함수 블록 | 값: 그 함수의 파라미터 수만큼 | 0 |
| return | 값 1(비워도 됨) | 0 |
| 기다리기 | 값 1(시간) | 0 |

### 함수 데이터에 담는 것
- 저장하는 것
  - GUID
  - 이름
  - 반환 타입 (돌려주는 값이 없으면 `void`)
  - 파라미터 목록: 파라미터마다 이름, 타입
  - 블록 목록: 위에서부터 순서대로
- 저장하지 않고 계산하는 것 (프로퍼티 또는 함수)
  - 지역 변수 목록: 블록 목록의 선언 블록, 배열 만들기 블록에서 모음
  - 블록 수: 블록 목록의 블록을 셈
- 편집 화면 연결: 편집 화면의 블록 UI가 블록 데이터를 직접 들고 있음 (임혜원과 확인함, block-editor.md)
  - 저장: 함수 데이터를 그대로 저장, 별도 변환 단계 없음
  - 불러오기: 함수 데이터를 보고 블록 UI 생성

### 함수 블록이 부를 함수를 가리키는 방법
- 사용자 정의 함수: GUID로 지정
- 사전 정의 함수: 사람이 읽을 수 있는 고정 ID로 지정 (예: `c.thrust`), 화면 이름(`Name`)과 별도
  - ID 규칙, 고정 ID로 함수 찾기: `predefined-function.md`

### 코드 영역 크기 규칙
- 기준: 슬롯에 넣은 모든 함수와 그 함수들이 호출로 닿는 모든 함수의 블록 수 합
- 계산 시점: 슬롯에 넣을 때 (더 나은 방식이 있으면 나중에 변경)
- 블록 1개: 1
- 반복 횟수: 곱하지 않음
- 같은 함수 여러 번 호출, 여러 함수가 같은 함수 호출: 한 번만 셈
- 재귀 호출: 아직 불가
- 크기 계산과 구조 검사: 분리
  - 구조 검사(재귀, 없는 함수 호출 포함): 「만든 함수를 서버로 보내 검사·저장하기」 이슈에서 전부
- C 스킬 호출 블록 크기: 그 스킬의 `CodeSize` (`predefined-function.md`). 지금은 모두 1
  - 없는 고정 ID: 1
- 슬롯에 바로 넣은 C 스킬: C 스킬 블록 하나만 든 함수와 같게 셈 (그 스킬의 `CodeSize`)
- 편집 화면 표시: 편집 중인 함수의 코드 영역 크기 (그 함수 + 그 함수가 호출로 닿는 함수들)
  - 슬롯 전체 크기: 슬롯 장착 화면에서 표시
- 캐릭터별 한도 위치: 메모리 작업 때 결정

### 저장 형식
- 형식: JSON 글자열 (Newtonsoft Json)
  - `TypeNameHandling` 사용 안 함: 조작된 데이터가 엉뚱한 클래스를 만들지 못하게
  - 블록 구분: 블록 데이터의 종류 칸
- GUID 생성: 플레이어가 새 함수를 만드는 순간, 함수 데이터 코드가 한 번 생성 (`System.Guid.NewGuid()`)
  - 이름을 바꾸거나 블록을 고쳐도 그대로 유지
- 형식을 넓힐 때 (예: 다른 언어 추가): 새로 더하기만 함. 아래를 어기면 예전에 저장한 함수가 깨짐
  - enum 이름 바꾸기, 지우기 금지 (예: `Declare` → `Declaration` 금지)
  - 블록의 칸 순서 바꾸기 금지. 칸을 더하면 맨 뒤에 더하고, 읽는 코드는 예전 데이터에 그 칸이 없을 수 있음을 처리
  - 이미 있는 것의 뜻 바꾸기 금지. 새 뜻이면 새 이름을 더함 (예: Java의 `new`는 `Malloc`을 쓰지 않고 새 블록 종류로)

## 다른 시스템이 쓰는 것
- 코드 위치: `Assets/_UL/Scripts/FunctionData/`
  - `Data/`: `FunctionData`, `BlockData`, `SlotData`, `VariableType`, `ParameterData`, `BlockOptions`
  - `Json/`: `FunctionDataJson`
  - `CodeSize/`: `CodeSizeCalculator`
  - `Test/`: `FunctionDataTest` (씬의 아무 오브젝트에 붙이고 Play, 콘솔 로그로 결과 확인)

### `FunctionData` (함수 하나)
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Create(string name)` | 함수 이름 | `FunctionData` | 새 함수. GUID가 이때 한 번 붙음. 반환 타입 `Void`, 파라미터·블록은 빔 |
| `Guid`, `Name`, `ReturnType`, `Parameters`, `Blocks` | - | - | 저장하는 값. 편집 화면이 직접 고침 |
| `AllBlocks()` | - | `IEnumerable<BlockData>` | 함수 몸통의 모든 블록 (칸, 블록 영역 안까지) |
| `CountBlocks()` | - | `int` | 이 함수 하나의 블록 개수. 부르는 함수는 세지 않음 |
| `LocalVariables()` | - | `List<(string Name, VariableType Type)>` | 선언, 배열 만들기, `for`의 변수. 프레임 크기 계산에 씀 |

### `BlockData` (블록 하나)
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Kind` | - | `BlockKind` | 블록 종류 |
| `Slots` | - | `List<SlotData>` | 칸들. 순서는 「블록마다 가진 칸」 |
| `InnerBlocks` | - | `List<BlockData>` | 블록 영역. 조건문, `for`, `while`만 가지고 나머지는 null |
| `FunctionId`, `IsUserFunction` | - | `string`, `bool` | 함수 블록만 씀. 부를 함수의 GUID 또는 고정 ID, 사용자 정의 함수인지 |
| `SelfAndInnerBlocks()` | - | `IEnumerable<BlockData>` | 이 블록과 안쪽 블록 모두 |
| `CountBlocks()` | - | `int` | 이 블록과 안쪽 블록의 개수 |
| `CanBeValue(BlockKind)` | 블록 종류 | `bool` | 값 칸에 끼울 수 있는 종류인지. 함수 블록은 부를 함수의 반환 타입이 `Void`가 아닐 때만 끼울 수 있음 (부르는 쪽이 확인) |
| `CanBeStatement(BlockKind)` | 블록 종류 | `bool` | 함수 몸통, 블록 영역에 놓을 수 있는 종류인지 |
| 만들기 함수 | 블록마다 다름 | `BlockData` | `Declare`, `Assign`, `Arithmetic`, `Compare`, `Logic`, `Not`, `Conditional`, `For`, `While`, `Break`, `Continue`, `ArrayDeclare`, `Malloc`, `Free`, `CallUserFunction`, `CallPredefinedFunction`, `Return`, `Wait`. 칸을 순서대로 채워 줌 |

### `SlotData` (칸 하나)
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Kind` | - | `SlotKind` | `Value`, `Variable`, `Option`, `Name` |
| `Type`, `Literal` | - | `VariableType`, `string` | 직접 입력한 값 (예: `Int`, `"3"`). 드롭다운에서 고른 타입은 `Type`만 |
| `VariableName` | - | `string` | 넣은 변수 |
| `Block` | - | `BlockData` | 값 칸에 끼운 블록 |
| `Option` | - | `string` | 드롭다운에서 고른 것. `BlockOptions`의 enum 이름 (예: `"Add"`, `"ElseIf"`) |
| `Name` | - | `string` | 이름 칸에 적은 새 이름 |
| 만들기 함수 | 칸마다 다름 | `SlotData` | `FromLiteral`, `FromVariable`, `FromBlock`, `EmptyValue`, `VariableOnly`, `FromOption`, `FromTypeOption`, `FromName` |

### `VariableType` (타입 하나), `ParameterData` (파라미터 하나)
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `new VariableType(VariableTypeKind)` | 타입 종류 | `VariableType` | 기본 타입 (예: `Int`) |
| `ArrayOf`, `ListOf`, `PointerTo` | 원소 타입 | `VariableType` | 예: `Sword*` = `PointerTo(StructOf("Sword"))` |
| `StructOf(string)` | struct 이름 | `VariableType` | 예: `Sword` |
| `new ParameterData(string, VariableType)` | 이름, 타입 | `ParameterData` | |

### `BlockOptions.cs` (드롭다운 선택지)
| enum | 값 |
| --- | --- |
| `ConditionalBranch` | `If`, `ElseIf`, `Else` |
| `ArithmeticOperator` | `Add`, `Subtract`, `Multiply`, `Divide`, `Modulo` |
| `CompareOperator` | `Greater`, `Less`, `Equal`, `NotEqual`, `GreaterOrEqual`, `LessOrEqual` |
| `LogicOperator` | `And`, `Or` |

### `FunctionDataJson` (JSON 저장·불러오기)
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `ToJson(FunctionData)` | 함수 데이터 | `string` | 서버 전송, DB 저장에 씀 |
| `TryFromJson(string, out FunctionData)` | JSON 글자열 | `bool` | 실패하면 오류를 던지지 않고 `false`, 함수 데이터는 null |

### `CodeSizeCalculator` (코드 영역 크기)
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Calculate(slotUserFunctionGuids, slotPredefinedFunctionIds, ownedFunctions)` | 슬롯의 사용자 정의 함수 GUID들, 슬롯에 바로 넣은 C 스킬 ID들, 캐릭터가 가진 모든 함수 | `int` | 한도와 비교는 하지 않음. 재귀, 없는 함수를 만나도 멈추지 않음 |

```csharp
// 예시: 편집 화면이 새 함수를 만들고 블록을 넣은 뒤 저장
FunctionData function = FunctionData.Create("Attack");
function.Blocks.Add(BlockData.Declare(new VariableType(VariableTypeKind.Int), "x",
	SlotData.FromLiteral(new VariableType(VariableTypeKind.Int), "3")));
string json = FunctionDataJson.ToJson(function);
```

### 편집 화면에서 쓰는 법
- 새 함수: `FunctionData.Create(이름)`
- 블록 목록에서 블록을 끌기 시작: 함수 데이터는 바뀌지 않음
- 블록을 놓음
  - 새 블록 만들기: 놓은 블록 종류의 만들기 함수
    - 문법 블록: `BlockData.Declare(…)`, `BlockData.Conditional(…)` 등
    - C 스킬, 입력 함수: `BlockData.CallPredefinedFunction(고정 ID)`
    - 사용자 정의 함수: `BlockData.CallUserFunction(GUID)`
  - 놓은 자리에 넣기
    - 함수 몸통: `function.Blocks`
    - 조건문, `for`, `while`의 블록 영역: 그 블록의 `InnerBlocks`
    - 값 칸: 그 칸의 `SlotData.Block`
  - 놓을 수 있는지 판단
    - 값 칸: `BlockData.CanBeValue(블록 종류)`. 함수 블록은 부를 함수의 `ReturnType`이 `Void`가 아닐 때만
    - 함수 몸통, 블록 영역: `BlockData.CanBeStatement(블록 종류)`
- 블록 UI와 블록 데이터: 블록 UI 하나가 자기 `BlockData` 하나를 들고 있음
- 칸 고치기: 플레이어가 칸을 바꾸면 블록 UI가 `BlockData.Slots[칸 번호]`를 바로 고침 (칸 번호: 「블록마다 가진 칸」 순서)
  - 직접 입력한 값: `Type`, `Literal` (`bool`은 `"true"` / `"false"`, 소수는 점으로 `"0.5"`)
  - 변수: `VariableName`
  - 드롭다운에서 고른 연산자, 조건문 갈래: `Option`에 `BlockOptions`의 enum 이름 (예: `ArithmeticOperator.Add.ToString()` → `"Add"`)
  - 드롭다운에서 고른 타입: `Type`
  - 이름 칸: `Name`
  - 조건문 드롭다운: 바로 위 블록이 `If` 또는 `ElseIf` 조건문이 아니면 `ElseIf`, `Else` 선택 불가
- 놓은 블록을 옮김: 원래 자리(함수 몸통, 블록 영역, 값 칸 중 하나)에서 빼고 새 자리에 넣음. 같은 `BlockData`를 옮김
- 놓은 블록을 버림 (블록 목록 쪽으로 끌어 놓음): 원래 자리에서 빼기만 함
  - 값 칸에서 뺄 때: 그 칸의 `SlotData.Block`을 null로
- 저장: `FunctionDataJson.ToJson(function)`
- 불러오기: `FunctionDataJson.TryFromJson(json, out function)`으로 되돌린 뒤, `function.Blocks`, 각 블록의 `Slots[칸 번호].Block`, `InnerBlocks`를 따라가며 블록 UI 생성
- 코드 영역 크기 표시: `CodeSizeCalculator.Calculate(new[] { function.Guid }, new string[0], 캐릭터가 가진 함수들)`
- 언어별 표시: `Option`의 enum 이름과 `VariableTypeKind`를 지금 언어(C)의 글자로 바꿔 보여 줌 (예: `"ElseIf"` → `else if`, `"Add"` → `+`)

## 맞춘 결과
- 임혜원과 정한 것 (#72)
  - 편집 화면 연결: 블록 UI가 블록 데이터를 직접 들고 있음. 저장할 때 따로 바꾸지 않음
  - 조건문·for·while 안쪽 블록은 `InnerBlocks`, 값 칸에 들어가는 블록은 `SlotData.Block`
  - 해금은 일단 보지 않음
- 임혜원과 정할 것
  - 블록 종류, 블록별 칸 (「블럭코딩 화면 2」에서 값 칸 채우기와 함께)
