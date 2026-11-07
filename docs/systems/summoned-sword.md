# 소환검 (SummonedSwords)

- 담당자: 박진우

## 하는 일
- 플레이어가 malloc으로 만든 소환검(최대 5개)의 목록, 포인터, 상태를 관리한다.
- 소환검 오브젝트(소환검 프리팹)를 만들고 없앤다.
- C 스킬(소환검 필요 검사), 슬롯(여러 명 선택 최대 인원), HUD가 소환검 개수와 상태를 읽을 수 있게 한다.
- 하지 않는 일
  - heap에 올리고 내리기(malloc, free): 검 소환·해제 스킬(`PredefinedFunction.Malloc` / `Free`, c-skills.md)이나 플레이어의 `malloc(Sword)` / `free` 블록이 한다. 이 컴포넌트는 오브젝트와 목록만 맡는다.
  - 소환검으로 때리는 스킬의 데미지: 각 스킬 이슈 (`Hit` 이벤트를 받아서 처리)
  - 실제 HUD 그림: 임혜원 (지금은 OnGUI 임시 표시)
  - 네트워크 동기화: #229 (아래 「네트워크」)

## 규칙
- 포인터
  - 소환검 하나 = heap 객체 하나. 포인터는 이우림 heap(`ProcessMemory.Heap`)의 포인터를 그대로 쓴다.
  - 검 소환 스킬 순서: `SpawnSword`(오브젝트) → `PredefinedFunction.Malloc`(heap) → `Register(sword, pointer)`(목록). malloc 실패면 `DespawnSword`.
  - 블록코딩 `malloc(Sword)` (#191, #220): `Start`에서 `ProcessMemory.StructObjects.Register<SummonedSword>(PredefinedStructs.Sword, () => CanSummon ? SpawnSword() : null, Register)`로 등록. 실행기가 heap 확인 → `SpawnSword` → heap에 올림 → `Register` 순서로 부른다. 5개면 `NULL`.
  - 해제 순서: `Free`(해제 스킬이든 `free` 블록이든, heap) → `HeapStorage.ObjectFreed` → 이 컴포넌트가 `DespawnSword`(오브젝트, 목록에서 뺌) (#220). 스킬은 `DespawnSword`를 부르지 않는다.
  - heap 크기: 소환검 하나 = `Sword` struct 크기(`PredefinedStructs`, 임시 2칸).
  - 포인터는 소환한 순서대로 커진다. 목록(`Swords`)도 소환 순서.
- 최대 5개
  - 누수된 소환검도 개수에 들어간다 (`HeapStorage.CountObjects`). 누수가 쌓이면 새로 소환할 수 있는 수가 줄어든다. `Destroy`된 오브젝트는 세지 않는다 (#325, `TryGet`과 같은 기준).
  - 플레이어가 장소를 옮겨도 누수 검은 같이 옮겨지고 개수를 계속 차지한다 (`MoveToScene`).
  - 5개거나 heap이 모자라면 검 소환 스킬이 만들지 않는다 (C의 malloc처럼 에러로 끝내지 않음).
- 상태
  - 사용 가능(`Ready`): 플레이어 뒤통수 부근에 반원으로 떠서 따라다니고, 칼끝은 모두 바라보는 쪽을 향한다 (#399, 발로란트 제트 궁극기 느낌).
    - 반원 가운데 = 머리 뒤로 `Orbit Behind`, 머리 꼭대기(플레이어 콜라이더 윗변)에서 `Orbit Below Head Top`만큼 아래. 높이가 콜라이더 기준이라 캐릭터 크기가 바뀌어도 머리를 따라간다. 검 가운데는 거기서 `Orbit Radius`만큼 떨어진다.
    - 가운데 검이 바로 앞, 위아래로 검마다 `Orbit Angle Step`(30°)씩 벌어진다. 소환한 순서대로 위에서부터.
    - 검마다 조금씩 다른 박자로 위아래로 둥둥 뜬다 (`Bob Height`, `Bob Speed`).
    - 방향을 바꾸면 반원이 앞쪽으로 따라오고 칼끝도 돌아간다. 다른 상태에서 돌아오면 자리와 각도로 부드럽게 돌아간다.
    - 돌아오는 검(`Recall`, 찌르기 뒤)은 반원 가운데 앞 자리로 먼저 온다.
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
  - 안에서 `NetworkSpawner.Spawn(프리팹, 위치, 회전, gameObject.scene)` / `NetworkSpawner.Despawn`을 부른다 (place-scenes.md, #125). 플레이어가 있는 장소 씬에 생기고, 서버면 모든 클라이언트에 보인다. 서버에서만 부른다.
  - 소환검 프리팹: `NetworkIdentity`, `Base` 씬과 Test_JW의 NetworkManager Registered Spawnable Prefabs에 등록
  - 그림 순서: SpriteRenderer Sorting Layer `Projectile`, Order 5 (#341). `Default`면 맵 배경 뒤에 그려져 안 보인다
  - 만들지 못하면(프리팹 설정 오류, 클라이언트에서 부름) `SpawnSword`가 `null`을 돌려주고 검 소환은 아무것도 만들지 않는다
- 떨어질 땅 찾기는 소환검이 속한 씬의 물리 세계(`gameObject.scene.GetPhysicsScene2D()`)에 묻는다 (place-scenes.md, #125).

### 지금 값 (공용 Player, #268)

| 항목 | 값 |
| --- | --- |
| Sword Prefab | `Assets/_JW/SummonedSword.prefab` (임시 그림: 하늘색 막대) |
| Ground Layers | Ground, Platform |
| Orbit Behind / Orbit Below Head Top | 0.8 / 0.4 (#399) |
| Orbit Radius | 0.7 |
| Orbit Angle Step | 30° |
| Bob Height / Bob Speed | 0.06 / 2.5 |
| Pick Up Range | 1.5 |
| 소환검 Throw Speed / Max Flight Time | 22 / 1.2초 (프리팹) |

## 네트워크 (#229)
- 소환검 움직임(따라가기, 던지기, 찌르기, 돌아오기, 꽂힘)은 서버에서만 한다. 클라이언트의 `SummonedSword.Update`는 움직이지 않는다.
- 위치·회전: 소환검 프리팹의 `NetworkTransform`(Sync Direction: Server To Client, 위치·회전)이 맞춘다.
- 포인터, 상태, 주인 플레이어: `SummonedSword`의 `[SyncVar]`. 클라이언트에서도 `Pointer`, `State`가 맞고 `StateChanged`가 불린다 (누수 반투명).
- 목록: 클라이언트에는 heap이 없어서, 소환검 오브젝트가 주인의 `SummonedSwords` 목록에 스스로 들어가고 나간다(포인터 순서). 그래서 클라이언트에서도 `Swords`, `Count`, `ReadyCount`, `Changed`를 그대로 읽는다 (HUD, 여러 명 선택 최대 인원).
- 줍기: `SwordPickUpInput`이 Z를 `[Command]`로 서버에 요청한다 (network.md 경우 4). 죽은 동안·경직 중(`PlayerHealth.CanAct`가 false)에는 서버가 요청을 무시한다 (#325).
- 스킬이 부르는 함수(`Throw`, `Stab`, `Recall` 등)는 서버에서만 부른다. 클라이언트에서 부르면 클라이언트 목록만 보고 아무 일도 하지 않는다.

## 다른 시스템이 쓰는 것

플레이어 프리팹에 `SummonedSwords`가 붙어 있다 (`ProcessMemory` 필요). `GetComponent<SummonedSwords>()`로 가져온다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `MaxCount` (const) | - | `int` | 5 |
| `Swords` | - | `IReadOnlyList<SummonedSword>` | 소환 순서 목록 (누수 포함). HUD가 읽는다 |
| `Count` | - | `int` | heap에 있는 소환검 수 (누수 포함) |
| `CanSummon` | - | `bool` | 5개 미만이면 `true` |
| `ReadyCount` | - | `int` | 사용 가능한 소환검 수 |
| `SpawnSword()` | - | `SummonedSword` | 소환검 오브젝트를 만든다 (목록·포인터 없음, `NetworkSpawner`). 검 소환 스킬이 Malloc 전에 부른다. 만들지 못하면 `null` |
| `Register(SummonedSword, int pointer)` | 소환검, 포인터 | - | malloc에 성공한 소환검을 목록에 올린다 |
| `DespawnSword(SummonedSword)` | 소환검 | - | 오브젝트를 없애고 목록에서 뺀다. heap은 건드리지 않음 (heap에서 해제되면 `ObjectFreed`를 받아 이 컴포넌트가, 검 소환 스킬은 malloc 실패 때 부른다) |
| `TryGetSword(int pointer, out SummonedSword)` | 포인터 | `bool` | 포인터로 찾기. 해제·누수면 `false` |
| `TryGetFirstReady(out SummonedSword)` | - | `bool` | 가장 먼저 소환한 사용 가능한 검 |
| `Throw(SummonedSword, GameObject target)` | 검, 대상 (없으면 빗나감) | `bool` | 사용 가능한 검만 던진다 |
| `Recall(SummonedSword)` / `RecallFirstThrown()` | 검 | `bool` | 던진 검을 되돌린다 |
| `Stab(SummonedSword, GameObject target)` | 검, 대상 | `bool` | (#165) 사용 가능한 검을 대상에게 보내 찌르고 돌아오게 한다. 닿으면 `StabHit`, 돌아오면 다시 `Ready`. 대상이 사라지거나 1.2초 안에 못 닿으면 그냥 돌아온다. 소환검 찌르기가 쓴다 |
| `Stab(SummonedSword, GameObject target, int side)` | 검, 대상, 옆(-1 왼쪽, 1 오른쪽, 0 바로) | `bool` | (#243) `Stab`과 같고, 먼저 대상의 그쪽 옆(`Stab Side Distance`, 임시 1.5)으로 간 뒤 안쪽으로 찌른다. 표식 남기기가 쓴다 |
| `Dance(SummonedSword, GameObject target, float startAngle)` | 검, 대상, 시작 각도(도) | `bool` | (#243) 사용 가능한 검을 대상 둘레로 보내 돌게 한다 (`Dance Radius` 1.2, `Dance Degrees Per Second` 540, 임시). `Dancing` 상태. 데미지는 스킬이 준다. 검무가 쓴다 |
| `EndDance(SummonedSword)` | 검 | - | (#243) 검무를 끝낸다. 도는 중인 검만 `Ready`로 바뀌고 곁의 자리로 날아온다 |
| `Ride(SummonedSword, float seconds)`, `EndRide(SummonedSword)` | 검, 초 | `bool` / - | (#256) 검 타기. 사용 가능한 검을 seconds초 동안 발밑(`Ride Offset`)에 붙이고(`Riding`), 시간이 다 되면 스스로 곁으로 돌아온다. `EndRide`는 바로 돌려보냄 |
| `MoveToScene(Scene scene)` | 갈 씬 | - | (#325) 소환검을 모두 새 씬으로 옮긴다. 누수가 아닌 검은 모든 상태(던짐·꽂힘·찌르기·검무·검 타기)에서 `Ready`로 돌려 곁의 자리에 놓고, 누수 검은 `Leaked` 그대로 곁에 놓는다. 장소 이동 때 `InvokeNetworkManager.TravelPlayer`가 서버에서 부른다 (place-scenes.md) |
| `EndAllRides()` | - | - | (#325) 타고 있는(`Riding`) 검을 모두 끝낸다. 소유 클라이언트의 `PlayerMovement`가 대시·움직임 잠금·경직·사망으로 검 타기를 먼저 끝냈을 때 서버에서 부른다 |
| `HasRidingSword` | - | `bool` | (#325) 타고 있는 검이 있으면 `true`. 검 타기 스킬이 이미 타는 중에 두 번째 검을 묶지 않으려고 확인한다 |
| `Throw(SummonedSword, GameObject target, Vector2 direction)` | 검, 대상(빗나가면 `null`), 방향 | `bool` | (#167) 일직선으로 던진다. 대상에 닿으면 `Hit`을 알리고 몬스터 몸에 꽂혀 따라다닌다(몬스터가 죽으면 떨어짐). 대상이 `null`이면 안 맞고 날아가 벽·땅(Ground)에 꽂히거나 14칸에서 떨어진다. 한쪽 방향 발판(Platform)은 통과한다. 소환검 던지기가 쓴다 |
| `StartReturn(SummonedSword)` | 검 | `bool` | (#167) 던진(날아가는 중, 떨어짐·꽂힘) 검을 곁으로 날아오게 한다. 지나가는 몬스터는 `ReturnHit`, 닿으면 `Ready`. 검 돌아오기가 쓴다 |
| `ReturnHit` (event) | `SummonedSword`, `GameObject` | - | [로직] (#167) 돌아오는 검이 몬스터를 지나갔을 때. 몬스터마다 한 번 |
| `StabHit` (event) | `SummonedSword`, `GameObject` | - | [로직] (#165) `Stab`으로 보낸 검이 대상에 닿았을 때. 데미지는 받은 스킬이 준다 |
| `TryPickUpNearest()` | - | `bool` | 근처의 떨어진 소환검을 줍는다 |
| `Changed` (event) | - | - | [표시] 목록·상태가 바뀔 때 |
| `Hit` (event) | `SummonedSword`, `GameObject` | - | [로직] 던진 소환검이 대상에 닿았을 때 |

`SummonedSword`(소환검 하나): `Pointer`, `State`(`Ready` / `Flying` / `Dropped` / `Leaked` / `Stabbing` / `Returning` / `Dancing` / `Riding`), `StateChanged`(event, [표시]), `ThrowOrder`(던진 순서, #167), `StuckTo`(꽂힌 몬스터, #167).
- `Throw(검, 대상)`(대상만 넘김)도 #167부터 대상 쪽 일직선으로 날아간다 (예전: 대상을 따라가는 유도). 대상이 `null`이면 바로 떨어지는 건 그대로.
- `Returning`(#167): 검 돌아오기로 곁으로 날아오는 중. 사용 가능 개수에 안 들어간다. 꽂혀 있는 검은 `Dropped` 상태라 Z로 주울 수도 있다.
- `Riding`(#256): 검 타기로 플레이어 발밑에 붙어 있음. 사용 가능 개수에 안 들어간다.
- `Dancing`(#243): 검무로 대상 둘레를 도는 중. 칼끝이 대상을 본다. 사용 가능 개수에 안 들어간다. 대상이 사라지면 그 자리에 떠 있다가 스킬이 끝나면 곁으로 돌아온다.
- `Stabbing`(#165): 소환검 찌르기로 날아가 찌르고 돌아오는 중. 사용 가능 개수(`ReadyCount`)에 안 들어가서 다른 스킬에 쓰이지 않는다. 속도 `Stab Speed`(프리팹, 임시 35).

### 임시 표시
- `SummonedSwordView`(소환검 프리팹): `StateChanged`를 받아서 누수면 반투명(`Leaked Alpha` 0.35)으로 바꾼다.
- `SummonedSwordHud`(임시, OnGUI): 내 캐릭터 화면 왼쪽 위에 「소환검 n/5 (사용 가능 m)」과 포인터별 상태를 그린다. 게임 HUD(`HudView`, 임혜원 #366)에 소환검 칸이 생겨서, 공용 `Player.prefab`에서는 뺐다 (#376). HUD가 없는 Test_JW에서 보려고 `Player_JW`에만 붙여 둔다.

### 테스트
| 이름 | 설명 |
| --- | --- |
| `SummonedSwordSimTest` | Test_JW의 SummonTest. 켜고 Play → Host, Game 창을 한 번 클릭 (Unity가 화면 맨 앞에 있어야 가짜 키가 들어감). 실제 스킬(검 소환·해제)로 확인: 검 소환을 heap이 허락하는 만큼(최대 5개, 3개 이상이어야 함, 그다음은 안 됨), 포인터 순서, 첫 번째 사용 가능한 검, 던지기·돌아오기, 줍기(가짜 Z 키), 해제(마지막 소환 검, heap 감소), 떨어진 검 해제, 검 소환 도중 실행 중단 → 누수(반투명, 개수에 남음, 해제 대상 아님), 소환검 없을 때 소환검 찌르기 실패, 해제할 검 없으면 `NoSummonedSwordError`, 영역 비우기. 「[SummonTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

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
  - 소환검 heap 크기(지금 `Sword` 임시 2칸, 기본 메모리 10칸이라 5개를 다 못 만듦), 날아가는 속도: 기획
- 이우림과 정함 (#191, #220)
  - 소환검 = `Sword` struct. `malloc(Sword)` 블록과 검 소환 스킬이 같은 소환검을 만든다. 플레이어가 `Sword*`를 직접 관리한다
  - 해제한 소환검 오브젝트는 `ObjectFreed`를 받은 이 컴포넌트가 없앤다
