# 진행도 저장소 (서버 DB)

- 담당자: 이우림

## 하는 일
- 서버가 캐릭터의 진행도를 SQLite DB에 저장하고 불러온다. 서버를 껐다 켜도 저장한 값이 그대로 남는다.
- 저장하는 것: 레벨, 경험치, 돈, 재료, 스킬 트리 해금 목록, 슬롯 8개, 사용자 정의 함수, 전역 변수
- 클라이언트는 DB에 직접 접근하지 않는다. 클라이언트가 필요한 값은 서버가 보낸다.
- 하지 않는 일
  - 레벨업 규칙, 경험치 표: `LevelRules`(김도훈, `player-stats.md`). `AddExp`가 이 규칙으로 레벨을 올린다
  - 해금 버튼, 해금 목록·재료·실패 이유 화면: 「재료 써서 스킬 트리 해금하기」 (#294, 임혜원)
  - 함수 구조 검사: 「만든 함수를 서버로 보내 검사·저장하기」 (해금 검사와 코드 영역 한도 검사는 #306에서 서버가 저장 요청 때 함)

## 동작 규칙

### 정한 기획
| 항목 | 결정 |
| --- | --- |
| 진행도의 주인 | 캐릭터. 돈·재료·레벨·경험치 모두 캐릭터 key에 붙음. 새 언어 캐릭터는 0부터 |
| 레벨·경험치 | 레벨 + 현재 레벨 안의 경험치를 함께 저장. 경험치 표를 바꿔도 레벨 유지 |
| 스킬 트리 해금 | 모든 노드를 캐릭터마다 따로 엶 (공통 노드, 문법 노드 포함). 캐릭터마다 트리가 같다는 보장이 없음 |
| 시작 장소 | 항상 로비. 장소·위치는 저장하지 않음 |
| 새 캐릭터 처음 값 | 레벨 1, 경험치 0, 돈 0, 재료·페이지 함수·전역 변수 없음 |
| 새 캐릭터 처음 해금 | `SkillTree.StartingNodes()` + `SkillTree.AutoUnlockedBy` |
| 슬롯 | 슬롯 8개는 언제나 그 슬롯의 슬롯 함수 하나. 빈 슬롯 없음, 장착(다른 함수·C 스킬을 슬롯에 넣기) 없음. C 스킬은 슬롯 함수 function body 안의 블록 |
| 슬롯 함수 | 캐릭터를 만들 때 서버가 8개를 만듦. 이름 `slot1`~`slot8` 고정, 파라미터 없음, 반환 타입 `Void`, 비동기 아님. 이름·파라미터·반환 타입·비동기 여부 변경 불가. function body만 바꿈(빈 본문 가능) |
| 새 캐릭터 처음 슬롯 함수 | `slot1` = `thrust(GetBug());`, `slot2` = `sword_throw(GetBug());`, `slot3`~`slot8` = 빈 본문 (`CharacterProgressData.NewSlotFunction`) |
| 페이지 함수 | 슬롯에 걸리지 않은 함수. 편집 화면의 +로 추가, 최대 `ProgressRules.MaxPageFunctions`개(임시 5). 이름·파라미터 변경 가능 |
| 빈 본문 슬롯 시전 | 그대로 시전됨 (stack 1칸). 서버가 빈 본문을 따로 검사하지 않음 |
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
| `CharacterSlots` | 슬롯 번호(0~7), 종류(`UserFunction`), 슬롯 함수 GUID | 8개 |
| `CharacterFunctions` | 함수 GUID, 함수 데이터 JSON | 슬롯 함수 8개 + 페이지 함수 수 |
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
| `Kind` | 문자열 | `SlotLoadout.EntryKind`의 이름. 실제 계정은 늘 `UserFunction` |
| `EntryId` | 문자열 | 그 슬롯의 슬롯 함수 GUID (`CharacterFunctions.FunctionGuid`) |

- (`CharacterKey`, `SlotIndex`) unique: 같은 캐릭터에 같은 슬롯 row는 하나
- 캐릭터 하나에 늘 row 8개. 캐릭터를 만들 때 슬롯 함수 8개의 `CharacterFunctions` row와 함께 `Characters` row와 한 트랜잭션으로 넣음
- 만든 뒤 고치지 않음 (장착 없음, 슬롯 함수 GUID 고정)
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
- 새 캐릭터는 슬롯 함수 row 8개
- 슬롯 함수와 페이지 함수를 한 테이블에 저장. 구분은 `CharacterSlots`에 GUID가 있는지 (있으면 슬롯 함수)
- 개수 제한: 페이지 함수만 최대 `ProgressRules.MaxPageFunctions`개 (임시 5). 슬롯 함수 8개는 세지 않음. 제한 숫자는 DB가 아니라 서버·클라이언트가 함께 읽는 한 곳에 둠
  - 서버: 새 GUID 저장 요청을 페이지 함수로 보고, 페이지 함수 row 수(`CountPageFunctions`)가 제한이면 거부하고 이유를 알림. 이미 있는 함수(슬롯 함수 포함)를 고쳐 저장하는 것은 거부하지 않음
  - 클라이언트: 편집 화면이 먼저 검사해 안내를 보여 줌 (서버도 다시 검사)
- 슬롯 함수의 이름·파라미터·반환 타입·비동기 여부를 바꾼 저장 요청: 서버가 거부(`SlotFunctionLocked`). 편집 화면도 바꾸지 못하게 막음
  - 이름: `ProgressRules.SlotFunctionName(슬롯 번호)`와 다르면 거부
  - 파라미터: 하나라도 있으면 거부
  - 반환 타입(`ReturnType`): `Void`가 아니면 거부 (비어 있으면 `Void`로 봄)
  - 비동기 여부(`IsAsync`): true면 거부
- 함수 지우기: 지금 없음. 생기면 페이지 함수만 지울 수 있음 (슬롯 함수는 지우지 않음)

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
| `Slots` | `SlotLoadout.Entry[8]` | 슬롯 0~7. 각 슬롯의 슬롯 함수 GUID |
| `Functions` | `List<FunctionData>` | 읽기에 성공한 함수 (슬롯 함수 + 페이지 함수) |

- 함께 쓰는 함수: `NewSlotFunction(슬롯 번호)` (처음 슬롯 함수 하나), `FillInitialSlotFunctions()` (처음 슬롯 함수 8개를 `Functions`·`Slots`에 넣음)
| `Globals` | `GlobalVariablesData` | 전역 변수 선언 |

- 진행도 row가 없는 캐릭터(이 이슈 전에 만든 캐릭터): `Load`가 새 캐릭터와 같은 처음 row를 넣은 뒤 돌려줌
  - `CreateInitial`은 없는 row만 넣음. 진행도 row만 빠진 캐릭터는 함수·재료·슬롯·해금을 그대로 두고 레벨 1부터. 슬롯 row가 빠진 슬롯에만 새 슬롯 함수를 만듦

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
  - 붙은 곳: 공용 `Player.prefab` (`Player_UL`, `Player_DH`, `Player_JW`, `Player_HW`가 상속). Base 씬 NetworkManager의 Player Prefab은 공용 `Player.prefab`
  - 같은 오브젝트의 `PlayerLevelStats`(김도훈)도 공용 `Player.prefab`에 붙음
- `Changed`: 서버(호스트 포함)는 값을 바꾼 함수가, 클라이언트 전용은 동기화 콜백이 부름 (한 번만 불림)
- 이 값들은 `PlayerProgress`만 바꿈 (레포 규칙 2). 다른 시스템은 아래 함수를 부름
- 값을 바꾸는 함수는 서버에서만 동작. 값을 바꾼 직후 `ProgressStore`로 DB에 씀
- 임시 계정은 DB에 쓰지 않음 (값은 메모리에만)

| 함수 | 하는 일 | 부를 시스템 |
| --- | --- | --- |
| `ApplyLoaded(CharacterProgressData)` | 불러온 값을 넣음. DB에 쓰지 않음 | 서버 (`EnterGameRequest` 처리) |
| `AddExp(int)` | 경험치 더함. 다음 레벨 경험치(`LevelRules.ExpToNext`)를 채우면 레벨을 올리고 남은 경험치를 넘김. 최대 레벨에서는 무시 | 몬스터 처치 (김도훈) |
| `LeveledUp` (event) | [로직] 레벨이 오른 순간 (새 레벨). 서버에서만. 여러 레벨이 오르면 레벨마다 | 레벨업 연출·알림 |
| `AddMoney(int)`, `TrySpendMoney(int)` | 돈 더하기, 빼기. 모자라면 빼지 않고 `false` | 드롭 (김도훈), 상점 |
| `AddMaterial(string, int)` | 재료 더함 | 드롭 (김도훈) |
| `RequestUnlock(string 노드 ID)` | [소유 클라이언트] 서버에 노드 해금을 요청 (`[Command]`). 결과는 `UnlockAnswered`. 내 플레이어가 아니면 아무것도 하지 않음 | 스킬 트리 화면 (임혜원, #294) |
| `TryUnlock(string 노드 ID)` | [서버] 아래 「노드 해금 검사」 순서로 검사 → 재료 빼기 + 노드·같이 열리는 노드 추가를 `ProgressStore.SaveUnlock`으로 한 트랜잭션에 저장. `UnlockResult`를 돌려줌 | `RequestUnlock` |
| `UnlockAnswered(string 노드 ID, UnlockResult)` (event) | [표시] 서버가 처리한 해금 요청의 결과 (`[TargetRpc]`). 성공(`Unlocked`)도 알림. 소유 클라이언트에서 불림 | 스킬 트리 화면 (실패 이유 표시) |
| `Level`, `Exp`, `Money`, `GetMaterialCount(string)`, `Unlocked` | 값 읽기 | 화면, 스킬 트리 |

- 레벨업 (김도훈, 이우림 허락): `AddExp` 안에서 `LevelRules`(경험치 표, 최대 레벨)로 레벨을 올린다. 레벨에 따른 능력치는 `PlayerLevelStats`(김도훈)가 `PlayerStats.AddBonus`로 준다

#### 노드 해금 검사 (`TryUnlock`, #315)
- 먼저 걸린 이유 하나만 돌려줌

| 순서 | 검사 | 걸리면 |
| --- | --- | --- |
| 1 | `SkillTree.TryGet`에 노드가 있음 | `UnknownNode` |
| 2 | 해금 목록(`Unlocked`)에 아직 없음 | `AlreadyUnlocked` |
| 3 | `SkillTree.CanUnlock`: 선행 노드를 모두 해금함 | `PrerequisiteMissing` |
| 4 | 노드의 `Cost`마다 `GetMaterialCount`가 `Count` 이상 | `MaterialShort` |

- 통과하면 `ProgressStore.SaveUnlock`으로 DB에 먼저 한 번에 쓰고, 성공한 뒤에 메모리를 바꿈 (재료를 `Cost`만큼 빼고, 노드와 `SkillTree.AutoUnlockedBy`가 돌려준 같이 열리는 노드를 해금 목록에 넣음). 저장이 예외로 실패하면 메모리는 그대로
- `store`가 `null`인 플레이어(임시 계정)는 DB에 쓰지 않고 메모리의 값만 바꿈
- 화면 쪽(해금 버튼 연결, 목록·재료·실패 이유 표시)은 「재료 써서 스킬 트리 해금하기」(#294)

### 클라이언트에 진행도 보내기 (`PlayerProgress`)
- Mirror 동기화로 보냄. 서버가 값을 바꾸면 Mirror가 클라이언트에 같은 값을 맞춤
  - `Level`, `Exp`, `Money`: `[SyncVar]`
  - `Materials`: `SyncDictionary<string, int>`
  - `Unlocked`: `SyncList<string>`
- Sync Mode: `Owner`. 그 플레이어의 소유 클라이언트에만 보냄. 다른 플레이어의 클라이언트는 받지 않음
- 클라이언트에서 값이 바뀌면 `PlayerProgress.Changed` (event)가 불림. 화면은 이것을 받아 다시 그림
- 처음 값: `ApplyLoaded`가 플레이어를 만든 직후 값을 넣으므로, 소유 클라이언트는 첫 동기화 때 DB의 값을 받음

### 함수·슬롯·전역 변수 주고받기 (`FunctionExecutorNetwork`)
- 서버 → 소유 클라이언트 (플레이어를 만든 직후, `ApplyPlayerData` 뒤): 함수 JSON 목록, 슬롯 8개, 전역 변수 JSON (`[TargetRpc]`). 클라이언트는 받은 데이터로 편집 화면을 그림
  - 슬롯 i의 슬롯 함수: `ReceivedSlots[i].Id`가 GUID인 `ReceivedFunctions`의 함수. 나머지 함수는 페이지 함수
- 소유 클라이언트 → 서버 (`[Command]`), 서버는 처리 뒤 결과를 소유 클라이언트에 보냄 (`[TargetRpc]`)

| 요청 함수 (소유 클라이언트) | 서버가 하는 일 | 거부 |
| --- | --- | --- |
| `RequestSaveFunction(FunctionData)` | JSON 읽기 → 슬롯 함수 틀 검사 → 내용 검사(아래) → 새 GUID(페이지 함수)면 개수 제한(`ProgressRules.MaxPageFunctions`) 검사 → `CharacterFunctions`에 씀 → 슬롯 원본을 다시 만듦. 슬롯 함수·페이지 함수 모두 이 요청. 슬롯 함수는 이름·파라미터·반환 타입·비동기 여부가 그대로일 때만 | `InvalidData`, `TooManyFunctions`, `SlotFunctionLocked`, `BlockLocked`, `SkillLocked`, `CodeSizeExceeded` |

#### 함수 내용 검사 (`FunctionExecutorNetwork.CheckFunctionContent`, #306)
- 서버가 저장 요청의 함수를 `PlayerProgress.Unlocked`(해금한 노드 ID)로 검사. 네트워크와 상관없는 `static` 함수라 서버 없이 부를 수 있음
- 함수 안의 모든 블록(칸에 끼운 블록, 블록 영역 안까지, `FunctionData.AllBlocks()`)을 차례로 검사하고, 먼저 걸린 이유로 거부

| 검사 | 거부 | 규칙 |
| --- | --- | --- |
| 블록 종류 | `BlockLocked` | `SkillTree.IsBlockUnlocked(블록 종류, 해금 목록)`이 false. 어떤 노드에도 없는 블록 종류는 통과 |
| C 스킬 | `SkillLocked` | 함수 블록(`IsUserFunction`이 false)이 부르는 고정 ID의 노드가 `SkillTree`에 있는데 해금 목록에 없음. 스킬 트리에 노드가 없는 ID(입력 함수 `input.*`, 테스트 함수 `test.*`)는 통과 |
| 코드 영역 크기 | `CodeSizeExceeded` | 저장하는 함수를 반영한 함수 목록으로 `CodeSizeCalculator.Calculate(슬롯 함수 GUID 8개, 없음, 목록)`을 센 값이 `ProgressRules.MaxCodeSize`(임시 1000)보다 큼 |

- 스킬 노드 ID와 고정 ID는 같은 값이라 (`c.thrust`) 해금 목록에서 바로 찾음
- 해금 목록을 읽을 수 없으면(`PlayerProgress`가 없음) 검사할 수 없으므로 `NotAllowed`로 거부
- 크기는 슬롯 8개 전체를 셈 (`function-data.md` 「코드 영역 크기 규칙」). 슬롯 함수가 부르는 함수를 고쳐도 그 함수를 저장하는 순간 걸림. 슬롯에서 닿지 않는 함수는 세지 않으므로 거부하지 않음
- 이미 저장된 함수는 다시 검사하지 않음. 나중에 해금 규칙이 바뀌어도 저장된 함수는 그대로 남고, 그 함수를 다시 저장할 때 걸림
| `RequestSaveGlobals(GlobalVariablesData)` | `FunctionExecutor.CanUseGlobalVariables`로 검사 → `CharacterGlobals`에 씀 → 프로세스를 새로 시작 | `InvalidData`, `GlobalsRejected` |

- 장착을 바꾸는 요청: 없음 (슬롯 함수 GUID 고정)
- 결과: `PlayerDataRequestAnswered(요청 종류, 결과)`. 성공(`Saved`)이면 그보다 먼저 `ReceivedFunctions`·`ReceivedGlobals`가 새 값으로 바뀌고 `PlayerDataReceived`가 불림
- 거부되면 `ReceivedXxx`는 그대로. 서버 DB를 쓰지 않는 플레이어(임시 계정, 네트워크를 켜지 않은 테스트)의 요청은 `NotAllowed`
- 「로그인 건너뛰기」 임시 계정: 지금처럼 PC 파일 (`SlotLoadout`의 장착 그대로)
- `SlotLoadout` 연결 (#325): 실제 계정의 소유 클라이언트(호스트 포함)에서 `FunctionExecutorNetwork`가 `PlayerDataReceived` 때마다 박진우의 `SlotLoadout.SetEntries(ReceivedSlots, ReceivedFunctions)`를 부름
  - 이유: 서버의 시전은 서버의 슬롯 원본으로 되지만, 내 화면의 `SlotCaster`는 `SlotLoadout`을 보고 시전 요청 여부와 방식(바로, 대상 고르기)을 정함. 연결 전에는 `SlotLoadout`이 이 컴퓨터의 `SlotLoadout.json`·기본값이라 서버에 함수가 있어도 빈 슬롯은 시전되지 않고 방식이 어긋났음
  - 구독은 `OnStartLocalPlayer`(실제 계정)에서 하고 `PlayerDataReceived`의 다른 구독자(편집 화면, 장착 화면)보다 먼저 연결됨
- 함수 구조 검사: 「만든 함수를 서버로 보내 검사·저장하기」. 해금한 블록·스킬만 썼는지, 코드 영역 한도는 위 「함수 내용 검사」

### `ProgressStore` 함수 (서버)
- 부르는 곳: `AccountStore`, `AccountServer`, `PlayerProgress`, `FunctionExecutorNetwork`만. 다른 시스템은 `PlayerProgress`를 부름

| 함수 | 하는 일 | 테이블 |
| --- | --- | --- |
| `CreateInitial(캐릭터 key)` | 새 캐릭터의 처음 row(슬롯 함수 8개 포함)를 넣음. `AccountStore.CreateCharacter`의 트랜잭션 안에서 부름 | Progress, Unlocks, Slots, Functions, Globals |
| `Load(캐릭터 key)` | `CharacterProgressData`를 돌려줌. 진행도 row가 없으면 `CreateInitial` 뒤 돌려줌 | 6개 모두 |
| `SaveProgress(캐릭터 key, 레벨, 경험치, 돈)` | row 하나를 덮어씀 | Progress |
| `SetMaterialCount(캐릭터 key, 재료 ID, 개수)` | 넣거나 고침. 0이면 row를 지움 | Materials |
| `SaveUnlock(캐릭터 key, 노드 ID 목록, 재료 개수 목록)` | 노드 row 추가와 재료 개수 변경을 한 트랜잭션으로 | Unlocks, Materials |
| `CountPageFunctions(캐릭터 key)` | 슬롯에 걸리지 않은 함수 row 수 (개수 제한 검사). 읽을 수 없는 함수 row도 셈 | Slots, Functions |
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
  - ⋮ → 「처음 값 JSON 채우기」: 새 캐릭터와 같은 모양(슬롯 함수 8개 포함)을 채움
  - Slots: `Kind` `UserFunction`, `Id` 슬롯 함수 GUID. 그 GUID의 함수가 Functions에 없는 슬롯(빠진 슬롯, `Empty`·`Skill`, 다른 슬롯과 같은 GUID)은 새 캐릭터와 같은 슬롯 함수를 만듦
  - Functions: 슬롯 함수 + 페이지 함수. 페이지 함수는 `ProgressRules.MaxPageFunctions`개까지만
- 서버가 받는 경우: 에디터, 또는 실행 인자 `-allowTestProgressData`로 켠 서버 (`InvokeAuthenticator.AllowTestProgressDataArg`). 씬 설정으로 두지 않아 서비스 서버 빌드에서는 기본으로 꺼짐
  - EC2에서 쓸 때: `invoke-server` 서비스의 실행 명령 끝에 `-allowTestProgressData`를 붙임
- 쫓겨나 곧 끊길 연결의 요청도 거부

## 다른 시스템이 쓰는 것
- `PlayerProgress`: 위 「진행도 컴포넌트」 표 (레벨·경험치·돈·재료·해금 목록 읽기와 바꾸기)
- `FunctionExecutorNetwork`의 저장 요청 함수: 위 「함수·슬롯·전역 변수 주고받기」 표, [network.md](network.md)
- `UnlockResult` (`ProgressRules.cs`): 해금 요청의 결과 값 `Unlocked`, `UnknownNode`, `AlreadyUnlocked`, `PrerequisiteMissing`, `MaterialShort`, `NotAllowed`(서버가 아닌 곳에서 `TryUnlock`을 부름, 값을 바꾸지 않음). `PlayerProgress.UnlockAnswered`가 알림
- `ProgressRules.MaxPageFunctions`: 페이지 함수 최대 개수. 편집 화면이 저장 전에 먼저 안내할 때 씀
- `ProgressRules.MaxCodeSize`: 슬롯 8개의 코드 영역 크기 최대 (임시 1000). 편집 화면이 먼저 안내할 때 씀
- `ProgressRules.SlotFunctionName(슬롯 번호)`: 슬롯 함수 이름 `slot1`~`slot8`

## 정할 것
- `FunctionStorage`(임혜원)가 PC 파일 대신 `RequestSaveFunction`을 부르게 바꾸는 일
- `SlotLoadout.SetEntries`(박진우)가 생긴 뒤 `FunctionExecutorNetwork`에서 부르는 일
- `Player_JW`(박진우)와 `Player_HW`(임혜원)에 직접 붙은 중복 컴포넌트를 지우는 일: 별도 이슈
  - `Player_JW`: `PlayerHealth` 2개
  - `Player_HW`: `FunctionExecutor`, `ProcessMemory`, `LanguageRule`, `PlayerHealth`, `PlayerHealthEffects` 각 2개

## 맞춘 결과
- 임혜원과 확인할 것
  - 스킬 트리 공통 노드(대시, 더블점프)는 캐릭터마다 따로 엶 (skill-tree.md 「정해야 할 것」)
  - `FunctionStorage`가 PC 파일 대신 서버로 함수를 보내도록 바뀜
- 박진우와 확인할 것
  - 실제 계정은 장착 없음. 서버가 보낸 슬롯 함수 8개를 `SlotLoadout.SetEntries`로 받음 (PR #226 코멘트로 요청)
  - 실제 캐릭터의 처음 슬롯 함수는 서버가 정함 (`slot1` 검 찌르기, `slot2` 검 던지기)
