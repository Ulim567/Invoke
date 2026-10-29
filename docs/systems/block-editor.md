# BlockEditor

- 담당자: 임혜원

## 하는 일
- 블럭코딩 편집 화면. 블록 목록(팔레트)에서 블록을 끌어 편집 영역에 놓고, 위아래로 이어 붙이고, 조건문·for·while 안에 넣는다.
- 편집 중인 함수는 `FunctionData` 하나다. 블록 UI(`BlockView`)가 `BlockData`를 직접 들고 있어서, 화면에서 블록을 옮기면 함수 데이터가 바로 바뀐다. 데이터 형식은 function-data.md를 따른다.
- 하지 않는 일
  - 값 칸 채우기(조건, 숫자, 변수 고르기), 함수 저장·불러오기는 아직 없다. 「블럭코딩 화면 2」에서 만든다.
  - 해금은 아직 보지 않는다. 블록을 모두 보여 준다. 스킬 트리가 생기면 해금된 블록만 보여 준다.
  - 함수 실행은 하지 않는다. `FunctionExecutor`(이우림)가 한다.

## 동작 규칙
- 스크립트: `Assets/_HW/Scripts/BlockCoding`

| 스크립트 | 하는 일 |
| --- | --- |
| `BlockEditor` | 편집 화면 전체. 편집 중인 함수를 들고, 블록 목록에 견본 블록을 만든다 |
| `BlockView` | 블록 하나. 끌어서 옮기기 |
| `BlockList` | 블록을 쌓는 자리. 함수 몸통(`FunctionData.Blocks`)과 블록 영역(`BlockData.InnerBlocks`)이 각각 하나다. 자식 순서 = 데이터 순서 |
| `BlockLabels` | 블록에 보여 줄 글자(C 모양)와 분류별 색 |

- 블록 목록의 블록은 견본이다. 끌면 견본은 그대로 두고 새 블록을 만든다.
- 편집 영역의 블록을 블록 목록이나 빈 곳에 놓으면 지워진다.
- else if, else는 if 또는 else if 바로 아래에만 놓을 수 있다. 다른 곳에 놓으면 원래 자리로 돌아간다 (새로 꺼낸 블록이면 지워진다).
- 놓을 자리는 마우스 아래에서 가장 안쪽의 `BlockList`다. 블록 가운데보다 위에 놓으면 그 블록 앞에 들어간다.
- 블록 그림은 임시로 색 네모와 글자(Galmuri11)다.

| 분류 | 블록 | 색 |
| --- | --- | --- |
| 제어 | if, else if, else, for, while, break, continue | `#B8742F` |
| 변수 | 선언, 대입, 배열 | `#2F8F86` |
| 함수 | C 스킬, 사용자 함수 | `#6A5AC9` |
| 메모리 | malloc, free | `#C24D86` |
| 기타 | return, 기다리기 | `#4A5578` |

- 화면 구성 (테스트 씬 `Test_HW`)
  - `BlockEditorCanvas` (Canvas Scaler 640×360, `BlockEditor`)
    - `Palette`: Image + Vertical Layout Group
    - `Workspace`: Image + Vertical Layout Group + `BlockList`
    - `DragLayer`: 화면 전체, Canvas 맨 아래 자식
  - 블록 프리팹 `Assets/_HW/UI/BlockCoding/Block.prefab`: Image + Vertical Layout Group + Canvas Group + `BlockView`, 자식 `Label`(TMP), `Inner`(Image + Vertical Layout Group + Layout Element + `BlockList`)
- 확인: Play 중 `BlockEditor` 컴포넌트 ⋮ 메뉴 → 「지금 함수를 JSON으로 보기」로 Console에 함수 데이터를 찍는다.

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `BlockEditor.Function` | - | `FunctionData` | 지금 편집 중인 함수. 읽기만 한다 |
| `BlockEditor.Open(function)` | `FunctionData` | - | 함수 하나를 편집 영역에 연다. 블록이 이미 있으면 그대로 화면에 만든다 |

## 맞춘 결과
- 이우림과 정한 것 (#72)
  - 블록 UI가 블록 데이터(`BlockData`)를 직접 들고 있다. 저장할 때 따로 바꾸지 않고 `FunctionData`를 그대로 저장한다.
  - 조건문·for·while 안쪽 블록은 `InnerBlocks`, 값 칸에 들어가는 블록은 `SlotData.Block`에 넣는다.
  - 해금은 일단 보지 않는다.
