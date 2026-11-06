# PlaceScenes (장소 씬)

- 담당자: 이우림

## 하는 일
- 서버 하나에서 로비·사냥터 씬과, 입장마다 따로 여는 연습장·레이드 던전 씬 동시 운영
- 씬마다 물리 세계 분리: 다른 씬의 오브젝트끼리 충돌·판정 없음
- 같은 씬의 네트워크 오브젝트만 그 씬에 있는 클라이언트에 전송 (`SceneInterestManagement`)
- 들어가기(로그인 뒤 캐릭터 고르기)에 성공한 플레이어를 로비 시작 위치에 생성 (`account.md`)
- 게임 중에 생기고 사라지는 네트워크 오브젝트의 공용 생성·삭제 함수 (`NetworkSpawner`)
- 장소 사이 이동 (#134): 로비 ↔ 사냥터, 로비 ↔ 연습장
  - 도착 위치: 로비로 돌아오면 들어갔던 입구 앞(`ArrivalPoint`), 그 밖의 장소는 `PlayerStartPosition`
  - 출구: 사냥터·연습장에서 로비로 (`PlaceExit`)
  - 로딩 화면: 이동하는 동안, 접속 직후 로컬 플레이어의 함수 데이터가 준비될 때까지 (`LoadingScreen`)
- 파티 레이드 (#300): 파티장이 시작하면 파티원 모두를 레이드 던전 하나로, 레이드가 끝나면 모두 로비로
  - 레이드 한 판 기록 (`RaidParties`): 던전, 파티원, 진행 상태
  - 파티원 이탈 실패: 레이드 도중 파티원이 한 명이라도 빠지면 실패
  - 결과 화면 전달: 서버의 판정 결과를 파티원 클라이언트에
- 하지 않는 일
  - 레이드 방, 보스, 성공·실패 판정, 결과 화면 그림: [raid.md](raid.md) (김도훈, 결과 화면 그림은 임혜원)
  - 보스·플레이어 체력의 화면 동기화: 김도훈의 동기화 이슈
  - 조작된 이동 요청 막기 (예: 로비를 거치지 않는 이동): 보안 작업 때. 검사 자리는 `InvokeNetworkManager.OnTravelRequest`

## 동작 규칙

### 씬 구성
- 기준 씬 `Base`: `NetworkManager`(`InvokeNetworkManager`, 로그인·암호화 컴포넌트는 `account.md`), `Camera`, `EventSystem`, `PlaceTravel`(`PlaceTravelClient` + 로딩 화면 Canvas). 씬 목록 0번 (Windows·Linux Server 빌드 프로필 모두 전역 씬 목록). 서버·클라이언트 모두 계속 열려 있음
- 내용 씬 `Lobby`, `HuntingGround`, `TrainingRoom`, `RaidDungeon`: `Grid`(Ground·Platform 타일맵), `PlayerStartPosition`(`NetworkStartPosition`)
  - 카메라, NetworkManager, EventSystem, 빛 없음 (기준 씬 위에 겹쳐 불러오므로 넣으면 여러 개가 됨)
  - `Lobby`: `ArrivalPoint` 3개 (`Arrival_HuntingGround`, `Arrival_TrainingRoom`, `Arrival_RaidDungeon`, 각 입구 앞)
  - `HuntingGround`, `TrainingRoom`: `PlaceExit` 1개 (맵 왼쪽, `PlayerStartPosition`과 겹치지 않는 위치)
  - `RaidDungeon`: 출구 없음 (#300). 레이드는 클리어·실패로만 끝남
- 서버
  - 시작 시 로비·사냥터를 한 번씩 불러옴 (Additive, `LocalPhysicsMode.Physics2D`)
  - 연습장은 플레이어가 이동을 요청할 때마다 같은 씬을 한 번 더 불러옴 (아래 「연습장」)
  - 레이드 던전은 시작한 파티마다 같은 씬을 한 번 더 불러옴
  - 따로 불러온 씬은 이름이 모두 같으므로 `Scene` 값으로 구분
- 클라이언트
  - 서버가 알려 준 씬만 불러옴 (자기 플레이어가 있는 씬)
  - 물리 분리 없이 기본 물리 세계에 불러옴

### 장소 사이 이동 (#134)
- 요청: 로컬 플레이어의 클라이언트 → 서버 `TravelRequestMessage { destination }` (갈 씬 이름)
  - 로비 입구: `LobbyEntrance.TravelRequested` → `PlaceTravelClient.RequestTravel`
  - 레이드 입구(`RaidEntryRequested`)는 이동 요청이 아님. `RaidLobbyOpener`가 레이드 방 화면을 엶 (#242, [raid.md](raid.md))
  - 출구: `PlaceExit` 앞에서 ↑ → `PlaceTravelClient.RequestTravel(PlaceScenes.Lobby)`
- 클라이언트의 요청 순서: Player·Attack 액션 맵 비활성화 → 로딩 화면 페이드 인 → 화면을 다 덮은 뒤 요청 전송
  - 다 덮은 뒤 보내는 이유: 먼저 보내면 이전 장소가 사라지는 모습이 페이드 사이로 보임
- 서버의 요청 처리 (`OnTravelRequest`)
  - 거절: 플레이어 오브젝트 없음, 이미 그 장소, 레이드 던전(`PlaceScenes.RaidDungeon`), 모르는 장소 → 경고 로그 + `TravelEndedMessage`
  - 무시: 레이드 이동을 기다리는 파티원의 요청 → 경고 로그만, `TravelEndedMessage` 없음. 클라이언트는 이동 끝을 하나만 기다리므로, 보내면 레이드 이동 전에 로딩 화면이 걷힘. 그 클라이언트의 로딩 화면은 레이드 이동의 `TravelEndedMessage`가 걷음
    - 레이드 던전을 거절하는 이유: 레이드 던전에는 파티장이 시작할 때 서버가 옮겨 줄 때만 들어감 (아래 「파티 레이드」)
  - 연습장: 요청한 플레이어를 위해 `TrainingRooms.TryOpen`이 씬을 한 번 불러옴 → 다 불러오면 `TravelPlayer`. 불러오는 동안 같은 연결의 요청이 또 오면 경고 로그만, `TravelEndedMessage` 없음 (먼저 한 요청의 로딩 화면을 일찍 걷지 않기 위함)
  - 그 밖의 장소(로비·사냥터): 서버가 시작할 때 로드한 씬으로 `TravelPlayer`
  - 레이드 중인 파티원의 요청: 출구가 없어 보내는 클라이언트 코드는 없음. 받으면 그 장소로 옮기고 파티원 이탈로 봄
- 서버의 이동 순서 (`TravelPlayer`)
  1. `FunctionExecutor.InterruptAll()`: 실행 중인 함수 모두 중단 (이전 장소에서 시전한 작업이 새 장소에서 이어지지 않도록)
  2. 클라이언트에 이전 씬 `SceneMessage(UnloadAdditive)` (호스트 제외)
  3. `PutPlayerInScene`: 서버의 플레이어를 새 씬으로 이동, 클라이언트에 새 씬 `SceneMessage(LoadAdditive)`
  4. `NetworkTransformBase.ServerTeleport`: 서버·클라이언트의 플레이어 위치를 도착 위치로 변경
     - 이유: Player 위치는 소유 클라이언트가 정함(Client To Server). 서버 위치만 바꾸면 클라이언트가 덮어씀
     - 씬 메시지 뒤에 보내는 이유: 클라이언트가 새 씬을 다 로드한 뒤에 처리하므로, 로드하는 동안 떨어진 플레이어도 도착 위치로 돌아옴
  5. `TravelEndedMessage`
  6. 이전 씬이 연습장이면 그 씬을 내림
  7. 이전 씬이 레이드 던전이면 `RaidParties`에 그 파티원이 빠졌다고 알림 (던전을 내릴지는 `RaidParties`가 정함)
- 클라이언트의 처리 순서: 요청(움직임 잠금) → 이전 씬 언로드 → 새 씬 로드 → teleport → `TravelEndedMessage`(움직임 잠금 해제, 로딩 화면 페이드 아웃, 액션 맵 활성화)
- 이동하는 동안 움직임 잠금 (#216): `PlaceTravelClient`가 `RequestTravel`에서 로컬 플레이어의 `PlayerMovement.SetMovementLocked(true)`, `TravelEndedMessage`를 받으면 `SetMovementLocked(false)` 호출
  - 이유(클라이언트): 이전 씬을 언로드한 뒤 밟을 땅이 없어 떨어지고, teleport 뒤에도 떨어지던 속도가 남음
  - 이유(서버): 그동안 클라이언트가 보낸 위치(떨어지는 이전 위치)로 서버의 플레이어가 새 씬에 놓여 몬스터와 판정될 수 있음
  - 서버가 거절했을 때도 `TravelEndedMessage`가 오므로 잠금 해제
  - 접속이 끊기면 로컬 플레이어 오브젝트가 삭제되어 잠금도 함께 사라짐
- 출구와 호스트: 호스트에서는 떠난 장소의 씬이 서버에 남고, 다른 씬의 물리 세계로 옮겨진 플레이어에는 `OnTriggerExit2D`가 불리지 않음 → `PlaceExit`는 로컬 플레이어의 물리 세계(`scene.GetPhysicsScene2D()`)가 자기와 다르면 놓아줌
  - 씬이 아니라 물리 세계로 비교: 클라이언트에서는 Mirror가 플레이어를 `Base`에 만들어 씬이 늘 다름. 장소 씬과 `Base`는 같은 기본 물리 세계
  - 순서 보장: 씬 로드·언로드 중에는 Mirror가 다음 메시지 처리를 미룸(`NetworkClient.isLoadingScene`)
- 다른 플레이어 보이기: 플레이어의 씬이 바뀌면 `SceneInterestManagement`가 observer 목록을 다시 만듦 → 같은 씬의 플레이어만 보임
- 도착 위치
  - 도착하는 씬에서 `fromPlace`가 떠나온 씬 이름과 같은 `ArrivalPoint` → 없으면 `PlayerStartPosition`
  - 높이: 서버가 표시 아래 10 안의 땅(Ground·Platform 레이어, 트리거 제외)을 찾아 플레이어 콜라이더의 아래쪽을 땅 윗면에 맞춤 (`PlaceOnGround`, #216)
    - 로그인 직후 로비 `PlayerStartPosition`에 생성할 때도 같음
    - 이유: 표시를 땅보다 높게 두면 플레이어가 생기자마자 떨어짐
    - 땅이 없으면: 표시 위치 그대로, 경고 로그
  - `ArrivalPoint`가 있는데 맞는 것이 없으면 경고 로그 (`fromPlace` 오타)
- 접속 직후: 로딩 화면을 페이드 없이 덮음 → 로컬 플레이어의 `FunctionExecutorNetwork.IsDataReady`가 true가 되면 페이드 아웃
  - 로컬 플레이어는 들어가기 성공 뒤에 생김 → 로그인·캐릭터 화면을 보는 동안에도 로딩 화면이 덮고 있음 (`account.md` 「맞춘 결과」)
  - `FunctionExecutorNetwork`가 없는 플레이어 프리팹(시험 씬의 프리팹 등): 로컬 플레이어가 생기면 바로 걷힘. 공용 `Player.prefab`에는 있음 (#268)
- 접속이 끊기면: 로딩 화면 숨김. 액션 맵은 `PlayerInputReader.OnStopLocalPlayer`가 다시 활성화

### 연습장 (#301)
- 코드: 서버의 `TrainingRooms`(static)가 연습장을 언제 열고 내릴지 정하고, `InstanceScenes`(static)가 씬을 불러오고 내림
  - `TrainingRooms`: 연습장을 연 플레이어마다 기록(`TrainingRoom`: 주인 연결, 씬) 하나. 레이드의 `RaidParties`와 같은 방식
  - `InstanceScenes`: 연습장·레이드 던전이 함께 쓰는 도구. 불러오기 요청은 한 줄에 서서 한 번에 하나씩 처리됨. 언제 불러오고 내릴지는 정하지 않음
  - `InvokeNetworkManager`는 이동 요청·이동 끝·접속 끊김·플레이어 없앰 때 `TrainingRooms`를 부르기만 함. 서버가 물리를 돌릴 씬 목록은 `FixedUpdate`가 쓰므로 `InvokeNetworkManager`에 남고, `InstanceScenes`가 씬을 열고 내릴 때 넣고 뺌
- 단위: 플레이어 한 명마다 연습장 씬 하나. 다른 플레이어의 연습장과 물리 세계·네트워크 전송이 모두 분리됨
- 열기: 로비의 입구 창에서 「들어가기」 → 이동 요청(`TrainingRoom`) → `OnTravelRequest`가 `TrainingRooms.TryOpen`을 부름 → 씬을 다 불러오면 `TravelPlayer`
  - 입장할 때마다 새 씬: 나갔다 다시 들어가면 이전 연습장의 상태(허수아비 등)가 남지 않음
- 내리기: 아래 경우에 그 플레이어의 연습장 기록을 지우고 씬을 내림 (연결로 기록을 찾으므로 어느 씬인지 따로 기억하지 않음)
  - 로비로 나감: `TravelPlayer`가 `TrainingRooms.OnLeftRoom`
  - 접속이 끊김: `OnServerDisconnect`가 `TrainingRooms.OnPlayerGone`
  - 같은 계정의 다른 로그인으로 쫓겨남: `DespawnPlayer`가 `TrainingRooms.OnPlayerGone`
  - 불러오는 사이에 접속이 끊김: 기록만 지우고, 다 불러온 뒤 기록이 없는 것을 보고 바로 내림
- 개수: 동시에 여는 개수에 상한 없음. 서버 부담이 문제가 되면 `OnTravelRequest`에서 막음
- 연습장 안의 오브젝트(허수아비, 편집 터미널, 장착대)는 씬에 놓은 일반 오브젝트(`NetworkIdentity` 없음)라 씬마다 따로 생김. `Monster`, `DamageNumberView`, `PanelStation`, `InputBlockingPanel`에 `static` 멤버 없음

### 파티 레이드 (#300)
- 레이드 한 판 기록: 서버의 `RaidParties`(static)가 시작한 레이드마다 `RaidParty` 하나를 가짐
  - 던전: `LoadRaidDungeon`이 넘긴 `Scene` (불러오기 전에는 비어 있음)
  - 파티원: 연결(`NetworkConnectionToClient`) 목록. 닉네임이 아니라 연결인 이유: 접속 끊김·이동 때 서버가 받는 값이 연결
  - 상태: 불러오는 중 → 진행 중 → 끝남
  - `InvokeNetworkManager`는 이동·접속 끊김·플레이어 없앰 때 `RaidParties`에 알리기만 함. 던전을 내리는 판단은 `RaidParties` 한 곳
- 시작
  1. 서버에서 `RaidFlow.StartRequested(파티원 이름 목록)` → `RaidParties`가 다음 프레임에 처리
     - 다음 프레임인 이유: `RaidRooms.TryStart`는 `StartRequested`를 부른 뒤에 방 목록을 보냄. 이동 알림이 방 목록보다 먼저 도착하면, 방 목록을 받은 `RaidLobbyUI`가 화면을 닫으며 액션 맵을 다시 활성화함
  2. 이름마다 `AccountSession.Nickname`이 같은 연결을 찾아 기록 생성 (상태: 불러오는 중)
     - 찾지 못한 파티원이 있으면(한 프레임 사이에 접속이 끊김) 시작하지 않음. 아직 아무에게도 이동을 알리지 않았으므로 모두 로비에 그대로
     - 지금은 `RaidRooms.TryStart`가 이미 방을 지워서 남은 파티원이 방을 잃음. 방 유지(아래 「김도훈에게 요청할 것 (#300 뒤)」)가 생기면 `RaidParties`가 시작 취소를 알려 방을 대기로 되돌림
  3. 파티원마다 `TravelStartedMessage`, 던전을 `LoadRaidDungeon`으로 한 번 불러옴
  4. 파티원마다 던전을 다 불러옴 + 그 파티원의 `TravelReadyMessage` 도착 → `TravelPlayer`로 던전에 이동 (모두 옮기면 상태: 진행 중)
- 서버가 시작한 이동 (시작, 끝)
  - 서버 → 파티원 클라이언트 `TravelStartedMessage`: `PlaceTravelClient`가 `RequestTravel`과 같이 액션 맵 비활성화, 움직임 잠금, 로딩 화면 페이드 인
  - 화면을 다 덮으면 클라이언트 → 서버 `TravelReadyMessage`. 서버는 이 답을 받은 파티원만 옮김 (화면을 다 덮은 뒤 옮기는 이유는 「장소 사이 이동」과 같음)
    - 클라이언트가 이미 이동 중(자기 요청의 답을 기다림): 덮은 뒤면 바로 답함. 덮기 전이면 아직 보내지 않은 요청 대신 답을 보냄 (먼저 보낸 요청이 레이드 던전에서 다시 옮기지 않게)
    - 로컬 플레이어나 `PlaceTravelClient`가 없는 클라이언트: 화면을 막지 않고 바로 답함
    - 답 기다리기 시간 5초: 지나도 답이 없으면 서버가 답 없이 옮김. 답이 오지 않아 그 파티원이 이동을 기다리는 채로 남거나 던전이 내려가지 않는 일을 막기 위함
  - 마무리는 다른 이동과 같음: `TravelPlayer`의 `TravelEndedMessage`
  - 이동 취소: 서버가 `TravelEndedMessage`만 보냄 → 로딩 화면 걷힘, 그 자리에 그대로
- 끝: 서버에서 `RaidFlow.Ended(던전 씬, 클리어 여부)`
  - 그 던전의 기록을 찾아 상태: 끝남. 기록이 없는 던전(네트워크 없이 시험하는 씬 등): 경고 로그, 무시
  - 파티원마다 「서버가 시작한 이동」으로 로비에 이동. 도착 위치: 로비의 `Arrival_RaidDungeon`
  - 죽어 있던 파티원: `RaidJudge`가 `ReportEnd` 전에 부활을 되돌리므로 부활한 뒤 이동 ([raid.md](raid.md))
- 파티원 이탈 실패: 시작할 때 있던 파티원이 한 명이라도 빠지면 그 레이드는 실패
  - 이탈로 보는 일 (상태가 불러오는 중·진행 중일 때)
    - 접속 끊김 (`OnServerDisconnect`)
    - 같은 계정의 다른 로그인으로 쫓겨남 (`DespawnPlayer`)
    - 파티원의 플레이어가 던전에서 다른 장소로 옮겨짐 (`TravelPlayer`)
  - 이탈로 보지 않는 일
    - 죽음: 관전하는 파티원도 파티원. 모두 죽으면 `RaidJudge`의 실패
    - 상태가 끝남일 때 빠짐: 기록에서만 뺌. 클리어가 실패로 바뀌지 않음
    - 시작 전 파티방에서 나감: 방 나가기 ([raid.md](raid.md))
  - 진행 중에 이탈: 그 던전의 `RaidJudge.FailByServer()` → 결과 화면(지금의 「레이드 실패」 그대로) 3초 → `Ended` → 남은 파티원 로비로
    - 이미 클리어로 끝난 뒤(결과 화면 3초 동안)의 이탈: `FailByServer`가 무시. 클리어 그대로
    - 결과 화면 3초 동안 아직 던전에 들어오지 않은 파티원: 던전에 넣지 않음. 로딩 화면이 덮인 채 로비에서 기다리다 `Ended` 때 이동 취소
  - 불러오는 중에 이탈: 남은 파티원은 이동 취소(로비에 그대로), 결과 화면 없음, 던전은 다 불러온 뒤 바로 내림
- 레이드 포기: 파티원이 「레이드 포기」 버튼을 누르면 그 레이드는 실패
  - 클라이언트: 버튼이 `RaidParties.RequestGiveUp()`을 부름 → 서버에 `RaidGiveUpMessage`
  - 서버: 보낸 사람이 진행 중인 레이드의 파티원이고 그 던전 안에 있을 때만 처리. 불러오는 중·끝난 뒤·던전 밖에서 온 요청은 경고 로그, 무시
  - 처리: 진행 중에 이탈과 같은 실패(`FailByServer()` → 결과 화면 → `Ended` → 로비)
  - 이탈과 다른 점: 포기한 파티원은 기록에서 빠지지 않음. 다른 파티원과 함께 결과 화면을 보고 로비로
- 던전 내리기: 기록의 파티원이 0명이 되면 `RaidParties`가 기록을 지우고 `UnloadRaidDungeon`
  - 불러오는 중에 0명: 다 불러온 뒤 내림
  - 혼자 시작한 레이드에서 그 파티원이 이탈: 결과 화면 없이 던전만 내림
- 결과 화면 전달
  - 문제: `RaidResultView`(임혜원)는 자기 컴퓨터의 `RaidJudge.ResultEffectPlayed`를 받음. `RaidJudge`는 서버에서만 판정하므로 호스트가 아닌 클라이언트에서는 결과 화면이 뜨지 않음
  - 서버: 던전을 불러온 뒤 그 던전의 `RaidJudge.ResultEffectPlayed`를 받아, 그 던전 안에 있는 파티원 클라이언트에 `RaidResultMessage { cleared }`
    - 호스트의 클라이언트 제외: 서버의 `RaidJudge`를 같이 써서 이미 뜸
    - 아직 던전에 들어오지 않은 파티원 제외: 던전 씬을 불러오지 않아 띄울 곳이 없음
  - 클라이언트: 자기가 불러온 레이드 던전의 `RaidJudge.PlayResultOnly(cleared)` → `ResultEffectPlayed` → `RaidResultView`가 결과 화면 (`RaidResultView`는 그대로)

### 판정 (물리 세계)
- `Physics2D.OverlapBox`, `Physics2D.Raycast`, `Physics2D.OverlapCircleAll` 등 `Physics2D`의 판정 함수: 기준 씬의 물리 세계만 검사. 서버에서는 내용 씬의 오브젝트를 찾지 못함
- 판정 방법: 오브젝트가 속한 씬의 물리 세계(`gameObject.scene.GetPhysicsScene2D()`)에 질의
  - 예: `gameObject.scene.GetPhysicsScene2D().OverlapBox(center, size, 0f, filter, results)`
  - 서버·클라이언트·호스트 모두 같은 결과
- 바꾸는 방법: `Physics2D.` → `gameObject.scene.GetPhysicsScene2D().`
  - `OverlapBox`, `OverlapCircle`, `Raycast`: 받는 값 그대로
  - `OverlapBoxAll`, `OverlapCircleAll` 등 이름이 `All`로 끝나는 함수: `PhysicsScene2D`에 없음. `ContactFilter2D`와 `List<Collider2D>`를 받는 함수로 대체 (돌려주는 값: 찾은 개수, 찾은 Collider는 `List`에 채워짐)
    - 예: `Physics2D.OverlapCircleAll(pos, range, layers)` → `gameObject.scene.GetPhysicsScene2D().OverlapCircle(pos, range, filter, results)`
    - `filter` 설정: `filter.SetLayerMask(layers)`, `filter.useTriggers = Physics2D.queriesHitTriggers` (예전 함수와 같은 결과)
- `GetPhysicsScene2D()` 결과: 저장하지 않고 판정할 때마다 호출. 장소 이동으로 오브젝트의 씬이 바뀌면 저장해 둔 값은 이전 씬의 물리 세계를 가리킴
- 그대로 동작하는 것
  - `Rigidbody2D` 이동(`linearVelocity`, `AddForce`), 중력
  - 충돌 이벤트(`OnCollisionEnter2D`, `OnTriggerEnter2D`)
  - `Physics2D.IgnoreCollision`
  - `Collider2D.Overlap`, `Rigidbody2D.Cast`처럼 Collider·Rigidbody에서 부르는 판정 함수
  - Physics 2D 설정(중력 값, Layer Collision Matrix)
- 물리 진행
  - 서버의 내용 씬: `InvokeNetworkManager`가 `FixedUpdate`마다 씬별로 진행
  - 기준 씬, 클라이언트: Unity가 자동 진행

### 네트워크 오브젝트
- 기준: 서버가 판정에 쓰거나, 모든 플레이어가 같은 위치·상태로 봐야 하는 오브젝트
  - 예: 몬스터, 몬스터 투사체, 소환검, 떨어진 아이템
- 네트워크 오브젝트가 아닌 것: 보여 주기만 하는 오브젝트. 각 클라이언트가 연출 함수에서 직접 생성
  - 예: 피격 이펙트, 데미지 숫자, 대상 강조 표시
- 프리팹 준비
  - `NetworkIdentity` 추가
  - `Base` 씬 `NetworkManager`의 Registered Spawnable Prefabs에 등록
- 생성·삭제: 서버에서 `NetworkSpawner.Spawn` / `NetworkSpawner.Despawn`
  - 이유: `Instantiate`는 활성 씬(서버에서는 `Base`)에 생성. 그대로 두면 장소 씬의 플레이어와 판정되지 않고, 어느 클라이언트에도 보이지 않음

## 다른 시스템이 쓰는 것

### `NetworkSpawner`

static 클래스. `NetworkSpawner.Spawn(...)`처럼 바로 부른다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Scene scene)` | 프리팹, 위치, 회전, 만들 씬 (보통 `gameObject.scene`) | `GameObject` (실패 시 `null`) | 네트워크 오브젝트를 그 씬에 생성. 서버: 생성 후 `NetworkServer.Spawn`. 네트워크를 켜지 않은 테스트: 생성만. 클라이언트: 오류 로그, 생성 안 함. `NetworkIdentity`가 없거나 등록되지 않은 프리팹: 원인 오류 로그, 생성 안 함 |
| `Despawn(GameObject obj)` | 없앨 오브젝트 | - | 바로 삭제. 서버: `NetworkServer.Destroy` (모든 클라이언트에서 사라짐). 네트워크를 켜지 않은 테스트: `Destroy`. 클라이언트: 오류 로그, 삭제 안 함. 시간을 두고 없애는 일은 부르는 쪽 |

### `PlaceScenes`

씬 이름 상수. 씬 이름을 문자열로 직접 쓰지 않고 이 상수를 쓴다.

| 이름 | 값 | 설명 |
| --- | --- | --- |
| `Lobby`, `HuntingGround`, `TrainingRoom`, `RaidDungeon` | `string` | 각 장소의 씬 이름 |
| `FixedPlaces` | `string[]` | 서버가 시작할 때 한 번씩 불러오는 장소 (로비, 사냥터). 연습장·레이드 던전은 입장마다 따로 불러오므로 넣지 않음 |

### `InvokeNetworkManager`

`Base` 씬의 NetworkManager. `NetworkManager.singleton as InvokeNetworkManager`로 가져온다. 모든 함수는 서버에서만 부른다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TravelPlayer(NetworkConnectionToClient conn, GameObject player, Scene target)` | 플레이어의 연결, 플레이어 오브젝트, 갈 씬 | - | 플레이어를 다른 장소로 이동 (실행 중인 함수 중단, 이전 씬 언로드 알림, 씬 이동, 도착 위치로 teleport, `TravelEndedMessage`). 레이드 시작·끝에는 `RaidParties`가 파티원마다 부름 |
| `DespawnPlayer(NetworkConnectionToClient conn)` | 플레이어의 연결 | - | 접속은 둔 채 그 연결의 플레이어를 없앰 (서버·모든 클라이언트). 연습장에 있었으면 그 연습장을 내림. 레이드 파티원이었으면 파티원 이탈. 같은 계정 중복 로그인으로 쫓겨난 연결에 `InvokeAuthenticator`가 부름 |
| `PutPlayerInScene(NetworkConnectionToClient conn, GameObject player, Scene scene)` | 플레이어의 연결, 플레이어 오브젝트, 넣을 씬 | - | 서버: 플레이어를 그 씬으로 이동. 클라이언트: 그 씬을 불러오라고 알림. 위치 변경, 이전 씬 내리기는 하지 않음 (장소 사이 이동은 `TravelPlayer`) |
| `LoadRaidDungeon(Action<Scene> onLoaded)` | 다 불러온 뒤 부를 함수 | - | 레이드 던전 씬을 한 번 더 불러옴 (`InstanceScenes.Load`). 다 불러오면 `onLoaded`에 그 `Scene` 값을 넘김. 요청은 한 번에 하나씩 처리 |
| `UnloadRaidDungeon(Scene scene)` | `LoadRaidDungeon`으로 받은 씬 | - | 그 레이드 던전 씬을 내림 (`InstanceScenes.Unload`). 던전 안의 플레이어는 부르기 전에 다른 씬으로 옮겨야 함 (남아 있으면 씬과 함께 사라짐) |

### `PlaceTravelClient`

`Base` 씬의 `PlaceTravel` 프리팹. `PlaceTravelClient.Instance`로 가져온다. 로컬 플레이어의 클라이언트에서 부른다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Instance` | - | `PlaceTravelClient` | `Base` 씬의 `PlaceTravelClient`. 장소 씬의 오브젝트는 Inspector로 다른 씬의 오브젝트를 연결할 수 없으므로 이것으로 찾음 |
| `RequestTravel(string destination)` | 갈 씬 이름 (`PlaceScenes`) | - | 서버에 이동 요청. 접속 안 함, 로컬 플레이어 없음, 이전 요청의 답을 기다리는 중이면 무시. 답(`TravelEndedMessage`)이 올 때까지 로컬 플레이어의 Player·Attack 액션 맵 비활성화, 움직임 잠금. 레이드 던전(`PlaceScenes.RaidDungeon`)은 서버가 거절 |

- 레이드 시작·끝의 이동은 서버가 시작함 (`TravelStartedMessage`, 위 「파티 레이드」). 다른 스크립트가 부를 함수는 없음

### `RaidParties`

static 클래스. `RaidParties.RequestGiveUp()`처럼 바로 부른다. 아래 함수 말고는 `InvokeNetworkManager`만 부른다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `RequestGiveUp()` | - | - | [클라이언트] 서버에 레이드 포기를 보냄. 레이드 던전의 「레이드 포기」 버튼이 부름. 서버가 그 레이드를 실패로 끝내고 포기한 사람을 포함해 파티원 모두를 로비로 옮김. 싸우는 중이 아니면 서버가 무시. 접속하지 않았으면 경고 로그만 |

### 시험

| 이름 | 설명 |
| --- | --- |
| `TrainingRoomTest` | 연습장 입장마다 열기(#301). 씬에 놓지 않음. 에디터 `Base` 씬 Play 뒤 eval로 `TrainingRoomTest.StartInEditor(false)`(호스트 혼자) 또는 `StartInEditor(true)` + 시험 빌드를 `-trainingRoomTest member`로 실행(호스트 + 클라이언트). 입장 전 연습장 없음 → 입장하면 하나 열림 → (클라이언트가 있으면) 둘이 입장해 연습장이 둘, 서로 다른 씬, 서로의 observer가 아님 → 나가면 내 연습장만 내림 → 클라이언트가 접속을 끊으면 그 연습장도 내림 → 다시 입장하면 새 씬 → 플레이어가 없어지면(`DespawnPlayer`) 내림. 「[TrainingRoomTest][역할] 전체 통과」면 통과 |
| `RaidPartyTest` | 파티 레이드(#300). 씬에 놓지 않음. 에디터 `Base` 씬 Play 뒤 eval로 `RaidPartyTest.StartInEditor(false)`(호스트 혼자) 또는 `StartInEditor(true)` + 시험 빌드를 `-raidPartyTest member`로 실행(호스트 + 클라이언트). 직접 던전 요청 거절, 로비에서 보낸 레이드 포기 무시, 시작 → 같은 던전 도착 → 보스 처치 → 클리어 결과 화면(클라이언트는 서버가 보낸 결과) → 로비 복귀·던전 내림, 다시 시작해 레이드 포기(호스트 혼자면 호스트, 아니면 클라이언트) → 실패 결과 화면 3초 뒤 모두 로비, 클라이언트가 던전에서 접속을 끊으면 호스트가 실패 결과 화면 3초 뒤 로비로 돌아옴. 결과 화면은 `RaidJudge.ResultEffectPlayed`로 확인. 「[RaidPartyTest][역할] 전체 통과」면 통과 |

### 씬에 놓는 컴포넌트

출구와 로딩 화면은 이 컴포넌트를 붙여 프리팹으로 만든다. `Assets/_UL/Prefabs`의 `PlaceExit`, `PlaceTravel` 안 Canvas는 임시본.

| 이름 | 붙이는 곳 | 꼭 필요한 설정 | 자유롭게 바꿔도 되는 것 | 설명 |
| --- | --- | --- | --- | --- |
| `ArrivalPoint` | 장소 씬의 빈 GameObject | `fromPlace`: 떠나온 장소의 씬 이름 (`PlaceScenes`, 대소문자까지 같게). 높이: 땅 위 10 안 (서버가 땅에 맞춰 세움) | - | 그 장소에서 온 플레이어가 도착할 위치 (`transform.position`) |
| `PlaceExit` | 출구 프리팹의 root | Is Trigger를 체크한 `Collider 2D`. `prompt`: ↑ 안내 오브젝트 (비우면 안내 없음). 사냥터·연습장에 하나씩, `PlayerStartPosition`과 겹치지 않게. 레이드 던전에는 두지 않음 (#300) | 스프라이트, 색, Collider 크기, 안내의 모양·위치 | 로컬 플레이어가 범위 안에서 ↑(`InteractPressed`)를 누르면 로비로 이동 요청 |
| `LoadingScreen` | 로딩 화면 Canvas | `panelRoot`: 화면을 덮는 UI의 부모. `canvasGroup`: `panelRoot`에 붙인 `Canvas Group`. 배경 Image의 Raycast Target 체크. Canvas Sort Order는 로비 UI(0)보다 크게. `Base`에 놓고 `PlaceTravel`의 `loadingScreen` 칸에 연결 | 배경, 글자, 글꼴, 애니메이션, `fadeSeconds`(페이드 시간, 기본 0.25초) | `PlaceTravelClient`만 `Show`·`ShowImmediate`·`Hide`를 부름. 이동하는 동안과 접속 직후에 화면을 덮음 |

## 맞춘 결과
- 김도훈에게 알릴 것
  - `ContactDamage.cs`의 `Physics2D.OverlapBox`: 서버에서 내용 씬의 플레이어를 찾지 못함. 자기 씬의 물리 세계에 질의 필요
  - `ProjectileAttack.cs`의 투사체 생성, `MonsterProjectile.cs`·`Monster.cs`의 삭제: 네트워크를 붙일 때 `NetworkSpawner.Spawn` / `Despawn` 사용
- 박진우에게 알릴 것
  - `SlotCaster.cs`의 대상 찾기(`Physics2D.OverlapCircleAll`): 서버에서 내용 씬의 대상을 찾지 못함. 자기 씬의 물리 세계에 질의 필요
  - `PlayerMovement.cs`의 바닥 판정·발판 내려가기: 내 캐릭터(클라이언트)에서만 실행하므로 전용 서버에서는 영향 없음. 호스트 모드에서는 바닥을 찾지 못함 (자기 씬의 물리 세계에 질의하면 해결)
  - 소환검 생성·삭제: `NetworkSpawner.Spawn` / `Despawn` 사용
- 박진우에게 요청할 것 (#134) → 박진우가 #207에서 만듦 (player-movement.md)
  - `PlayerMovement`에 움직임 잠그기 함수 `SetMovementLocked(bool locked)`, `IsMovementLocked`
    - 잠근 동안: 속도 0, 중력 없음, 진행 중인 돌진 중단, 입력·스킬로 움직이지 않음
    - 풀 때: 속도 0에서 시작
    - 쓰는 곳: `PlaceTravelClient`가 요청을 보낼 때 잠그고 `TravelEndedMessage`를 받으면 풂 (위 「이동하는 동안 움직임 잠금」, #216)
    - `Rigidbody2D`를 `PlaceTravelClient`가 직접 다루지 않는 이유: 속도는 `PlayerMovement`가 매 `FixedUpdate`에 씀. 두 곳에서 바꾸면 돌진 상태 등과 어긋남 (코드 작성 규칙 2)
- 임혜원에게 알릴 것 (#134)
  - `LobbyInteractable`도 호스트에서 같은 문제: 호스트가 로비를 떠나도 로비 씬이 서버에 남고 `OnTriggerExit2D`가 불리지 않아, 입구가 로컬 플레이어를 계속 붙잡음 → 다른 장소에서 ↑를 누르면 로비 입구 창이 열림. `PlaceExit`처럼 `Update`에서 로컬 플레이어의 물리 세계(`scene.GetPhysicsScene2D()`)가 자기와 다르면 놓아주면 해결 (씬으로 비교하면 클라이언트에서 늘 달라 상호작용이 안 됨)
- 임혜원과 맞춘 것 (#134)
  - 로비 입구는 이벤트(`LobbyEntrance.TravelRequested`, `RaidEntryRequested`) 그대로. `TravelRequested`는 `PlaceTravelClient`, `RaidEntryRequested`는 `RaidLobbyOpener`가 받음 (#242)
  - 「들어가기」 뒤 `LobbyEntrancePanel.Close()`가 액션 맵을 다시 활성화하므로, 이동하는 동안 `PlaceTravelClient`가 Player·Attack 액션 맵을 다시 비활성화
  - `Base`의 `EventSystem` 그대로 사용. 로딩 화면 Canvas도 같은 `EventSystem`
  - 출구 모양: `Entrance`의 문 스프라이트(`UrbanDecay_42`, 색 `6EE4F5`)와 `Prompt`를 `PlaceExit` 프리팹에서 참조·복사 (`_HW` 파일 변경 없음)
- 임혜원에게 넘길 것
  - 출구와 로딩 화면을 `Assets/_HW`에 프리팹으로 만들기 (사용법: 「씬에 놓는 컴포넌트」)
    - 출구: 사냥터·연습장에 하나씩 (레이드 던전은 출구 없음, #300)
    - 로딩 화면: `Base`에 놓고 `PlaceTravel`의 `loadingScreen` 칸에 연결
  - 공용 씬의 임시본 삭제: 사냥터·연습장의 `PlaceExit`, `Base`의 `PlaceTravel` 안 Canvas (`PlaceTravel`은 이동 코드가 붙어 있어 남김)
  - 지금 임시본의 모습: 출구는 `Entrance`의 문 스프라이트와 `Prompt` 복사, 로딩 화면은 검은 배경과 "Loading..." 글자
- `Assets/_UL/Prefabs`의 임시 프리팹 파일: 임혜원의 프리팹으로 바꾼 뒤 이우림이 삭제
- 이우림 결정 (#300)
  - 범위: 파티 던전 열기·옮기기, 끝난 뒤 로비로, 파티원 이탈 실패, 레이드 포기, 결과 화면 전달. 보스·플레이어 체력의 화면 동기화는 김도훈의 동기화 이슈
  - 시작 인원: `RaidRooms.TryStart` 그대로 1~3명
  - 파티원이 한 명이라도 빠지면 실패, 레이드 던전에 출구 없음
  - 파티원 이탈 실패의 결과 화면: 지금의 「레이드 실패」 그대로. 이탈에 맞는 문구는 나중에 임혜원과
  - 레이드 포기: 포기한 사람도 다른 파티원과 함께 결과 화면을 보고 로비로. 결과 화면은 「레이드 실패」 그대로
  - 시작: 시작을 누른 파티원이 모두 있어야 시작. 한 명이라도 없으면 시작하지 않음
  - 레이드 방 (#300 뒤 김도훈 구현): 클리어·실패·파티원 이탈·레이드 포기 모든 경우에 남음. 접속이 끊긴 사람만 빠지고, 1명만 남아도 유지
- 김도훈에게 요청한 것 (#300, `RaidJudge`) → 김도훈이 #308에서 만듦 ([raid.md](raid.md)), 이우림이 #310에서 연결
  - 실패로 끝내기 (서버): `FailByServer()`. 파티원이 이탈하거나 레이드를 포기하면 `RaidParties.FailRaid`가 부름. 모두 죽었을 때와 같이 결과 화면 이벤트 → `Result Delay` 뒤 `ReportEnd(던전, false)`. 이미 끝났으면 무시
  - 결과 화면 이벤트만 부르기 (클라이언트): `PlayResultOnly(bool)`. `RaidResultMessage`를 받은 클라이언트에서 `RaidParties`가 부름. 판정 없이 `ResultEffectPlayed(클리어 여부)`만
- 김도훈에게 요청할 것 (#300 뒤, `RaidRooms` 방 유지)
  - 시작해도 방을 지우지 않고 「레이드 중」으로 남김
  - 레이드 끝 또는 시작 취소를 받는 함수: `RaidParties`가 부름. 방을 대기로 되돌리고 파티원의 준비를 풂
  - 함수 이름은 김도훈이 정하고 `raid.md`에 먼저 적음
- 임혜원에게 알릴 것 (#300)
  - 「레이드 포기」 버튼: 클릭에 `RaidParties.RequestGiveUp()` 연결 (위 「`RaidParties`」). 버튼을 둘 화면은 정하는 중 (시안: 레이드 중 설정 창)
