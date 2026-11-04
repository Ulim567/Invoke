# Raid (레이드)

- 담당자: 김도훈

## 하는 일
- 레이드 방 목록, 방 만들기(이름·비밀번호), 파티방(정비: 파티원 목록, 준비 상태, 시작)과 레이드 시작 알림
  - 데이터: `RaidRooms` (방·파티원 값은 여기서만 바꾼다)
  - 화면: `RaidLobbyUI`, `RaidRoomRowUI`, `RaidMemberRowUI`
  - 시작·끝 접점: `RaidFlow`
- 레이드 던전 안: 보스 만들기(`RaidBossSpawner`), 클리어·실패 판정과 관전(`RaidJudge`), "레이드 끝" 알림(`RaidFlow.ReportEnd`). 보스 패턴·페이즈는 `monster.md`의 `GatewayAI`, `GatewayPhases`
- 지금은 멀티 없이 내 컴퓨터 안에서만 도는 임시 버전이다 (이우림과 정함). 네트워크는 이우림이 나중에 `RaidRooms` 안쪽과 `RaidFlow.StartRequested`를 받는 쪽에 붙인다
- 레이드 던전 씬(`RaidDungeon`)에 `RaidBossSpawner`·`RaidJudge`를 놓고, `Base`에 Gateway 등록·`BossHealthBar`를 두는 일은 이우림의 공용 씬 작업이 끝난 뒤 따로 한다. 지금은 `Test_DH`에서 시험한다
- 하지 않는 일
  - 레이드 던전 씬을 파티마다 불러오고 내리기, 파티원 옮기기, 레이드가 끝난 뒤 로비로 돌려보내기: 이우림 (`place-scenes.md`). `RaidFlow.Ended`를 받아서 한다
  - 결과 화면 그림·연출: `RaidJudge.ResultEffectPlayed`를 받는 쪽 (임혜원)
  - 로비 레이드 입구(`LobbyEntrance.RaidEntryRequested`)와 이 화면을 잇는 일: 네트워크를 붙일 때 이우림과 함께 정한다
  - 레이드 던전 맵: 임혜원

## 동작 규칙
- 흐름: 방 목록 → (방 만들기 또는 입장) → 파티방 → 파티장이 시작 → `RaidFlow.RequestStart(파티원 이름 목록)`
- 방
  - 이름은 앞뒤 공백을 빼고 비어 있으면 안 된다. 비밀번호가 비어 있으면 공개 방
  - 최대 3명(`RaidRooms.MaxMembers`). 한 사람은 한 방에만 있을 수 있다. 파티원은 이름으로 구분한다
  - 0번이 파티장(방을 만든 사람). 파티장이 나가면 다음 사람이 파티장이 되고 준비가 풀린다. 아무도 없으면 방이 없어진다
- 준비와 시작
  - 파티원은 「준비」 / 「준비 취소」를 누른다. 파티장은 준비가 없다
  - 「시작」은 파티장에게만 보이고, 다른 파티원이 모두 준비하면 눌린다 (혼자여도 시작 가능)
  - 시작하면 방이 목록에서 없어지고 화면이 닫힌다
- 입장: 꽉 찬 방은 「입장」을 누르면 "인원이 다 찼습니다"를 알린다 (인원 글자가 빨갛게 보임). 비밀번호 방은 「입장」을 누르면 비밀번호 창이 뜨고, 공개 방은 바로 들어간다
- 실패하면 이유를 안내 글자에 보여 준다 (방 이름 없음, 인원 참, 비밀번호 틀림, 이미 다른 방에 있음, 파티장 아님, 준비 안 됨)
- 임시 테스트 버튼: 가짜 방 만들기(방장봇, 짝수 번째는 비밀번호 1234, 3의 배수 번째는 꽉 찬 방), 가짜 파티원 넣기, 가짜 파티원 준비 바꾸기
- 테스트 씬: `Assets/_DH/RaidRoomTest.unity` (Canvas + EventSystem + `RaidLobbyUI`, Open On Start)

