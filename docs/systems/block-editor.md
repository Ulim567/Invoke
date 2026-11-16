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
  - 장착대 표시 (#470): 임시 계정에서만 보임. 실제 계정은 슬롯이 늘 slot1~slot8이라 내 화면에서 장착대를 숨김 (`SlotEquipPanel.IsAvailableFor`)
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
| `SlotRules` | 값 칸마다 받을 수 있는 것 (#474) |
| `VariableScopes` | 블록 자리에서 보이는 변수(이름, 타입). C의 { } 범위 규칙 (#474) |
| `BlockLabels` | 블록 글자(C 모양)와 분류별 색 |
| `FunctionItemView` | 함수 탭의 함수 항목 한 줄. 끌면 그 함수의 호출 블록을 만들어 끔 (#489) |
| `FunctionStorage` | 함수 저장·불러오기 (임시, 이 컴퓨터 파일) |

### 블록 놓기
- 블록 목록의 블록은 견본이다. 끌면 견본은 그대로 두고 새 블록을 만든다.
- 함수 탭의 항목도 같다 (#489): 끌면 그 함수의 호출 블록을 새로 만든다 (`BlockEditor.BeginNewBlockDrag`, 아래 「화면 배치」의 「함수 탭」)
- 블록 목록(문법 탭): 문법 블록, 값 블록(`+`, `>`, `&&`, `!`), return (#489). 함수를 부르는 블록과 malloc·free·wait는 함수 탭에 있다. 함수 블록은 파라미터 수만큼 빈 값 칸을 가진다.
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
- 칸마다 받는 것 (`SlotRules`, #474): 맞지 않는 블록은 끼워지지 않고(끄는 동안 칸이 흐린 점선, #487), 맞지 않게 적은 값은 칸 글자를 빨갛게 남기고 저장하지 않으며 상태 줄에 「이 칸에는 ○○만 넣을 수 있어요」

| 칸 | 받는 것 |
| --- | --- |
| if·else if·while의 조건, `&&`·`\|\|`·`!` | `true`/`false`, 비교·논리·`!` 블록, bool을 돌려주는 함수 |
| 연산의 양쪽, wait의 초 | int, float, char, 연산 블록, 숫자를 돌려주는 함수 |
| for의 시작·끝, 원소 읽기·쓰기의 칸 번호 | int, char, 연산 블록, 정수를 돌려주는 함수 |
| 배열 만들기의 길이 | 직접 적은 1 이상의 int만 (변수, 블록 불가) |
| 비교의 양쪽 | string과 array를 뺀 값 (C는 string 비교 불가) |
| free | `NULL`, malloc, 포인터를 돌려주는 함수 |
| 선언의 처음 값 | 선언한 타입. 숫자(int, float, char)끼리는 서로 받음. `Sword*`는 `NULL`·malloc(Sword)·`summon_sword()`. `Target`은 `get_bug()`(NULL 불가). 전역 변수는 직접 적은 값만(`Target` 전역 변수는 비워 둠) |
| 함수 블록의 파라미터 | 그 파라미터의 타입 (예: `thrust`의 target은 `get_bug()`·Target 변수, `free_sword`의 sword는 `Sword*`) |
| return | 함수의 반환 타입. void 함수는 비워 둠 |
| 대입의 값, 원소 쓰기의 값 | 왼쪽 변수의 타입, array의 원소 타입 |

  - 변수와 원소 읽기(`arr[i]`)는 그 자리에서 보이는 변수의 타입으로 검사 (아래 「변수」). 직접 적은 값만 받는 칸에는 넣을 수 없음
  - 컴파일(`FunctionChecker`)도 같은 표로 검사: 「○○은(는) 이 칸에 넣을 수 없어요」. 선언 타입이나 반환 타입을 나중에 바꿔 맞지 않게 된 값, 예전에 저장한 값을 찾음. `FunctionChecker.Find`·`HasErrors`의 세 번째 값(캐릭터가 가진 함수들)으로 사용자 정의 함수의 파라미터 타입을 찾음 (없으면 그 칸은 검사하지 않음)
- 변수 (#474 5단계): 그 블록보다 위에서 선언했고 그 자리에서 보이는 변수만 쓸 수 있음 (C의 { } 범위, 실행기와 같음). 전역 변수·파라미터는 늘, for의 i는 그 안쪽에서만, if 안에서 선언한 변수는 if 밖에서 안 보임
  - 변수 칸(대입의 왼쪽, 원소 읽기·쓰기의 array): 이름을 적지 않고 드롭다운으로 고름(「int x」, 「int arr[]」). 대입은 array가 아닌 변수만, 원소 읽기·쓰기는 array만. 블록 목록에서 꺼내면 비어 있고 「변수」로 보임 (고르지 않으면 컴파일 「비어 있는 값 칸」)
  - 값 칸: 키보드로 이름을 적거나, 칸 옆 ▾ 버튼(「변수 고르기」, 바탕 네모 없이 삼각형만 #487)으로 이 칸에 넣을 수 있는 변수(보이고 타입이 맞는 것) 중에 고름. 직접 적은 값만 받는 칸(전역 변수의 처음 값, 배열 길이)에는 버튼이 없음
  - 적은 이름이 보이지 않는 변수이거나 타입이 칸에 맞지 않으면 칸 글자를 빨갛게, 저장하지 않고 상태 줄에 이유
  - 컴파일도 같은 범위 규칙으로 「선언하지 않은 변수」를 찾음
- 이름 칸(선언의 이름, for의 i): 영문자·숫자·밑줄만, 숫자로 시작 불가. 아니면 원래 이름으로 되돌리고, 글자를 1초 빨갛게 보이며 상태 줄에 이유를 알림 (#432). 바꾸지 않고 나오면 고친 것으로 치지 않음
- 함수 이름 칸: 글자(한글 포함)·숫자·밑줄만, 숫자로 시작 불가 (`BlockSlotText.IsFunctionName`, #432). 이름은 실행에 쓰이지 않지만(함수는 GUID로 부름) 화면의 글자 칸이 `<`를 태그로 읽어 깨지므로 막는다. 아니면 이름은 그대로 두고 상태 줄에 이유를 알림
- 선언의 처음 값 칸 (#474): 처음 값이 비었으면 접혀서 `int x; [>]`로 보임
  - `>` 버튼: `int x = [값] ; [<]`로 펼침. `<` 버튼: 접고 처음 값(적은 값, 끼운 블록)을 지움. 숨겨 둔 값으로 실행되어 화면과 동작이 달라지지 않게 하기 위함
  - 펼쳤는지는 함수 데이터에 저장하지 않음. 처음 값 칸이 비었으면 접힌 것으로 보고, 함수를 다시 열면 접힌 모양
  - 저장 버튼을 누르면 펼쳐 두고 비운 처음 값 칸을 접음 (빈 처음 값은 기본값으로 실행되므로 `int x;`와 같음)
  - 전역 변수 편집 화면도 같은 선언 블록이라 같음
- 타입 칸: 드롭다운 (아래 「화면 배치」)
  - 선언(지역 변수, 전역 변수), 배열 만들기의 원소 타입: int, float, bool, char, string, `Sword*`, `Target` (#474). `Sword*`는 `VariableType.PointerTo(StructOf("Sword"))`, `Target`은 `VariableType.TargetOf(Bug)`
  - 선언의 타입을 바꿨는데 이미 넣은 처음 값이 맞지 않으면: 값은 지우지 않고 칸을 빨갛게, 상태 줄에 알림. 저장할 때 컴파일이 막음
  - `Target` 변수 (#474): `get_bug()`로 고른 몬스터를 담아 여러 스킬에 넘김. 몬스터가 죽었는지는 `is_valid_target(b)`로 확인 (NULL과 비교 불가). 예: `if (!is_valid_target(b)) b = get_bug(); thrust(b);`
- 연산자 버튼: 누를 때마다 `BlockOptions`의 enum 순서대로. 데이터에는 enum 이름(`"Add"`)
- if·else if·else는 블록 목록에서 따로 고르고, 놓은 뒤에는 바꾸지 않는다.

### 화면 배치 (이우림 「블록 견본」 기준)
- 위: 제목줄(편집 터미널, 닫기), 함수 이름 칸과 금색 저장 버튼(`Save Button Prefab`)
- 왼쪽 함수 목록(`Function List`): `Global Variables` → 머리글 「슬롯」 → 슬롯 Q~F → 머리글 「페이지 n / 5 +」 → 페이지 함수. 임시 계정은 `Global Variables` → 「함수 n개 +」 → 저장한 함수
  - 줄은 `ListItemView`(번호, 이름, 오른쪽 글자, 고른 줄 모양, 페이지 함수 줄의 지우기 버튼 #494), 머리글은 `ListHeaderView`(글자, + 버튼). 지금 연 줄은 고른 모양, 저장하지 않은 변경(작업본)이 있으면 이름 뒤 `*`
  - 슬롯 이름 (#487): 슬롯 키(Q W E R A S D F)가 슬롯 함수의 이름. 함수 목록, 함수 틀(`void Q( ) {`), 위쪽 함수 이름 칸, 상태 글자(「슬롯 Q」), 확인창(「슬롯 W를 여시겠습니까?」)에 키로 보임
    - 데이터의 이름(`slot1`~`slot8`)은 그대로. 서버가 저장할 때 이름을 검사하므로 화면에서만 키로 바꿈 (`BlockEditor.DisplayNameOf`)
    - 빈 슬롯: 「비어 있음」 글자와 흐리게 하는 표시 없이 키만
    - 임시 계정도 슬롯에 끼운 함수를 슬롯 함수로 다룸: 목록·함수 틀·이름 칸·상태 글자에 키로 보이고, 이름과 반환 타입을 바꿀 수 없음 (이름을 바꾸려면 장착대에서 슬롯을 비운 뒤). C 스킬·빈 슬롯 줄은 키만 보이고 누를 수 없음
- 가운데 편집 영역(점 격자 바탕), 아래 상태 줄
  - 편집 영역 스크롤 (#476): 블록이 길거나 넓어지면 세로·가로 스크롤 + 오른쪽·아래 스크롤바(내용이 넘칠 때만 보임). 블록 자리(`Content`)는 가장 작을 때 보이는 칸 크기라서, 블록 아래 빈 곳에 놓아도 맨 끝에 들어감: 왼쪽 저장 상태(`Message Label`: 「슬롯 Q, 저장하지 않은 변경 있음」, 「저장됨」, 오류는 빨강), 오른쪽 블록 수·코드 크기(`Info Label`)
- 오른쪽 블록 목록(문법 탭): 분류 머리글(제어, 변수, 값, 함수) 아래에 견본 블록. 「함수」 분류에는 Return만 (#489)
  - 두 단계 분류 (#487): 분류 아래 소분류마다 칩 줄 하나, 줄 맨 앞에 소분류 이름(회색 `#8A84A0`)

    | 분류 | 소분류 |
    | --- | --- |
    | 제어 | 조건(If, Else If, Else), 반복(For, While, Break, Continue) |
    | 변수 | 변수(Declare, Assign), 배열(Array, Array Get, Array Set) |
    | 값, 함수 | 소분류 없음 (글씨 없이 칩만) |

    - 소분류 이름이 있는 줄은 내어쓰기: 칩이 넘쳐 다음 줄로 가면 이름 아래가 아니라 첫 칩 자리에서 시작 (`FlowLayoutGroup.HangingIndent`)
    - 남은 블록이 없는 소분류는 줄째 숨김
  - 검색 칸 (#476, 안내 「이름으로 검색」): 분류·소분류 이름, 칩 이름에 검색 글자가 들어 있는 블록만 (대소문자 구분 없음). 분류·소분류 이름이 맞으면 그 블록 전체 (예: 「반복」 → For, While, Break, Continue). 남은 블록이 없는 분류는 머리글도 없음
  - 세로 스크롤 + 오른쪽 스크롤바 (내용이 길 때만 보임)
- 오른쪽 끝 세로 탭: 문법 / 함수 / 오류 / 참고 (#489). 누른 탭의 창 하나만 보임. 블록 목록 탭은 문법 블록만 남으므로 「문법」
  - 네 창 모두 머리글(「오류」, 「참고」 등) 없음. 어느 창인지는 세로 탭이 알려 줌 (#489)
  - 오류 창의 「오류 없음」 글자는 창 둘레에서 여백 10
- 함수 탭 (#489, 게임 UI 캔버스 시안 6-12): 함수를 코드로 끌어다 놓는 목록. 블록 목록과 같이 머리글 없이 맨 위 검색 칸(안내 「이름으로 검색」), 아래에 구역 머리글(`ListHeaderView`, 색 네모 없음)과 항목(사이 간격 6)
  - 검색 칸: 구역 이름(메모리)이 맞으면 그 구역 전체, 아니면 한글 이름(휩쓸)이나 영문 이름(sweep)에 검색 글자가 든 항목 (대소문자 구분 없음). 남은 항목이 없는 구역은 머리글도 없음
  - 구역과 항목

    | 구역 | 항목 | 끌면 만드는 블록 |
    | --- | --- | --- |
    | 스킬 | 사전 정의 함수 중 고정 ID가 `c.`, `common.`인 것 | `BlockData.CallPredefinedFunction` |
    | 기본 함수 | get_bug, is_valid_target, wait | get_bug·is_valid_target: `CallPredefinedFunction`, wait: `BlockData.Wait` (초 칸 비움) |
    | 메모리 | malloc, free | `BlockData.Malloc(Sword)`, `BlockData.Free` |
    | 내가 만든 함수 | 페이지 함수 (슬롯 함수 제외) | `BlockData.CallUserFunction` |

  - 항목 한 줄(`FunctionItem` 프리팹, `FunctionItemView`): 굵은 제목(한글 이름), 아래에 함수 원형(`void thrust(Target target);`). 영문 이름은 원형에 들어 있으므로 따로 적지 않음
    - 제목: 한글 이름. 한글 이름이 없는 내가 만든 함수는 함수 이름
    - 원형 글자: 낱말마다 다른 색 (아래 「코드 색」). 길면 잘리지 않고 다음 줄로 감쌈 (`void battlefield_sweep(Target` / `array targets);`)
- 코드 색 (#489, VS Code 어두운 테마 Dark+ 기준, `BlockReference.ColorCode`): 함수 원형과 문법 블록 형식(`if (조건) { 블록 }`)을 낱말마다 칠함. 함수 탭 항목, 참고 카드의 형식 줄, 설명창이 같이 씀

  | 부분 | 예 | 색 |
  | --- | --- | --- |
  | 키워드 | `if`, `else`, `for`, `while`, `break`, `continue`, `return` | 보라 `#C586C0` |
  | 기본 타입, 상수 | `void`, `int`, `bool`, `float`, `array`, `true`, `NULL` | 파랑 `#569CD6` |
  | 게임 타입 (대문자로 시작) | `Target`, `Sword` | 청록 `#4EC9B0` |
  | 함수 이름 (뒤에 괄호) | `skewer`, `get_bug`, `malloc` | 노랑 `#DCDCAA` |
  | 변수·파라미터 이름 | `target`, `sec`, `i` | 하늘색 `#9CDCFE` |
  | 숫자 | `0`, `0.5` | 연두 `#B5CEA8` |
  | 문자열 | `"abc"` | 주황 갈색 `#CE9178` |
  | 괄호, 연산자, 포인터의 `*` | `( ) { } ; = < && *` | 밝은 회색 `#D4D4D4` |
  | 한글 자리 글자 | `조건`, `블록`, `처음 값` | 연보라 `#B9A2FF` (코드가 아니라 채울 곳. 흐린 보라 `#B8B3C7`는 설명 글자와 구분되지 않고, 주황은 진해서 바꿈) |

  - 참고 카드를 펼친 내용의 줄 이름도 같은 규칙으로 칠함: 「채울 곳」의 `타입`·`처음 값`(연보라, 형식 줄의 자리 글자와 같음), 「파라미터」의 `target`(하늘색, 원형의 파라미터와 같음), 「돌려주는 값」의 `Target`(청록, 원형의 반환 타입과 같음). 설명창의 파라미터 줄도 같음
  - 파라미터 이름은 핑크로 했다가 VS Code와 같은 하늘색으로 바꿈 (사용자가 정함)
  - 참고 카드를 펼쳤을 때의 「예시」 코드도 같은 규칙
    - 한글 이름: 스킬은 스킬 표의 이름(`PredefinedFunction.Name`), get_bug 「버그 찾기」, is_valid_target 「대상 확인」, wait 「기다리기」, malloc 「메모리 할당」, free 「메모리 해제」. 내가 만든 함수는 없음 (`BlockReference.KoreanNameOf`, `BuiltinOf`)
    - 원형: 사전 정의 함수·내가 만든 함수는 파라미터에서 만듦(`BlockReference.PrototypeOf`). malloc `Sword* malloc(Sword);`, free `void free(Sword* p);`, wait `void wait(float sec);`
    - 바탕: 함수 색(`#9E7BFF`)을 편집 영역 바탕(`#141823`)에 26% 섞은 불투명 색
  - 설명창: 항목에 마우스를 올리고 0.4초 멈추면 편집 영역의 같은 블록과 같은 설명창 (원형, 스킬 설명, 파라미터, 돌려주는 값). 항목에는 설명을 넣지 않음 (목록이 길어지지 않게)
  - 끄는 동안 (시안 6-8): 목록의 그 항목은 바탕 8%와 점선 테두리(`blk_round_dashed`, 함수 색 70%)로 비워 둠. 블록을 놓으면 원래대로
  - 해금: 블록 목록과 같은 규칙. 사전 정의 함수와 내가 만든 함수는 함수 블록(`BlockKind.Function`)을 해금해야 보이고, 스킬 트리에 노드가 있는 스킬은 그 노드도 해금해야 보임. malloc·free·wait는 각자의 블록 종류를 해금해야 보임. 임시 계정은 모두 보임. 항목이 없는 구역은 머리글도 없음
  - 함수 탭을 열 때와 저장한 함수 목록이 바뀔 때 목록을 다시 만듦
  - 아직 없음: 「남은/가진」 개수 표시 (블록·함수 개수 이슈)
- 참고 탭 (#476): 문법 탭과 같은 분류 순서(제어, 변수, 값, 함수)로 분류 머리글(`ListHeaderView`, 분류 색 네모) 아래에 카드. 검색해서 카드가 없는 분류는 머리글도 없음. 카드 사이 간격 7. 검색 칸 안내 「이름으로 검색」
  - 분류 태그 (#489, 시안 6-13): 「분류 > 소분류」 두 단계. 머리글은 앞 단계(`BlockReferenceEntry.Group`)마다 하나. 태그 너비는 글자에 맞춤 (가장 작을 때 34)

    | 카드 | 태그 |
    | --- | --- |
    | If, Else If, Else / For, While, Break, Continue | 제어 > 조건 / 제어 > 반복 |
    | Declare, Assign / Array, Array Get, Array Set | 변수 > 변수 / 변수 > 배열 |
    | Arithmetic, Compare, Logic, Not | 값 |
    | Return | 함수 |
    | 스킬(c., common.) / get_bug, is_valid_target, wait / malloc, free | 함수 > 스킬 / 함수 > 기본 함수 / 함수 > 메모리 |

    - 「·」 대신 「>」: Galmuri11 글꼴에 「·」가 없고, 소분류가 분류 안에 속한다는 뜻 (사용자가 정함)
  - 함수 카드 (#489): 제목은 한글 이름만(예: 「꼬챙이」, 함수 탭 항목 제목과 같음), 영문 이름은 형식 줄의 C 원형(`void skewer(Target target);`)에. 함수 탭과 같은 순서(스킬, 기본 함수, 메모리)
    - malloc, free, wait도 함수 카드 모양: 소문자 이름, 한글 이름, 원형(`Sword* malloc(Sword);`, `void free(Sword* p);`, `void wait(float sec);`). 채울 곳·예시는 문법 블록 표의 것
    - 검색: 이름, 분류 태그(조건, 스킬), 한글 이름(꼬챙), 고정 ID(c.skewer)
  - 카드 내용 (#476, 레퍼런스 페이지 형식): 접힌 카드는 이름·분류 태그, 형식(「코드 색」: `if (조건) { 블록 }`, `void thrust(Target target);`)만. 펼치면 설명 한 문장, 채울 곳 또는 파라미터(「- 이름: 설명」), 돌려주는 값, 예시, 실행(동작·쿨타임 틱, 코드 크기). 설명 앞에 붙이던 스킬 이름(「검 찌르기: 」)은 제목으로 옮김 (#489)
  - 펼치는 카드는 하나만 (#489): 카드를 펼치면 펼쳐져 있던 다른 카드가 접힘. 펼친 카드를 다시 누르면 접힘
  - 보이지 않는 것: C 표기, 놓는 곳, 동작 문단, 주의, 스킬 사용법·긴 설명
- 글꼴 주의: Galmuri11 TMP는 정적 아틀라스라 `—`, `·`, `•`, `…`, `▾`, `「」`가 없어 빈칸으로 보인다. 편집 화면 글자에는 `-`, `:`, `...`, 따옴표를 쓴다
- 그림: `Assets/_HW/UI/Frame/`의 9-slice 스프라이트(창, 제목줄, 패널, 머리글, 버튼, 목록 줄, 슬롯 카드, 키 배지, 격자)
- 블록 모양 (#487, 게임 UI 캔버스 시안 6): 그림은 `Assets/_HW/UI/BlockCoding/Shapes`. 흰색 그림이고 색은 `BlockView`가 칠함
  - 블록 줄만 칠함(`Row`의 Image). 블록 전체 바탕은 투명 (끌기·놓을 자리 찾기만 받음)
  - 문장 블록 줄: 둥근 네모(`blk_round`). 조건문·반복문 줄은 위 모서리만 둥긂(`blk_round_top`)
  - 값 블록(Arithmetic, Compare, Logic, Not, Array Get): 선 없는 알약(`blk_value`). 나머지는 네모
  - 조건문·반복문(ㄷ자): 안쪽 영역은 왼쪽 팔(너비 9, `blk_arm`)만 칠하고 안은 비움. 아래 막대(`Foot`)는 너비 60 × 높이 8 고정 (`blk_round_bottom`)
  - 값 칸: 투명 바탕에 1픽셀 테두리 알약(`blk_slot_line`, `#F4F1FA` 30%). 비었으면 점선(`blk_slot_dashed`, 45%). 블록을 끼우면 테두리를 숨기고 여백·최소 크기를 없애 블록 크기에 맞춤
  - 이름 칸은 밑줄, 변수 칸은 점선 상자, 드롭다운은 밝은 네모 + ▾
  - 그림은 메뉴 Invoke → UI → 편집·장착 화면 새 디자인으로 만들기가 코드로 그림 (`InvokeUIBuilder.BuildShapeSprites`)
- 블록 칠 (#487): 분류 색을 편집 영역 바탕(`#141823`)과 섞은 **불투명한 색**. 반투명으로 칠하면 칸에 끼운 블록 색이 바깥 블록 색과 섞여 탁해지기 때문

  | 칠하는 곳 | 분류 색의 비율 |
  | --- | --- |
  | 블록 줄, 블록 목록 칩 | 40% |
  | 값 블록 | 39% |
  | ㄷ자 팔, 아래 막대 | 22% |

  - 섞은 뒤 채도(HSL)를 1.8배로 올림. 밝기는 그대로라 글자 대비는 그대로
  - 밝기 교대: 값 칸 안으로 한 겹 들어간 블록은 14% 더 진하게, 두 겹째는 다시 원래대로. 선 없이도 같은 색 블록이 겹친 경계(`is_valid_target(get_bug())`)가 보임
  - 값 칸에 끼운 블록은 함수 블록이어도 값 블록처럼 얇은 여백(위아래 1). 겹칠수록 두꺼워지지 않게
  - 값은 Block 프리팹의 `BlockView`(`Row Alpha`, `Value Alpha`, `Arm Alpha`, `Saturation`, `Nested Boost`). 메뉴를 누르면 `InvokeUIBuilder`가 위 값으로 넣음
- 블록 목록은 칩(짧은 이름: If, For, Declare, Array Get, thrust…)을 분류별로 줄줄이 놓는다 (`FlowLayoutGroup`). 칩을 끌면 칸이 다 있는 블록이 나온다
  - 칩 크기: 편집 영역 블록과 같음 (#476, 예전 0.85배)
- 블록 크기 (#476): 끌어 놓기 쉽게 여백과 값 칸을 키움(값 칸 최소 24×17, 예전 18×13). 글자 크기 11은 그대로 (Galmuri11이 깨지지 않게). 블록 글자 색 `#F4F1FA`
  - #476의 어두운 1픽셀 테두리(`Outline`)는 없앰 (#487)
- 빈 값 칸의 안내 글자 (#476): 칸이 받는 타입을 흐리게 (`SlotRules.ShortLabel`). 칸 너비도 이 글자만큼

| 칸 | 안내 글자 |
| --- | --- |
| 조건 (if, while, &&, !) | `bool` |
| 연산의 양쪽, wait | `숫자` |
| for의 시작·끝, 칸 번호, 배열 길이 | `int` |
| free | `포인터` |
| 함수의 파라미터, 선언의 처음 값, return, 대입의 값 | 그 타입 (`Target`, `Sword*`, `int` …). 타입을 모르면 `값`, void 함수의 return은 비움 |
| 비교의 양쪽 | `값` |

- 설명창 (#476): 블록·값 칸에 마우스를 올리고 0.4초 멈추면 그 아래에 설명창(`BlockTooltip` 프리팹). 블록 목록의 칩과 편집 영역의 블록 모두. 겹치면 가장 안쪽 것. 끄는 동안은 띄우지 않음
  - 사전 정의 함수: 함수 원형(「코드 색」, `void thrust(Target target);`), 한글 이름(`BlockReference.KoreanNameOf`, 함수 탭·참고 카드와 같음), 설명(스킬 트리 노드 설명, 참고 카드와 같음), 파라미터마다 무엇을 넣는지, 돌려주는 값
  - 내가 만든 함수: 함수 원형
  - 문법 블록: 형식, 설명 한 문장 (참고 카드의 표). 채울 곳은 설명창이 길어지므로 참고 탭에서만
  - 값 칸: 함수의 파라미터면 「타입 이름」과 무엇을 넣는지, 그리고 넣을 수 있는 것 (`SlotRules.Describe`)
- 타입·연산자 칸은 드롭다운(`DropdownPopup` 프리팹): 타입(위 「칸 고치기」의 타입 칸), 연산 + - * / %, 비교 > < == != >= <=, 논리 && ||
- 블록 목록에 배열 만들기(Array), 원소 읽기(Array Get, 값 칸에 끼움), 원소 쓰기(Array Set)가 있다. 배열 블록 셋은 `syntax.list` 해금 뒤에 보인다 (#470)
- 내가 만든 함수 부르기(`CallUserFunction`)는 함수 탭의 「내가 만든 함수」에 있다 (#489). 페이지 함수만 붙이고 슬롯 함수(slot1~slot8)는 붙이지 않는다 (#470, 슬롯 키로 시전하는 함수라 다른 함수 안에서 부르지 않음)
- 값 칸에 `NULL`을 적으면 `SlotData.FromNull()`과 같은 값으로 넣는다 (function-data.md #120)
- 위쪽 줄의 「동기 / 비동기」 버튼: `FunctionData.IsAsync`를 고른다 (#132). 슬롯 함수와 전역 변수는 바꿀 수 없음
  - 지금은 항상 숨김 (#470). 버튼과 `ToggleAsync`는 남겨 둠, 저장된 `IsAsync` 값은 그대로
- 끄는 동안 (#476): 마우스를 따라오는 블록은 반투명하고 작게(0.6배, `BlockView.dragScale`). 들어갈 자리에는 같은 모양의 반투명 블록(미리보기, 불투명도 0.4)을 실제로 끼워 놓은 뒤의 모습을 보임
  - 블록 자리: 그 자리에 미리보기가 들어가 아래 블록이 밀려남. 값 칸: 칸 안에 미리보기가 들어가고 칸은 청록 테두리. 놓을 수 없는 곳이면 미리보기 없음
  - 빈 칸 표시 (#487, 시안 6-11): 값 칸에 넣을 수 있는 블록(연산, 비교, 값을 돌려주는 함수 등)을 끄는 동안 편집 영역의 빈 칸마다
    - 끼울 수 있는 칸: 청록 2픽셀 테두리(`blk_slot_bold`, `#5EF2D6`), 안내 글자도 청록
    - 끼울 수 없는 칸: 흐린 점선(`#F4F1FA` 18%), 안내 글자 `#5A5470`
    - 값이 들어 있는 칸, 값 칸에 넣을 수 없는 블록(if, void 함수 등)을 끌 때: 표시 없음
    - 판단은 `BlockValueSlot.CanAccept` (칸 규칙 `SlotRules`). 놓으면 표시를 지움
  - 미리보기는 데이터에 들어가지 않음. `BlockList.GetInsertIndex`는 미리보기를 세지 않고, 놓을 때는 미리보기를 지우기 전에 자리 번호를 셈 (미리보기에 보인 자리에 들어가게)
  - 예전의 보라색 줄(`InsertMarker`, #432)은 쓰지 않음 (프리팹 칸은 남겨 둠)
  - 놓인 블록을 끌면 블록 목록 위에 빨간 휴지통 「여기에 놓으면 블록을 지워요」
  - 놓을 자리: 끄는 동안의 미리보기와 같은 자리 (#474). 끄는 블록 자신과 그 안쪽(칸, 블록 영역)은 고르지 않음. 예전에는 놓는 순간 블록 자신이 걸려, 자기 블록 영역에 들어가 화면에 떠 있거나 지워졌음
- 컴파일(`FunctionChecker`): 반복문 밖의 break·continue, return이 없는 길, 비어 있는 값 칸, 지운 함수를 부르는 블록(#494, 「지운 함수를 부르고 있어요」. 실행기가 그 함수를 바꾸지 못해 슬롯 전체가 시전되지 않으므로). 오류가 있으면 오른쪽 오류 창으로 바뀌고, 비어 있는 값 칸과 반복문 밖의 break·continue 블록을 빨갛게 표시. 고치면 표시를 지운다
- 함수 틀 (#432): 편집 영역 위에 머리줄 「반환 타입 이름(파라미터) {」, 아래에 「}」 줄이 늘 있다 (`FunctionFrameView`). 편집 영역(`BlockList`)의 자식이 아니라 따로 둔 줄이다
  - 슬롯 함수: 반환 타입(void)·이름은 고정이라 누를 수 없고 「슬롯 함수 · 이름(슬롯 키)과 반환 타입은 고정」 안내. 페이지 함수: 반환 타입(void, int, float, bool, char, string)을 눌러 드롭다운으로 고른다
  - 파라미터는 편집할 수 없다. 이미 있는 파라미터는 머리줄에 보여 주기만 한다. 반환 타입을 바꿔서 틀린 곳(return이 없는 길 등)은 저장할 때 컴파일이 알려 준다. 이 함수를 부르는 다른 함수에 오류가 생기면 저장하지 않는다 (#494, 아래 「저장 버튼」)
  - 전역 변수를 고칠 때는 반환 타입 없이 「Global Variables」 이름과 안내만 보인다
- 저장하지 않은 변경 확인 (#432, #494): `BlockEditor.HasUnsavedChanges`(어느 페이지에든 저장하지 않은 변경이 있을 때: 지금 페이지를 열고 나서 고쳤거나, 「+」로 만든 새 페이지를 저장하지 않았거나, 다른 페이지에 작업본이 있을 때. 기본으로 열리는 빈 함수는 고치기 전까지 해당 없음)
  - 작업본 (#494): 다른 함수를 열어도(함수 목록의 줄·같은 줄 다시 누르기·`+`·`Global Variables`) 묻지 않고, 지금 페이지의 고친 내용을 작업본으로 둔다. 그 페이지를 다시 열면 작업본을 이어서 고친다
    - 이유: 지운 전역 변수를 쓰던 함수를 고치러 가려면 지운 것을 들고 옮겨야 하기 때문 (예: Global Variables에서 x를 지운 채로 슬롯 Q를 열어 고침). 예전(#432)에는 열기 전에 버릴지 물었음
    - 작업본이 있는 페이지는 함수 목록에서 이름 뒤 `*`, 상태 줄에는 지금 페이지만 「저장하지 않은 변경 있음」
    - 저장은 페이지마다 저장 버튼으로 한다. 저장하면 그 페이지의 작업본을 뺀다. 저장할 때의 검사는 「이 페이지의 새 내용 + 나머지는 저장된 상태」 (아래 「저장 버튼」의 「다른 함수 검사」). 그래서 x를 지울 때는 슬롯 Q를 먼저 고쳐 저장한 뒤 Global Variables를 저장한다
    - 변수 고르기와 칸 규칙은 전역 변수의 작업본이 아니라 저장된 전역 변수를 본다 (같은 이유)
    - 함수 이름이 겹치는지는 작업본의 이름까지 본다
  - 나가기(닫기 버튼, ESC)는 `BlockEditorPanel.Close`(`InputBlockingPanel.Close`를 virtual로 바꿈)가 묻는다: 「파일이 저장되지 않았습니다.(줄바꿈)저장하지 않고 나가시겠습니까?」 「나가기 (Enter)」 / 「취소 (Esc)」 (#494, `ConfirmDialog.Show`의 버튼 글자). 「나가기」이면 모든 페이지의 저장하지 않은 내용(작업본)을 버리고(`DiscardUnsaved`) 닫는다. 「취소」이면 편집 화면에 남는다
  - 서버에 저장을 요청하고 답을 기다리는 동안(`savePending`)은 열기·나가기를 막고 「저장하는 중입니다」만 보인다
  - 「+」로 만든 새 페이지는 저장하기 전에는 서버·파일에 없다. 다른 곳을 열면 작업본으로 남아 목록에 「저장 안 함」 줄로 보이고, 누르면 다시 열린다. 나가기에서 「나가기」를 누르면 사라진다
    - 실제 계정의 페이지 함수 수 제한(`ProgressRules.MaxPageFunctions`)에는 저장하지 않은 새 페이지도 센다
    - 작업본으로 두지 않은 새 함수(고치지 않은 기본 빈 함수)는 다른 곳을 열면 「저장 안 함」 줄을 지운다 (`RemoveStaleUnsavedEntry`)
  - 확인창 버튼 글자에 키(Enter, Esc)를 함께 적는다. 키 표시 부품(#430)이 생기면 배지로 바꾼다
- 페이지 함수 지우기 (#494): 함수 목록의 저장한 페이지 함수 줄 오른쪽 ×(`ListItemView`의 `Delete`, 그림 `ui_close`. Galmuri11에 `×` 글자가 없어서 제목줄 닫기 버튼과 같은 그림)
  - 슬롯 함수, Global Variables, 저장하지 않은 새 페이지 줄에는 ×가 없다. 임시 계정은 슬롯에 끼운 함수도 슬롯 함수라 지울 수 없다 (장착대에서 슬롯을 비운 뒤)
  - 저장된 다른 함수가 이 함수를 부르면 지우지 않는다: 상태 줄 「슬롯 Q에서 foo을(를) 불러서 지우지 않았습니다」, 오류 창에 「슬롯 Q」와 그 호출 블록의 C 글자, 「지운 함수를 부르고 있어요」 (저장할 때의 「다른 함수 검사」와 같은 `FunctionChecker.FindNewlyBroken`). 함수가 자기 자신을 부르는 재귀 호출은 세지 않는다
  - 부르는 함수가 없으면 확인창 「페이지 foo을(를) 지우시겠습니까?(줄바꿈)지운 함수는 되돌릴 수 없습니다.」 「지우기 (Enter)」 / 「취소 (Esc)」
  - 지우기는 바로 저장소에 반영된다 (저장 버튼과 상관없음): 실제 계정은 `FunctionExecutorNetwork.RequestDeleteFunction`(답을 기다리는 동안 「지우는 중...」, 열기·나가기 막음), 임시 계정은 `FunctionStorage.Delete`
  - 지운 뒤: 그 페이지의 작업본도 버리고, 지운 페이지를 열어 두었으면 실제 계정은 슬롯 Q, 임시 계정은 새 빈 함수를 연다. 상태 줄 「페이지 foo을(를) 지웠습니다」
- 저장 버튼도 먼저 컴파일한다 (#432). 오류가 있으면 서버·파일에 저장하지 않고 「컴파일 오류 n개가 있어 저장하지 않았습니다」를 빨갛게 알린다. 편집 중인 내용은 그대로 남는다
  - 전역 변수는 제외: 컴파일 대신 저장 검사(`CanUseGlobalVariables`)가 막는다
  - 다른 함수 검사 (#494): 저장된 상태에는 컴파일 오류가 없어야 한다. 그래서 이 저장 뒤의 함수·전역 변수로 저장된 다른 함수(슬롯 함수, 페이지 함수)를 다시 컴파일하고, 오류가 새로 생기면 저장하지 않는다 (`FunctionChecker.FindNewlyBroken`)
    - 예: 슬롯 Q가 쓰는 전역 변수 x를 지움, 페이지 함수의 반환 타입을 int에서 void로 바꿨는데 다른 함수가 그 값을 씀
    - 상태 줄 「슬롯 Q에 컴파일 오류가 생겨 저장하지 않았습니다」, 오류 창에 「슬롯 Q  x = 1;」과 오류 문장. 다른 함수의 블록은 편집 영역에 없으므로 「블록 보기」는 없음
    - 저장된 상태에서도 있던 오류는 세지 않는다. 이 저장과 상관없는 예전 오류 때문에 저장을 막지 않기 위함
    - 오류가 생기는 함수를 먼저 고쳐 저장하면 된다 (예: 슬롯 Q에서 x를 쓰는 블록을 지워 저장한 뒤 x를 지워 저장). 그 함수를 작업본에서 고쳐 두고 저장하지 않았으면 상태 줄에 「고친 함수를 먼저 저장해 주세요」를 덧붙인다 (#494)
    - 서버는 이 검사를 하지 않는다 (편집 화면만)
- 컴파일 오류가 있는 함수는 함수 목록에서 이름이 빨간 밑줄. 장착 화면에서는 「오류」로 흐리게 보이고 넣을 수 없다
  - 함수 목록의 밑줄은 작업본(지금 고치는 내용, 다른 페이지의 작업본, 전역 변수의 작업본)까지 반영해 고칠 때마다 다시 본다 (#494). 예: Global Variables에서 x를 지우면 저장하기 전에도 x를 쓰는 슬롯 Q에 밑줄. 지금 고치는 함수에 빈 값 칸이 있어도 밑줄이 생김
- 아직 없음: 키워드 굵은 글씨(굵은 Galmuri 글꼴 파일), 파라미터·반환 타입 고르기

### 전역 변수 (Global Variables)
- 함수 목록의 `Global Variables`를 누르면 전역 변수 선언(`GlobalVariablesData.Declarations`)을 같은 편집 영역에서 고친다. 함수 이름 칸은 끈다
- 불러오기: 실제 계정은 `ReceivedGlobals`의 복사본, 임시 계정은 `GlobalVariablesStorage.Load()`
- 저장: 실제 계정은 `RequestSaveGlobals`(결과 `PlayerDataRequestAnswered`의 `SaveGlobals`), 임시 계정은 내 캐릭터의 `FunctionExecutor.TrySaveGlobalVariables`(검사 → 저장 → 초기화). 검사에 걸리면 상태 줄에 빨갛게 알린다
- 선언 블록(`int x = 3;`)과 배열 만들기 블록(`int arr[5];`, #478)만 넣는다. 다른 블록이 있으면 저장 검사에서 거부된다 (function-executor.md 「전역 변수」)
  - 배열 만들기 블록은 함수 화면과 같이 `syntax.list` 해금 뒤에 블록 목록에 보인다
- 위쪽 정보 글자: 「전역 변수 n개   데이터 영역 m칸」. m은 `GlobalVariablesConverter.CountCells`(선언 1칸, 배열 만들기는 길이만큼, #478)

### 실제 계정 (서버 저장)
- 내 캐릭터(`NetworkClient.localPlayer`)의 `FunctionExecutorNetwork.UsesServerData`가 true면 서버 모드다. 편집 화면을 열 때마다(`BlockEditorPanel.OpenFor` → `BlockEditor.Refresh`) 다시 확인한다.
- 함수 목록: 슬롯 Q~F(`ReceivedSlots[i].Id`의 GUID인 함수, 없으면 키만 보이고 누를 수 없음), 그 뒤에 페이지 함수(나머지 `ReceivedFunctions`). 처음 열면 Q(slot1)를 연다.
- 슬롯 함수는 이름을 바꿀 수 없다 (이름 칸을 끈다). 페이지 함수는 `ProgressRules.MaxPageFunctions`개까지 더할 수 있다 (넘으면 「새 함수」·저장을 막고 안내).
- 저장: `RequestSaveFunction(Function)`을 부르고 「저장하는 중...」을 보인다. 결과는 `PlayerDataRequestAnswered`(저장됨, 개수 초과, 슬롯 함수 잠김, 데이터 오류)로 위쪽 줄에 보이고, 성공하면 `PlayerDataReceived`에서 목록을 다시 그린다.
- 함수 버튼을 누르면 서버가 보낸 함수의 복사본(JSON 왕복)을 연다. 저장하지 않은 고친 내용이 서버가 보낸 함수에 섞이지 않게 한다. 작업본이 있으면 작업본을 연다 (#494).
- 장착대(`SlotEquipPanel`)는 실제 계정이면 슬롯이 고정이므로 내 화면에서 숨긴다 (#470). 창을 열게 되더라도 slot1~slot8 이름과 안내만 보이고 장착 목록을 만들지 않는다.
- 장착 화면 배치: 위 슬롯 카드 8개(`SlotCardView`: 키 배지, 이름, 고른 칸은 금색 테두리), 아래 왼쪽 「내 함수」, 오른쪽 「C 스킬」(3열, 색 띠로 C 스킬·공통 이동 구분), 맨 아래 줄(고른 칸, 코드 영역, 안내, 「칸 비우기」)

### 저장·불러오기 (임시 계정, 이 컴퓨터 파일)
- 저장: `FunctionDataJson.ToJson`을 그대로 `Application.persistentDataPath/Functions/{GUID}.json`에 쓴다. 같은 GUID면 덮어씀
- 불러오기: 위쪽 줄의 함수 버튼을 누르면 파일에서 새로 읽어 연다. 작업본이 있으면 작업본을 연다 (#494). `function.Blocks`, `InnerBlocks`, `Slots[칸 번호].Block`을 따라가며 블록 UI를 만든다.
- 위쪽 줄 표시: 블록 수(`FunctionData.CountBlocks`), 코드 영역 크기(`CodeSizeCalculator.Calculate`, 저장한 함수 + 지금 함수)

### 블록 그림
- 분류 색은 `BlockLabels`, 모양은 「화면 배치」의 「블록 모양」·「블록 칠」 (#487)
- 분류 색: 시안 6의 색에서 채도·밝기는 두고 색상만 옮긴 값 (사용자가 고름). 블록에는 이 색을 바탕과 섞어 칠하고, 분류 머리글의 색 네모와 참고 카드의 분류 태그에는 그대로 씀

| 분류 | 블록 | 색 |
| --- | --- | --- |
| 제어 | if, else if, else, for, while, break, continue | `#DBF25A` |
| 변수 | 선언, 대입, 배열 | `#5EB7F2` |
| 값 | 연산, 비교, 논리, Not | `#79D36F` |
| 함수 | 함수 호출(C 스킬, 기본 함수, 사용자 함수), malloc, free, wait, return | `#9E7BFF` |

- 변수는 시안의 청록(`#5EF2D6`) 대신 하늘색: 청록은 값의 초록과 구분되지 않고, 끄는 동안 「끼울 수 있는 칸」 표시 색과도 같기 때문
- 예전의 메모리(malloc, free, `#F27FB4`)·기타(return, wait, `#8A84A0`) 분류는 없앰 (#489). malloc·free·wait는 함수 탭, return은 문법 탭 「함수」 분류

### 화면 구성 (테스트 씬 `Test_HW`)
- `BlockEditorCanvas` (Canvas Scaler 640×360, `BlockEditor`)
  - `Palette`: Image + Rect Mask 2D → `SearchField`, `Scroll`(Scroll Rect + `Scrollbar`) → `Viewport` → `Content`(Vertical Layout Group + Content Size Fitter). 참고 창과 같은 구조 (#476)
  - `ErrorView`, `ReferenceView`: 머리글 없음 (#489, `InvokeUIBuilder.HeaderlessRightPanel`)
  - `FunctionView` (#489): `Palette`와 같은 구조. `SearchField`, `Scroll` → `Viewport` → `Content`(구역 머리글과 `FunctionItem`이 쌓임). 처음엔 꺼 둠
  - `TabStrip`: `BlockTab`, `FunctionTab`, `ErrorTab`(오류 수 `Badge`), `ReferenceTab`
  - `Workspace`: Image(격자) + Scroll Rect → `Viewport`(Rect Mask 2D) → `Content`(Image 투명 + Vertical Layout Group + Content Size Fitter + Layout Element + `BlockList`), `VerticalScrollbar`, `HorizontalScrollbar` (#476)
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
| `FunctionStorage.Delete(guid)` | GUID | `bool` | 저장한 함수 파일을 지움 (임시 계정, #494). 지웠으면 true, 없으면 false. 슬롯에 끼운 함수인지, 다른 함수가 부르는지는 부르는 쪽이 먼저 봄 |

## 맞춘 결과
- 이우림과 정한 것 (#72)
  - 블록 UI가 블록 데이터(`BlockData`)를 직접 들고 있다. 저장할 때 따로 바꾸지 않고 `FunctionData`를 그대로 저장한다.
  - 조건문·for·while 안쪽 블록은 `InnerBlocks`, 값 칸에 들어가는 블록은 `SlotData.Block`에 넣는다.
  - 해금은 일단 보지 않는다. (#396에서 해금한 것만 보이게 바꿈, 위 「하는 일」)
- 이우림에게 알릴 것 (#77)
  - 실제 계정은 서버 DB, 임시 계정(서버 데이터를 쓰지 않는 캐릭터)은 이 컴퓨터 파일(`FunctionStorage`)에 저장한다.
  - 잘못 적은 값은 저장하지 않고 빈 칸으로 둔다. 빈 칸, 선언하지 않은 변수는 편집 화면에서 막지 않는다 (구조 검사에서).
