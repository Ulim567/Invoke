# C 스킬 (스킬 수치 표)

- 담당자: 박진우

## 하는 일
- C 스킬 24개(C 고유 22개 + 세 캐릭터 공통 이동 2개)를 사전 정의 함수(predefined-function.md)로 등록한다.
- 스킬 수치(이름, 틱 수, 쿨타임, 배율, 메모리, 대상 지정 방식, 필요 조건)를 코드가 아니라 표(`SkillTable` 에셋)에서 관리한다.
- 시전할 때 필요 조건(맨손, 소환검)을 검사해서 못 쓰면 스킬 에러로 끝낸다.
- 하지 않는 일
  - 효과(판정, 데미지, 상태 이상, 이동): 각 스킬 이슈(찌르기 1·2, 던지기, 휘두르기 1·2, 소환, 이동, 검 소환·해제, 패링, 급소공략)에서 채운다. 지금은 정해진 틱 동안 실행되고 로그만 남는다.
  - heap 계산: 메모리 작업(이우림). 표에는 heap 사용량만 적어 둔다.
  - 맨손 상태 관리: `PhysicalSword` (physical-sword.md)
  - 소환검 관리: summoned-sword.md (#99)

## 규칙
- 판정과 오브젝트 생성 (place-scenes.md, #125)
  - 판정은 `Physics2D`로 바로 하지 않고 시전자가 속한 씬의 물리 세계(`task.Caster.scene.GetPhysicsScene2D()`)에 묻는다. `All`로 끝나는 함수는 `ContactFilter2D` + `List<Collider2D>`를 받는 함수로 쓴다.
  - 게임 중에 생기는 네트워크 오브젝트(소환검, 검기 등)는 `NetworkSpawner.Spawn` / `Despawn`으로 만들고 없앤다. 이펙트처럼 보여 주기만 하는 것은 아니다.

### 수치 표
- 에셋: `Assets/_JW/Resources/SkillTable.asset`. Inspector에서 고친다.
- 스킬 클래스는 값을 쓸 때마다 표를 읽는다. 표를 고치면 코드를 고치지 않아도 다음 시전부터 반영된다.
- 한 ID에 한 줄. 표에 없는 ID는 콘솔 오류를 한 번 남기고, 이름은 ID, 틱 수는 1로 동작한다.
- **지금 수치는 전부 임시다** (노션 「C 캐릭터 - 1차」 10. 아직 정하지 않은 것). 틱 수 20, 쿨타임 0, 지역 변수 0, heap은 쓰는 스킬만 1.
  - 기본 배율은 1. 「데미지는 낮지만」, 「약함」이라고 적힌 스킬(꼬챙이, 검 크게베기, 검무)만 임시로 0.5.
  - 조건 배율(출혈, 표식, 미니게임, 치명타)은 표가 아니라 `DamageBonus`(damage-calculation.md)를 따른다.

| 칸 | 설명 |
| --- | --- |
| `id` | 고정 ID. `PredefinedFunctionIds`의 값과 같아야 한다 |
| `displayName` | 화면 이름 (전부 가칭) |
| `family` | 계열: 찌르기, 던지기, 휘두르기, 소환, 이동 |
| `targeting` | 대상 지정 방식. 파라미터 모양을 정한다 (아래 표) |
| `damageMultiplier` | 기본 데미지 배율 (`DamageRequest.SkillMultiplier`) |
| `tickCount` | 동작 시간 (틱 수) |
| `cooldownTicks` | 쿨타임 (틱 수). 동작이 끝난 뒤부터 흐른다 |
| `localVariableCount` | stack 부담. 프레임 크기 = 1 + 파라미터 수 + 이 값 |
| `heapSize` | heap 사용량 (칸). 검 소환, 검기, 패링, 이동 계열. 메모리 작업이 끝나면 실행기가 읽는다 |
| `areaRadius` | 범위 스킬(`Area`)의 반경 (유닛, 임시 4). 플레이어 주변 이 거리 안의 몬스터가 대상 (slot-caster.md) |
| `needsSummonedSword` | 쓸 수 있는 소환검이 있어야 쓸 수 있다 |
| `usableUnarmed` | 맨손이어도 쓸 수 있다 |
| `note` | 효과 요약, 기획과 다르게 가정한 것 |

### 대상 지정 방식 → 파라미터

| 방식 | 파라미터 | 스킬 |
| --- | --- | --- |
| `Single` 단일 | `Target` 1개 | 대부분 |
| `SingleMultiHit` 단일 + 다단 히트 | `Target` 1개 | 검 다중베기 |
| `Pierce` 관통 | `Target` 1개 (첫 대상) | 꼬챙이 |
| `MultiSelect` 여러 명 선택 | `Target` list 1개 | 전장 휩쓸기 |
| `Area` 범위 | 없음 | 검 크게베기, 검무 |
| `Path` 경로 | 없음 | 검 돌아오기 |
| `None` 대상 없음 | 없음 | 검 소환, 해제, 패링, 이동 계열 |

### 필요 조건 검사 (`DataDrivenSkill`)
- 첫 틱(`OnStart`)에 검사한다. 통과하지 못하면 `Fail`로 끝난다 (처리는 언어 규칙 표의 `SkillFailed`).
  1. 맨손으로 못 쓰는 스킬인데 맨손: `UnarmedError`
  2. 소환검이 필요한데 쓸 수 있는 소환검이 0개: `NoSummonedSwordError`
- 맨손: 시전자의 `PhysicalSword.IsUnarmed`. `PhysicalSword`가 없으면 `TempSwordState.IsUnarmed`.
- 소환검 개수: 시전자의 `SummonedSwords.ReadyCount` (사용 가능한 소환검). 없으면 `TempSwordState.SummonedSwordCount`, 그것도 없으면 소환검이 있는 것으로 본다.
  - 해제만 다르게 센다: 해제할 수 있는 소환검(전역 포인터 목록의 소환검, 던진 검 포함) 수.
- 둘 다 없으면 검을 들고 있고 소환검도 있는 것으로 본다.

### 스킬 목록

| 계열 | 스킬 | 고정 ID | 대상 | 소환검 필요 | 맨손 가능 | heap |
| --- | --- | --- | --- | --- | --- | --- |
| 찌르기 | 검 찌르기 | `c.thrust` | 단일 | | | |
| 찌르기 | 검 집중찌르기 | `c.focused_thrust` | 단일 | | | |
| 찌르기 | 급소공략 | `c.vital_strike` | 단일 | | | |
| 찌르기 | 꼬챙이 | `c.skewer` | 관통 | | | |
| 찌르기 | 소환검 찌르기 | `c.summoned_thrust` | 단일 | O | | |
| 찌르기 | 상처 공략 | `c.wound_exploit` | 단일 | | | |
| 찌르기 | 검 깊이찌르기 | `c.deep_thrust` | 단일 | | | |
| 던지기 | 검 던지기 | `c.sword_throw` | 단일 | | | |
| 던지기 | 소환검 던지기 | `c.summoned_throw` | 단일 | O | | |
| 던지기 | 검 돌아오기 | `c.sword_return` | 경로 | | O | |
| 휘두르기 | 검 휘두르기 | `c.sword_slash` | 단일 | | | O |
| 휘두르기 | 검 크게베기 | `c.wide_slash` | 범위 | | | O |
| 휘두르기 | 소환검 휘두르기 | `c.summoned_slash` | 단일 | O | | O |
| 휘두르기 | 상처 가르기 | `c.wound_slash` | 단일 | | | O |
| 휘두르기 | 검 다중베기 | `c.multi_slash` | 단일 + 다단 히트 | O | | |
| 휘두르기 | 전장 휩쓸기 | `c.battlefield_sweep` | 여러 명 선택 | O | | |
| 소환 | 검 소환 | `c.summon_sword` | 없음 | | O | O |
| 소환 | 해제 | `c.free_sword` | 없음 | O | O | |
| 소환 | 패링 | `c.parry` | 없음 | | | O |
| 소환 | 표식 남기기 | `c.mark` | 단일 | O | | |
| 소환 | 검무 | `c.sword_dance` | 범위 | O | | |
| 이동 | 대시 | `common.dash` | 없음 | | O | O |
| 이동 | 더블점프 | `common.double_jump` | 없음 | | O | O |
| 이동 | 검 타기 | `c.sword_ride` | 없음 | O | O | O |

- 기획 문서에 없어서 가정한 것 (기획 확인 필요)
  - 패링: 맨손 불가
  - 표식 남기기: 소환검 필요 (「검이 적 좌우로 찔러」)
  - 해제: 소환검이 있어야 쓸 수 있음

### 찌르기 1: 검 찌르기·집중찌르기·꼬챙이 (#152)
- 공통(`ThrustSkill`)
  - 대상: 파라미터 `target`(슬롯에서 고른 몬스터, 블록에서 넘긴 대상)을 `TryGetArgument("target")`로 읽는다. 값이 없으면 바라보는 방향 사거리 안의 가장 가까운 몬스터, 그것도 없으면 허공을 찌른다(빗나감, `Fired`만).
  - 사거리 `ThrustSkill.Reach` 2.5유닛 (임시). 몸 중심에서 대상 콜라이더의 가장 가까운 점까지 좌우 거리.
  - 대상이 사거리보다 멀면 대상 앞까지 돌진한다 (`PlayerMovement.StartLunge`, player-movement.md). 사거리 안에 들어온 틱에 한 번 찌른다.
  - 스킬이 끝날 때(20틱)까지 닿지 못하면 빗나간다. 돌진은 몬스터 몸에 막힌다 (뒤쪽 몬스터를 고르면 앞의 몬스터에 막혀 빗나감).
  - 데미지: `new DamageRequest(PlayerStats.AttackPower, 표 배율) { BonusRate }` → `DamageCalculator.Calculate(request, DamageTarget.FromMonster)` → `IDamageable.TakeDamage`. 표식은 계산기가 자동으로 더한다.
- 검 찌르기: 대상 1명.
- 검 집중찌르기: 대상이 출혈이면 `BonusRate = DamageBonus.BleedFocusedThrust`(+50%).
- 꼬챙이: 대상부터 찌르는 방향 뒤로 `CSkewer.PierceLength` 3유닛(임시), 높이 1.5유닛 안의 몬스터가 모두 맞는다. 배율은 표(0.5, 임시). 판정은 시전자 씬의 물리 세계.

### 검 소환·해제 (#102)
- 검 소환 (`c.summon_sword`, malloc)
  - 시작할 때(첫 틱) `SummonedSwords.SpawnSword` → `PredefinedFunction.Malloc(task, 소환검, heapSize)` → 성공하면 `SummonedSwords.Register`, `NullPointer`면 `SummonedSwords.DespawnSword`.
  - 소환검이 이미 5개(`CountHeapObjects<SummonedSword>`, 누수 포함)거나 heap이 모자라면 만들지 않는다. C의 malloc처럼 에러로 끝내지 않는다.
  - 포인터는 이 작업의 것. 정상으로 끝나면 전역 포인터 목록으로 옮겨지고, 도중에 실행 중단·실패로 끝나면 그 소환검은 누수된다.
- 해제 (`c.free_sword`, free)
  - 마지막에 소환한 소환검부터 해제한다 (`GetGlobalPointers<SummonedSword>`의 마지막).
  - 날아가는 중·떨어진 소환검도 거리와 상관없이 해제한다. 물리 검, 누수된 소환검, 아직 진행 중인 검 소환의 소환검은 해제하지 않는다.
  - 순서: `TryGetHeapObject` → `PredefinedFunction.Free` → `SummonedSwords.DespawnSword`. 쓸 수 없는 포인터면 `InvalidPointerError`로 그 작업만 끝난다.
  - 해제할 소환검이 없으면 `NoSummonedSwordError`.

## 다른 시스템이 쓰는 것

### 스킬 찾기
- 다른 사전 정의 함수와 같다. `PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.Thrust, out PredefinedFunction skill)` → `FunctionExecutor.Cast(skill)`

### `SkillTable`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `SkillTable.TryGet(string id, out SkillData data)` | 고정 ID | `bool` | 그 스킬의 수치. 표에 없으면 `false`. UI(스킬 설명, 쿨타임 표시)에서 읽는다 |
| `SkillTable.Instance.Skills` | - | `IReadOnlyList<SkillData>` | 표의 모든 줄. 스킬 트리, 스킬 목록 화면에서 쓴다 |

`SkillData`의 값은 위 「수치 표」의 칸과 같고, 이름은 PascalCase다 (예: `DisplayName`, `TickCount`, `NeedsSummonedSword`). 읽기만 할 수 있다.

### 연출 event (`SkillEffectEvents`, #152)

플레이어 프리팹에 `SkillEffectEvents`가 붙어 있다. 이펙트·소리·애니메이션(임혜원)은 이 event만 받아서 튼다 (코드 작성 규칙 1). 모든 event는 스킬 고정 ID(`PredefinedFunctionIds`)를 같이 보낸다.

| 이름 | 받는 값 | 설명 |
| --- | --- | --- |
| `Started` (event) | `string` 스킬 ID | [연출] 스킬이 시작했을 때 (필요 조건 통과 후). 시전 자세, 소리 |
| `Fired` (event) | `string` 스킬 ID, `Vector2` 시작 위치(몸 가운데), `Vector2` 방향 | [연출] 판정이 나간 순간. 빗나가도 불린다. 찌르기 섬광 |
| `Hit` (event) | `string` 스킬 ID, `GameObject` 맞은 대상, `Vector2` 맞은 위치, `DamageResult` 데미지 결과 | [연출] 대상이 맞은 순간. 관통이면 대상마다. 맞은 이펙트, 치명타·처형 연출 |
| `Ended` (event) | `string` 스킬 ID, `bool` 끝까지 실행했으면 `true` | [연출] 스킬이 끝났을 때. 끊기거나 실패하면 `false` |

- 순서: `Started` → (`Fired` → `Hit` 0번 이상) → `Ended`. 필요 조건(맨손 등)에 걸려 시작하지 못한 스킬은 아무 event도 내지 않는다.
- `Started`, `Ended`는 `DataDrivenSkill`이 모든 C 스킬에 대해 낸다. `Fired`, `Hit`은 효과를 채운 스킬부터 낸다 (지금: 검 찌르기, 집중찌르기, 꼬챙이).
- `Raise...` 함수는 C 스킬만 부른다. 네트워크를 붙이면 `[ClientRpc]`로 바꿔 모든 화면에서 불린다.
- 찌르기 섬광: `ThrustEffectView`(플레이어)가 `Fired`를 받아 `ThrustFlash`(검 찌르기·집중찌르기) / `SkewerFlash`(꼬챙이) 프리팹을 찌르는 방향으로 만든다. 그림은 임혜원 #114 (`_HW/Effects/Thrust_Flash`, `Skewer_Flash`). 보여 주기만 하는 오브젝트라 `OneShotEffect`가 0.25초 뒤 스스로 없앤다.

### 새 스킬 효과 만들기 (`DataDrivenSkill`)
- 스킬 클래스는 `DataDrivenSkill`을 상속하고 `Id`만 정한다. 이름, 틱 수, 쿨타임, 파라미터는 표에서 읽는다.
- 효과는 `OnSkillStart`(필요 조건을 통과한 뒤 첫 틱), `OnTick`, `OnSkillEnd`(끝까지 실행), `OnSkillCancel`(끊김·실패), `OnFail`을 덮어써서 만든다.
- `OnStart`, `OnEnd`, `OnCancel`은 덮어쓸 수 없다. 필요 조건 검사와 연출 event(`Started`, `Ended`)를 빼먹지 않기 위함
- 대상 등 파라미터 값은 `TryGetArgument("target", out RuntimeValue v)` → `v.AsBug()`로 읽는다 (predefined-function.md, #133)
- 스킬 클래스 위치: `Assets/_JW/Scripts/Skills/` 계열별 파일 (`ThrustSkills.cs` 등)

### 테스트
| 이름 | 설명 |
| --- | --- |
| `ThrustSkillSimTest` | Test_JW의 ThrustTest. 켜고 Play → Host, Game 창을 한 번 클릭 (Unity가 화면 맨 앞에 있어야 가짜 키가 들어감). 검 찌르기(사거리 밖 몬스터에게 돌진 후 맞음, 데미지 = 공격력 × 배율), 집중찌르기(출혈 1.5배), 꼬챙이(일직선 뒤 몬스터 모두), 맨손 실패, 슬롯 Q로 고른 대상에게 맞음, 연출 event 순서를 확인. 「[ThrustTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SkillCastTest` | Test_JW의 SkillTest. 켜고 Play → Host. 표 검사(이름·틱 수·쿨타임·파라미터), 24개 시전(표의 틱 수만큼 실행되는지), 맨손 검사(물리 검을 던져 맨손으로 만든 뒤 검 찌르기는 `UnarmedError`, 검 돌아오기는 정상)를 한다. 「[SkillTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

## 맞춘 결과
- 이우림과 정함
  - 예시 `_UL/.../Examples/Thrust.cs`(검 찌르기)를 지우고, `c.thrust`는 `CThrust`(`_JW`)가 쓴다. 같은 ID가 둘이면 둘 다 등록되지 않기 때문
  - 고정 ID 24개는 `PredefinedFunctionIds`에 모은다
  - heap 사용량은 표에 적어 두고, 메모리 작업(이우림)에서 실행기가 읽는다
- 정한 것
  - 대시는 스킬(`common.dash`)로 둔다. 왼쪽 Shift는 달리기(이동 속도 증가)로 바꾼다 (입력 키는 이우림이 반영)
- 임혜원과 정함 (#152)
  - 이펙트는 `SkillEffectEvents`의 event를 받아서 튼다 (스킬 코드는 고치지 않음)
- 정해야 할 것
  - 수치 전부 (데미지, 쿨타임, 메모리, 틱 수): 기획
  - 찌르기 사거리 2.5, 꼬챙이 관통 길이 3, 돌진 속도 30: 기획
  - 돌진이 몬스터 몸에 막히는 것 (지금은 막힘. 뚫고 지나갈지): 기획
  - 위 「가정한 것」 3개: 기획
  - 검기가 해제가 필요한지, 날아간 뒤 자동 반환인지: 기획