### 보스 만들기 (`RaidBossSpawner`)
- 레이드 던전 씬이 불러와지면 서버(Host 포함)에서 한 번 보스를 만든다. 파티마다 던전 씬을 따로 불러오므로 던전마다 보스가 한 마리씩 생긴다. 보스가 죽어도 다시 만들지 않는다
- 위치: 스포너에서 아래로 땅(`Ground Layers`)을 찾아, 보스 판정(BoxCollider2D) 바닥이 땅에서 `Hover Height`만큼 뜨게 둔다. 땅을 못 찾으면 스포너 위치에 만들고 경고를 남긴다
- 보스는 `NetworkSpawner.Spawn`(이우림)으로 스포너가 있는 씬에 만든다. 네트워크를 켜지 않은 테스트에서도 만든다
- `RaidDungeon` 배치 (예정): 위치 (0, 0), Boss Prefab `Gateway`, Ground Layers `Ground`, Hover Height 0 (맵 가운데, 임혜원과 정함)
- 보스 체력 바: `BossHealthBar`(`monster.md`)는 `Base` 씬에 하나 둔다. 레이드 던전 씬에 두지 않는다 (Host는 던전 씬을 여러 개 겹쳐 열어서 바가 여러 개 생긴다)

### 클리어·실패 판정 (`RaidJudge`)
- 클리어: 보스(`RaidBossSpawner`가 만든 보스)가 죽으면
- 실패: 던전 안 플레이어가 한 명 이상 있고 모두 죽었으면 (`PlayerHealth.IsDead`)
- 관전: 던전에 들어온 플레이어는 `SetAutoRespawn(false)`로 죽어도 부활하지 않는다. 던전을 나가거나 레이드가 끝나면 `SetAutoRespawn(true)`로 되돌린다 (죽어 있던 파티원은 이때 부활한다)
- 플레이어는 `Check Interval`(0.25초)마다 이 씬의 최상위 오브젝트 중 `PlayerHealth`가 붙은 것을 다시 센다
- 끝나면 `ResultEffectPlayed(클리어 여부)`(결과 화면용)를 부르고, `Result Delay`(3초) 뒤 `RaidFlow.ReportEnd(던전 씬, 클리어 여부)`를 부른다. 한 던전에서 한 번만 끝난다
- 클리어 보상은 Gateway의 `MonsterReward`(Whole Scene)가 준다 (`monster.md`)
- 서버(Host 포함)이거나 네트워크를 켜지 않은 테스트에서만 판정한다

## 다른 시스템이 쓰는 것

### `RaidFlow`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `RequestStart(IReadOnlyList<string>)` | 파티원 이름 목록 (0번 파티장) | - | 레이드 시작 함수. `RaidRooms.TryStart`가 부른다 |
| `StartRequested` (static event) | `IReadOnlyList<string>` | - | [로직] 레이드 시작 요청. 받는 쪽(이우림)이 던전을 불러오고 파티원을 옮긴다. 지금은 받는 쪽이 없어서 로그만 남는다 |
| `ReportEnd(Scene, bool)` | 끝난 던전 씬, 클리어면 `true` | - | 레이드 끝 함수. `RaidJudge`가 결과를 보여 준 뒤 부른다 |
| `Ended` (static event) | `Scene`, `bool` | - | [로직] 레이드 끝. 서버에서만. 받는 쪽(이우림)이 그 던전의 플레이어를 로비로 옮기고 던전을 내린다. 지금은 받는 쪽이 없어서 로그만 남는다 |

### `RaidBossSpawner`

