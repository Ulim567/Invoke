# 소환검 (SummonedSwords)

- 담당자: 박진우

## 하는 일
- 플레이어가 malloc으로 만든 소환검(최대 5개)의 목록, 포인터, 상태를 관리한다.
- 소환검 오브젝트(소환검 프리팹)를 만들고 없앤다.
- C 스킬(소환검 필요 검사), 슬롯(여러 명 선택 최대 인원), HUD가 소환검 개수와 상태를 읽을 수 있게 한다.
- 하지 않는 일
  - heap에 올리고 내리기(malloc, free): 검 소환·해제 스킬이 `PredefinedFunction.Malloc` / `Free`로 한다 (c-skills.md). 이 컴포넌트는 오브젝트와 목록만 맡는다.
  - 소환검으로 때리는 스킬의 데미지: 각 스킬 이슈 (`Hit` 이벤트를 받아서 처리)
  - 실제 HUD 그림: 임혜원 (지금은 OnGUI 임시 표시)
  - 네트워크 동기화: 「소환검·검기를 서버에서 생성하고 동기화하기」 이슈

## 규칙
- 포인터
  - 소환검 하나 = heap 객체 하나. 포인터는 이우림 heap(`ProcessMemory.Heap`)의 포인터를 그대로 쓴다.
  - 검 소환 스킬 순서: `SpawnSword`(오브젝트) → `PredefinedFunction.Malloc`(heap) → `Register(sword, pointer)`(목록). malloc 실패면 `DespawnSword`.
  - 해제 스킬 순서: `PredefinedFunction.Free`(heap) → `DespawnSword`(오브젝트, 목록에서 뺌).
  - 포인터는 소환한 순서대로 커진다. 목록(`Swords`)도 소환 순서.
- 최대 5개
  - 누수된 소환검도 개수에 들어간다 (`HeapStorage.CountObjects`). 누수가 쌓이면 새로 소환할 수 있는 수가 줄어든다.
  - 5개거나 heap이 모자라면 검 소환 스킬이 만들지 않는다 (C의 malloc처럼 에러로 끝내지 않음).
- 상태
  - 사용 가능(`Ready`): 플레이어 머리 뒤쪽에 줄 서서 따라다닌다 (`Follow Offset`, `Follow Spacing`).
  - 던져짐(`Flying`): 대상을 향해 날아가 닿으면 `Hit`을 알리고 그 자리에서 떨어진다. 대상이 없거나 `Max Flight Time` 안에 못 닿으면 바로 떨어진다.
  - 떨어짐(`Dropped`): 땅(Ground, Platform) 윗면에 멈춘다. 회수해야 다시 쓴다.
  - 누수(`Leaked`): 포인터를 잃은 소환검. 그 자리에 멈추고 반투명해진다(`SummonedSwordView`). 회수·해제할 수 없고 개수만 차지한다.
  - 상태는 `SummonedSwords`만 바꾼다. `SummonedSword`는 움직임만 맡고, 땅에 닿으면 `SummonedSwords`에 알린다(`NotifyLanded`).
- 회수
  - 검 돌아오기(`Recall`): 거리와 상관없이 바로 사용 가능으로.
  - 줍기(`TryPickUpNearest`): 떨어진 소환검과 `Pick Up Range`(1.5) 안. Z 키는 물리 검을 먼저 줍고, 못 주우면 소환검을 줍는다 (`SwordPickUpInput`).
  - 해제 후 다시 소환.
- 해제(free)
  - 해제 스킬이 마지막에 소환한 소환검부터 heap 칸을 돌려주고 오브젝트를 없앤다. 던진 검도 거리와 상관없이 해제된다.
  - 해제한 포인터는 heap에서 찾을 수 없다. 해제한 포인터로 명령하면 스킬이 `TryGetHeapObject` / `Free`로 쓸 때 `InvalidPointerError`로 그 작업만 끝난다. 추가 페널티는 없다 (기획 결정 후 바뀔 수 있음).
- 누수: 작업이 실패·중단으로 끝나면 그 작업이 malloc한 소환검은 heap이 누수로 표시한다 (`ObjectLeaked` → `Leaked`). 영역 비우기(`Heap.Clear`, 재부팅 등)로 heap이 비면 오브젝트도 없앤다 (`ObjectCleared`).
- 슬롯에서 바로 쓰는 스킬은 가장 먼저 소환한(포인터가 작은) 사용 가능한 검부터 쓴다 (`TryGetFirstReady`, C 배열처럼 앞에서부터).
- 소환검 필요 스킬(`needsSummonedSword`)은 사용 가능한 소환검이 0개면 `NoSummonedSwordError` (c-skills.md). 여러 명 선택 최대 인원 = 사용 가능한 소환검 수 (slot-caster.md).
- 소환검 오브젝트는 `SpawnSword` / `DespawnSword`로만 만들고 없앤다 (CLAUDE.md 코드 작성 규칙 3). 캐릭터가 사라지면 같이 없앤다.

