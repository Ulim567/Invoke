# 설명 카드 (해금 카드, 블록·함수·type 설명 카드)

- 담당자: 임혜원

## 하는 일
- 블록·함수·type의 설명을 카드 한 가지 모양으로 보여 준다 (#544, 노션 11/16 「7. 해금 요소」, 캔버스 9-3)
- 네 곳에서 같은 카드
  - 해금 직후: 스킬 트리에서 해금하면 가운데에 「> 스킬 해금!」 / 「> 문법 해금!  if문」 카드 (`ItemCardPopup`)
  - 스킬 트리 노드 클릭: 설명 칸의 상태 줄 아래 (`SkillTreeView`)
  - 블록 목록의 칩, 함수 탭의 항목을 끌지 않고 누르기: 가운데에 「> 블록 설명」 / 「> 함수 설명」 카드 (`BlockEditor.ShowItemCard`)
  - 블록 편집 화면 참고 탭: 카드 목록 (예전 참고 카드 대신). 카드가 수십 장이라 처음에는 「+ If  [제어 > 조건]」 이름 한 줄로 접어 두고(`Collapsible`, 여백·이름 글자도 작게), 누르면 그 카드만 첫 화면으로 펼치고 「-」로 바뀜. 다시 누르면 접고, 다른 카드를 펼치면 접힘 (사용자 요청 11/18: 스크롤로 이름을 훑어 찾기 쉽게). 참고 탭 카드는 오른쪽을 조금 비워 스크롤 막대에 붙지 않게. 목록 맨 위에 흐린 안내 「이름을 누르면 자세히 볼 수 있어요」
- 하지 않는 일
  - 카드의 글: 설명 표 `ItemDescriptions` (item-descriptions.md, 이우림·박진우). 카드 한 장의 내용은 `BlockReference`가 설명 표에서 만든 `BlockReferenceEntry`
  - 해금 자체: 스킬 트리·진행도 (skill-tree.md, progress-store.md)

## 동작 규칙
- 코드: `Assets/_HW/Scripts/UI/ItemCardView.cs`(카드), `ItemCardPopup.cs`(가운데 창), `BlockCoding/BlockReference.cs`(카드 내용)
- 프리팹: 메뉴 Invoke → UI → 설명 카드 만들기 (`ItemCardUIBuilder`)가 `Assets/_HW/Resources/UI/ItemCard.prefab`, `ItemCardPopup.prefab`과 그림(`Assets/_HW/UI/ItemCard/ic_*.png`)을 만든다. 게임 중에 `Resources.Load`로 꺼내므로 씬·다른 프리팹에 연결하지 않는다
- 카드 첫 화면 (캔버스 9-3)

| 줄 | 내용 |
| --- | --- |
| 머리줄 | 팝업에서만. 「> 스킬 해금!」 (주황), Esc 표시, x |
| 이름 | 한글 이름이 있으면 한글 이름, 그 아래 줄에 영문 이름 (꼬챙이 / skewer). 없으면 블록 이름 (If). 오른쪽 끝에 분류 태그 (분류 색, type은 주황). 영문 이름을 옆에 두면 좁은 곳에서 겹쳐서 아래 줄로 |
| 기능 | 하는 일 한 문장 (`Summary`) |
| 함수 원형 / 형식 | 함수(사전 정의 함수, malloc·free·wait)는 「함수 원형」, 문법 블록은 「형식」. 코드 색 (`BlockReference.ColorCode`). type은 없음 |
| 블록 사용 예 | 그림이 없는 카드(자세 규칙, #545)는 줄을 숨김. 블록 그림. 대상(target)을 받는 칸에는 `get_bug()`를 끼운 모양 (`skewer(get_bug());`). type은 「값 예」에 값 알약 (`0.5`) |
| 사용 방법 | 「[함수 탭] 에서 코드로 끌어다 놓기」, 문법 블록은 「[블록 탭] …」, type은 선언 블록에서 고르거나 값 칸에 적기 |
| 자세히 > | 누르면 용도, 예시, 슬롯·파라미터, return 값, 주의할 점, 실행 (참고 카드와 같은 순서·색). 다시 누르면 접기. 카드가 늘어선 곳(참고 탭)에서는 하나만 펼침 |
| < 1 / 3 > | 카드가 여러 장일 때만 (문법 노드가 여는 블록들) |

- 블록 그림: 편집 화면의 블록(`BlockView`)은 편집 화면 없이 만들 수 없어서(로비 스킬 트리), 같은 색 규칙(분류 색을 바탕에 연하게 섞음)으로 그림만 따로 그린다. 글자는 블록 목록과 같은 한 줄 글자(`BlockLabels.TextOf`). 문장 블록은 한 줄, 값 블록은 알약, ㄷ자 블록(if, for, while)은 머리줄 + 안쪽 자리(body) + 아래 막대
- 크기: 카드는 640×360 기준 캔버스 단위로 만들고, 놓인 캔버스의 기준 가로(로비 창 1920, 편집 화면 960)에 맞춰 키운다. 그래서 글자 크기가 어디서나 팝업과 같다. 설명 칸·참고 탭(`Compact`)은 0.85배(프리팹의 Compact Scale. 0 이하로 저장되어 있으면 코드가 0.85를 씀, #545: 0이면 카드 배율 0·가로 무한대로 「Invalid AABB」가 나고 카드가 보이지 않았음).
- 글자 크기: 게임 글꼴(Galmuri11)은 12픽셀 픽셀 글꼴이라 화면에서 12픽셀의 정수배가 아니면 1픽셀 획(i, l, 받침)이 빠져 보인다. 카드는 놓인 곳마다 배율이 달라서, 화면 픽셀로 가장 가까운 12의 배수가 되게 글자마다 크기를 고친다 (프리팹 크기 기준, 최소 12픽셀) 가로는 놓인 자리에 맞춰 글을 다시 줄바꿈하고, 높이를 Layout Element로 알려 위아래 칸이 밀린다
- 팝업 (`ItemCardPopup`)
  - 화면을 어둡게 덮고 가운데에 가로 300 카드. 카드가 330보다 길면(펼침) 그 안에서 세로 스크롤
  - 닫기: ESC, x, 카드 밖 누르기. ESC는 맨 위 창만 받으므로(UIShortcut) 아래의 스킬 트리·편집 화면은 닫히지 않는다
  - 캔버스 Sorting Order 30: 편집 화면·로비 창(10) 위, 설정(40)·로딩(100) 아래
- 해금 직후 카드에 들어가는 것 (`BlockReference.EntriesUnlockedBy`)
  - 스킬 노드: 그 스킬. 검 소환처럼 블록도 여는 노드는 그 블록(malloc, free)도
  - 문법 노드: 여는 블록 종류마다 (조건문은 If, Else If, Else 셋). 함수 블록 종류는 스킬마다 따로 열려서 빼고 return만
  - 자세 규칙 노드 (#545, 이우림): 규칙 카드 한 장. 이름은 규칙 이름, 분류 태그 「자세 규칙」, 기능 칸에 「자세 숫자가 홀수일 때 아래 효과가 붙습니다.」, 「효과:」, 효과 목록(「- 찌르기 계열 데미지 +15%」). 좋은 효과는 청록 `#5EF2D6`, 나쁜 효과는 빨강 `#E07070`(이우림 결정 11/18). 색 태그를 쓰므로 `BlockReferenceEntry.RichSummary`가 true이고, 카드는 「기능」 줄 이름을 숨긴다. 글은 설명 표가 아니라 자세 규칙 표(`StanceRule`, stance.md)에서 읽는다(효과 계산과 같은 표). 블록이 아니라서 형식·블록 사용 예·사용 방법 줄이 없다. 머리줄 「> 자세 규칙 해금!」
    - 스킬 트리 설명 칸에서는 해금 전이면 이름·기능이 「???」인 카드 (`EntriesUnlockedBy(node, revealed)`의 `revealed`가 false)
  - 내 캐릭터 언어에 없는 블록은 빠짐 (Python의 선언 등). 카드가 없으면 띄우지 않음
  - 이 창에서 내가 해금을 요청한 노드의 답(`UnlockAnswered`, Unlocked)에만 띄움
- type 카드: 설명 표의 type 다섯 개(float, char, bool, string, NULL). C 캐릭터의 참고 탭 맨 뒤 「type」 분류. 해금 노드가 생기면 해금 직후 카드도 같은 카드를 씀

## 다른 시스템이 쓰는 것
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `ItemCardPopup.Show(IReadOnlyList<BlockReferenceEntry> entries, string header)` | 카드들, 머리줄 글 | - | 가운데에 카드를 띄운다. 여러 장이면 넘겨 본다. 비어 있으면 아무것도 안 함 |
| `ItemCardPopup.Show(BlockReferenceEntry entry, string header)` | 카드 한 장, 머리줄 글 | - | 위와 같음 |
| `ItemCardPopup.Hide()`, `ItemCardPopup.IsOpen` | - | - / `bool` | 닫기, 떠 있는지 |
| `ItemCardView.Create(Transform parent)` | 놓을 곳 (Vertical Layout Group 안) | `ItemCardView` | 카드 하나를 만든다. 프리팹이 없으면 null |
| `ItemCardView.Show(IReadOnlyList<BlockReferenceEntry>)`, `Show(BlockReferenceEntry)` | 카드들 | - | 첫 장부터 접힌 채로 |
| `ItemCardView.SetHeader(string text, Action onClose)` | 머리줄 글(비우면 숨김), 닫기(null이면 x 숨김) | - | |
| `ItemCardView.Compact` | `bool` | - | 다른 글과 나란히 놓일 때 true (0.85배) |
| `ItemCardView.Collapsible` | `bool` | - | true면 이름 한 줄로 접고, 카드를 누르면 펼침 (참고 탭) |
| `BlockReference.EntryOf(string key)`, `EntryOf(BlockData block)` | key(`If`, `c.thrust`, `type.float`) 또는 블록 | `BlockReferenceEntry` | 카드 한 장. 없으면 null (내가 만든 함수 등) |
| `BlockReference.EntriesUnlockedBy(SkillTreeNode node, bool revealed = true)` | 스킬 트리 노드, 규칙 내용을 보일지 | `List<BlockReferenceEntry>` | 그 노드를 해금하면 쓸 수 있는 것들의 카드. 자세 규칙 노드는 규칙 카드 한 장, `revealed`가 false면 「???」 카드 (#545) |
| `BlockReferenceEntry.Key`, `Picture`, `TypeLiteral`, `UsageTab`, `UsageText` | - | - | (#544에서 더함) key, 그림으로 그릴 블록, type 값 모양, 사용 방법 |

## 맞춘 결과
- 노션 11/16 회의: 스킬 해금 카드 항목(이름, 기능, 함수 원형, 블록 사용 예, 사용 방법), 닫기 Esc 또는 x, 네 곳에서 같은 모양, 첫 화면은 하는 일 + 블록 그림이고 나머지는 「자세히」
- 카드의 글: 박진우(스킬), 이우림(블록 표) — 설명 표 `ItemDescriptions`
