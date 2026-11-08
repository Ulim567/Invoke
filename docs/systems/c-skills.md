# C 스킬 (스킬 수치 표)

- 담당자: 박진우

## 하는 일
- C 스킬 24개(C 고유 22개 + 세 캐릭터 공통 이동 2개)를 사전 정의 함수(predefined-function.md)로 등록한다.
- 스킬 수치(이름, 틱 수, 쿨타임, 배율, 메모리, 대상 지정 방식, 필요 조건)를 코드가 아니라 표(`SkillTable` 에셋)에서 관리한다.
- 시전할 때 필요 조건(맨손, 소환검)을 검사해서 못 쓰면 스킬 에러로 끝낸다.
- 하지 않는 일
  - 효과(판정, 데미지, 상태 이상, 이동): 각 스킬 이슈(찌르기 1·2, 던지기, 휘두르기 1·2, 소환, 이동, 검 소환·해제, 패링, 급소공략)에서 채운다. 지금은 정해진 틱 동안 실행되고 로그만 남는다.
  - heap 계산: 메모리 작업(이우림). 스킬은 표의 heap 사용량으로 `PredefinedFunction.Malloc`을 부른다 (검 소환만 `Sword` struct 크기, #220).
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
| `heapSize` | heap 사용량 (칸). 검기, 패링, 이동 계열이 `Malloc`할 때 쓴다. 검 소환은 이 값 대신 `PredefinedStructs`의 `Sword` 크기(임시 2칸)를 쓴다 (#220) |
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
| `MultiSelect` 여러 명 선택 | `Target` array 1개 (`VariableType.ArrayOf(Target)`, #191). 값은 `RuntimeValue.FromBugs`, 읽기는 `AsBugs()` | 전장 휩쓸기 |
| `Area` 범위 | 없음 | 검 크게베기 |
| `Path` 경로 | 없음 | 검 돌아오기 |
| `None` 대상 없음 | 없음 | 검 소환, 해제, 패링, 이동 계열 |

- 소환검 포인터 파라미터 (#220): 소환검 하나를 쓰는 스킬(소환검 찌르기, 소환검 던지기, 해제)은 위 파라미터 뒤에 `sword`(`Sword*`, `DataDrivenSkill.SwordPointerType()`)를 하나 더 받는다.
  - 예: `Sword* s = SummonSword(); SummonedThrow(target, s); FreeSword(s);`
  - 비워 두면 지금처럼 스킬이 고른다 (찌르기·던지기: 가장 먼저 소환한 사용 가능한 검, 해제: 마지막에 소환한 검). 슬롯에서 쓸 때는 늘 비워 둔다.
  - 넘긴 검이 곁에 없으면(던짐, 찌르는 중, 누수) 아무것도 안 하고 끝난다. 해제·`NULL`·소환검이 아닌 포인터면 `InvalidPointerError`.
  - 스킬 코드: `TakesSwordPointer`를 덮어쓰고 `TryPickSword` / `TryGetSwordArgument`로 꺼낸다.

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
| 던지기 | 소환검 던지기 | `c.summoned_throw` | 단일 | O | O | |
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
| 소환 | 검무 | `c.sword_dance` | 단일 | O | | |
| 이동 | 대시 | `common.dash` | 없음 | | O | O |
| 이동 | 더블점프 | `common.double_jump` | 없음 | | O | O |
| 이동 | 검 타기 | `c.sword_ride` | 없음 | O | O | O |

- 기획 문서에 없어서 가정한 것 (기획 확인 필요)
  - 패링: 맨손 불가 (#245에서도 유지)
  - 표식 남기기: 사용 가능한 소환검 2개 필요 (#243에서 정함)
  - 해제: 소환검이 있어야 쓸 수 있음

### 찌르기 1: 검 찌르기·집중찌르기·꼬챙이 (#152)
- 공통(`ThrustSkill`)
  - 대상: 파라미터 `target`(슬롯에서 고른 몬스터, 블록에서 넘긴 대상)을 `TryGetArgument("target")`로 읽는다. 값이 없으면 바라보는 방향 사거리 안의 가장 가까운 몬스터, 그것도 없으면 허공을 찌른다(빗나감, `Fired`만).
  - 사거리 `ThrustSkill.Reach` 2.5유닛 (임시). 몸 중심에서 대상 콜라이더의 가장 가까운 점까지 좌우 거리.
  - 높이 (#437): 찌르기와 돌진은 몸 높이에서 가로로만 나간다. 사거리 안에 들어온 틱에 대상 몸(콜라이더)이 시전자 몸 가운데 ± `ThrustSkill.HitHalfHeight`(1.25유닛)에 걸쳐야 맞는다. 안 걸치면(위·아래 발판의 대상) 허공을 찌르고 데미지 없이 끝난다(`Fired`만). 꼬챙이는 첫 대상이 맞아야 뒤쪽도 맞는다.
  - 대상이 사거리보다 멀면 대상 앞까지 돌진한다 (`PlayerMovement.StartLunge`, player-movement.md). 사거리 안에 들어온 틱에 한 번 찌른다.
  - 스킬이 끝날 때(20틱)까지 닿지 못하면 빗나간다. 돌진은 몬스터 몸에 막힌다 (뒤쪽 몬스터를 고르면 앞의 몬스터에 막혀 빗나감).
  - 데미지: `new DamageRequest(PlayerStats.AttackPower, 표 배율) { BonusRate }` → `DamageCalculator.Calculate(request, DamageTarget.FromMonster)` → `IDamageable.TakeDamage`. 표식은 계산기가 자동으로 더한다.
- 검 찌르기: 대상 1명.
- 검 집중찌르기: 대상이 출혈이면 `BonusRate = DamageBonus.BleedFocusedThrust`(+50%).
- 꼬챙이: 대상부터 찌르는 방향 뒤로 `CSkewer.PierceLength` 3유닛(임시), 높이 1.5유닛 안의 몬스터가 모두 맞는다. 배율은 표(0.5, 임시). 판정은 시전자 씬의 물리 세계.

### 급소공략 (#161)
- 찌르기와 같다(`ThrustSkill`: 대상, 돌진, 사거리, 데미지). 배율은 표(1.0).
- 시작할 때(`OnSkillStart`) 시전자의 `VitalStrikeMinigame.TakeResult()`로 미니게임 결과를 한 번 가져간다.
  - 눈(`Eye`): `BonusRate = DamageBonus.VitalStrikeMinigame`(+200%, 총 3.0배) + 맞은 대상에 실명(`StatusEffects.TryApply`, 기본 지속 시간)
  - 발목(`Ankle`): +200% + 이속 감소
  - 실패(`Miss`), 미니게임 안 함(`None`): 기본 데미지. 페널티 없음
- 미니게임 입력은 슬롯 입력이 대상을 고르는 동안 한다 (slot-caster.md 「급소공략 미니게임」).
- `ThrustSkill`에 `OnHitMonster(task, monster, result)`를 더했다. 대상마다 데미지를 준 뒤 불린다. 상태 이상을 거는 찌르기가 덮어쓴다.

#### `VitalStrikeMinigame` (플레이어 프리팹)
- 다이얼 위로 바늘이 0도(12시)에서 시계 방향으로 한 바퀴(`Revolution Seconds`, 임시 1.2초) 돈다.
- 눈 칸, 발목 칸: 각각 `Zone Degrees`(임시 30도). 시작할 때마다 위치를 새로 정한다. 두 칸은 겹치지 않고, 출발 직후 `Zone Min Start Degrees`(70도) 전에는 놓지 않는다.
- 멈춘 순간 바늘이 든 칸이 결과. 칸 밖이거나 한 바퀴 안에 안 멈추면 `Miss`.
- 키 입력은 읽지 않는다. `SlotCaster`가 Space(`MarkPressed`)로 `Begin`, `Stop`을 부른다.
- 네트워크 (#223, network.md 「경우별 처리 방법」 4번): `NetworkBehaviour`다. 미니게임은 소유 클라이언트 화면에서 돌고, 결과가 바뀔 때마다(판정, `Miss`로 끝남, 취소, 지우기) `[Command]`로 서버의 `VitalStrikeMinigame`에 같은 결과를 넣는다. 서버의 급소공략은 서버의 `TakeResult()`로 읽는다.
  - Mirror는 보낸 순서대로 처리하므로 결과가 시전 요청보다 먼저 서버에 닿는다. 그래서 바늘이 도는 중에 슬롯을 떼면 `SlotCaster`가 시전 요청 전에 `FinishAsMiss()`로 끝낸다.
  - Host·네트워크 없는 테스트는 같은 오브젝트라 보내지 않는다.
  - 한계: 조작한 클라이언트가 늘 `Eye`를 보낼 수 있다 (보안은 나중).

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Started` (event) | - | - | [표시] 미니게임 시작. 다이얼을 띄운다 |
| `Finished` (event) | `VitalStrikeResult` | - | [표시] 끝남 (`Eye`, `Ankle`, `Miss`, 취소면 `None`). 성공·실패 연출 |
| `IsRunning` | - | `bool` | 바늘이 도는 중 |
| `NeedleAngle` | - | `float` | 바늘 각도 (0~360, 0 = 12시, 시계 방향) |
| `EyeZoneStart`, `AnkleZoneStart`, `ZoneDegrees` | - | `float` | 칸 위치. 칸은 `Start` ~ `Start + ZoneDegrees` |
| `PendingResult` | - | `VitalStrikeResult` | 급소공략이 아직 가져가지 않은 결과 |
| `Begin()`, `Stop()`, `Cancel()` | - | - / `VitalStrikeResult` / - | `SlotCaster`가 부른다 |
| `FinishAsMiss()` | - | - | 진행 중이면 `Miss`로 끝낸다. 슬롯을 떼서 시전하기 직전에 `SlotCaster`가 부른다 (#223) |
| `TakeResult()` | - | `VitalStrikeResult` | 급소공략이 시작할 때 부른다. 가져가면 지워짐. 진행 중이면 `Miss`로 끝냄 |

- `VitalStrikeMinigameView`: (임시) 머리 위에 다이얼을 OnGUI로 그린다. 빨간 칸 = 눈, 파란 칸 = 발목, 노란 선 = 바늘. 실제 그림(임혜원)은 위 event와 값을 읽어 바꾼다.

### 찌르기 2: 상처 공략·검 깊이찌르기·소환검 찌르기 (#165)
- 상처 공략: 검 찌르기와 같고(`ThrustSkill`), 대상이 출혈이면 `BonusRate = DamageBonus.BleedWoundExploit`(+100%, 2.0배).
- 검 깊이찌르기: 검 찌르기와 같고, `CDeepThrust.DeepCritChance`(임시 30%) 확률로 치명타가 나면 `CritBonusRate = DamageBonus.CritDeepThrust`(+150%, 2.5배). 치명타 여부는 `Hit`의 `DamageResult.IsCritical`.
  - `ThrustSkill`에 `CritChance`, `CritBonusRate`(기본 0)를 더했다. 덮어쓰면 `DamageRequest`에 들어간다.
- 소환검 찌르기: 캐릭터는 안 움직인다. 가장 먼저 소환한 사용 가능한 소환검이 대상에게 날아가 찌르고 돌아온다 (`SummonedSwords.Stab`, summoned-sword.md).
  - 데미지는 검이 닿은 순간(`StabHit`) 공격력 × 표 배율. 스킬(20틱)이 끝나기 전에 못 닿으면 데미지 없이 돌아온다. 끊겨도 검은 돌아온다.
  - 소환검이 없으면 시작 전에 `NoSummonedSwordError` (필요 조건 검사). 대상 값이 없으면 검을 보내지 않는다.
  - `Fired`는 검을 보낸 순간(검 위치에서 대상 방향), `Hit`은 닿은 순간.

### 던지기: 검 던지기·소환검 던지기·검 돌아오기 (#167)
- 검 움직임은 `PhysicalSword`(physical-sword.md), `SummonedSwords`(summoned-sword.md)가 맡고, 스킬은 명중 판정과 데미지만 한다. 데미지는 공격력 × 표 배율 (`ThrowSkillBase.DealDamage`).
- 검 던지기: 손에 든 검을 포물선으로 던진다. 대상이 `CSwordThrow.HitRange`(임시 5칸, 몸 가운데끼리 거리) 안이면 명중, 밖이면 빗나가 그 자리에 떨어진다(`Throw(null)`). 맞으면 데미지 + 처형(`DamageRequest.CanExecute`, 체력 2% 이하 일반 몬스터).
- 소환검 던지기: 가장 먼저 소환한 사용 가능한 소환검을 대상 쪽으로 일직선으로 던진다. `CSummonedThrow.HitRange`(임시 8칸) 안이면 명중, 밖이면 맞지 않고 날아가 벽·땅에 꽂힌다. 맞으면 데미지, 소환검은 몬스터 몸에 꽂혀 따라다닌다. 처형 없음.
  - 8칸인 이유: 슬롯의 대상 후보 거리(`Target Range`)가 8칸이라 그보다 먼 대상은 고를 수 없다.
- 검 돌아오기: 던진 **소환검** 중 실제로 가장 먼저 던진 것 하나를 날려서 돌려받는다 (`SwordThrowOrder` 번호표). 돌아오는 길에 지나가는 몬스터를 한 번씩 벤다. 돌려받을 소환검이 없으면 아무 일도 없이 끝난다. 맨손 가능.
  - 물리 검은 돌려받지 않는다. 물리 검은 Z(줍기)로만 손에 든다 (#232).
  - 스킬(20틱)이 끝난 뒤에도 검은 계속 날아오지만 그 뒤에 지나간 몬스터에게는 데미지가 없다.
- 데미지는 검이 닿은 순간 준다. 스킬이 끝날 때까지 닿지 못하면 데미지 없음.

### 휘두르기 2: 소환검 휘두르기·상처 가르기·검 다중베기 (#172)
- 소환검 휘두르기: 사용 가능한 소환검이 모두 제자리에서 각자 검기를 대상(없으면 바라보는 방향) 쪽으로 날린다. 검기는 검 휘두르기와 같다. 검기 하나당 heap 1칸을 따로 잡고 사라지면 따로 돌려준다 (`SwordWaveSkill.LaunchWave`). heap이 모자라면 거기까지만 날린다.
- 상처 가르기: 검 휘두르기와 같은 검기. 맞은 적이 출혈 중이면 `StatusEffects.AmplifyBleed(DamageBonus.BleedWoundSlash)`로 그 출혈이 끝날 때까지 출혈 틱 데미지 +100%(2.0배). 검기 데미지는 기본.
- 검 다중베기: 사용 가능한 소환검이 `CMultiSlash.StaggerTicks`(임시 2틱) 간격으로 차례로 대상에게 날아가 한 번씩 벤다 (`SummonedSwords.Stab`, 소환검 찌르기와 같은 움직임). 검마다 공격력 × 표 배율. 스킬이 끝날 때까지 못 닿은 검은 데미지 없이 돌아온다.
- 전장 휩쓸기 (#220): 고른 몬스터(`targets`, Target array)마다 사용 가능한 소환검이 하나씩, 가장 먼저 소환한 검부터 `CBattlefieldSweep.StaggerTicks`(검 다중베기와 같은 2틱) 간격으로 날아가 벤다 (`SummonedSwords.Stab`). 검마다 공격력 × 표 배율.
  - 대상이 소환검보다 많으면 남는 대상은 안 벤다. 소환검이 많으면 남는 검은 곁에 그대로.
  - 같은 몬스터를 두 번 넣으면 한 번만. 보내기 전에 죽은 대상은 건너뜀. 배열 값 검사(`InvalidTarget`)는 실행기가 시작할 때 한다.
  - 슬롯에서 여러 명 선택으로 찍어서 쓴다. `FunctionExecutorNetwork.RequestCast(슬롯, 대상 배열)` (이우림 #238, #243에서 연결).
- 주의: 실행기는 스킬을 `MemberwiseClone`으로 복사해 실행한다. 스킬 안의 리스트 같은 참조 필드는 초기값으로 만들지 말고 `OnSkillStart`(또는 처음 쓸 때) 새로 만든다. 안 그러면 원본과 복사본이 같은 리스트를 쓴다.

### 휘두르기 1: 검 휘두르기·검 크게베기 (#163)
- 공통(`SwordWaveSkill`): 검기(투사체)를 몸 가운데에서 날린다.
  - 검기는 플레이어의 `SwordWaveLauncher.SpawnWave` / `DespawnWave`로만 만들고 없앤다 (`NetworkSpawner`). 프리팹 `SwordWave`, `SwordWave_Wide`는 NetworkManager에 등록한다. 그림 순서는 Sorting Layer `Projectile` (#341).
  - 네트워크 (#229): 판정은 서버의 검기만 한다. `Launch`할 때 `[ClientRpc]`로 출발 위치·방향·속도·사거리를 보내고, 클라이언트는 같은 길로 그림만 날린다(판정 없음). 그래서 검기 프리팹에는 `NetworkTransform`을 붙이지 않는다.
  - heap: 날릴 때 검기를 heap에 올리고(`Malloc`, 표의 heap 크기 1), 검기가 끝나면(사거리 끝, 첫 대상에서 멈춤) 그 틱에 검기를 없애고 `Free`. 스킬이 끝날 때 아직 날고 있어도 같다. 플레이어가 해제할 필요 없음.
  - heap이 모자라면 검기를 날리지 않는다 (에러 아님). 끊기면(실행 중단, 취소) 검기는 없애고 heap은 누수.
  - 판정: 검기가 매 물리 프레임 판정 상자에 걸린 몬스터를 알리고(`SwordWave.MonsterHit`), 스킬이 데미지를 준다. 시전자 씬의 물리 세계.
  - 데미지: `new DamageRequest(공격력, 표 배율)` → `DamageCalculator.Calculate` → `IDamageable.TakeDamage` → `SkillEffectEvents.Hit`. `Fired`는 날린 순간.
- 검 휘두르기: 고른 대상(`target`)의 몸 가운데 쪽으로 날린다 (값이 없으면 바라보는 방향). 처음 닿은 몬스터 한 명에서 멈춘다. 검기 1×2, 사거리 8, 속도 25 (임시).
- 검 크게베기: 바라보는 방향으로 날린다. 관통하며 판정 상자(1×4)에 걸린 몬스터를 한 번씩 벤다. 맞은 몬스터에 출혈(`StatusEffects.TryApply`). 사거리 6, 속도 25, 배율 0.5 (임시).
- 그림: 임혜원 `_HW/Effects/SwordWave`, `SwordWave_Wide` (오른쪽을 보는 그림, 날아가는 방향으로 돌림).

### 소환 2: 표식 남기기·검무 (#243)
- 표식 남기기 (`CMark`, 단일)
  - 사용 가능한 소환검이 2개(`CMark.RequiredSwords`) 미만이면 시작 전에 `NoSummonedSwordError` (`CountUsableSummonedSwords`를 덮어씀).
  - 가장 먼저 소환한 사용 가능한 검 2개가 대상 왼쪽·오른쪽 옆으로 간 뒤 안쪽으로 찌르고 돌아온다 (`SummonedSwords.Stab(검, 대상, side)`).
  - 검마다 닿은 순간 공격력 × 표 배율 (약하게, 임시 0.5). 두 검이 모두 닿으면 표식(`Mark`, 5초) + 기절(`Stun`, 1.0초). 표식은 다음 공격부터 2배라 이번 찌르기에는 안 붙는다.
  - 스킬이 끝날 때까지 둘 다 닿지 못하면 표식·기절은 없다.
- 검무 (`CSwordDance`, 단일. #243에서 범위 → 단일로 바꿈)
  - 사용 가능한 소환검이 모두 대상 둘레에 고르게 나뉘어 돈다 (`SummonedSwords.Dance`, `Dancing` 상태).
  - `CSwordDance.HitIntervalTicks`(임시 6틱)마다, 대상 가운데에서 2.5칸 안의 검마다 한 번씩 공격력 × 표 배율(0.5) + 출혈(`StatusEffects.TryApply(Bleed)`). 20틱이면 검 하나당 대략 3번.
  - 스킬이 끝나거나 끊기면 `EndDance`로 검이 곁으로 돌아온다. 대상이 죽으면 더 베지 않는다.
- 소환 계열 데미지는 `SummonDamage.Deal`(공격력 × 표 배율 → `TakeDamage` → `SkillEffectEvents.Hit`)을 함께 쓴다.
- 슬롯에서 전장 휩쓸기: 여러 명 선택으로 찍은 대상을 `RequestCast(슬롯, 대상 배열)`로 서버에 보내 시전한다 (slot-caster.md, #238).

### 패링 (#245)
- 누른 순간부터 `CParry.WindowSeconds`(임시 0.3초) 동안 패링 가능한 몬스터 투사체(`MonsterAttackData.Parryable`)를 잡는다. 몸통 박치기, 낙하 공격은 못 잡는다. 맨손 불가.
- 판정은 플레이어의 `ParryGuard`(아래 표)가 한다. 몬스터 투사체(`MonsterProjectile`, 김도훈)가 플레이어에 공격 효과를 주기 전에 `ParryGuard.TryParry(this)`를 부르고, `true`면 효과 없이 투사체를 없앤다 (관통 투사체도 잡히면 없앰).
- 한 번 잡으면 판정이 닫힌다 (1회).
- 반격: 잡은 다음 틱에 몸 가운데에서 투사체가 날아온 쪽(투사체 속도의 반대)으로 검기를 날린다 (`SwordWaveSkill`, 검 휘두르기와 같은 1×2, 사거리 14, 속도 30, 임시). 처음 닿은 몬스터에게 공격력 × 표 배율. heap은 표의 1칸, 검기가 끝나면 돌려준다.
- 판정·반격은 서버에서 한다. 잡은 순간 `ParryGuard.ParryEffectPlayed`가 모든 화면에서 불린다 (연출용).

#### `ParryGuard` (플레이어 프리팹 최상위)

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TryParry(MonsterProjectile projectile)` | 닿은 투사체 | `bool` | 몬스터 투사체가 플레이어에 닿을 때 김도훈 코드가 부른다. 판정 시간이고, `Attack.Parryable`이고, `Misses`가 아니면 잡고 `true` (공격 효과를 주지 말고 투사체를 없앤다). 아니면 `false` |
| `IsOpen` | - | `bool` | 패링 판정 시간이면 `true` |
| `Open(float seconds)`, `Close()` | 초 | - | 판정 시간을 열고 닫는다. 패링 스킬이 부른다 |
| `Parried` (event) | `Vector2` 잡은 위치, `Vector2` 되돌려 보낼 방향 | - | [로직] 잡았을 때. 패링 스킬이 반격한다. 서버에서만 |
| `ParryEffectPlayed` (event) | `Vector2` 잡은 위치 | - | [연출] 잡았을 때. 모든 화면에서. 패링 이펙트·소리 (임혜원) |

### 이동 스킬: 대시·더블점프·검 타기 (#256)
- 공통(`MovementSkill`): 스킬 동안 표의 heap(1칸)을 잡고 끝나면 돌려준다. heap이 모자라면 이동하지 않는다. 끊기면 heap은 누수.
- 이동은 소유 클라이언트가 하므로 스킬은 `PlayerMovement`에 요청만 한다 (서버에서는 `[TargetRpc]`, player-movement.md 「이동 스킬」).
- 대시 (`common.dash`): 바라보는 쪽으로 4칸 / 0.15초 (임시). 공중 가능, 맨손 가능.
- 더블점프 (`common.double_jump`): 해금하면 공중에서 Space로 한 번 더 (패시브, `PlayerMovement`가 함). **슬롯에는 넣지 못한다** (`SlotLoadout.IsEquippableSkill`이 false). 블록코딩에서 부르면 공중 점프 한 번.
- 검 타기 (`c.sword_ride`): 사용 가능한 소환검 1개가 발밑에 붙고(`SummonedSwords.Ride`, `Riding` 상태), 스킬 동안 중력 없이 ←→↑↓로 난다. `PlayerMovement.RideSeconds`(임시 30초) 동안 날고, 스킬(20틱)은 시작만 하고 먼저 끝난다. 시간이 다 되면 내려오고 검은 곁으로 (끊기면 바로). 대시·장소 이동·경직·사망으로 타기가 먼저 끝나도 검은 바로 곁으로 돌아와 다른 스킬에 쓸 수 있다 (#325, `PlayerMovement`가 서버에 알림). 이미 타는 중(`SummonedSwords.HasRidingSword`)에 다시 시전하면 두 번째 검을 쓰지 않는다. 머리 위 작은 바로 남은 시간 표시(`SwordRideTimerView`, 임시). 슬롯 키를 뗀 뒤 ←→↑↓로 난다. ↑↓는 `MoveY`(#271)를 `PlayerMovement`가 읽는다 (#317, player-movement.md). 키를 떼면 그 높이에 멈추고, 아래 바닥과는 `Ride Clearance`만큼 떠 있어 ↓로도 바닥에 닿지 않는다. 발판 아래로는 ↓ + Space로 내려간다(땅은 못 뚫음, #351).

### 검 소환·해제 (#102, #220)
- 검 소환 (`c.summon_sword`, malloc)
  - 시작할 때(첫 틱) `SummonedSwords.SpawnSword` → `PredefinedFunction.Malloc(task, 소환검, CSummonSword.SwordHeapSize)` → 성공하면 `SummonedSwords.Register`, `NullPointer`면 `SummonedSwords.DespawnSword`.
  - heap 크기는 `PredefinedStructs`의 `Sword` 크기(임시 2칸). 블록코딩의 `malloc(Sword)`와 같은 소환검이다 (#220).
  - 반환: 만든 소환검의 `Sword*`. 못 만들면 `NULL` (#220).
  - 소환검이 이미 5개(`CountHeapObjects<SummonedSword>`, 누수 포함)거나 heap이 모자라면 만들지 않는다. C의 malloc처럼 에러로 끝내지 않는다.
  - 포인터는 이 작업의 것. 정상으로 끝나면 전역 포인터 목록으로 옮겨지고, 도중에 실행 중단·실패로 끝나면 그 소환검은 누수된다.
- 해제 (`c.free_sword`, free)
  - `sword`(`Sword*`)를 넘기면 그 소환검을 해제한다 (`free(s)`와 같음, #220).
  - 안 넘기면 마지막에 소환한 소환검부터 해제한다 (`GetGlobalPointers<SummonedSword>`의 마지막). 블록코딩 `malloc(Sword)`로 만든 검은 이 목록에 없으니 포인터를 넘기거나 `free`로 해제한다.
  - 날아가는 중·떨어진 소환검도 거리와 상관없이 해제한다. 물리 검, 누수된 소환검, 아직 진행 중인 검 소환의 소환검은 해제하지 않는다.
  - 타고 있는 검(검 타기)도 해제한다. 발밑 검이 없어지므로 검 타기도 끝나서 떨어진다 (#440, `SummonedSwords.DespawnSword`가 `PlayerMovement.StopRide`).
  - 순서: `TryGetHeapObject` → `PredefinedFunction.Free`. 오브젝트는 `SummonedSwords`가 `HeapStorage.ObjectFreed`를 받아 없앤다 (#191, #220). 쓸 수 없는 포인터면 `InvalidPointerError`로 그 작업만 끝난다.
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
- `Started`, `Ended`는 `DataDrivenSkill`이 모든 C 스킬에 대해 낸다. `Fired`, `Hit`은 효과를 채운 스킬부터 낸다 (지금: 찌르기·던지기 계열 전부, 휘두르기 계열(전장 휩쓸기 빼고)).
- `Raise...` 함수는 C 스킬만 부른다.
- 네트워크 (#223, network.md 「경우별 처리 방법」 2번): `SkillEffectEvents`는 `NetworkBehaviour`다. 서버에서 `Raise...`를 부르면 서버(Host 포함)에서 바로 event를 내고, `[ClientRpc]`로 다른 클라이언트에서도 같은 event를 낸다. 받는 쪽은 바꿀 것 없음.
  - `Hit`의 대상은 `NetworkIdentity`가 있는 오브젝트만 클라이언트에 전해진다. 없으면 클라이언트에서는 `null` (맞은 위치는 옴).
  - 네트워크 없는 테스트에서는 그 자리에서만 낸다.
- 찌르기 섬광: `ThrustEffectView`(플레이어)가 `Fired`를 받아 `ThrustFlash`(검 찌르기·집중찌르기) / `SkewerFlash`(꼬챙이) 프리팹을 찌르는 방향으로 만든다. 그림은 임혜원 #114 (`_HW/Effects/Thrust_Flash`, `Skewer_Flash`). 보여 주기만 하는 오브젝트라 `OneShotEffect`가 0.25초 뒤 스스로 없앤다. 그림 순서는 Sorting Layer `Effect` (#341).

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
| `VitalStrikeSimTest` | Test_JW의 VitalTest. 켜고 Play → Host, Game 창을 한 번 클릭 (가짜 키 입력, 약 10초). Q에 급소공략을 넣고(끝나면 되돌림) 눈 칸 → 3.0배 + 실명, 미니게임 중 대상 고정, 발목 칸 → 3.0배 + 이속 감소, 미니게임 안 함 → 기본 데미지, 칸 밖에서 멈춤 → 기본 데미지, 결과는 한 번 쓰면 지워짐을 확인. 「[VitalTest] 전체 통과」면 통과. 평소에는 꺼 둔다. 직접 해 보려면 `Restore Slot`을 끄고 한 번 돌리면 Q에 급소공략이 남는다. |
| `Slash2SimTest` | Test_JW의 Slash2Test. 켜고 Play → Host (약 12초). 사용 가능한 소환검을 3개까지 소환한 뒤 소환검 휘두르기(날린 검기 수만큼 맞음. heap이 모자라면 소환검 수보다 적게 날림, heap 원래대로, 검은 제자리), 상처 가르기(출혈 대상 배율 +100%, 다음 출혈 틱 2배, 출혈 풀리면 배율 없어짐, 출혈 아닌 대상은 배율 없음), 검 다중베기(소환검 수만큼 벰, 검 모두 돌아옴)를 확인. 「[Slash2Test] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SwordPointerSimTest` | Test_JW의 SwordPointerTest. 켜고 Play → Host (약 12초). 몬스터 2마리 이상. 블록코딩 `malloc(Sword)` → `free(s)`(진짜 소환검이 생겼다 사라짐, heap 원래대로), `Sword* s = SummonSword(); FreeSword(s);`, 소환검 던지기·찌르기에 두 번째 검 포인터를 넘기면 그 검을 씀, 해제에 포인터를 넘기면 그 검만 해제·같은 포인터로 또 해제하면 `InvalidPointerError`, 전장 휩쓸기 `[a, b, a]`(각 1번씩 맞음, 검 모두 돌아옴)를 확인. 「[SwordPointerTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SkillSyncLogTest` | Test_JW의 SkillSyncLog (#223). 켜 두고 에디터 Play → Host, `Test_JW`만 넣은 빌드 → Client. 클라이언트 캐릭터로 스킬을 쓰면 Host 콘솔에 「[SkillSyncLog] [클라이언트 …] 스킬 발사: 방향 / 명중: 대상, 데미지, 실명·이속감소」가 남는다. 돌진·바라보는 방향·급소공략 결과가 서버에 왔는지 사람이 로그로 확인한다. 평소에는 꺼 둔다. |
| `SwordSyncSlotsTest` | Test_JW의 SwordSyncSlots (#229). 켜 두면 내 캐릭터 슬롯에 Q 검 소환, W 소환검 던지기, E 검 던지기, R 검 돌아오기, A 검 휘두르기, S 대시, D 검 타기(#256)를 넣는다(에디터 Host·빌드 Client 각자). `Restore Slots`면 Play를 멈출 때 되돌린다. 소환검·검기·던진 검이 두 화면에서 같은 자리로 보이는지 사람이 확인한다. 평소에는 꺼 둔다. |
| `MarkDanceSimTest` | Test_JW의 MarkDanceTest (#243). 켜고 Play → Host (약 8초). 소환검 1개로 표식 남기기 → `NoSummonedSwordError`, 2개로 표식 남기기 → 검 2개가 각각 데미지·표식·기절·검 돌아옴, 검무 → 소환검 수 이상 맞음·출혈·검무 중 상태·끝나면 돌아옴을 확인. 「[MarkDanceTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `ParrySimTest` | Test_JW의 ParryTest (#245). Inspector에 Bullet Prefab(`MonsterBullet`), Parryable Attack(`Loop_Shot`), Unparryable Attack(`Packet_Drop`)을 넣고 켜서 Play → Host (약 4초). `ParryGuard.TryParry`를 직접 불러 패링 안 했을 때·패링 불가 공격은 못 잡음, 패링 가능 공격은 잡고 한 번에 닫힘, 날아온 쪽 몬스터에게 반격 검기가 맞음·heap 원래대로, 0.3초 뒤엔 못 잡음을 확인. 「[ParryTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `MovementSkillSimTest` | 이동 스킬 테스트 (#256). player-movement.md 「테스트」 참고. |
| `ThrowSkillSimTest` | Test_JW의 ThrowTest. 켜고 Play → Host (약 10초). 소환검 던지기(명중·몬스터에 꽂힘), 검 던지기(사거리 안 명중·데미지·근처에 떨어짐), 검 돌아오기(소환검이 곁으로 오고 오는 길 몬스터에 데미지, 물리 검은 땅에 그대로. 한 번 더 써도 물리 검은 안 옴, #232), 처형(체력 2% 이하 몬스터 죽음)을 확인. 몬스터 하나를 처형한다. 「[ThrowTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `Thrust2SimTest` | Test_JW의 Thrust2Test. 켜고 Play → Host (약 10초). 상처 공략(출혈 없음 기본, 출혈 2.0배), 깊이찌르기 6번(치명타는 2.5배, 아니면 기본, 한 번도 안 나면 경고), 소환검 찌르기(캐릭터 안 움직임, 고른 몬스터 기본 데미지, 검이 돌아옴), 소환검 없을 때 `NoSummonedSwordError`를 확인. 소환검이 없으면 하나 소환하고, 끝에 모두 해제한다. 「[Thrust2Test] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SwordWaveSimTest` | Test_JW의 WaveTest. 켜고 Play → Host (약 4초). 검 휘두르기(가장 가까운 몬스터 한 명만 맞음, 데미지, 날아가는 동안 heap 사용, 끝나면 heap 원래대로, 검기 사라짐), 검 크게베기(사거리 안 몬스터 모두 관통해서 맞음·밖은 안 맞음, 모두 출혈, 0.5배, heap 원래대로. 사거리 안에 2명 미만이면 경고)를 확인. 「[WaveTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `SkillCastTest` | Test_JW의 SkillTest. 켜고 Play → Host. 표 검사(이름·틱 수·쿨타임·파라미터), 24개 시전(표의 틱 수만큼 실행되는지. 시작 전에 1.5초 기다리고, 소환검이 필요한 스킬 앞에서는 던진 검을 되돌리거나 모자란 만큼 소환함(표식 남기기는 2개), #220, #243. 던진 물리 검은 매번 손으로 되돌림, #232), 맨손 검사(물리 검을 던져 맨손으로 만든 뒤 검 찌르기는 `UnarmedError`, 검 돌아오기는 정상)를 한다. 「[SkillTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

## 맞춘 결과
- 이우림과 정함
  - 예시 `_UL/.../Examples/Thrust.cs`(검 찌르기)를 지우고, `c.thrust`는 `CThrust`(`_JW`)가 쓴다. 같은 ID가 둘이면 둘 다 등록되지 않기 때문
  - 고정 ID 24개는 `PredefinedFunctionIds`에 모은다
  - heap 사용량은 표에 적어 두고, 스킬이 `PredefinedFunction.Malloc`에 넘긴다. 검 소환은 `Sword` struct 크기 (#220)
  - 소환검 = `Sword` struct. `malloc(Sword)` 블록과 검 소환 스킬이 같은 소환검을 만든다. `Sword` 크기는 임시 2칸 (#191, #220)
- 정한 것
  - 대시는 스킬(`common.dash`)로 둔다. 왼쪽 Shift는 달리기(이동 속도 증가)로 바꾼다 (입력 키는 이우림이 반영)
- 임혜원과 정함 (#152)
  - 이펙트는 `SkillEffectEvents`의 event를 받아서 튼다 (스킬 코드는 고치지 않음)
- 정해야 할 것
  - 수치 전부 (데미지, 쿨타임, 메모리, 틱 수): 기획
  - 찌르기 사거리 2.5, 꼬챙이 관통 길이 3, 돌진 속도 30: 기획
  - 급소공략 미니게임 한 바퀴 1.2초, 칸 크기 30도: 기획
  - 돌진이 몬스터 몸에 막히는 것 (지금은 막힘. 뚫고 지나갈지): 기획
  - 위 「가정한 것」 3개: 기획
  - 검기 크기·사거리·속도 (휘두르기 1×2·8, 크게베기 1×4·6, 속도 25): 기획
  - 깊이찌르기 치명타 확률 30%, 소환검 찌르기 속도 35: 기획
  - 던지기 명중 사거리 (검 5, 소환검 8), 돌아오는 속도 30: 기획
  - 검 다중베기 간격 2틱: 기획
- 정한 것 (#172)
  - 상처 가르기의 「출혈 도트 2.0배」는 맞은 적의 남은 출혈 틱 데미지가 2배 (이번 검기 데미지가 아님)
  - 소환검 휘두르기는 검기, 검 다중베기는 소환검이 직접 날아가 벰
- 정한 것 (#167)
  - 빗나감은 확률이 아니라 거리로 정함
  - 물리 검은 포물선, 소환검은 일직선으로 날아가 꽂힘
  - 검 돌아오기는 소환검을 실제로 던진 순서대로 하나씩, 날아서 돌아옴. 물리 검은 Z로만 줍는다 (#232에서 바꿈. #167에서는 물리 검도 돌아왔음)
  - 소환검 던지기는 맨손(물리 검을 던진 상태)이어도 쓸 수 있음 (표 `usableUnarmed` 켬)
- 정한 것 (#163)
  - 검기 heap은 검기가 사라지면 자동 해제 (플레이어가 free하지 않음)
  - 검 휘두르기는 첫 대상에서 멈춤, 검 크게베기는 관통하며 판정 범위 안의 몬스터만
