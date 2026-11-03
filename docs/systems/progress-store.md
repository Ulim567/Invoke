# 진행도 저장소 (서버 DB)

- 담당자: 이우림

## 하는 일
- 서버가 캐릭터의 진행도를 SQLite DB에 저장하고 불러온다. 서버를 껐다 켜도 저장한 값이 그대로 남는다.
- 저장하는 것: 레벨, 경험치, 돈, 재료, 스킬 트리 해금 목록, 슬롯 8개, 사용자 정의 함수, 전역 변수
- 클라이언트는 DB에 직접 접근하지 않는다. 클라이언트가 필요한 값은 서버가 보낸다.
- 하지 않는 일
  - 레벨업 규칙, 경험치 표: 레벨 시스템 이슈
  - 노드 해금, 재료 빼기: 「재료 써서 스킬 트리 해금하기」
  - 함수 내용 검사: 「만든 함수를 서버로 보내 검사·저장하기」

## 동작 규칙

### 정한 기획
| 항목 | 결정 |
| --- | --- |
| 진행도의 주인 | 캐릭터. 돈·재료·레벨·경험치 모두 캐릭터 key에 붙음. 새 언어 캐릭터는 0부터 |
| 레벨·경험치 | 레벨 + 현재 레벨 안의 경험치를 함께 저장. 경험치 표를 바꿔도 레벨 유지 |
| 스킬 트리 해금 | 모든 노드를 캐릭터마다 따로 엶 (공통 노드, 문법 노드 포함). 캐릭터마다 트리가 같다는 보장이 없음 |
| 시작 장소 | 항상 로비. 장소·위치는 저장하지 않음 |
| 새 캐릭터 처음 값 | 레벨 1, 경험치 0, 돈 0, 재료·함수·전역 변수 없음 |
| 새 캐릭터 처음 해금 | `SkillTree.StartingNodes()` + `SkillTree.AutoUnlockedBy` |
| 새 캐릭터 처음 슬롯 | 처음 해금 목록의 스킬 노드를 순서대로 슬롯 1부터. 나머지는 `Empty`. 이미 만든 캐릭터는 그대로 |
| 저장 시점 | 값이 바뀔 때마다 서버가 바로 DB에 씀 |
| 함수·슬롯·전역 변수의 원본 | 서버 DB만. 플레이어 PC에는 저장하지 않음 |
| 예외 | 에디터 「로그인 건너뛰기」 임시 계정은 DB를 쓰지 않고 지금처럼 PC 파일을 씀 |

