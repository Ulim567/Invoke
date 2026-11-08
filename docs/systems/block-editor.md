# BlockEditor

- 담당자: 임혜원

## 하는 일
- 블럭코딩 편집 화면. 블록 목록(팔레트)에서 블록을 끌어 편집 영역에 놓고, 위아래로 이어 붙이고, 조건문·for·while 안에 넣는다. (#72)
- 블록의 칸을 채운다. 값 칸에 값·변수를 적거나 값 블록을 끼우고, 이름·타입·연산자를 고친다. (#77)
- 만든 함수를 저장하고 다시 불러온다. 실제 계정이면 서버 DB, 아니면 이 컴퓨터의 파일에 저장한다. (#77, 서버 저장은 이우림 #226 #239)
- 편집 중인 함수는 `FunctionData` 하나다. 블록 UI(`BlockView`)가 `BlockData`를 직접 들고 있어서, 화면에서 블록을 옮기거나 칸을 고치면 함수 데이터가 바로 바뀐다. 데이터 형식은 function-data.md를 따른다.
- 하지 않는 일
  - 해금 (#396, 이우림 #309): 실제 계정이면 해금한 문법 블록(`SkillTree.IsBlockUnlocked`)과 해금한 C 스킬만 보여 준다. 서버의 저장 검사(`FunctionExecutorNetwork.CheckFunctionContent`)와 같은 규칙. 스킬 트리에 노드가 없는 함수(`input.*` 등)는 늘 보이고, 블록이 다 걸러진 분류는 머리글도 없다. 임시 계정(로그인 건너뛰기)은 모두 보여 준다
  - 저장 거부 안내: `BlockLocked`(해금 안 한 블록), `SkillLocked`(해금 안 한 스킬), `CodeSizeExceeded`(슬롯 코드 영역 `ProgressRules.MaxCodeSize` 넘음)
  - 함수 실행은 하지 않는다. `FunctionExecutor`(이우림)가 한다.
  - 구조 검사(빈 칸, 선언하지 않은 변수, 짝 없는 else 등)는 하지 않는다. 「만든 함수를 서버로 보내 검사·저장하기」(이우림)에서 한다.
  - 함수의 구조 검사·저장 판정은 서버가 한다 (`FunctionExecutorNetwork.RequestSaveFunction`, network.md).

## 동작 규칙
### 연습장에서 열기 (#225)
- 연습장(TrainingRoom)의 편집 터미널(`PanelStation`)에서 ↑ → `BlockEditorPanel`이 편집 화면을 연다. 열린 동안 Player·Attack 액션 맵이 꺼진다 (`InputBlockingPanel`, lobby-places.md)
- 저장하면 내 캐릭터의 `SlotLoadout.ReloadFunctions()`를 부른다. 슬롯에 넣어 둔 함수를 고쳐도 바로 고친 내용으로 시전되고, `FunctionExecutorNetwork`가 서버로 함수를 다시 보낸다 (slot-caster.md, network.md)
- 슬롯 장착은 같은 연습장의 장착대(`SlotEquipPanel`)에서 한다: 위의 슬롯 칸을 고르고 아래 목록(저장한 함수, C 스킬)에서 누르면 들어간다. `SlotLoadout`의 `EquipFunction`, `EquipSkill`, `Unequip`, `CodeSize`, `Changed`만 쓴다 (박진우 #160)
- 연습장 허수아비: `Monster`의 「체력 안 줄어듦」(`invulnerable`, 김도훈 #214) + `DamageNumberView`(맞으면 머리 위에 데미지 숫자)
  - 네트워크 오브젝트다 (#297): 프리팹 `Assets/_HW/Training/TrainingDummy`(NetworkIdentity, Base NetworkManager에 등록). 연습장 씬의 `DummySpawner`(`TrainingDummySpawner`)가 서버에서 자기 자리에 1마리 만들고, 연습장 씬이 내려가면 없앤다 (`NetworkSpawner.Spawn`/`Despawn`). 씬에 허수아비를 직접 두면 대상 배열 시전(전장 휩쓸기)이 서버에서 「대상 없음」이 된다

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
- 이름 칸(선언의 이름, for의 i), 변수 칸(대입의 왼쪽): 영문자·숫자·밑줄만, 숫자로 시작 불가. 아니면 원래 이름으로 되돌리고, 글자를 1초 빨갛게 보이며 상태 줄에 이유를 알림 (#432). 바꾸지 않고 나오면 고친 것으로 치지 않음
- 함수 이름 칸: 글자(한글 포함)·숫자·밑줄만, 숫자로 시작 불가 (`BlockSlotText.IsFunctionName`, #432). 이름은 실행에 쓰이지 않지만(함수는 GUID로 부름) 화면의 글자 칸이 `<`를 태그로 읽어 깨지므로 막는다. 아니면 이름은 그대로 두고 상태 줄에 이유를 알림
- 타입 버튼: 누를 때마다 int → float → bool → char → string
- 연산자 버튼: 누를 때마다 `BlockOptions`의 enum 순서대로. 데이터에는 enum 이름(`"Add"`)
- if·else if·else는 블록 목록에서 따로 고르고, 놓은 뒤에는 바꾸지 않는다.

### 화면 배치 (이우림 「블록 견본」 기준)
- 위: 제목줄(편집 터미널, 닫기), 함수 이름 칸과 금색 저장 버튼(`Save Button Prefab`)
- 왼쪽 함수 목록(`Function List`): `Global Variables` → 머리글 「슬롯」 → 슬롯 1~8 → 머리글 「페이지 n / 5 +」 → 페이지 함수. 임시 계정은 `Global Variables` → 「함수 n개 +」 → 저장한 함수
  - 줄은 `ListItemView`(번호, 이름, 오른쪽 글자, 고른 줄 모양), 머리글은 `ListHeaderView`(글자, + 버튼). 지금 연 줄은 고른 모양, 열고 나서 고쳤으면 이름 뒤 `*`
- 가운데 편집 영역(점 격자 바탕), 아래 상태 줄: 왼쪽 저장 상태(`Message Label`: 「슬롯 1 · 저장하지 않은 변경 있음」, 「저장됨」, 오류는 빨강), 오른쪽 블록 수·코드 크기(`Info Label`)
- 오른쪽 블록 목록: 분류 머리글(제어, 변수, 값, 함수, 메모리, 기타) 아래에 견본 블록
- 그림: `Assets/_HW/UI/Frame/`의 9-slice 스프라이트(창, 제목줄, 패널, 머리글, 버튼, 목록 줄, 슬롯 카드, 키 배지, 격자)
- 블록 모양(`Assets/_HW/UI/BlockCoding/Shapes`): 문장 블록은 위 왼쪽 홈(`blk_stmt`), 값 블록은 알약(`blk_value`), 조건문·반복문은 왼쪽 기둥과 아래 막대로 안쪽을 감쌈(`blk_inner`), 값 칸은 어두운 알약(`blk_slot`), 이름 칸은 밑줄, 변수 칸은 점선 상자, 드롭다운은 밝은 네모 + ▾
- 블록 목록은 칩(짧은 이름: If, For, Declare, Array Get, thrust…)을 분류별로 줄줄이 놓는다 (`FlowLayoutGroup`). 칩을 끌면 칸이 다 있는 블록이 나온다
- 타입·연산자 칸은 드롭다운(`DropdownPopup` 프리팹): 타입 int/float/bool/char/string, 연산 + - * / %, 비교 > < == != >= <=, 논리 && ||
- 블록 목록에 배열 만들기(Array), 원소 읽기(Array Get, 값 칸에 끼움), 원소 쓰기(Array Set), 내가 만든 함수 부르기(`CallUserFunction`, 「함수」 분류 끝)가 있다. malloc은 불러온 함수에서만 보인다
- 값 칸에 `NULL`을 적으면 `SlotData.FromNull()`과 같은 값으로 넣는다 (function-data.md #120)
- 위쪽 줄의 「동기 / 비동기」 버튼: `FunctionData.IsAsync`를 고른다 (#132). 슬롯 함수와 전역 변수는 바꿀 수 없음
- 끄는 동안: 블록이 반투명, 들어갈 자리에 보라색 줄(`InsertMarker`, 시작 위치에서 그 편집 영역·블록 영역의 오른쪽 끝까지, 두께 3. #432), 끼울 수 있는 값 칸은 보라색. 놓인 블록을 끌면 블록 목록 위에 빨간 휴지통 「여기에 놓으면 블록을 지워요」
- 컴파일(`FunctionChecker`): 반복문 밖의 break·continue, return이 없는 길, 비어 있는 값 칸. 오류가 있으면 오른쪽 오류 창으로 바뀌고, 비어 있는 값 칸과 반복문 밖의 break·continue 블록을 빨갛게 표시. 고치면 표시를 지운다
- 함수 틀 (#432): 편집 영역 위에 머리줄 「반환 타입 이름(파라미터) {」, 아래에 「}」 줄이 늘 있다 (`FunctionFrameView`). 편집 영역(`BlockList`)의 자식이 아니라 따로 둔 줄이다
  - 슬롯 함수: 반환 타입(void)·이름은 고정이라 누를 수 없고 「슬롯 함수 · 이름과 반환 타입은 고정」 안내. 페이지 함수: 반환 타입(void, int, float, bool, char, string)을 눌러 드롭다운으로 고른다
  - 파라미터는 편집할 수 없다. 이미 있는 파라미터는 머리줄에 보여 주기만 한다. 반환 타입을 바꿔서 틀린 곳(return이 없는 길 등)은 저장할 때 컴파일이 알려 준다. 이 함수를 이미 부르는 다른 함수의 호출 블록은 다시 확인하지 않는다
  - 전역 변수를 고칠 때는 반환 타입 없이 「Global Variables」 이름과 안내만 보인다
- 저장하지 않은 변경 확인 (#432): `BlockEditor.HasUnsavedChanges`(열고 나서 고쳤거나, 「+」로 만든 새 페이지를 저장하지 않았을 때. 기본으로 열리는 빈 함수는 고치기 전까지 해당 없음)
  - 다른 함수를 열기(함수 목록의 줄·같은 줄 다시 누르기·`+`·`Global Variables`) 전에 `ConfirmDialog`로 묻는다: 「파일이 저장되지 않았습니다. 슬롯 2 guard를 여시겠습니까?」 예(Enter) / 아니오(ESC). 예이면 지금 내용을 버리고 연다
  - 나가기(닫기 버튼, ESC)도 `BlockEditorPanel.Close`(`InputBlockingPanel.Close`를 virtual로 바꿈)가 묻는다: 「파일이 저장되지 않았습니다. 저장하지 않고 나가시겠습니까?」. 예이면 저장하지 않은 내용을 버리고(`DiscardUnsaved`) 닫는다
  - 서버에 저장을 요청하고 답을 기다리는 동안(`savePending`)은 열기·나가기를 막고 「저장하는 중입니다」만 보인다
  - 「+」로 만든 새 페이지는 저장하기 전에는 서버·파일에 없으므로, 예를 누르면 사라지고 다시 들어갈 수 없다. 다른 곳을 열면 목록의 「저장 안 함」 줄도 함께 지운다 (`RemoveStaleUnsavedEntry`)
  - 예·아니오 글자에 키(Enter, Esc)를 함께 적는다. 키 표시 부품(#430)이 생기면 배지로 바꾼다
- 저장 버튼도 먼저 컴파일한다 (#432). 오류가 있으면 서버·파일에 저장하지 않고 「컴파일 오류 n개가 있어 저장하지 않았습니다」를 빨갛게 알린다. 편집 중인 내용은 그대로 남는다
  - 전역 변수는 제외: 컴파일 대신 저장 검사(`CanUseGlobalVariables`)가 막는다
- 오류가 있는 저장한 함수는 함수 목록에서 이름이 빨간 밑줄. 장착 화면에서는 「오류」로 흐리게 보이고 넣을 수 없다
- 아직 없음: 키워드 굵은 글씨(굵은 Galmuri 글꼴 파일), 파라미터·반환 타입 고르기

### 전역 변수 (Global Variables)
- 함수 목록의 `Global Variables`를 누르면 전역 변수 선언(`GlobalVariablesData.Declarations`)을 같은 편집 영역에서 고친다. 함수 이름 칸은 끈다
- 불러오기: 실제 계정은 `ReceivedGlobals`의 복사본, 임시 계정은 `GlobalVariablesStorage.Load()`
- 저장: 실제 계정은 `RequestSaveGlobals`(결과 `PlayerDataRequestAnswered`의 `SaveGlobals`), 임시 계정은 내 캐릭터의 `FunctionExecutor.TrySaveGlobalVariables`(검사 → 저장 → 초기화). 검사에 걸리면 상태 줄에 빨갛게 알린다
- 선언 블록(`int x = 3;`)만 넣는다. 다른 블록이 있으면 저장 검사에서 거부된다 (function-executor.md 「전역 변수」)

### 실제 계정 (서버 저장)
- 내 캐릭터(`NetworkClient.localPlayer`)의 `FunctionExecutorNetwork.UsesServerData`가 true면 서버 모드다. 편집 화면을 열 때마다(`BlockEditorPanel.OpenFor` → `BlockEditor.Refresh`) 다시 확인한다.
- 함수 목록: 슬롯 1~8(`ReceivedSlots[i].Id`의 GUID인 함수, 없으면 「비어 있음」), 그 뒤에 페이지 함수(나머지 `ReceivedFunctions`). 처음 열면 slot1을 연다.
- 슬롯 함수는 이름을 바꿀 수 없다 (이름 칸을 끈다). 페이지 함수는 `ProgressRules.MaxPageFunctions`개까지 더할 수 있다 (넘으면 「새 함수」·저장을 막고 안내).
- 저장: `RequestSaveFunction(Function)`을 부르고 「저장하는 중...」을 보인다. 결과는 `PlayerDataRequestAnswered`(저장됨, 개수 초과, 슬롯 함수 잠김, 데이터 오류)로 위쪽 줄에 보이고, 성공하면 `PlayerDataReceived`에서 목록을 다시 그린다.
- 함수 버튼을 누르면 서버가 보낸 함수의 복사본(JSON 왕복)을 연다. 저장하지 않은 고친 내용이 목록에 섞이지 않게 한다.
- 장착대(`SlotEquipPanel`)는 실제 계정이면 슬롯이 고정이므로 slot1~slot8 이름과 안내만 보이고 장착 목록을 만들지 않는다.
- 장착 화면 배치: 위 슬롯 카드 8개(`SlotCardView`: 키 배지, 이름, 고른 칸은 금색 테두리), 아래 왼쪽 「내 함수」, 오른쪽 「C 스킬」(3열, 색 띠로 C 스킬·공통 이동 구분), 맨 아래 줄(고른 칸, 코드 영역, 안내, 「칸 비우기」)

### 저장·불러오기 (임시 계정, 이 컴퓨터 파일)
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
| `BlockEditor.SaveFunction()` | - | - | 지금 함수를 저장한다 (실제 계정이면 서버에 요청) |
| `BlockEditor.Refresh()` | - | - | 실제 계정인지 다시 보고 함수 목록을 새로 그린다. 편집 화면을 열 때 `BlockEditorPanel`이 부른다 |
| `FunctionStorage.LoadAll()` | - | `List<FunctionData>` | 저장한 함수 모두 (이름 순서). 슬롯 장착 화면 등에서 쓸 수 있다. 서버 저장이 생기면 바뀐다 |
| `FunctionStorage.Load(guid)` | GUID | `FunctionData` | 저장한 함수 하나. 없으면 null |

## 맞춘 결과
- 이우림과 정한 것 (#72)
  - 블록 UI가 블록 데이터(`BlockData`)를 직접 들고 있다. 저장할 때 따로 바꾸지 않고 `FunctionData`를 그대로 저장한다.
  - 조건문·for·while 안쪽 블록은 `InnerBlocks`, 값 칸에 들어가는 블록은 `SlotData.Block`에 넣는다.
  - 해금은 일단 보지 않는다. (#396에서 해금한 것만 보이게 바꿈, 위 「하는 일」)
- 이우림에게 알릴 것 (#77)
  - 실제 계정은 서버 DB, 임시 계정(서버 데이터를 쓰지 않는 캐릭터)은 이 컴퓨터 파일(`FunctionStorage`)에 저장한다.
  - 잘못 적은 값은 저장하지 않고 빈 칸으로 둔다. 빈 칸, 선언하지 않은 변수는 편집 화면에서 막지 않는다 (구조 검사에서).