레이드 던전 씬에 놓는다. Scene 창에서 노란 상자가 보스가 설 자리다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Boss` | - | `Monster` | 이 던전에 만든 보스. 아직 없으면 `null`. 서버에서만 값이 있다 |
| `BossSpawned` (event) | `Monster` | - | [로직] 보스를 만든 순간. 서버에서만 |

Inspector: `Boss Prefab`(Gateway), `Ground Layers`(Ground), `Hover Height`(0)

### `RaidJudge`

`RaidBossSpawner`와 같은 오브젝트에 붙인다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `IsEnded` | - | `bool` | 레이드가 끝났으면 `true` |
| `ResultEffectPlayed` (event) | `bool` (클리어면 `true`) | - | [연출] 레이드가 끝난 순간. 결과 화면("레이드 성공/실패")에 쓴다 |

Inspector: `Check Interval`(0.25초), `Result Delay`(3초)

### `RaidRooms`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Rooms` | - | `IReadOnlyList<RaidRoom>` | 지금 있는 방 목록 |
| `TryCreate(name, password, leaderName, out room, out reason)` | 방 이름, 비밀번호, 파티장 이름 | `bool` | 방 만들기 |
| `TryJoin(room, password, memberName, out reason)` | 방, 비밀번호, 이름 | `bool` | 방 들어가기 |
| `Leave(room, memberName)` | 방, 이름 | - | 방 나가기 |
| `SetReady(room, memberName, ready)` | 방, 이름, 준비 여부 | - | 준비 바꾸기 (파티장은 무시) |
| `TryStart(room, requesterName, out reason)` | 방, 누른 사람 이름 | `bool` | 레이드 시작 (파티장만, 모두 준비했을 때) |
| `FindRoomOf(memberName)` | 이름 | `RaidRoom` | 그 사람이 있는 방, 없으면 `null` |
| `RoomsChanged` (static event) | - | - | 방이 생기거나 없어지거나 인원이 바뀜 |
| `RoomChanged` (static event) | `RaidRoom` | - | 한 방의 파티원·준비가 바뀜 |

`RaidRoom`: `Id`, `Name`, `HasPassword`, `Members`(`RaidMember`: `Name`, `IsReady`), `Leader`, `IsFull`, `AllReady`. 모두 읽기만 한다.

### `RaidLobbyUI`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Open()` | - | - | 레이드 화면 열기. 방에 있으면 파티방, 아니면 방 목록 |
| `Close()` | - | - | 화면 닫기 (방에서 나가지는 않음) |

## 맞춘 결과
- 이우림과 정한 것 (11/4)
  - 레이드는 방 파기 → 정비하는 파티방(로스트아크처럼) → 레이드 시작 흐름이다
  - 김도훈이 1~4를 멀티 없이 임시로 만들고, 네트워크는 이우림이 나중에 붙인다
    1. 레이드 방 만들기 UI (방 이름·비번, 방 목록)
    2. 파티방(정비) UI (파티원 목록, 준비 상태, 시작 버튼 → "레이드 시작 함수"에 파티원 목록을 넘김)
    3. 레이드 씬 안의 내용 (맵, 보스·몬스터 배치, 보스 패턴·페이즈)
    4. 클리어·실패 판정 (끝나면 "레이드 끝 함수"를 부름)
  - "레이드 시작 함수"는 `RaidFlow.RequestStart`, 받는 쪽은 `RaidFlow.StartRequested`
  - "레이드 끝 함수"는 `RaidFlow.ReportEnd(던전 씬, 클리어 여부)`, 받는 쪽은 `RaidFlow.Ended`. 로비로 돌려보내고 던전을 내리는 일은 이우림
  - 보스를 만들 수 있도록 김도훈이 `Gateway`를 `Base` 씬 NetworkManager의 Registered Spawnable Prefabs에 등록하고 `Base`에 `BossHealthBar`를 둔다 (이우림의 공용 씬 작업 뒤)
- 임혜원과 정한 것
  - Gateway는 레이드 던전 맵 가운데에 둔다 (`map-background.md`). 레이드 던전 씬에는 보스 스포너 오브젝트만 추가하고 땅·꾸미기는 건드리지 않는다
