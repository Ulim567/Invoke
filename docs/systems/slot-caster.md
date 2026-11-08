# SlotCaster

- 담당자: 박진우

## 하는 일
- 슬롯 키(Q W E R A S D F)로 함수를 시전하는 입력 흐름을 맡는다.
  - 단일: 슬롯을 누르면 주변 몬스터가 후보가 되고, 누른 채 ← →로 고르고, 떼면 고른 몬스터를 대상으로 시전을 부탁한다 (`FunctionExecutorNetwork.RequestCast(슬롯, 몬스터)`, #212).
  - 여러 명: 단일처럼 고르면서 Space로 찍고, 떼면 찍은 대상 모두로 시전을 요청한다 (#95).
  - 범위: 누르는 순간 시전을 부탁하고, 주변 범위 안의 몬스터를 대상으로 알린다 (#95).
  - 대상 없음: 누르는 순간 시전을 부탁한다.
- 시전은 이 컴퓨터의 실행기를 직접 부르지 않고 `FunctionExecutorNetwork`로 서버에 부탁한다 (#212, network.md). 함수는 서버의 실행기에서만 돈다.
  - 시전: `RequestCast(슬롯, 대상)` → 답 `CastAccepted(슬롯, 작업 번호)` / `CastRejected(슬롯, 이유)`. 시전 로그, `CastRequested`, 취소용 기록은 `CastAccepted`를 받은 뒤에 한다. 거부되면 로그만(급소공략 미니게임 결과도 지움).
  - 작업은 작업 번호로 기억하고, 끝났는지는 `TaskEnded`로 안다.
  - 대상 고르기(← →, Space 찍기, 급소공략 미니게임)는 그대로 내 화면에서 한다.
  - 블록코딩 함수: 누르는 순간 시전한다. 함수 안의 `GetBug`가 대상을 기다리면 그때 고른다 (#159).
- 슬롯 8개에 무엇을 넣었는지 가진다 (`SlotLoadout`, #159). 장착 화면(임혜원)이 이 API를 부른다.
- 취소 키(C)로 가장 최근에 슬롯으로 시전한 작업을 끊는다 (#159).
- 지금 고른 대상과 찍은 대상을 알려 준다 (대상 표시 연출용).
- 하지 않는 일
  - 장착 화면(UI): 임혜원. 이 문서의 `SlotLoadout` API를 부른다.
  - 코드 영역 크기 한도 검사: 크기만 알려 주고(`SlotLoadout.CodeSize`) 막지 않는다. 한도는 메모리 작업에서 정한다.
  - 관통(꼬챙이), 다단 히트(검 다중베기)의 맞는 방식: 입력은 단일과 같고, 맞는 방식은 각 스킬 이슈.
  - 쿨타임, 시전 가능 검사(메모리 등)는 하지 않는다. 「시전 가능한지 검사하기와 쿨타임」 이슈(이우림).

## 동작 규칙
- 시전이 거부되면(쿨타임, 메모리 부족 등, `Cast`가 `null`) 시전 로그와 `CastRequested`를 내지 않는다. 거부 이유는 실행기가 `CastRejected`로 알린다 (#152).
- 대상 찾기(가까운 몬스터, 범위 안 몬스터)는 플레이어가 속한 장소 씬의 물리 세계(`gameObject.scene.GetPhysicsScene2D().OverlapCircle`)에 묻는다. `Physics2D`로 바로 찾으면 서버에서 장소 씬의 몬스터를 못 찾는다 (place-scenes.md, #125).
- 입력 방식 (`CastMode`)
  - 스킬 표(c-skills.md)의 대상 지정 방식이 `Area`이면 범위.
  - 그 밖에는 함수의 `Parameters`로 정한다: `Target` 1개면 단일(관통·다단 히트 포함), `Target` array면 여러 명, 없으면 대상 없음 (predefined-function.md 「대상 지정 방식」).
- 여러 명 선택
  - 슬롯을 누른 채 Space(`PlayerInputReader.MarkPressed`)로 지금 고른 대상을 찍는다. 이미 찍은 대상을 다시 찍으면 뺀다.
  - 떼면 찍은 대상 모두로 시전을 요청한다 (찍은 순서). **아무도 찍지 않고 떼면 지금 고른 대상 한 명**으로 요청한다.
  - 서버에는 `FunctionExecutorNetwork.RequestCast(슬롯, 대상 배열)`로 보낸다 (#238, #243). 서버가 `RuntimeValue.FromBugs`로 바꿔 스킬의 `targets`에 넣는다. 로그와 `CastRequested`는 다른 시전처럼 `CastAccepted`를 받은 뒤에 한다.
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

### 장착 (`SlotLoadout`, #159)
- 슬롯 하나에는 비어 있음 / 블록코딩 함수(GUID) / C 캐릭터 스킬(고정 ID) 중 하나가 들어간다.
- 넣을 수 있는 스킬: `PredefinedFunctionRegistry`에 등록된 사전 정의 함수. 입력 함수(`input.*`)는 넣을 수 없다. 해금 여부는 아직 보지 않는다 (해금 저장이 생기면 해금한 것만).
- 넣을 수 있는 블록 함수: `FunctionStorage`에 저장된 함수.
- 같은 함수·스킬은 한 슬롯에만. 다른 슬롯에 넣으면 원래 슬롯은 빈다(옮겨짐).
- 시전할 원본: 같은 슬롯은 같은 원본 객체를 계속 쓴다. 그래야 쿨타임, 실행 중 검사, 캔슬이 이어진다 (function-executor.md). 블록 함수는 장착할 때(또는 `ReloadFunctions`) `FunctionDataConverter`로 한 번 바꿔 둔다. 실행할 수 없는 함수면 변환기가 콘솔 오류를 내고 그 슬롯은 눌러도 시전되지 않는다.
- 저장: 이 컴퓨터 파일 `persistentDataPath/SlotLoadout.json`. 장착을 바꿀 때마다 저장하고, 켤 때 불러온다. 파일이 없으면(처음) Inspector의 `Default Skill Ids`로 채운다. 임시 계정(로그인 건너뛰기)과 오프라인 테스트만 이 방식이다.

### 실제 계정 슬롯 (`SetEntries`, #263)
- 실제 계정(로그인한 계정)은 슬롯·함수의 원본이 서버 DB다 (progress-store.md). 슬롯 8칸은 늘 그 캐릭터의 슬롯 함수(slot1 ~ slot8)이고 장착을 바꾸지 않는다 (이우림 #239).
- `FunctionExecutorNetwork`(이우림)가 `PlayerDataReceived` 때(처음 받을 때, 함수를 저장해 값이 바뀔 때) `SetEntries(ReceivedSlots, ReceivedFunctions)`를 부른다. 실제 계정(`UsesServerData`)의 소유 클라이언트에서만.
- `SetEntries` 뒤로는
  - 슬롯 원본, `CodeSize`, `ReloadFunctions`가 이 컴퓨터의 함수 파일(`FunctionStorage`) 대신 받은 함수 목록을 쓴다.
  - `SlotLoadout.json`을 읽거나 쓰지 않는다. 켤 때 파일로 다시 덮어쓰지도 않는다.
  - 장착을 바꾸는 함수(`EquipFunction`, `EquipSkill`, `Unequip`, `Swap`)는 경고만 남기고 거부한다. 장착 화면(`SlotEquipPanel`)은 `UsesServerData`면 숨기거나 막는다.
- 편집 화면이 저장 뒤 `ReloadFunctions()`를 부르면 지금 받아 둔 목록으로 원본을 다시 만들고, 서버 저장이 끝나 `PlayerDataReceived`가 오면 `SetEntries`가 새 목록으로 다시 채운다.
- 시전: 실제 계정 슬롯은 모두 블록 함수라 `SlotCaster`는 늘 「누르는 순간 시전, 대상은 함수 안의 `GetBug`가 받음」으로 간다. C 스킬 대상 고르기·급소공략 슬롯 미니게임은 `GetBug` 입력 흐름으로만 동작한다 (이우림 확인 요청에 답함).
- `EntryKind.Skill`, `EntryKind.Empty`, `EquipSkill`, `Unequip`, `Default Skill Ids`는 임시 계정·오프라인 테스트용으로 남긴다.

### GetBug 입력 (#159)
- 슬롯으로 시전한 작업이 `GetBug`로 대상을 기다리면(`FunctionExecutorNetwork.InputRequested(작업 번호, 입력 함수 ID, 슬롯)`, #212)
  - 그 작업을 시전한 슬롯 키를 아직 누르고 있으면: 단일 고르기와 똑같이 ← →로 고르고, 떼면 고른 몬스터를 `ProvideTarget(작업 번호, 몬스터)`로 서버에 보낸다. 후보가 없으면 대상 없음(`FromBug(null)`).
  - 누르고 있지 않으면(이미 뗐음, 다른 슬롯을 고르는 중, 함수 안에서 따로 시전된 작업): 가장 가까운 몬스터를 바로 넣는다. 없으면 대상 없음.
- 고르는 중에 경직·사망으로 취소되면 대상 없음을 넣는다. 기다리던 작업이 끝나면(취소, 실행 중단 공격) 고르기도 끝낸다.
- 대상 없음이 들어가면 그 값을 받은 스킬이 `InvalidTarget`으로 실패한다 (function-executor.md).

### 급소공략 미니게임 (#161)
- 입력 순서: 급소공략 슬롯을 누른 채 ← →로 대상 고르기 → Space(`MarkPressed`)로 미니게임 시작 → Space로 바늘 멈춤(판정) → 슬롯을 떼면 시전.
- 미니게임을 시작하면 대상이 고정된다 (← → 무시).
- 결과는 `VitalStrikeMinigame`에 남고, 급소공략이 시작할 때 가져간다 (c-skills.md 「급소공략」).
  - Space를 안 누르고 떼면 미니게임 없이(기본 데미지) 시전
  - 바늘이 도는 중에 떼면 `Miss`로 시전 (시전 요청 전에 `VitalStrikeMinigame.FinishAsMiss()`로 끝내서 서버에 결과를 먼저 보냄, #223)
  - 대상이 없어 시전하지 않거나, 고르기가 취소되면(경직·사망) 결과를 지운다. 새로 대상 고르기를 시작해도 지운다.
- GetBug 입력으로 고를 때도 같다. 블록 함수 `VitalStrike(GetBug())`는 GetBug로 고르는 동안 미니게임을 하고, 그 결과로 찌른다.
- 여러 명 선택 스킬의 Space(찍기)와는 겹치지 않는다 (급소공략은 단일).

### 취소 키 (#159)
- `PlayerInputReader.CancelPressed`(C)가 들어오면, 슬롯으로 시전해 아직 끝나지 않은 작업 중 가장 최근 것의 슬롯으로 `FunctionExecutorNetwork.RequestCancel(슬롯)`을 부른다 (#212). 서버가 `FunctionExecutor.Cancel`로 끊는다.
- 실행기의 `Cancel`은 원본 단위지만, 같은 원본은 실행 중이면 다시 시전되지 않으므로(쿨타임으로 거부) 한 슬롯의 작업은 늘 하나다. 그래서 C 한 번에 작업 하나만 끊긴다.
- 슬롯 키는 시전만 한다 (연타해도 끊지 않음). 대상을 고르는 중인 슬롯은 아직 시전 전이라 C로 취소되지 않는다.

## 다른 시스템이 쓰는 것

플레이어 프리팹에 `SlotCaster`가 붙어 있다. `GetComponent<SlotCaster>()`로 가져온다.
- 같은 오브젝트에 `PlayerInputReader`, `FunctionExecutor`(+ `ProcessMemory`), `SlotLoadout`, `FunctionExecutorNetwork`(Sync Mode: Owner, #212)가 있어야 한다. `VitalStrikeMinigame`이 있으면 급소공략 미니게임을 한다.

### `SlotCaster`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `CastRequested` (event) | `int` 슬롯 번호(0~7), `FunctionBlock` 함수 (스킬은 `PredefinedFunction`, 블록 함수는 `UserFunction`, #159), `IReadOnlyList<GameObject>` 대상 목록 | - | [로직] 시전을 요청했을 때 불린다. 단일은 1명, 여러 명은 찍은 대상(없으면 고른 1명), 범위는 범위 안 몬스터 모두, 대상 없음은 빈 목록. 범위·대상 없음·블록 함수는 이미 `Cast`된 상태다 (블록 함수는 빈 목록). 단일은 고른 대상을 `target`으로 넘겨 이미 `Cast`된 상태다 (#152). 여러 명은 찍은 대상 배열로 `Cast`된 상태다 (#243). 시전이 거부되면 불리지 않는다. 네트워크 동기화는 이 이벤트에서 시작하면 된다. |
| `TargetChanged` (event) | `GameObject` 대상 | - | [표시] 선택된 대상이 바뀌었을 때 불린다 (고르기 시작, ← →, 끝). 끝나면 `null`. 대상 표시 연출에 쓴다. |
| `MarkedChanged` (event) | `IReadOnlyList<GameObject>` 찍은 대상 | - | [표시] 여러 명 선택에서 찍은 대상이 바뀌었을 때 (찍기, 빼기, 끝). 끝나면 빈 목록. |
| `CurrentTarget` | - | `GameObject` | 지금 선택된 대상. 고르는 중이 아니면 `null` |
| `MarkedTargets` | - | `IReadOnlyList<GameObject>` | 여러 명 선택에서 지금까지 찍은 대상 (찍은 순서) |
| `SelectingSlot` | - | `int` | 지금 대상을 고르는 슬롯 번호(0~7). GetBug 입력을 고르는 중이어도 그 슬롯 번호. 고르는 중이 아니면 -1 |

### `SlotLoadout` (장착, #159)

플레이어 프리팹에 붙어 있다. `GetComponent<SlotLoadout>()`로 가져온다. 장착 화면(임혜원)이 쓴다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `SlotCount` | - | `int` (8) | 슬롯 수. 0~7 = Q W E R A S D F |
| `GetEntry(int slot)` | 슬롯 번호 | `SlotLoadout.Entry` | 슬롯의 내용. `Kind`(`Empty`, `UserFunction`, `Skill`), `Id`(GUID 또는 고정 ID), `IsEmpty` |
| `EquipFunction(int slot, string guid)` | 슬롯 번호, 함수 GUID | `bool` | 블록 함수를 넣는다. 저장된 함수가 아니면 `false` |
| `EquipSkill(int slot, string skillId)` | 슬롯 번호, 고정 ID | `bool` | C 스킬을 넣는다. 없는 ID, 입력 함수면 `false` |
| `Unequip(int slot)` | 슬롯 번호 | - | 비운다 |
| `Swap(int a, int b)` | 슬롯 번호 두 개 | - | 맞바꾼다 (끌어 옮기기) |
| `CodeSize` | - | `int` | 슬롯 전체 코드 영역 크기 (`CodeSizeCalculator`). 장착 화면에 보여 준다. 한도 검사는 하지 않음 |
| `ReloadFunctions()` | - | - | 블록 함수를 저장소에서 다시 읽어 원본을 새로 만든다. 편집 화면에서 장착된 함수를 저장한 뒤 부른다 |
| `TryGetFunction(int slot, out FunctionBlock function)` | 슬롯 번호 | `bool` | 시전할 원본. `SlotCaster`가 쓴다 |
| `IsEquippableSkill(string skillId)` (static) | 고정 ID | `bool` | 슬롯에 넣을 수 있는 스킬인지. 장착 화면의 스킬 목록을 거를 때 |
| `Changed` (event) | `int` 슬롯 번호 | - | [표시] 슬롯 내용이 바뀌었을 때. 여러 슬롯이 한꺼번에 바뀌면 -1. 장착 화면을 다시 그린다 |
| `SetEntries(IReadOnlyList<Entry> slots, IReadOnlyList<FunctionData> functions)` | 슬롯 8칸, 이 캐릭터의 함수 전부 | - | (#263) [실제 계정] 서버가 보낸 슬롯·함수로 채운다. `Changed(-1)`. 위 「실제 계정 슬롯」. `FunctionExecutorNetwork`가 부른다 |
| `UsesServerData` | - | `bool` | (#263) `SetEntries`로 서버 데이터를 쓰고 있으면 `true`. 장착 화면이 장착을 막을 때 본다 |
| `UseLocalFiles()` | - | - | (#263) 서버 데이터를 버리고 이 컴퓨터 파일로 돌아간다. 테스트 정리, 로그아웃해 임시 계정으로 바뀔 때 |

```csharp
// 예시: 장착 화면에서 함수 목록의 함수를 W 슬롯에 끌어 놓음
SlotLoadout loadout = NetworkClient.localPlayer.GetComponent<SlotLoadout>();
if (!loadout.EquipFunction(1, function.Guid)) ShowError("넣을 수 없는 함수");
sizeLabel.text = $"코드 영역 {loadout.CodeSize}";
```

### Inspector 값

| 값 | 설명 |
| --- | --- |
| `SlotLoadout` > `Default Skill Ids` | 저장한 장착 내용이 없을 때(처음 켤 때) 넣어 둘 스킬. 기본 Q `c.thrust`, W `test.function_a`, E `c.battlefield_sweep`, R `c.wide_slash` |
| `Target Range` | 대상 후보를 찾는 거리 (유닛, 기본 8) |
| `Target Layers` | 대상 후보 레이어 (Monster) |

### 테스트용 컴포넌트

| 이름 | 설명 |
| --- | --- |
| `SlotTargetMarker` | (#438) 고른 대상에 빨간 조준점, 찍은 대상에 하늘색 조준점을 대상 몸(콜라이더) 가운데에 겹쳐 띄운다 (48×48, 8프레임 숨쉬기 반복, Sorting Layer WorldUI). 고른 대상이 찍힌 대상이기도 하면 하늘색을 `Marked Under Scale`(1.25)배로 뒤에 깐다. 조준점은 대상의 자식이 아니고 매 프레임 따라가며, 대상이 없어지면 숨긴다. Inspector: `Reticle Prefab`/`Marked Prefab`(비우면 `Resources/TargetReticle`, `TargetReticleMarked`), `Scale`(0.5, 1.5칸). 프리팹은 메뉴 Invoke → JW → 조준점 프리팹 만들기(`Assets/_JW/Effects/Target` 시트). `SetTarget`, `SetMarked`는 테스트도 직접 부른다. |
| `SlotTargetMarkerSimTest` | (#438) Test_JW의 ReticleTest. 켜고 Play → Host. 고른 대상 조준점 위치, 찍은 대상 2개·겹칠 때 크기, 끝나면 숨김, 대상이 꺼지면 숨김을 확인. 「[ReticleTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SlotMultiAreaSimTest` | Test_JW의 MultiTest. Play → Host, Game 창을 한 번 클릭. 슬롯 3(E)=전장 휩쓸기, 슬롯 4(R)=검 크게베기로 두 명 찍기, 안 찍고 떼기, 다시 찍어 빼기, 소환검 1개 제한, 범위를 확인. 시작할 때 검 소환을 2번 시전해 소환검 2개를 만들고(찍을 수 있는 인원 = 사용 가능한 소환검 수), 해제로 1개·0개로 줄인다. 「[MultiTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SlotLoadoutSimTest` | Test_JW의 LoadoutTest. 켜고 Play → Host (약 8초, 가짜 키 입력). 장착·거부·옮기기·자리 바꾸기·해제·코드 영역 크기, 저장 파일 다시 불러오기, W에 넣은 `Thrust(GetBug())`를 누른 채 고르고 떼면 고른 몬스터가 찔림, 슬롯 밖 시전은 가장 가까운 몬스터, E로 시전한 `while (true) { }`가 C로 `Cancelled`를 확인. 끝나면 원래 장착 내용으로 되돌리고 테스트 함수 파일을 지운다. 「[LoadoutTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SlotServerDataSimTest` | (#263) Test_JW의 ServerSlotTest. 켜고 Play → Host (약 3초). 서버가 보낸 것처럼 새 GUID 슬롯 함수 8개를 `SetEntries`로 넣고, 받은 GUID·`Changed(-1)`·파일로 덮어쓰지 않음, 받은 목록으로 원본 만들기·시전 `Completed`·`CodeSize`, 장착 바꾸기 거부·`SlotLoadout.json` 안 씀, `ReloadFunctions`·다시 `SetEntries` 뒤 새 원본, `UseLocalFiles`로 원래 장착 복귀를 확인. 「[ServerSlotTest] 전체 통과」면 통과. 테스트 씬은 임시 계정이라 `FunctionExecutorNetwork`가 가짜 슬롯을 서버로 다시 보내 「슬롯 N의 함수를 받지 못했습니다」 경고 8줄, 장착 거부 경고 4줄이 뜨는 것은 정상. 평소에는 꺼 둔다. |
| `SlotTargetInputSimTest` | Play → Host 2초 뒤 가짜 키 입력(Q, Q+→, Q+→, Q+←, 뗌)을 넣어 대상 넘기기를 확인한다. `TargetChanged` 5번이면 통과. 자동으로 돌기 때문에 평소에는 꺼 둔다. |

## 맞춘 결과
- 이우림과 정함 (#133, #152)
  - 단일 대상 슬롯 시전은 `Cast(function, RuntimeValue.FromBug(몬스터))`로 넘긴다. 죽었거나 없어진 대상은 실행기가 `InvalidTarget`으로 막는다
  - 함수 안의 `GetBug` 입력 대기(슬롯 키 누른 채 고르기 → `ProvideInput`): #159에서 함. #212에서 `ProvideTarget`으로 바꿈. 위 「GetBug 입력」
- 이우림 요청 반영 (#173 → #212)
  - 시전·취소·입력을 `FunctionExecutorNetwork`로. 클라이언트로만 접속한 플레이어도 슬롯으로 시전된다
  - 찌르기 돌진·바라보는 방향·급소공략 미니게임 결과·스킬 이펙트 서버 동기화: #223 (player-movement.md, c-skills.md)
  - 실제 계정 슬롯을 서버 데이터로 채우기: #263 `SetEntries` (#226 코멘트 요청). 부르는 일은 이우림 `FunctionExecutorNetwork`
  - 검 줍기 서버 동기화: #229. 여러 명 선택 대상 배열: 이우림 #238(`RequestCast` 대상 여러 개) → #243에서 연결
  - 스킬 캔슬: C 키를 누르면 가장 최근에 슬롯으로 시전한 작업 하나를 `FunctionExecutor.Cancel`로 끊는다. 슬롯 키는 시전만 한다(연타해도 안 끊김). C 키 입력은 이우림이 추가(#157), 캔슬은 #159에서 함
- 임혜원과 정할 것 (#159)
  - 장착 화면은 `SlotLoadout` API로 만든다. 코드 영역 크기는 `CodeSize`로 표시
  - 편집 화면에서 장착된 함수를 저장하면 `ReloadFunctions()`를 불러야 고친 내용으로 시전된다
- 기획에 확인할 것 (#159)
  - GetBug를 기다릴 때 슬롯 키를 이미 뗐으면 가장 가까운 몬스터를 자동으로 넣음 (다시 누르게 할지)
  - 코드 영역 크기 한도
- 이우림과 정해야 할 것
  - ← → 대상 넘기기가 동작하지 않는 문제
    - 원인: Input System의 Shortcut Keys Consume Input(Complexity Consumption) 때문에 `Move`(1D Axis 조합, 복잡도 3)가 ← →를 먼저 가져가고, `TargetPrev`/`TargetNext`(단일 키, 복잡도 1) 신호가 지워진다. `TargetPrev`/`TargetNext`를 조합으로 바꿔 복잡도를 맞춰도, 먼저 처리된 쪽이 나머지를 지우므로 해결되지 않는다.
    - 해결(#59): 이우림이 `PlayerInputReader`에 액션 맵 활성화·비활성화 함수(`SetPlayerMapEnabled`, `SetAttackMapEnabled`)를 추가했다(#60). `SlotCaster`가 대상을 고르는 동안 Player 액션 맵을 비활성화한다. `SlotTargetInputSimTest`로 확인.
    - 처음 제안한 방법(`PlayerInputReader`에서 `move`만 끄고 켜기)은 쓰지 않았다. UI 등 다른 이유로 맵을 끄는 코드와 같은 상태를 따로 바꾸게 되기 때문이다.
  - 고른 대상을 함수의 Target 파라미터에 넣어 `Cast`하는 방법과, Target 값의 C# 타입 (지금은 `GameObject`로 넘긴다). 「함수 실행 중 대상 입력 기다리기」 이슈와 같은 방법을 쓴다.
  - function-executor.md에는 「Player 프리팹에 `FunctionExecutor`, `ProcessMemory`가 붙어 있다」고 되어 있지만 #268에서 공용 `Player.prefab`에 실행기 묶음(`FunctionExecutor`, `ProcessMemory`, `LanguageRule`, `FunctionExecutorLogger`, `SlotLoadout`, `FunctionExecutorNetwork`)과 `SlotCaster`를 붙였다.
- 임혜원과 정해야 할 것
  - 대상 표시 연출을 `TargetChanged`, `MarkedChanged`로 붙이는지