### 지금 값 (Player_JW)

| 항목 | 값 |
| --- | --- |
| Sword Prefab | `Assets/_JW/SummonedSword.prefab` (임시 그림: 하늘색 막대) |
| Ground Layers | Ground, Platform |
| Follow Offset | (0.6, 3.2) |
| Follow Spacing | 0.45 |
| Pick Up Range | 1.5 |
| 소환검 Throw Speed / Max Flight Time | 22 / 1.2초 (프리팹) |

## 다른 시스템이 쓰는 것

플레이어 프리팹에 `SummonedSwords`가 붙어 있다 (`ProcessMemory` 필요). `GetComponent<SummonedSwords>()`로 가져온다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `MaxCount` (const) | - | `int` | 5 |
| `Swords` | - | `IReadOnlyList<SummonedSword>` | 소환 순서 목록 (누수 포함). HUD가 읽는다 |
| `Count` | - | `int` | heap에 있는 소환검 수 (누수 포함) |
| `CanSummon` | - | `bool` | 5개 미만이면 `true` |
| `ReadyCount` | - | `int` | 사용 가능한 소환검 수 |
| `SpawnSword()` | - | `SummonedSword` | 소환검 오브젝트를 만든다 (목록·포인터 없음). 검 소환 스킬이 Malloc 전에 부른다 |
| `Register(SummonedSword, int pointer)` | 소환검, 포인터 | - | malloc에 성공한 소환검을 목록에 올린다 |
| `DespawnSword(SummonedSword)` | 소환검 | - | 오브젝트를 없애고 목록에서 뺀다. heap은 건드리지 않음 (해제 스킬이 Free 다음에, 검 소환 스킬이 malloc 실패 때 부른다) |
| `TryGetSword(int pointer, out SummonedSword)` | 포인터 | `bool` | 포인터로 찾기. 해제·누수면 `false` |
| `TryGetFirstReady(out SummonedSword)` | - | `bool` | 가장 먼저 소환한 사용 가능한 검 |
| `Throw(SummonedSword, GameObject target)` | 검, 대상 (없으면 빗나감) | `bool` | 사용 가능한 검만 던진다 |
| `Recall(SummonedSword)` / `RecallFirstThrown()` | 검 | `bool` | 던진 검을 되돌린다 |
| `TryPickUpNearest()` | - | `bool` | 근처의 떨어진 소환검을 줍는다 |
| `Changed` (event) | - | - | [표시] 목록·상태가 바뀔 때 |
| `Hit` (event) | `SummonedSword`, `GameObject` | - | [로직] 던진 소환검이 대상에 닿았을 때 |

`SummonedSword`(소환검 하나): `Pointer`, `State`(`Ready` / `Flying` / `Dropped` / `Leaked`), `StateChanged`(event, [표시]).

### 임시 표시
- `SummonedSwordView`(소환검 프리팹): `StateChanged`를 받아서 누수면 반투명(`Leaked Alpha` 0.35)으로 바꾼다.
- `SummonedSwordHud`: 내 캐릭터 화면 왼쪽 위에 「소환검 n/5 (사용 가능 m)」과 포인터별 상태를 그린다. 실제 HUD는 임혜원 이슈에서 `Swords`와 `Changed`를 읽어 바꾼다.

### 테스트
| 이름 | 설명 |
| --- | --- |
| `SummonedSwordSimTest` | Test_JW의 SummonTest. 켜고 Play → Host, Game 창을 한 번 클릭 (Unity가 화면 맨 앞에 있어야 가짜 키가 들어감). 실제 스킬(검 소환·해제)로 확인: 검 소환 5번(6번째 안 됨), 포인터 순서, 첫 번째 사용 가능한 검, 던지기·돌아오기, 줍기(가짜 Z 키), 해제(마지막 소환 검, heap 감소), 떨어진 검 해제, 검 소환 도중 실행 중단 → 누수(반투명, 개수에 남음, 해제 대상 아님), 소환검 없을 때 소환검 찌르기 실패, 해제할 검 없으면 `NoSummonedSwordError`, 영역 비우기. 「[SummonTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

## 맞춘 결과
- 박진우 결정
  - 해제한 포인터로 명령하면 `InvalidPointerError`로 그 작업만 끝남. 추가 페널티는 기획 결정 후.
  - 슬롯에서 바로 쓸 때는 가장 먼저 소환한 사용 가능한 검부터.
- 이우림과 맞출 것 (#102)
  - 검 소환·해제는 function-executor.md·predefined-function.md 대로 `PredefinedFunction.Malloc` / `Free`로 heap을 쓰고, 소환검 오브젝트는 `SummonedSwords`가 만들고 없앤다
- 박진우 결정 (#102)
  - 해제는 마지막에 소환한 소환검부터
  - 검 소환은 시작할 때 malloc (도중에 끊기면 누수)
- 정해야 할 것
  - 소환검 그림, 누수 연출, HUD: 임혜원
  - 소환검 heap 크기, 날아가는 속도: 기획
