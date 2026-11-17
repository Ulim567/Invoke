# 설명 표 (블록·함수·type 설명)

- 담당자: 이우림 (스킬 24개의 칸: 박진우)

## 하는 일
- 블록·함수·type의 플레이어용 설명을 한 곳(`ItemDescriptions`)에 담는다. 참고 카드, 설명창, 스킬 트리 설명이 모두 이 표를 읽는다.
  - 문구를 고치고 Play하면 바로 화면에 보인다. 예전에는 `SkillTreeDefaults`의 문구를 `SkillTree.asset`에 다시 채워야 보였음
- 코드: `Assets/Scripts/Descriptions/`
  - `ItemDescription.cs`: 설명 하나의 칸
  - `ItemDescriptions.cs`: 문법 블록 20개, type 5개, 기본 함수(get_bug, is_valid_target), 옛 문법 노드 10개, 파라미터 이름의 뜻
  - `ItemDescriptions.Skills.cs`: 스킬 24개
- 하지 않는 일
  - 이름: 스킬은 `SkillTable`의 `DisplayName`(c-skills.md), 기본 함수의 한글 이름은 `BlockReference.KoreanNameOf`(block-editor.md)
  - 함수 원형: 사전 정의 함수의 파라미터와 반환 타입으로 `BlockReference`가 만듦
  - 화면 그리기: `BlockReference`(참고 카드·설명창), `SkillTreeView`(스킬 트리)
  - 해금 설명 카드 화면: 다음 이슈

## 칸
- 순서: 해금 설명 카드와 같음 (하는 일 → 용도 → 예시 → 슬롯 → 주의할 점)
- 원본: Notion 「블록 표」(11/15 확정)의 같은 칸. 이름만 다름: Notion 「쓰는 때」 → 용도, 「빈칸」 → 슬롯, 함수 카드에서는 파라미터 (11/17 이우림)

| 칸 | 이름 | 모양 | 예 (If) |
| --- | --- | --- | --- |
| `Summary` | 하는 일 | 한 문장 | condition이 true일 때만 body를 실행합니다. |
| `Purpose` | 용도 | 「~하고 싶을 때 씁니다.」 한 문장 | 조건에 따라 실행할지 말지를 정하고 싶을 때 씁니다. |
| `Example` | 예시 상황 | 「예를 들면,」으로 시작하는 한 문장 | 예를 들면, 고른 몬스터가 살아 있을 때만 검 찌르기를 쓰도록 만들 수 있습니다. |
| `ExampleCode` | 예시 코드 | 한 줄 C 코드 | `if (is_valid_target(get_bug())) { thrust(get_bug()); }` |
| `ExampleResult` | 예시 결과 | 게임에서 생기는 일 | 고른 몬스터가 살아 있으면 검 찌르기를 쓰고, ... |
| `Slots` | 슬롯 (함수 카드는 「파라미터」) | 줄마다 `이름 : 역할 \| 넣을 수 있는 것` | `condition : true면 body를 실행 \| bool` |
| `Caution` | 주의할 점 | 실수하기 쉬운 것, 쓸 수 없는 경우 | condition이 비어 있으면 compile error입니다. |
| `Format` | 형식 | 블록을 C로 적은 모양. 문법 블록·type만 | `if (condition) { body }` |
| `Returns` | return 값 | 값을 만드는 블록·함수만 | `bool`, 「만든 소환검의 포인터입니다.」 |

- 문장: 하십시오체. C 용어는 영어 그대로 (type, literal, variable, expression, return type, statement, body, slot)
- 비어 있는 칸: 화면에 보이지 않음
- 사전 정의 함수의 `Slots`가 비어 있을 때: 파라미터마다 이름의 뜻(`ParameterMeaningOf`: target, targets, sword)
- 예시(`Example`, `ExampleCode`)가 비어 있을 때: 참고 카드에 호출 한 줄(`thrust(get_bug());`). 대상 array를 받는 함수(battlefield_sweep)는 예시 없음
- type의 `Slots`: 그 type을 넣을 수 있는 빈칸 한 줄 (「넣을 수 있는 빈칸: ...」, 이름 없음)

## key

| 대상 | key | 예 |
| --- | --- | --- |
| 블록 | 블록·함수 개수의 항목 ID (`BlockUsageCounter.KeyOf`, function-data.md) | `If`, `ElseIf`, `Else`, `Declare`, `ArrayDeclare`, `ArrayGet`, `Wait`, `Malloc` |
| 사전 정의 함수 | 고정 ID | `c.thrust`, `input.get_bug` |
| type | `type.` + 이름 | `type.float`, `type.NULL` |
| 옛 문법 노드 | 노드 ID | `syntax.if` (스킬 트리를 하나로 합치면 지움) |

## 화면에서 보이는 곳
- 참고 카드(블록 편집 화면 참고 탭): 펼치면 하는 일 → 용도 → 예시(상황, 코드, 결과) → 슬롯(함수 카드는 「파라미터」) → return 값 → 주의할 점 → 실행(함수만)
- 설명창(블록·함수 탭 항목에 마우스 올리기): 문법 블록은 형식·하는 일, 사전 정의 함수는 원형·한글 이름·하는 일·파라미터·return 값
- 값 칸 설명창: 사전 정의 함수의 파라미터면 그 슬롯 줄의 「역할 | 넣을 수 있는 것」, 없으면 이름의 뜻
- 스킬 트리 설명 칸: 하는 일, 용도, 주의할 점 가운데 적힌 것을 이어서
- 스킬 트리 문법 탭의 노드 옆: 옛 문법 노드의 하는 일
- type 5개: 아직 보이는 곳 없음 (해금 설명 카드에서 씀)

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `ItemDescriptions.Get(string key)` | key (위 표) | `ItemDescription` | 없으면 `null` |
| `ItemDescriptions.Of(BlockData block)` | 블록 | `ItemDescription` | 블록의 key로 찾음. 내가 만든 함수를 부르는 블록은 `null` |
| `ItemDescriptions.ParameterMeaningOf(string name)` | 파라미터 이름 | `string` | 무엇을 넣는지. 없으면 빈 글자 |
| `ItemDescription.SlotItems()` | - | `List<(string Name, string Text)>` | 슬롯 줄을 (이름, 역할 \| 넣을 수 있는 것)으로 나눔. 이름 없는 줄은 이름이 빈 글자 |
| `ItemDescription.SlotTextOf(string name)` | 슬롯 이름 | `string` | 그 슬롯의 「역할 \| 넣을 수 있는 것」. 없으면 `null` |

- `ItemDescription`의 칸은 위 「칸」 표. 읽기만 한다

## 맞춘 결과
- 이우림이 정함 (11/17)
  - 설명의 원본: C# 표 하나 (asset·Notion이 아님)
  - 스킬 카드의 칸: 블록과 같은 다섯 칸 (캔버스 9-3의 이름·원형·사용 방법은 코드가 채움)
  - 블록 20개·type 5개의 문구: Notion 「블록 표」 확정본
- 박진우와 맞출 것
  - 스킬 24개(`ItemDescriptions.Skills.cs`)의 용도, 예시, 주의할 점, 하는 일 확인
  - 각 스킬의 「예전 자세한 설명」 주석: 하는 일·용도·주의할 점으로 나누어 옮긴 뒤 지움
