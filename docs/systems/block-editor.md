# BlockEditor

- 담당자: 임혜원

## 하는 일
- 블럭코딩 편집 화면. 블록 목록(팔레트)에서 블록을 끌어 편집 영역에 놓고, 위아래로 이어 붙이고, 조건문·for·while 안에 넣는다. (#72)
- 블록의 칸을 채운다. 값 칸에 값·변수를 적거나 값 블록을 끼우고, 이름·타입·연산자를 고친다. (#77)
- 만든 함수를 저장하고 다시 불러온다. 지금은 이 컴퓨터의 파일에 저장한다. (#77)
- 편집 중인 함수는 `FunctionData` 하나다. 블록 UI(`BlockView`)가 `BlockData`를 직접 들고 있어서, 화면에서 블록을 옮기거나 칸을 고치면 함수 데이터가 바로 바뀐다. 데이터 형식은 function-data.md를 따른다.
- 하지 않는 일
  - 해금은 아직 보지 않는다. 블록을 모두 보여 준다. 스킬 트리가 생기면 해금된 블록만 보여 준다.
  - 함수 실행은 하지 않는다. `FunctionExecutor`(이우림)가 한다.
  - 구조 검사(빈 칸, 선언하지 않은 변수, 짝 없는 else 등)는 하지 않는다. 「만든 함수를 서버로 보내 검사·저장하기」(이우림)에서 한다.
  - 서버 저장은 하지 않는다. 위 이슈에서 `FunctionStorage`를 바꾸거나 대신한다.
  - 편집 화면을 여닫을 때 플레이어 입력 끄기(`PlayerInputReader.SetPlayerMapEnabled`)는 아직 하지 않는다. 편집 화면을 게임에 붙일 때 한다.

## 동작 규칙
- 스크립트: `Assets/_HW/Scripts/BlockCoding`

| 스크립트 | 하는 일 |
| --- | --- |
| `BlockEditor` | 편집 화면 전체. 편집 중인 함수, 블록 목록, 위쪽 줄(함수 이름, 새 함수, 저장, 블록 수, 저장한 함수), 블록 UI 부품 만들기·지우기 |
| `BlockView` | 블록 하나. 끌어서 옮기기, 한 줄(글자와 칸) 만들기 |
| `BlockList` | 블록을 쌓는 자리. 함수 몸통(`FunctionData.Blocks`)과 블록 영역(`BlockData.InnerBlocks`)이 각각 하나다. 자식 순서 = 데이터 순서 |
| `BlockValueSlot` | 값 칸 하나. 적은 값 또는 끼운 블록 |
| `BlockField` | 키보드로 적는 칸. 글자 길이에 맞춰 너비가 늘어남 |
| `BlockSlotText` | 값 칸에 적은 글자 ↔ 칸 데이터 |
| `BlockLabels` | 블록 글자(C 모양)와 분류별 색 |
| `FunctionStorage` | 함수 저장·불러오기 (임시, 이 컴퓨터 파일) |

### 블록 놓기
- 블록 목록의 블록은 견본이다. 끌면 견본은 그대로 두고 새 블록을 만든다.
- 블록 목록: 문법 블록, 값 블록(`+`, `>`, `&&`, `!`), `free`, 사전 정의 함수 중 고정 ID가 `c.`, `common.`, `input.`으로 시작하는 것 (`PredefinedFunctionRegistry.All`). 함수 블록은 파라미터 수만큼 빈 값 칸을 가진다.
- 편집 영역의 블록을 블록 목록이나 빈 곳에 놓으면 지워진다.
- 놓을 자리는 마우스 아래에서 가장 안쪽의, 이 블록을 받을 수 있는 자리다.
  - 값 칸: 비어 있고 `BlockEditor.CanBeValue`가 true일 때. 함수 블록은 부를 함수의 `ReturnType`이 `Void`가 아닐 때만
  - 블록 자리: `BlockData.CanBeStatement`가 true일 때. else if, else는 if 또는 else if 바로 아래에만
  - 받을 수 없으면 원래 자리로 돌아간다 (새로 꺼낸 블록이면 지워진다).
- 블록 가운데보다 위에 놓으면 그 블록 앞에 들어간다.

### 칸 고치기
- 값 칸에 적은 글자 (`BlockSlotText`)

| 적은 글자 | 칸 데이터 |
| --- | --- |
| `3`, `-1` | `Type` Int, `Literal` |
| `0.5`, `.5` | `Type` Float, `Literal` (점으로 저장) |
| `true`, `false` | `Type` Bool, `Literal` |
| `'a'` | `Type` Char, `Literal` `a` |
| `"abc"` | `Type` String, `Literal` `abc` |
| `x`, `hp_2` | `VariableName` |
| 빈칸 | 모두 비움 |
| 그 밖 (`3x`, `1,5`) | 모두 비움. 칸 글자를 빨갛게 보여 줌 (저장되지 않음) |

- 값 블록을 끼우면 `SlotData.Block`에 넣고 적은 값은 지운다. 빼면 `Block`을 null로.
- 이름 칸(선언의 이름, for의 i), 변수 칸(대입의 왼쪽): 영문자·숫자·밑줄만, 숫자로 시작 불가. 아니면 원래 이름으로 되돌림
- 타입 버튼: 누를 때마다 int → float → bool → char → string
- 연산자 버튼: 누를 때마다 `BlockOptions`의 enum 순서대로. 데이터에는 enum 이름(`"Add"`)
- if·else if·else는 블록 목록에서 따로 고르고, 놓은 뒤에는 바꾸지 않는다.

### 저장·불러오기
- 저장: `FunctionDataJson.ToJson`을 그대로 `Application.persistentDataPath/Functions/{GUID}.json`에 쓴다. 같은 GUID면 덮어씀
- 불러오기: 위쪽 줄의 함수 버튼을 누르면 파일에서 새로 읽어 연다 (저장하지 않은 고친 내용은 버린다). `function.Blocks`, `InnerBlocks`, `Slots[칸 번호].Block`을 따라가며 블록 UI를 만든다.
- 위쪽 줄 표시: 블록 수(`FunctionData.CountBlocks`), 코드 영역 크기(`CodeSizeCalculator.Calculate`, 저장한 함수 + 지금 함수)

### 블록 그림
- 임시로 색 네모와 글자(Galmuri11)다. 아트 이슈에서 프리팹의 Image 스프라이트와 `BlockLabels`의 색만 바꾼다.

| 분류 | 블록 | 색 |
| --- | --- | --- |
| 제어 | if, else if, else, for, while, break, continue | `#B8742F` |
| 변수 | 선언, 대입, 배열 | `#2F8F86` |
| 값 | 연산, 비교, 논리, Not | `#3E8E4F` |
| 함수 | C 스킬, 사용자 함수 | `#6A5AC9` |
| 메모리 | malloc, free | `#C24D86` |
| 기타 | return, 기다리기 | `#4A5578` |

### 화면 구성 (테스트 씬 `Test_HW`)
- `BlockEditorCanvas` (Canvas Scaler 640×360, `BlockEditor`)
  - `Palette`: Image + Rect Mask 2D + Scroll Rect → `Content`(Vertical Layout Group + Content Size Fitter)
  - `Workspace`: Image + Vertical Layout Group + `BlockList`
  - `TopBar`: Image + Horizontal Layout Group (안의 칸과 버튼은 `BlockEditor`가 만든다)
  - `DragLayer`: 화면 전체, Canvas 맨 아래 자식
- 프리팹 `Assets/_HW/UI/BlockCoding`
  - `Block`: Image + Vertical Layout Group + Canvas Group + `BlockView`, 자식 `Row`(Horizontal Layout Group), `Inner`(Image + Vertical Layout Group + Layout Element + `BlockList`)
  - `BlockText`: Text - TextMeshPro (Raycast Target 끔)
  - `BlockField`: Input Field - TextMeshPro + Layout Element + `BlockField`. 안쪽 Text의 **Extra Padding은 끈다** (켜면 Galmuri 픽셀 글자가 깨짐)
  - `BlockValueSlot`: Image(투명, Raycast Target 켬) + Horizontal Layout Group + Layout Element + `BlockValueSlot`
  - `BlockButton`: Button - TextMeshPro + Horizontal Layout Group + Layout Element
- 확인: Play 중 `BlockEditor` 컴포넌트 ⋮ 메뉴 → 「지금 함수를 JSON으로 보기」로 Console에 함수 데이터를 찍는다.

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `BlockEditor.Function` | - | `FunctionData` | 지금 편집 중인 함수. 읽기만 한다 |
| `BlockEditor.Open(function)` | `FunctionData` | - | 함수 하나를 편집 영역에 연다. 블록이 이미 있으면 그대로 화면에 만든다 |
| `BlockEditor.SaveFunction()` | - | - | 지금 함수를 저장한다 |
| `FunctionStorage.LoadAll()` | - | `List<FunctionData>` | 저장한 함수 모두 (이름 순서). 슬롯 장착 화면 등에서 쓸 수 있다. 서버 저장이 생기면 바뀐다 |
| `FunctionStorage.Load(guid)` | GUID | `FunctionData` | 저장한 함수 하나. 없으면 null |

## 맞춘 결과
- 이우림과 정한 것 (#72)
  - 블록 UI가 블록 데이터(`BlockData`)를 직접 들고 있다. 저장할 때 따로 바꾸지 않고 `FunctionData`를 그대로 저장한다.
  - 조건문·for·while 안쪽 블록은 `InnerBlocks`, 값 칸에 들어가는 블록은 `SlotData.Block`에 넣는다.
  - 해금은 일단 보지 않는다.
- 이우림에게 알릴 것 (#77)
  - 저장은 임시로 이 컴퓨터 파일(`FunctionStorage`)에 한다. 서버 저장 이슈에서 바꾼다.
  - 잘못 적은 값은 저장하지 않고 빈 칸으로 둔다. 빈 칸, 선언하지 않은 변수는 편집 화면에서 막지 않는다 (구조 검사에서).