### DB
- 파일: `Accounts.db` (#174의 `AccountStore`와 같은 파일)
- 코드: `ProgressStore` (서버·에디터만, `#if UNITY_SERVER || UNITY_EDITOR`). 진행도 테이블 6개만 다룸
  - `AccountStore`가 연 SQLite 연결 하나를 함께 씀. 그래서 캐릭터를 만들 때 `Characters` row와 진행도 처음 row를 한 트랜잭션으로 넣을 수 있음
  - `AccountStore.CreateCharacter`가 트랜잭션 안에서 `ProgressStore`의 처음 row 넣기를 부름
- 테이블: 종류마다 하나. 모두 캐릭터 key(`Characters.Key`)로 캐릭터를 가리킴
- 스킬 트리의 모양(선행 조건)은 `SkillTree` 에셋에 있음. DB는 해금한 노드 ID만 저장

| 테이블 | row 하나에 담는 것 | 캐릭터 하나당 row 수 |
| --- | --- | --- |
| `CharacterProgress` | 레벨, 현재 레벨 안의 경험치, 돈 | 1개 |
| `CharacterMaterials` | 재료 ID, 개수 | 가진 재료 종류 수 |
| `CharacterUnlocks` | 해금한 노드 ID | 해금한 노드 수 |
| `CharacterSlots` | 슬롯 번호(0~7), 종류(`Empty`/`UserFunction`/`Skill`), ID | 8개 |
| `CharacterFunctions` | 함수 GUID, 함수 데이터 JSON | 만든 함수 수 |
| `CharacterGlobals` | 전역 변수 JSON | 1개 |

#### `CharacterProgress`
| column | 타입 | 규칙 |
| --- | --- | --- |
| `CharacterKey` | 문자열 | 기본 키. `Characters.Key`와 같은 값 |
| `Level` | 정수 | 처음 값 1 |
| `Exp` | 정수 | 현재 레벨 안의 경험치. 처음 값 0. 레벨업하면 0부터 |
| `Money` | 정수 | 처음 값 0 |

- 캐릭터 하나에 정확히 row 하나
- 캐릭터를 만들 때 `Characters` row와 한 트랜잭션으로 함께 넣음
- `Characters`와 나눈 이유: `Characters`는 로그인·캐릭터 선택용, `CharacterProgress`는 게임 중 바뀌는 값

#### `CharacterMaterials`
| column | 타입 | 규칙 |
| --- | --- | --- |
| `Id` | 정수 | 기본 키. SQLite가 자동으로 붙임 |
| `CharacterKey` | 문자열 | `Characters.Key` |
| `MaterialId` | 문자열 | 재료 ID. 예: `bug_fragment` |
| `Count` | 정수 | 가진 개수. 1 이상 |

- (`CharacterKey`, `MaterialId`) unique: 같은 캐릭터에 같은 재료 row는 하나
- row가 없으면 0개. 개수가 0이 되면 row를 지움
- 새 캐릭터는 row 없음
- 재료 ID는 한 번 정하면 바꾸지 않음 (DB에 ID로 저장되기 때문)

#### `CharacterUnlocks`
| column | 타입 | 규칙 |
| --- | --- | --- |
| `Id` | 정수 | 기본 키. SQLite가 자동으로 붙임 |
| `CharacterKey` | 문자열 | `Characters.Key` |
| `NodeId` | 문자열 | 해금한 노드 ID. 예: `syntax.if`, `c.thrust` |

- (`CharacterKey`, `NodeId`) unique: 같은 캐릭터에 같은 노드 row는 하나
- 문법 노드(`syntax.이름`)와 스킬 노드(사전 정의 함수 ID, 예: `c.thrust`)를 한 테이블에 저장. ID 앞부분이 달라 겹치지 않음. 종류는 `SkillTree.TryGet`의 `Kind`
- 선행 조건(순서)은 DB에 저장하지 않음. 해금할 때 서버가 `SkillTree.CanUnlock`으로 검사한 뒤 row를 넣음
- 캐릭터를 만들 때 처음 해금 노드의 row를 `Characters` row와 한 트랜잭션으로 함께 넣음
- row를 넣기만 하고 지우지 않음 (해금 취소 없음)
- `SkillTree` 에셋에 없는 노드 ID의 row는 서버가 불러올 때 건너뜀

#### `CharacterSlots`
| column | 타입 | 규칙 |
| --- | --- | --- |
| `Id` | 정수 | 기본 키. SQLite가 자동으로 붙임 |
| `CharacterKey` | 문자열 | `Characters.Key` |
| `SlotIndex` | 정수 | 슬롯 번호 0~7 (Q W E R A S D F 순서) |
| `Kind` | 문자열 | `SlotLoadout.EntryKind`의 이름: `Empty`, `UserFunction`, `Skill` |
| `EntryId` | 문자열 | `UserFunction`이면 함수 GUID, `Skill`이면 사전 정의 함수 ID, `Empty`면 빈 문자열 |

- (`CharacterKey`, `SlotIndex`) unique: 같은 캐릭터에 같은 슬롯 row는 하나
- 캐릭터 하나에 늘 row 8개. 캐릭터를 만들 때 `Empty` 슬롯 포함 row 8개를 `Characters` row와 한 트랜잭션으로 함께 넣음
- 장착을 바꾸면 그 슬롯 row만 고침. 옮기기·바꾸기(`Swap`)처럼 두 슬롯이 바뀌면 두 row를 한 트랜잭션으로 고침
- `Kind`를 숫자가 아니라 이름으로 저장하는 이유: `EntryKind`의 순서가 바뀌어도 저장한 값이 다른 종류로 읽히지 않음

#### `CharacterFunctions`
| column | 타입 | 규칙 |
| --- | --- | --- |
| `Id` | 정수 | 기본 키. SQLite가 자동으로 붙임 |
| `CharacterKey` | 문자열 | `Characters.Key` |
| `FunctionGuid` | 문자열 | 함수의 GUID (`FunctionData.Guid`) |
| `Json` | 문자열 | 함수 데이터 JSON (`FunctionDataJson.ToJson`) |

- (`CharacterKey`, `FunctionGuid`) unique: 같은 캐릭터에 같은 함수 row는 하나
- 함수 하나를 저장하면 그 함수 row만 넣거나 덮어씀
- 함수 이름, 블록 등은 column으로 나누지 않고 JSON 안에 둠. 서버가 DB에서 함수 내용으로 찾아볼 일이 없음
- 불러올 때 `FunctionDataJson.TryFromJson`이 실패한 row는 건너뛰고 로그를 남김
- 새 캐릭터는 row 없음
- 개수 제한: 캐릭터마다 최대 개수 고정 (임시 5). 제한 숫자는 DB가 아니라 서버·클라이언트가 함께 읽는 한 곳에 둠
  - 서버: 새 함수 저장 요청 때 row 수를 세어, 넘으면 거부하고 이유를 알림. 이미 있는 함수를 고쳐 저장하는 것은 거부하지 않음
  - 클라이언트: 편집 화면이 먼저 검사해 안내를 보여 줌 (서버도 다시 검사)
- 함수 지우기: 지금 `FunctionStorage`에 없음. 생기면 그 row를 지우고, 그 함수를 장착한 `CharacterSlots` row를 `Empty`로 한 트랜잭션에서 고침

#### `CharacterGlobals`
| column | 타입 | 규칙 |
| --- | --- | --- |
| `CharacterKey` | 문자열 | 기본 키. `Characters.Key`와 같은 값 |
| `Json` | 문자열 | 전역 변수 데이터 JSON (`GlobalVariablesData`를 `FunctionDataJson.ToJson`으로 바꾼 것) |

- 캐릭터 하나에 정확히 row 하나
- 저장하는 것은 선언(이름, 타입, 초기값)만. 실행 중 바뀐 값은 `FunctionExecutor`의 데이터 영역(메모리)에만 있고 저장하지 않음 (초기화 때 초기값으로 돌아감)
- 캐릭터를 만들 때 전역 변수가 없는 JSON으로 `Characters` row와 한 트랜잭션으로 함께 넣음
- 전역 변수를 저장하면 JSON 전체를 덮어씀. 편집 화면이 전역 변수를 한꺼번에 저장하기 때문 (`FunctionExecutor.TrySaveGlobalVariables`)
- 불러올 때 `FunctionDataJson.TryGlobalVariablesFromJson`이 실패하면 전역 변수 없음으로 보고 로그를 남김

### 불러온 데이터 (`CharacterProgressData`)
- 「들어가기」 때 `ProgressStore.Load(캐릭터 key)`가 테이블 6개를 읽어 돌려주는 객체. SQLite를 모르는 일반 C# 클래스

| 이름 | 타입 | 내용 |
| --- | --- | --- |
| `Level`, `Exp`, `Money` | `int` | `CharacterProgress` |
| `Materials` | `Dictionary<string, int>` | 재료 ID → 개수. 없는 재료는 0 |
| `Unlocked` | `List<string>` | 해금한 노드 ID. `SkillTree`에 넘기는 해금 목록 |
| `Slots` | `SlotLoadout.Entry[8]` | 슬롯 0~7 |
| `Functions` | `List<FunctionData>` | 읽기에 성공한 함수 |
| `Globals` | `GlobalVariablesData` | 전역 변수 선언 |

- 진행도 row가 없는 캐릭터(이 이슈 전에 만든 캐릭터): `Load`가 새 캐릭터와 같은 처음 row를 넣은 뒤 돌려줌

### 「들어가기」 흐름
1. `AccountServer`: 캐릭터를 찾고 `session.Enter(캐릭터 key)` 뒤 `ProgressStore.Load(캐릭터 key)`. `CharacterProgressData`를 `AccountSession`에 넣어 둠
2. `InvokeNetworkManager.SpawnPlayer`: 지금처럼 로비에 플레이어를 만듦 (장소 씬을 기다리는 동안 `CharacterProgressData`는 `AccountSession`에 있음)
3. 플레이어를 만든 직후 서버가 `CharacterProgressData`의 값을 각 컴포넌트에 넣음
   - `Slots`, `Functions`, `Globals` → `FunctionExecutorNetwork.ApplyPlayerData` → `IsDataReady`, 로딩 화면 닫힘
   - `Level`, `Exp`, `Money`, `Materials`, `Unlocked` → `PlayerProgress.ApplyLoaded`
- 클라이언트는 함수·슬롯·전역 변수를 보내지 않음
- 「로그인 건너뛰기」 임시 계정: `Load`를 부르지 않음. 지금처럼 클라이언트가 PC 파일을 보냄

### 진행도 컴포넌트 (`PlayerProgress`)
- 플레이어 프리팹 루트에 붙음. 레벨, 경험치, 돈, 재료, 해금 목록을 가짐
  - 지금 붙은 곳: `Player_UL` (Base 씬 NetworkManager의 Player Prefab). 공용 `Player.prefab`에는 실행기 컴포넌트가 없고, `Player_JW`·`Player_HW`가 실행기 컴포넌트를 따로 더해 두어 공용에 붙이면 중복됨 → 공용으로 합치기는 별도 이슈
- `Changed`: 서버(호스트 포함)는 값을 바꾼 함수가, 클라이언트 전용은 동기화 콜백이 부름 (한 번만 불림)
- 「노드 해금」(`TryUnlock`)은 아직 없음
- 이 값들은 `PlayerProgress`만 바꿈 (레포 규칙 2). 다른 시스템은 아래 함수를 부름
- 값을 바꾸는 함수는 서버에서만 동작. 값을 바꾼 직후 `ProgressStore`로 DB에 씀
- 임시 계정은 DB에 쓰지 않음 (값은 메모리에만)

| 함수 | 하는 일 | 부를 시스템 |
| --- | --- | --- |
| `ApplyLoaded(CharacterProgressData)` | 불러온 값을 넣음. DB에 쓰지 않음 | 서버 (`EnterGameRequest` 처리) |
| `AddExp(int)` | 경험치 더함 | 몬스터 처치 (김도훈) |
| `AddMoney(int)`, `TrySpendMoney(int)` | 돈 더하기, 빼기. 모자라면 빼지 않고 `false` | 드롭 (김도훈), 상점 |
| `AddMaterial(string, int)` | 재료 더함 | 드롭 (김도훈) |
| `TryUnlock(string 노드 ID)` | 선행 조건·재료 검사 → 재료 빼기 + 노드·같이 열리는 노드 추가를 한 트랜잭션으로 | 스킬 트리 해금 (임혜원, 「재료 써서 스킬 트리 해금하기」) |
| `Level`, `Exp`, `Money`, `GetMaterialCount(string)`, `Unlocked` | 값 읽기 | 화면, 스킬 트리 |

- 레벨업 규칙(`AddExp` 안에서 레벨을 올리는 일): 레벨 시스템 이슈. 이번 이슈는 경험치만 더함
- `TryUnlock`의 검사·화면 연결: 「재료 써서 스킬 트리 해금하기」. 이번 이슈는 `ProgressStore`에 재료와 해금을 한 트랜잭션으로 쓰는 함수까지

### 클라이언트에 진행도 보내기 (`PlayerProgress`)
- Mirror 동기화로 보냄. 서버가 값을 바꾸면 Mirror가 클라이언트에 같은 값을 맞춤
  - `Level`, `Exp`, `Money`: `[SyncVar]`
  - `Materials`: `SyncDictionary<string, int>`
  - `Unlocked`: `SyncList<string>`
- Sync Mode: `Owner`. 그 플레이어의 소유 클라이언트에만 보냄. 다른 플레이어의 클라이언트는 받지 않음
- 클라이언트에서 값이 바뀌면 `PlayerProgress.Changed` (event)가 불림. 화면은 이것을 받아 다시 그림
- 처음 값: `ApplyLoaded`가 플레이어를 만든 직후 값을 넣으므로, 소유 클라이언트는 첫 동기화 때 DB의 값을 받음

### 함수·슬롯·전역 변수 주고받기 (`FunctionExecutorNetwork`)
- 서버 → 소유 클라이언트 (플레이어를 만든 직후, `ApplyPlayerData` 뒤): 함수 JSON 목록, 슬롯 8개, 전역 변수 JSON (`[TargetRpc]`). 클라이언트는 받은 데이터로 편집 화면·장착 화면을 그림
- 소유 클라이언트 → 서버 (`[Command]`), 서버는 처리 뒤 결과를 소유 클라이언트에 보냄 (`[TargetRpc]`)

| 요청 함수 (소유 클라이언트) | 서버가 하는 일 | 거부 |
| --- | --- | --- |
| `RequestSaveFunction(FunctionData)` | JSON 읽기 → 새 함수면 개수 제한(`ProgressRules.MaxFunctions`) 검사 → `CharacterFunctions`에 씀 → 슬롯 원본을 다시 만듦 | `InvalidData`, `TooManyFunctions` |
| `RequestEquipFunction(slot, guid)`, `RequestEquipSkill(slot, id)`, `RequestUnequip(slot)`, `RequestSwap(a, b)` | `SlotLoadout`과 같은 규칙(같은 함수·스킬은 한 슬롯에만)으로 바꿈 → 바뀐 슬롯의 `CharacterSlots` row를 한 트랜잭션으로 씀 → 슬롯 원본을 다시 만듦 | `InvalidSlot`, `UnknownFunction`, `NotEquippable` |
| `RequestSaveGlobals(GlobalVariablesData)` | `FunctionExecutor.CanUseGlobalVariables`로 검사 → `CharacterGlobals`에 씀 → 프로세스를 새로 시작 | `InvalidData`, `GlobalsRejected` |

- 결과: `PlayerDataRequestAnswered(요청 종류, 결과)`. 성공(`Saved`)이면 그보다 먼저 `ReceivedSlots`·`ReceivedFunctions`·`ReceivedGlobals`가 새 값으로 바뀌고 `PlayerDataReceived`가 불림
- 거부되면 `ReceivedXxx`는 그대로. 서버 DB를 쓰지 않는 플레이어(임시 계정, 네트워크를 켜지 않은 테스트)의 요청은 `NotAllowed`
- 장착할 때 해금 여부는 아직 보지 않음 (정할 것)
- 「로그인 건너뛰기」 임시 계정: 지금처럼 PC 파일
- 함수 내용 검사(구조, 해금한 블록만 썼는지): 「만든 함수를 서버로 보내 검사·저장하기」. 이번 이슈는 JSON 읽기와 개수 제한까지

### `ProgressStore` 함수 (서버)
- 부르는 곳: `AccountStore`, `AccountServer`, `PlayerProgress`, `FunctionExecutorNetwork`만. 다른 시스템은 `PlayerProgress`를 부름

| 함수 | 하는 일 | 테이블 |
| --- | --- | --- |
| `CreateInitial(캐릭터 key)` | 새 캐릭터의 처음 row를 넣음. `AccountStore.CreateCharacter`의 트랜잭션 안에서 부름 | Progress, Unlocks, Slots, Globals |
| `Load(캐릭터 key)` | `CharacterProgressData`를 돌려줌. 진행도 row가 없으면 `CreateInitial` 뒤 돌려줌 | 6개 모두 |
| `SaveProgress(캐릭터 key, 레벨, 경험치, 돈)` | row 하나를 덮어씀 | Progress |
| `SetMaterialCount(캐릭터 key, 재료 ID, 개수)` | 넣거나 고침. 0이면 row를 지움 | Materials |
| `SaveUnlock(캐릭터 key, 노드 ID 목록, 재료 개수 목록)` | 노드 row 추가와 재료 개수 변경을 한 트랜잭션으로 | Unlocks, Materials |
| `SaveSlots(캐릭터 key, 슬롯 번호와 `SlotLoadout.Entry` 목록)` | 바뀐 슬롯 row를 한 트랜잭션으로 | Slots |
| `CountFunctions(캐릭터 key)` | 함수 row 수 (개수 제한 검사) | Functions |
| `HasFunction(캐릭터 key, 함수 GUID)` | 그 함수 row가 있는지 (새 함수인지 볼 때) | Functions |
| `SaveFunction(캐릭터 key, 함수 GUID, JSON)` | 넣거나 덮어씀 | Functions |
| `SaveGlobals(캐릭터 key, JSON)` | row 하나를 덮어씀 | Globals |

- 표의 테이블 이름은 앞의 `Character`를 뺀 것 (예: Progress = `CharacterProgress`)

### 테스트용 데이터 넣기
- 서버 DB는 개발 중 주기적으로 지움. 그래서 원하는 진행도(JSON)를 캐릭터의 DB에 한 번에 넣는 테스트용 기능을 둠
- 컴포넌트: `ProgressTestData`. 진행도 JSON 칸(`Json`)과 언어 칸(`Language`, 기본 C)을 가짐. Base 씬의 오브젝트에 붙임
- 사용: 에디터에서 로그인 → 캐릭터 화면에서 Inspector ⋮ → 「진행도 넣기」 → 「들어가기」
  - `InvokeAuthenticator.RequestProgressTestData(언어, JSON)` → `ProgressTestDataRequest` → `AccountServer.HandleProgressTestData` → `ProgressStore.ReplaceAll`
  - 서버가 그 캐릭터의 진행도 테이블 6개를 한 트랜잭션으로 모두 바꿈. 결과는 `ProgressTestDataResult` (Console에 로그)
  - 거부: 테스트 허용 설정이 꺼짐, 임시 계정, 이미 들어감, 그 언어 캐릭터가 없음, 읽을 수 없는 JSON
- JSON 형식: `CharacterProgressData`를 `FunctionDataJson.ToJson`으로 바꾼 모양 (Level, Exp, Money, Materials, Unlocked, Slots, Functions, Globals)
  - ⋮ → 「처음 값 JSON 채우기」: 새 캐릭터와 같은 빈 모양을 채움
  - Slots: 모자라면 빈 슬롯으로 8개를 채움. Kind는 `Empty`, `UserFunction`, `Skill`
  - Functions: `ProgressRules.MaxFunctions`개까지만
- 서버의 테스트 허용 설정: `InvokeAuthenticator`의 Allow Test Progress Data (기본 켜짐). 서버는 요청이 에디터에서 왔는지 알 수 없으므로 서버에서 막음. 실제 서비스 서버에서는 끔

## 다른 시스템이 쓰는 것
- `PlayerProgress`: 위 「진행도 컴포넌트」 표 (레벨·경험치·돈·재료·해금 목록 읽기와 바꾸기)
- `FunctionExecutorNetwork`의 저장 요청 함수: 위 「함수·슬롯·전역 변수 주고받기」 표, [network.md](network.md)
- `ProgressRules.MaxFunctions`: 함수 최대 개수. 편집 화면이 저장 전에 먼저 안내할 때 씀

## 정할 것
- 장착할 때 해금한 스킬만 넣게 할지 (`PlayerProgress.Unlocked`로 서버가 검사)
- `FunctionStorage`(임혜원)·`SlotLoadout`(박진우)이 PC 파일 대신 위 요청 함수를 부르게 바꾸는 일
- 공용 `Player.prefab`에 실행기 컴포넌트(`FunctionExecutor`, `LanguageRule`, `ProcessMemory`, `SlotLoadout`, `FunctionExecutorNetwork`)와 `PlayerProgress`를 합치는 일: 박진우·임혜원의 Variant에서 중복 컴포넌트를 지우는 일과 함께 별도 이슈

## 맞춘 결과
- 임혜원과 확인할 것
  - 스킬 트리 공통 노드(대시, 더블점프)는 캐릭터마다 따로 엶 (skill-tree.md 「정해야 할 것」)
  - `FunctionStorage`가 PC 파일 대신 서버로 함수를 보내도록 바뀜
- 박진우와 확인할 것
  - `SlotLoadout`이 PC 파일 대신 서버로 장착 내용을 보내도록 바뀜
  - 실제 캐릭터의 처음 슬롯은 서버가 정함. `defaultSkillIds`를 쓸 곳
