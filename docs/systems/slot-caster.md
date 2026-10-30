# SlotCaster

- 담당자: 박진우

## 하는 일
- 슬롯 키(Q W E R A S D F)로 함수를 시전하는 입력 흐름을 맡는다.
  - 단일: 슬롯을 누르면 주변 몬스터가 후보가 되고, 누른 채 ← →로 고르고, 떼면 시전을 요청한다.
  - 여러 명: 단일처럼 고르면서 Space로 찍고, 떼면 찍은 대상 모두로 시전을 요청한다 (#95).
  - 범위: 누르는 순간 `FunctionExecutor.Cast`로 시전하고, 주변 범위 안의 몬스터를 대상으로 알린다 (#95).
  - 대상 없음: 누르는 순간 `FunctionExecutor.Cast`로 시전한다.
- 지금 고른 대상과 찍은 대상을 알려 준다 (대상 표시 연출용).
- 하지 않는 일
  - 고른 대상을 함수의 Target 파라미터에 넣지 않는다. 넣는 방법이 정해지기 전까지 `CastRequested` 이벤트와 로그까지만 한다.
  - 슬롯에 함수를 장착하지 않는다. 「슬롯 8개에 함수 장착·해제하기」 이슈. 그 전까지는 Inspector의 `Slot Function Ids`에 고정 ID를 적는다(임시).
  - 관통(꼬챙이), 다단 히트(검 다중베기)의 맞는 방식: 입력은 단일과 같고, 맞는 방식은 각 스킬 이슈.
  - 쿨타임, 시전 가능 검사(메모리 등)는 하지 않는다. 「시전 가능한지 검사하기와 쿨타임」 이슈(이우림).

## 동작 규칙
- 입력 방식 (`CastMode`)
  - 스킬 표(c-skills.md)의 대상 지정 방식이 `Area`이면 범위.
  - 그 밖에는 함수의 `Parameters`로 정한다: `Target` 1개면 단일(관통·다단 히트 포함), `Target` list면 여러 명, 없으면 대상 없음 (predefined-function.md 「대상 지정 방식」).
- 여러 명 선택
  - 슬롯을 누른 채 Space(`PlayerInputReader.MarkPressed`)로 지금 고른 대상을 찍는다. 이미 찍은 대상을 다시 찍으면 뺀다.
  - 떼면 찍은 대상 모두로 시전을 요청한다 (찍은 순서). **아무도 찍지 않고 떼면 지금 고른 대상 한 명**으로 요청한다.
  - 찍을 수 있는 최대 인원 = 쓸 수 있는 소환검 개수. `SummonedSwords.ReadyCount`를 읽는다 (summoned-sword.md). 없으면 `TempSwordState.SummonedSwordCount`, 그것도 없으면 제한 없음.
  - 고르는 동안 Player 맵이 꺼져 있어 Space가 점프로 들어가지 않는다.
- 범위
  - 누르는 순간 시전하고, 플레이어 주변 `Area Radius`(스킬 표, 임시 4) 안의 살아 있는 몬스터를 대상으로 알린다. 0명이어도 시전한다.
- 후보: 플레이어 주변 `Target Range`(기본 8유닛) 안의 살아 있는 `Monster`. 왼쪽부터 오른쪽 순서로 정렬한다.
  - 처음 선택: 가장 가까운 몬스터
  - ← →: 한 명씩 넘긴다. 끝에서 넘기면 반대쪽 끝으로 돌아간다.
  - 고르는 사이 후보가 죽으면 빼고, 선택된 대상이 빠지면 같은 자리(다음 후보)를 고른다.
- 단일·여러 명은 떼는 순간 대상이 없으면(후보 0명) 시전하지 않는다. 헛손질은 없다 (검 던지기의 빗나감은 던지기 스킬이 확률로 정한다, physical-sword.md).
- 한 번에 한 슬롯만 고른다. 고르는 중에 다른 슬롯을 누르면 무시한다 (`PlayerInputReader`도 먼저 누른 슬롯 하나만 기록).
- 대상을 고르는 동안 Player 액션 맵을 비활성화한다 (`PlayerInputReader.SetPlayerMapEnabled(false)`). 이동·점프·대시·내려가기를 할 수 없고, ← →가 `Move` 대신 `TargetPrev`·`TargetNext`로 들어간다.
  - 고르기가 끝나면(떼서 시전, 경직·사망으로 취소, 컴포넌트가 꺼짐) 다시 활성화한다. **이 컴포넌트가 비활성화한 경우에만** 다시 활성화한다.
  - 대상이 없는 함수는 누르는 순간 시전하므로 비활성화하지 않는다.
  - 지금 Player 액션 맵을 비활성화하는 곳은 여기뿐이다. UI·공격 불가 지역 등 다른 이유가 생기면, 모든 이유가 풀렸을 때만 다시 활성화하도록 함께 정해야 한다 (player-input.md).
- 경직·사망 중(`PlayerHealth.CanAct`가 false)에는 시전하지 않고, 고르던 대상도 취소한다. `PlayerHealth`가 없는 플레이어는 검사하지 않는다.
- 내 캐릭터(`isLocalPlayer`)에서만 동작한다.

## 다른 시스템이 쓰는 것

플레이어 프리팹에 `SlotCaster`가 붙어 있다. `GetComponent<SlotCaster>()`로 가져온다.
- 같은 오브젝트에 `PlayerInputReader`, `FunctionExecutor`(+ `ProcessMemory`)가 있어야 한다.

### `SlotCaster`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `CastRequested` (event) | `int` 슬롯 번호(0~7), `PredefinedFunction` 함수, `IReadOnlyList<GameObject>` 대상 목록 | - | [로직] 시전을 요청했을 때 불린다. 단일은 1명, 여러 명은 찍은 대상(없으면 고른 1명), 범위는 범위 안 몬스터 모두, 대상 없음은 빈 목록. 범위·대상 없음은 이미 `Cast`된 상태다. 단일·여러 명은 아직 `Cast`되지 않았다. 대상을 Target 파라미터에 넣는 방법이 정해지면 이 이벤트를 받는 쪽(또는 `SlotCaster`)에서 `Cast`한다. 네트워크 동기화는 이 이벤트에서 시작하면 된다. |
| `TargetChanged` (event) | `GameObject` 대상 | - | [표시] 선택된 대상이 바뀌었을 때 불린다 (고르기 시작, ← →, 끝). 끝나면 `null`. 대상 표시 연출에 쓴다. |
| `MarkedChanged` (event) | `IReadOnlyList<GameObject>` 찍은 대상 | - | [표시] 여러 명 선택에서 찍은 대상이 바뀌었을 때 (찍기, 빼기, 끝). 끝나면 빈 목록. |
| `CurrentTarget` | - | `GameObject` | 지금 선택된 대상. 고르는 중이 아니면 `null` |
| `MarkedTargets` | - | `IReadOnlyList<GameObject>` | 여러 명 선택에서 지금까지 찍은 대상 (찍은 순서) |
| `SelectingSlot` | - | `int` | 지금 대상을 고르는 슬롯 번호(0~7). 고르는 중이 아니면 -1 |

### Inspector 값

| 값 | 설명 |
| --- | --- |
| `Slot Function Ids` | (임시) 슬롯 1~8의 사전 정의 함수 고정 ID. 장착 이슈에서 없앤다. Player_JW: Q `c.thrust`, W `test.function_a`, E `c.battlefield_sweep`, R `c.wide_slash` |
| `Target Range` | 대상 후보를 찾는 거리 (유닛, 기본 8) |
| `Target Layers` | 대상 후보 레이어 (Monster) |

### 테스트용 컴포넌트

| 이름 | 설명 |
| --- | --- |
| `SlotTargetMarker` | 고른 대상 위에 노란 V, 찍은 대상 위에 하늘색 V를 OnGUI로 그리는 임시 표시. 실제 대상 표시 연출(임혜원)이 생기면 바꾼다. |
| `SlotMultiAreaSimTest` | Test_JW의 MultiTest. Play → Host, Game 창을 한 번 클릭. 슬롯 3(E)=전장 휩쓸기, 슬롯 4(R)=검 크게베기로 두 명 찍기, 안 찍고 떼기, 다시 찍어 빼기, 소환검 1개 제한, 범위를 확인. 「[MultiTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SlotTargetInputSimTest` | Play → Host 2초 뒤 가짜 키 입력(Q, Q+→, Q+→, Q+←, 뗌)을 넣어 대상 넘기기를 확인한다. `TargetChanged` 5번이면 통과. 자동으로 돌기 때문에 평소에는 꺼 둔다. |

## 맞춘 결과
- 이우림과 정해야 할 것
  - ← → 대상 넘기기가 동작하지 않는 문제
    - 원인: Input System의 Shortcut Keys Consume Input(Complexity Consumption) 때문에 `Move`(1D Axis 조합, 복잡도 3)가 ← →를 먼저 가져가고, `TargetPrev`/`TargetNext`(단일 키, 복잡도 1) 신호가 지워진다. `TargetPrev`/`TargetNext`를 조합으로 바꿔 복잡도를 맞춰도, 먼저 처리된 쪽이 나머지를 지우므로 해결되지 않는다.
    - 해결(#59): 이우림이 `PlayerInputReader`에 액션 맵 활성화·비활성화 함수(`SetPlayerMapEnabled`, `SetAttackMapEnabled`)를 추가했다(#60). `SlotCaster`가 대상을 고르는 동안 Player 액션 맵을 비활성화한다. `SlotTargetInputSimTest`로 확인.
    - 처음 제안한 방법(`PlayerInputReader`에서 `move`만 끄고 켜기)은 쓰지 않았다. UI 등 다른 이유로 맵을 끄는 코드와 같은 상태를 따로 바꾸게 되기 때문이다.
  - 고른 대상을 함수의 Target 파라미터에 넣어 `Cast`하는 방법과, Target 값의 C# 타입 (지금은 `GameObject`로 넘긴다). 「함수 실행 중 대상 입력 기다리기」 이슈와 같은 방법을 쓴다.
  - function-executor.md에는 「Player 프리팹에 `FunctionExecutor`, `ProcessMemory`가 붙어 있다」고 되어 있지만 공용 `Player.prefab`에는 아직 없다. 지금은 `Player_JW`에만 붙여서 쓴다.
- 임혜원과 정해야 할 것
  - 대상 표시 연출을 `TargetChanged`, `MarkedChanged`로 붙이는지
