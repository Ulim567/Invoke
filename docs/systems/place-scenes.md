# PlaceScenes (장소 씬)

- 담당자: 이우림

## 하는 일
- 서버 하나에서 로비·사냥터·연습장·레이드 던전 씬 동시 운영
- 씬마다 물리 세계 분리: 다른 씬의 오브젝트끼리 충돌·판정 없음
- 같은 씬의 네트워크 오브젝트만 그 씬에 있는 클라이언트에 전송 (`SceneInterestManagement`)
- 접속한 플레이어를 로비 시작 위치에 생성
- 게임 중에 생기고 사라지는 네트워크 오브젝트의 공용 생성·삭제 함수 (`NetworkSpawner`)
- 하지 않는 일
  - 장소 사이 이동, 도착 위치, 로딩 화면: 「장소 사이 이동하기」 이슈
  - 파티별 레이드 던전 생성, 성공·실패 판정: 「파티별 레이드 던전 만들고 성공·실패 판정하기」 이슈

## 동작 규칙

### 씬 구성
- 기준 씬 `Base`: `NetworkManager`(`InvokeNetworkManager`), `Camera`. Build Profiles 0번. 서버·클라이언트 모두 계속 열려 있음
- 내용 씬 `Lobby`, `HuntingGround`, `TrainingRoom`, `RaidDungeon`: `Grid`(Ground·Platform 타일맵), `PlayerStartPosition`(`NetworkStartPosition`)
  - 카메라, NetworkManager, EventSystem, 빛 없음 (기준 씬 위에 겹쳐 불러오므로 넣으면 여러 개가 됨)
- 서버
  - 시작 시 로비·사냥터·연습장을 한 번씩 불러옴 (Additive, `LocalPhysicsMode.Physics2D`)
  - 레이드 던전은 요청마다 같은 씬을 한 번 더 불러옴. 이름이 모두 같으므로 `Scene` 값으로 구분
- 클라이언트
  - 서버가 알려 준 씬만 불러옴 (자기 플레이어가 있는 씬)
  - 물리 분리 없이 기본 물리 세계에 불러옴

### 판정 (물리 세계)
- `Physics2D.OverlapBox`, `Physics2D.Raycast`, `Physics2D.OverlapCircleAll` 등 `Physics2D`의 판정 함수: 기준 씬의 물리 세계만 검사. 서버에서는 내용 씬의 오브젝트를 찾지 못함
- 판정 방법: 오브젝트가 속한 씬의 물리 세계(`gameObject.scene.GetPhysicsScene2D()`)에 질의
  - 예: `gameObject.scene.GetPhysicsScene2D().OverlapBox(center, size, 0f, filter, results)`
  - 서버·클라이언트·호스트 모두 같은 결과
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
| `FixedPlaces` | `string[]` | 서버가 시작할 때 한 번씩 불러오는 장소 (로비, 사냥터, 연습장) |

### `InvokeNetworkManager`

`Base` 씬의 NetworkManager. `NetworkManager.singleton as InvokeNetworkManager`로 가져온다. 모든 함수는 서버에서만 부른다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `PutPlayerInScene(NetworkConnectionToClient conn, GameObject player, Scene scene)` | 플레이어의 연결, 플레이어 오브젝트, 넣을 씬 | - | 서버: 플레이어를 그 씬으로 이동. 클라이언트: 그 씬을 불러오라고 알림. 위치 변경, 이전 씬 내리기는 하지 않음 |
| `LoadRaidDungeon(Action<Scene> onLoaded)` | 다 불러온 뒤 부를 함수 | - | 레이드 던전 씬을 한 번 더 불러옴. 다 불러오면 `onLoaded`에 그 `Scene` 값을 넘김. 요청은 한 번에 하나씩 처리 |
| `UnloadRaidDungeon(Scene scene)` | `LoadRaidDungeon`으로 받은 씬 | - | 그 레이드 던전 씬을 내림. 던전 안의 플레이어는 부르기 전에 다른 씬으로 옮겨야 함 (남아 있으면 씬과 함께 사라짐) |

## 맞춘 결과
- 김도훈에게 알릴 것
  - `ContactDamage.cs`의 `Physics2D.OverlapBox`: 서버에서 내용 씬의 플레이어를 찾지 못함. 자기 씬의 물리 세계에 질의 필요
  - `ProjectileAttack.cs`의 투사체 생성, `MonsterProjectile.cs`·`Monster.cs`의 삭제: 네트워크를 붙일 때 `NetworkSpawner.Spawn` / `Despawn` 사용
- 박진우에게 알릴 것
  - `SlotCaster.cs`의 대상 찾기(`Physics2D.OverlapCircleAll`): 서버에서 내용 씬의 대상을 찾지 못함. 자기 씬의 물리 세계에 질의 필요
  - `PlayerMovement.cs`의 바닥 판정·발판 내려가기: 내 캐릭터(클라이언트)에서만 실행하므로 전용 서버에서는 영향 없음. 호스트 모드에서는 바닥을 찾지 못함 (자기 씬의 물리 세계에 질의하면 해결)
  - 소환검 생성·삭제: `NetworkSpawner.Spawn` / `Despawn` 사용
