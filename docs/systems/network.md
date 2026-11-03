# Network (멀티플레이 규칙, 함수 시전 동기화)

- 담당자: 이우림

## 하는 일
- 멀티플레이에서 무엇을 어디서 실행하고, 무엇을 어느 클라이언트에 보내는지의 공통 규칙
- 다른 시스템을 네트워크에 붙일 때 쓰는 경우별 처리 방법
- 함수 시전의 네트워크 쪽: `FunctionExecutorNetwork` (#173)
  - 함수 데이터 전송 (소유 클라이언트 → 서버)
  - 시전·취소·입력 대기 요청과 결과 알림
  - 메모리·쿨타임 값 (서버 → 소유 클라이언트)
- 하지 않는 일
  - 장소 씬, 네트워크 오브젝트 생성·삭제(`NetworkSpawner`): [place-scenes.md](place-scenes.md)
  - 받은 함수 데이터 검사, 서버 저장: 「만든 함수를 서버로 보내 검사·저장하기」
  - 다른 시스템의 동기화(스킬 연출, 검, 몬스터, 상태 이상): 각 담당자의 이슈. 아래 「경우별 처리 방법」을 따름

## 동작 규칙

### 실행 위치

| 하는 일 | 실행하는 곳 | 예 |
| --- | --- | --- |
| 값 바꾸기 | 서버 | 함수 실행, 데미지, 체력, 메모리, 쿨타임, 네트워크 오브젝트 생성·삭제 |
| 키 입력 읽기 | 로컬 플레이어의 클라이언트(`isLocalPlayer`) | `PlayerInputReader`, `SlotCaster`의 대상 고르기 |
| 연출 | 모든 클라이언트 | 이펙트, 소리, 애니메이션 |

| 실행 방식 | 함수 실행기(`FunctionExecutor`) |
| --- | --- |
| 전용 서버 | 서버: 실행 / 클라이언트: 실행 안 함(`TickEnabled` false) |
| 호스트 | 실행 (호스트의 캐릭터 = 서버의 캐릭터) |
| 오프라인 테스트(서버·클라이언트를 시작하지 않음) | 실행 (지금과 같음) |

### Mirror 표시(attribute)
- 붙는 곳: `NetworkBehaviour`를 상속한 컴포넌트. `MonoBehaviour`에서는 동작 안 함
- 보낼 수 있는 값: `int`, `float`, `bool`, `string`, `enum`, 배열, 필드만 가진 struct, `NetworkIdentity`가 붙은 `GameObject`
  - 보낼 수 없는 값: 일반 클래스 객체(`FunctionTask`, `FunctionBlock` 등) → 번호로 바꿔 보냄
  - 보낼 수 없는 타입을 쓰면 컴파일할 때 Mirror Weaver가 오류로 알림

| 표시 | 방향 | 쓰는 때 |
| --- | --- | --- |
| `[Command]` | 소유 클라이언트 → 서버 | 로컬 플레이어 입력의 결과를 서버에 요청. 그 오브젝트의 소유 클라이언트만 호출 가능 |
| `[ClientRpc]` | 서버 → 그 오브젝트를 보는(observer) 모든 클라이언트 | 모두가 봐야 하는 연출 |
| `[TargetRpc]` | 서버 → 그 오브젝트의 소유 클라이언트 | 소유 클라이언트만 받는 결과(시전 거부 등) |
| `[SyncVar]` | 서버 → 클라이언트 | 계속 맞춰야 하는 값. 바뀐 값만 보냄. `hook`으로 바뀔 때 함수 호출 |
| `[Server]` | - | 서버에서만 실행하는 함수 표시. 클라이언트에서 부르면 경고 후 실행 안 함 |

- `[SyncVar]`를 받는 클라이언트: 컴포넌트 Inspector의 Sync Mode. `Observers`(그 오브젝트를 보는 모든 클라이언트, 기본), `Owner`(소유 클라이언트만)
- 호스트: 서버와 로컬 클라이언트가 한 프로그램. `[ClientRpc]`, `[TargetRpc]`가 그 자리에서 실행되고 `[SyncVar]`도 같은 변수
- 오프라인 테스트: 표시가 동작하지 않음 → 서버·클라이언트가 모두 시작되지 않았으면 보내지 않고 그 자리에서 처리(`FunctionExecutorNetwork`의 방식, 아래 2번 예)

### 경우별 처리 방법

**1. 서버에서 실행되는 코드가 값을 바꿈 → 그대로**
- C 스킬 코드는 서버의 실행기에서만 실행됨. 스킬이 부르는 값 바꾸기 함수도 서버에서 불림
- 예: `ThrustSkill.Strike`의 `TakeDamage`, `SummonedSwords.SpawnSword`의 `NetworkSpawner.Spawn` — 바꿀 것 없음

**2. 연출 event를 모든 클라이언트에 → `NetworkBehaviour` + `[ClientRpc]`**
- 서버에서 불린 `Raise...`가 `[ClientRpc]`를 거쳐 모든 클라이언트에서 event를 냄
- 플레이어 프리팹 루트에 `NetworkIdentity`가 있어야 함(공용 `Player.prefab`에 있음)
- 예: `SkillEffectEvents.RaiseFired`

```csharp
// 바꾸기 전
public class SkillEffectEvents : MonoBehaviour
{
	public void RaiseFired(string skillId, Vector2 origin, Vector2 direction) => Fired?.Invoke(skillId, origin, direction);
}

// 바꾼 뒤
public class SkillEffectEvents : NetworkBehaviour
{
	public void RaiseFired(string skillId, Vector2 origin, Vector2 direction)
	{
		// 오프라인 테스트: 그 자리에서 event
		if (!NetworkServer.active) { Fired?.Invoke(skillId, origin, direction); return; }
		RpcFired(skillId, origin, direction);
	}

	[ClientRpc]
	void RpcFired(string skillId, Vector2 origin, Vector2 direction) => Fired?.Invoke(skillId, origin, direction);
}
```

**3. 서버의 스킬이 플레이어 오브젝트의 이동을 바꿈 → `[TargetRpc]`**
- 플레이어 이동은 소유 클라이언트가 정함(공용 `Player.prefab`의 `NetworkRigidbodyReliable2D`, Sync Direction: Client To Server). 서버에서 `PlayerMovement`를 불러도 움직이지 않음
- 서버가 그 플레이어 오브젝트의 소유 클라이언트에 `[TargetRpc]`로 이동을 요청
- 예: 찌르기의 `PlayerMovement.StartLunge` — #223에서 반영 (player-movement.md)

**4. 로컬 플레이어 입력의 결과를 서버로 → `[Command]`**
- 키는 로컬 플레이어의 클라이언트에서 읽고, 값을 바꾸는 일은 서버에 요청
- 예: `SwordPickUpInput`의 줍기

```csharp
// 바꾸기 전
void Update()
{
	if (!isLocalPlayer || !input.PickUpPressed) return;
	if (sword.TryPickUp()) return;
	if (summoned != null) summoned.TryPickUpNearest();
}

// 바꾼 뒤
void Update()
{
	if (!isLocalPlayer || !input.PickUpPressed) return;
	CmdPickUp();
}

[Command]
void CmdPickUp()
{
	if (sword.TryPickUp()) return;
	if (summoned != null) summoned.TryPickUpNearest();
}
```

**5. 몬스터·작업을 가리킴 → 몬스터는 `GameObject`, 작업은 작업 번호**
- 몬스터: `[Command]`, `[ClientRpc]`의 값으로 `GameObject`를 그대로 넘김. Mirror가 netId로 바꿔 보내고 받는 쪽에서 같은 몬스터로 되돌림(몬스터 프리팹에 `NetworkIdentity` 필요)
- 작업: `FunctionTask.Id`. 서버에서 `FunctionExecutor.TryGetTask(번호)`로 작업을 찾음

**6. 실행기 값을 UI에 표시 → 그대로**
- 메모리 게이지: `ProcessMemory`의 `Capacity`, `DataUsed`, `StackUsed`, `HeapUsed`, `Free`. 클라이언트에서는 서버의 값으로 답함
- 쿨타임 UI: `FunctionExecutor.GetRemainingCooldownTicks`. 클라이언트에서는 서버가 알려 준 끝 시각으로 답함
- 시전 모션: `FunctionExecutorNetwork.TaskStarted` (`FunctionExecutor.TaskStarted`는 클라이언트에서 불리지 않음)

**7. 슬롯으로 시전 → `FunctionExecutorNetwork`의 함수**
- `FunctionExecutor.Cast`, `Cancel`, `ProvideInput`을 직접 부르지 않음 → `RequestCast`, `RequestCancel`, `ProvideTarget`
- 결과는 바로 오지 않음 → `CastAccepted`, `CastRejected`를 받은 뒤에 시전 뒤 처리

**8. Bug가 아닌 값을 받는 입력 함수가 생김 → `FunctionExecutorNetwork`에 보내는 함수 추가**
- 예: 위치를 받는 입력 함수 → `ProvideTarget` 옆에 위치를 보내는 함수
- 서버 처리: `TryGetTask` → `ProvideInput`. 기다리는 타입과 다른 값이면 `ProvideInput`이 던지는 `ArgumentException`을 받아 버림(`ProvideTarget`과 같음)

### 함수 데이터 전송 (#173)
- 보내는 것: 슬롯 8칸, 이 컴퓨터에 저장한 함수 전부(`FunctionStorage.LoadAll`), 전역 변수 파일(`GlobalVariablesStorage.Load`)
- 보내는 때: 로컬 플레이어 오브젝트가 생길 때(`OnStartLocalPlayer`) 전부, `SlotLoadout.Changed` 때 슬롯과 함수(다음 `Update`에 한 번)
- 함수: 하나씩 따로 보냄. 한 메시지 한도(KCP `ReliableMaxMessageSize`, `Base` 씬 설정 297,433바이트)
- 서버: `ApplyPlayerData` 한 곳에서 슬롯마다 원본 생성
  - C 스킬: `PredefinedFunctionRegistry`, 블록 함수: `FunctionDataConverter`
  - 블록 함수 원본: 함수 데이터가 모두 같으면 이어 씀(쿨타임·실행 중 검사 유지), 하나라도 바뀌면 모두 새로 만듦
  - 전역 변수: `SetGlobalVariablesFile` 뒤 `RestartProcess`. 장착만 바뀐 경우는 다시 시작 안 함
- 「데이터 준비됨」: 처음 슬롯·함수·전역 변수를 모두 적용하면 `IsDataReady` true, `DataReady` 한 번
- 받은 데이터 검사: 없음(보안은 나중). 검사 자리: `ApplyPlayerData`, `ProvideTarget`의 서버 처리

## 다른 시스템이 쓰는 것

### `FunctionExecutorNetwork`
- 붙이는 곳: 플레이어 프리팹 루트, `FunctionExecutor`·`SlotLoadout`와 함께(`RequireComponent`). Sync Mode: `Owner`
- 가져오기: `GetComponent<FunctionExecutorNetwork>()`. 로컬 플레이어는 `NetworkClient.localPlayer`
- 오프라인 테스트: 같은 함수·event가 그 자리에서 동작(슬롯 원본은 이 컴퓨터의 `SlotLoadout`)

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `RequestCast(int slot, GameObject target = null)` | 슬롯 번호(0~7), 대상 몬스터 | - | [소유 클라이언트] 서버에 시전 요청. 대상(`Target`) 파라미터가 있는 스킬만 `target` 사용. 답은 `CastAccepted` / `CastRejected` |
| `RequestCancel(int slot)` | 슬롯 번호 | - | [소유 클라이언트] 그 슬롯으로 시전한 작업 끊기 요청(`FunctionExecutor.Cancel`). 끊기면 `TaskEnded(Cancelled)` |
| `ProvideTarget(int taskId, GameObject target)` | 작업 번호, 고른 몬스터(없으면 `null`) | - | [소유 클라이언트] 입력을 기다리는 이 플레이어의 작업에 몬스터 넣기 요청. 이미 끝난 작업이면 서버가 버림 |
| `CastAccepted` (event) | `int` 슬롯, `int` 작업 번호 | - | [로직] 시전 성공. 소유 클라이언트에만 |
| `CastRejected` (event) | `int` 슬롯, `CastRejection` 이유 | - | [로직] 시전 거부. 소유 클라이언트에만. 이유: `OnCooldown`, `NotEnoughMemory`, `NoFunction`(서버에 그 슬롯의 함수 없음) |
| `TaskStarted` (event) | `int` 작업 번호, `int` 슬롯(비동기 작업 -1), `string` 함수 이름 | - | [연출] 작업이 진행 시작. 모든 클라이언트. 시전 모션 |
| `TaskEnded` (event) | `int` 작업 번호, `FunctionTaskEndReason` | - | [로직] 작업이 끝남. 모든 클라이언트. 시작 전에 끊긴 작업은 이것만 |
| `InputRequested` (event) | `int` 작업 번호, `string` 입력 함수 ID, `int` 슬롯(비동기 작업 -1) | - | [로직] 이 플레이어의 작업이 입력을 기다림. 소유 클라이언트에만. 이 안에서 바로 `ProvideTarget`을 불러도 됨 |
| `IsDataReady` | - | `bool` | 서버가 이 플레이어의 함수 데이터를 처음 다 받아 시전 준비가 됨. 로딩 화면이 기다림 |
| `DataReady` (event) | - | - | [표시] `IsDataReady`가 true가 될 때 한 번. 소유 클라이언트(호스트 포함) |
| `ApplyPlayerData(slots, functions, globals)` | 슬롯 8칸(`SlotLoadout.Entry`), 함수 데이터 목록, 전역 변수 파일(`null`이면 그대로) | - | [서버] 플레이어 데이터를 넣고 슬롯 원본 생성. 실제 계정은 DB에서 읽은 데이터(`ApplyLoadedData`), 임시 계정은 클라이언트가 보낸 이 컴퓨터의 파일 데이터 |
| `ApplyLoadedData(CharacterProgressData, ProgressStore, characterKey)` | DB에서 불러온 진행도, 저장할 DB, 캐릭터 key | - | [서버] 실제 계정의 슬롯·함수·전역 변수를 넣음. 플레이어를 연결에 붙이기 전에 `AccountServer`가 부름 (#205) |
| `UsesServerData` | - | `bool` | 슬롯·함수·전역 변수의 원본이 서버 DB이면 true(실제 계정). 이때 클라이언트는 파일을 보내지 않음 (#205) |
| `ReceivedSlots`, `ReceivedFunctions`, `ReceivedGlobals` | - | 슬롯 8개, 함수 목록, 전역 변수 선언 | [소유 클라이언트] 서버가 보낸 데이터. 받기 전이면 `null` (#205) |
| `PlayerDataReceived` (event) | - | - | [표시] 서버가 보낸 슬롯·함수·전역 변수를 처음 다 받았을 때, 저장 요청이 성공해 바뀌었을 때. 실제 계정의 소유 클라이언트 (#205) |
| `RequestSaveFunction(FunctionData)` | 함수 | - | [소유 클라이언트] 서버 DB에 함수 저장 요청. 결과는 `PlayerDataRequestAnswered` (#205, [progress-store.md](progress-store.md)) |
| `RequestEquipFunction(slot, guid)`, `RequestEquipSkill(slot, id)`, `RequestUnequip(slot)`, `RequestSwap(a, b)` | 슬롯 번호, 함수 GUID / 스킬 ID | - | [소유 클라이언트] 서버 DB에 장착 변경 요청. 결과는 `PlayerDataRequestAnswered` (#205) |
| `RequestSaveGlobals(GlobalVariablesData)` | 전역 변수 선언 | - | [소유 클라이언트] 서버 DB에 전역 변수 저장 요청. 결과는 `PlayerDataRequestAnswered` (#205) |
| `PlayerDataRequestAnswered` (event) | `PlayerDataRequest`, `PlayerDataRequestResult` | - | [로직] 저장 요청의 결과. 소유 클라이언트 (#205) |

### 시험
| 이름 | 설명 |
| --- | --- |
| `CastSyncTest` | `NetworkTest` 씬. 에디터 Play → Host, 시험 빌드(`NetworkTest` 씬만 넣은 Windows 빌드) → Client. 서버가 플레이어마다 몬스터와 시험 슬롯을 넣고, 클라이언트가 시전·입력 대기·메모리·쿨타임·거부를 확인. 「[CastSyncTest] 클라이언트 전체 통과」면 통과. 활성화되어 있으면 Host로 시작할 때 자동으로 동작(플레이어 슬롯을 시험 슬롯으로 바꾸고 몬스터를 만듦) |

## 맞춘 결과
- 이우림 결정 (#173)
  - 함수는 서버의 실행기만 실행. `FunctionExecutor`는 Mirror 없이 두고 네트워크는 `FunctionExecutorNetwork`에 모음
  - 식별값: 함수는 슬롯 번호, 작업은 작업 번호, 몬스터는 `GameObject`
  - 함수 데이터는 이번 이슈에서 클라이언트가 보냄. 검사·저장은 「만든 함수를 서버로 보내 검사·저장하기」
  - 메모리 값은 소유 클라이언트에만. 시전 결과는 소유 클라이언트에만, 작업 시작·끝은 모든 클라이언트에
- 박진우에게 요청 (#173, `SlotCaster`)
  - 시전·취소: `executor.Cast`, `Cancel` → `RequestCast(슬롯, 대상)`, `RequestCancel(슬롯)`
  - 시전 직후 처리(로그, `CastRequested`, 취소용 기록): `CastAccepted` / `CastRejected`를 받은 뒤로
  - 입력 대기: `executor.InputRequested` → `FunctionExecutorNetwork.InputRequested`, `executor.ProvideInput` → `ProvideTarget(작업 번호, 몬스터)`. 기다리는 작업은 작업 번호로 기억, 끝났는지는 `TaskEnded`
  - 고르는 방식(가장 가까운 몬스터, ← →): 그대로
- 임혜원에게 요청 (#173)
  - `PlayerAnimator`: 시전 모션 event `FunctionExecutor.TaskStarted` → `FunctionExecutorNetwork.TaskStarted`
  - `MemoryGauge`: `DataUsed` 그리기(#139 요청과 같음). 읽는 값은 그대로 `ProcessMemory`
- 박진우에게 제안 (이우림 결정) → 박진우 반영 (#223). 아래 제안대로 함. 돌진·돌아서기는 `PlayerMovement` 안에서 `[TargetRpc]`로 보내므로 스킬 코드는 그대로
  - 찌르기 돌진 (`ThrustSkill` → `PlayerMovement.StartLunge`)
    - 지금: 클라이언트로 접속한 플레이어의 찌르기가 제자리에서 나감(멀리 있는 대상에게 달려가지 않음)
    - 이유: 스킬은 서버에서 실행, 플레이어 위치는 소유 클라이언트가 정함(`NetworkRigidbodyReliable2D`, Client To Server). 서버의 `StartLunge`는 소유 클라이언트의 플레이어 오브젝트를 움직이지 못함
    - 제안: 서버의 스킬이 소유 클라이언트에 `[TargetRpc]`로 돌진을 요청하고, 소유 클라이언트가 움직임(「경우별 처리 방법」 3번)
    - 이유: "스킬은 서버, 플레이어 이동은 소유 클라이언트" 규칙 하나로 유지. 시전이 거부되면 돌진도 없음. 늦어지는 시간은 알림 한 번(예: 0.05초)
    - 고르지 않은 안: 슬롯 키를 뗄 때 소유 클라이언트가 먼저 돌진. 반응은 빠르지만 거부됐을 때 캐릭터만 달려가는 경우를 따로 처리해야 함
  - 바라보는 방향 (`PlayerMovement.Facing`)
    - 지금: 클라이언트로 접속한 플레이어가 왼쪽을 보고 검 크게베기를 써도 검기가 오른쪽으로 날아감
    - 이유: `Facing`은 소유 클라이언트에서만 바뀌고 서버에 전달되지 않음. 서버의 스킬은 처음 값(오른쪽)을 읽음
    - 제안: `Facing`을 `[SyncVar]`로, `PlayerMovement`의 Sync Direction을 Client To Server로. 다른 클라이언트에서 보이는 그림 뒤집기도 함께 맞춰짐
    - 서버의 스킬이 캐릭터를 돌려세울 때(`FaceTowards`): 돌진과 같은 `[TargetRpc]`로 소유 클라이언트에 요청
    - 한계: 방향을 바꾼 거의 같은 순간에 시전하면 서버가 바뀌기 전 방향을 읽을 수 있음. 문제가 되면 `RequestCast`에 그 순간의 방향을 함께 보냄(이우림)
  - 급소공략 미니게임 결과 (`VitalStrikeMinigame`)
    - 지금: 클라이언트로 접속한 플레이어가 눈 칸에 맞춰 멈춰도 기본 데미지만 들어감
    - 이유: 미니게임은 소유 클라이언트에서 돌고 결과도 거기 남음. 서버의 `CVitalStrike`가 `TakeResult()`로 읽는 서버의 `VitalStrikeMinigame`은 비어 있음
    - 제안: 소유 클라이언트가 판정한 결과를 `[Command]`로 서버에 보냄(「경우별 처리 방법」 4번)
    - 이유: 플레이어가 본 칸 그대로 판정됨. 미니게임은 시전 전에 끝나고 Mirror는 보낸 순서대로 처리하므로, 결과가 시전 요청보다 먼저 서버에 도착함
    - 한계: 조작한 클라이언트가 늘 「눈」을 보낼 수 있음(보안은 나중)
    - 고르지 않은 안: Space를 누른 순간만 보내고 서버가 판정. 조작은 막지만 지연 때문에 플레이어가 본 칸과 판정이 달라질 수 있음
  - 그 밖: 스킬 이펙트(`SkillEffectEvents`)는 「경우별 처리 방법」 2번, 검 줍기(`SwordPickUpInput`)는 4번
    - 검 줍기: 지금은 소유 클라이언트에서 `PhysicalSword.TryPickUp`을 불러 `NetworkSpawner.Despawn`이 클라이언트 오류를 냄
- 다른 이슈로 넘긴 것
  - 박진우 서버 동기화: 찌르기 돌진·바라보는 방향, 급소공략 미니게임 판정, `SkillEffectEvents` 연출 전달 → #223에서 함
  - 「소환검·검기를 서버에서 생성하고 동기화하기」: 검 오브젝트 위치·상태, 소환검 HUD, 줍기
  - 몬스터 서버 동기화: 몬스터 체력·사망
  - 「데미지·상태 이상을 서버에서 판정하기」: 상태 이상
- 지금 한계
  - 함수·슬롯·전역 변수 파일이 컴퓨터마다 하나: 같은 컴퓨터의 다른 계정·캐릭터도 같은 데이터를 보냄(서버 DB 이슈에서 해결)
  - 공용 `Player.prefab`에 실행기 묶음(`FunctionExecutor`, `SlotLoadout`, `FunctionExecutorNetwork` 등)이 아직 없음. 시험은 `Player_UL` + `NetworkTest` 씬
  - `SlotCaster` 연결 전: 클라이언트로만 접속한 플레이어의 `GetBug`에 답하는 곳이 없음
