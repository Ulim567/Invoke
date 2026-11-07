# 상태 이상 (StatusEffects)

- 담당자: 박진우

## 하는 일
- 대상(몬스터)에 걸린 상태 이상 5종(출혈, 표식, 기절, 실명, 이속 감소)의 걸기, 지속 시간, 풀림을 맡는다.
- 출혈의 지속 데미지를 준다.
- 표식은 `DamageCalculator`가 읽어 데미지 +100%를 붙인다 (damage-calculation.md).
- 다른 시스템이 대상이 어떤 상태인지 읽을 수 있게 한다.
- 상태 이상 아이콘을 대상 머리 위에 띄운다 (`StatusIconView`, 아래).
- 하지 않는 일
  - 기절·이속 감소·실명에 대한 몬스터 반응(AI 정지, 이동 속도, 공격 빗나감): 김도훈 「몬스터가 상태 이상에 반응하고 죽으면 드롭하기」. 이 컴포넌트의 값을 읽기만 하면 된다.
  - 어떤 스킬이 어떤 상태를 거는지: 각 스킬 이슈 (아래 표)
  - 몸 효과(상태 이상이 걸린 대상의 모습): 박진우 `StatusEffectView` (위 네트워크 항목의 읽는 값과 이벤트를 쓴다)

## 동작 규칙
- 같은 상태를 다시 걸면 **지속 시간만 새로 시작**한다. 남은 시간에 더하지 않고, 겹치지 않는다.
- 시간이 다 되면 풀린다. 대상이 죽으면(`Monster.Died`) 모든 상태가 풀린다. 죽은 대상에게는 걸리지 않는다.
- 출혈
  - 처음 건 뒤 `BleedInterval`(1초)마다 `BleedDamage`(5)를 준다. 지속 5초면 5번, 총 25.
  - 출혈 중에 다시 걸면 지속 시간만 늘어나고 데미지 간격은 그대로다.
  - 출혈 배율 (#172): `AmplifyBleed(bonusRate)`를 부르면 지금 출혈이 풀릴 때까지 틱 데미지에 `BonusRate`를 더한다 (상처 가르기 +100% → 10). 겹치지 않고 큰 값 하나. 출혈이 풀리면(시간·`Clear`·죽음) 없어진다.
  - 데미지는 `DamageCalculator.Calculate(DamageRequest.Flat(BleedDamage))`를 거쳐 같은 오브젝트의 `IDamageable.TakeDamage`로 준다.
  - 출혈은 스킬 공격이 아니라서 **표식 2배와 처형을 받지 않는다** (대상 정보 없이 계산). 기획 확인 필요.
- 표식: 걸려 있는 동안 `DamageTarget.FromMonster`가 `IsMarked = true`로 만들어 계산기가 +100%를 붙인다.
- 상태 값은 이 컴포넌트만 바꾼다 (코드 작성 규칙 2).

### 수치 (`StatusDefaults`, 전부 임시. 기절 1초만 기획 값)

| 상태 | 효과 | 지속 | 값 | 거는 스킬 (기획) |
| --- | --- | --- | --- | --- |
| 출혈 `Bleed` | 도트 데미지 | 5초 | 1초마다 5 | 검 크게베기, 검무 |
| 표식 `Mark` | 데미지 2.0배 | 5초 | +100% | 표식 남기기 |
| 기절 `Stun` | 행동 불가 | 1초 | - | 표식 남기기 |
| 실명 `Blind` | 이 대상의 공격이 빗나감 | 3초 | - | 급소공략 (눈, #161) |
| 이속 감소 `Slow` | 이동 속도 감소 | 3초 | ×0.5 | 급소공략 (발목, #161) |

## 다른 시스템이 쓰는 것

몬스터 프리팹 최상위 오브젝트(`Monster`와 같은 곳)에 `StatusEffects`를 붙인다. `GetComponent<StatusEffects>()`로 가져온다.

### `StatusEffects`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Apply(StatusType type)` | 상태 | - | 기본 지속 시간으로 건다. 이미 걸려 있으면 시간만 새로 시작 |
| `Apply(StatusType type, float duration)` | 상태, 초 | - | 지속 시간을 정해 건다 |
| `StatusEffects.TryApply(GameObject target, StatusType type)` | 대상, 상태 | `bool` | 대상(또는 부모)의 `StatusEffects`로 건다. 없으면 `false`. 스킬이 맞은 대상에 쓴다 |
| `Has(StatusType type)` | 상태 | `bool` | 걸려 있으면 `true` |
| `RemainingTime(StatusType type)` | 상태 | `float` | 남은 시간(초). 없으면 0 |
| `Clear(StatusType type)` / `ClearAll()` | 상태 / - | - | 바로 푼다 |
| `IsStunned` | - | `bool` | 기절 중. **몬스터 AI가 이 동안 멈춘다** |
| `IsBlinded` | - | `bool` | 실명 중. **이 대상의 공격은 빗나간다** |
| `MoveSpeedMultiplier` | - | `float` | 이속 감소 중이면 0.5, 아니면 1. **몬스터 AI가 이동 속도에 곱한다** |
| `IsMarked` | - | `bool` | 표식 중. 계산기가 자동으로 읽는다 |
| `StatusApplied` (event) | `StatusType` | - | [표시] 걸렸을 때 (다시 걸 때도) |
| `StatusRemoved` (event) | `StatusType` | - | [표시] 풀렸을 때 |
| `AmplifyBleed(float bonusRate)` | 추가분 | - | (#172) 출혈 중이면 그 출혈이 끝날 때까지 틱 데미지 추가분. 상처 가르기가 부른다 |
| `BleedBonusRate` | - | `float` | (#172) 지금 출혈 틱 데미지 추가분. 0이면 기본 |
| `BleedTicked` (event) | `int` 데미지 | - | [로직] 출혈 데미지를 줬을 때 |

### 네트워크 (#325)

- `StatusEffects`는 `NetworkBehaviour`다. 몬스터 프리팹의 `NetworkIdentity`와 함께 쓴다. 스크립트 연결은 그대로라 프리팹을 고치지 않는다.
- 상태를 걸고 푸는 일(`Apply`, `Clear`, `ClearAll`, `AmplifyBleed`), 출혈 데미지, 시간이 다 된 상태를 푸는 일은 **서버에서만** 한다. 클라이언트에서 `Apply`와 `Clear`를 불러도 아무 일도 없다.
- 서버가 걸린 상태와 끝나는 시각(`NetworkTime.time` 기준)을 `SyncDictionary`로 클라이언트에 보낸다. 나중에 접속한 클라이언트도 현재 걸린 상태를 받는다.
- 클라이언트에서도 서버와 같게 읽는 값: `Has`, `RemainingTime`(전송 지연만큼 오차), `IsStunned`, `IsBlinded`, `IsMarked`, `MoveSpeedMultiplier`
- 모든 화면(서버, Host, 클라이언트)에서 불리는 이벤트: `StatusApplied`(다시 걸 때도), `StatusRemoved` [표시]. `StatusIconView`와 몸 효과(`StatusEffectView`)는 이 이벤트로 그린다.
  - 접속하기 전부터 걸려 있던 상태는 접속한 순간 `StatusApplied`로 한 번씩 알린다.
- 서버에서만 의미 있는 값: `BleedTicked`(이벤트), `BleedBonusRate`. 클라이언트에는 보내지 않는다.
- 몬스터 AI와 `DamageCalculator`가 읽는 값(`IsStunned`, `IsBlinded`, `IsMarked`, `MoveSpeedMultiplier`)은 서버에서 읽으므로 판정은 그대로다.

### `StatusIconView` (상태 이상 아이콘 표시)

몬스터 프리팹 최상위 오브젝트(`StatusEffects`와 같은 곳)에 붙인다. `StatusEffects`가 없으면 자동으로 붙는다 (RequireComponent).

- `StatusApplied` / `StatusRemoved`를 받아 걸린 상태마다 아이콘 하나를 띄운다. 값은 바꾸지 않는다 (표시 전용).
- 아이콘은 몬스터의 자식으로 만들어져 몬스터를 따라다니고, 몬스터가 사라지면 같이 사라진다.
- 위치: 콜라이더 윗변에서 `heightAboveTop`(1.4) 위(그 아래에 몬스터 체력 바가 뜬다, monster-health-bar.md). 여러 개면 걸린 순서대로 `spacing`(1.1) 간격으로 가로로 놓고, 몸 가운데에 맞춰 정렬한다.
- 같은 상태를 다시 걸어도 아이콘은 늘지 않는다. 풀리면 그 아이콘만 사라지고 남은 것이 다시 가운데 정렬된다.
- 아이콘 프리팹: `Assets/_JW/Resources/StatusIcon.prefab` (SpriteRenderer, Sorting Layer `WorldUI`). 인스펙터 `iconPrefab`이 비어 있으면 이걸 불러온다.
- 그림: 임혜원 #114 `StatusIcons_0..4`를 `StatusType` 순서(출혈, 표식, 기절, 실명, 이속 감소)로 쓴다. 그림을 바꾸려면 StatusIcon 프리팹의 `sprites`만 바꾸면 된다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `ShownStatuses` | - | `IReadOnlyList<StatusType>` | 지금 띄운 아이콘들 (왼쪽부터) |
| `TryGetIconPosition(StatusType type, out Vector3 position)` | 상태 | `bool` | 그 아이콘의 월드 위치. 없으면 `false` (테스트용) |

### `StatusEffectView` (상태 이상 몸 효과, #353)

몬스터 프리팹 최상위 오브젝트(`StatusEffects`와 같은 곳)에 붙인다. `StatusEffects`가 없으면 자동으로 붙는다 (RequireComponent).

- `StatusApplied` / `StatusRemoved`를 받아 그린다 (모든 화면, 위 「네트워크」). 값은 바꾸지 않는다 (표시 전용). 네트워크 오브젝트가 아니다.
- 색 효과 (그림 없이 됨, 값은 임시)
  - 실명: SpriteRenderer 색에 `Blind Tint`(어두운 회색)를 곱한다
  - 이속 감소: `Slow Tint`(파란색)를 곱한다. 실명과 같이 걸리면 둘 다 곱한다
  - 출혈: 데미지가 들어갈 때마다 `Bleed Flash Seconds`(0.15초) 동안 `Bleed Flash Color`(빨강)로 번쩍인다. 서버·Host는 `BleedTicked`(실제 데미지)에 맞추고, 클라이언트는 걸린 순간부터 `BleedInterval`(1초)마다 번쩍인다
  - 색은 상태가 걸린 동안만 `LateUpdate`에서 칠하고, 모두 풀리면 처음 색(`Awake` 때 색)으로 한 번 되돌린다. 그 뒤로는 색을 건드리지 않는다
- 그림 효과: 걸린 동안 대상의 자식으로 띄우고, 풀리면 없앤다. Inspector 칸이 비어 있으면 기본 그림 `Assets/_JW/Resources/StatusEffects/{상태 이름}.prefab`(Bleed, Mark, Stun, Blind, Slow, #363)을 쓴다. 그래서 몬스터 프리팹마다 칸을 채우지 않아도 된다. 기본 그림도 없으면 색 효과만 낸다
  | 칸 | 상태 | 자리 |
  | --- | --- | --- |
  | `Bleed Effect` | 출혈 (핏방울) | 몸 가운데 |
  | `Mark Effect` | 표식 (다이아몬드) | 몸 가운데 |
  | `Stun Effect` | 기절 (머리 위 도는 별) | 콜라이더 윗면 + `Head Effect Height`(0.2). 아이콘보다 낮게 |
  | `Blind Effect` | 실명 (검은 연기) | 머리 쪽 (윗면에서 몸 높이의 20% 아래) |
  | `Slow Effect` | 이속 감소 (발밑에서 자라나는 얼음) | 발밑 |
  - 기본 그림 (#363, PixelLab, 16 PPU): 스프라이트 시트 `Assets/_JW/Effects/Status/status_*.png`를 잘라, 프리팹(SpriteRenderer Sorting Layer `Effect` + `SpriteFrameLoop`)으로 만들었다
    | 프리팹 | 시트 | 프레임 | 재생 |
    | --- | --- | --- | --- |
    | `Bleed` | `status_bleed.png` 32×32 | 9 | 반복 (맺힘 → 떨어지며 작아짐 → 사라짐) |
    | `Mark` | `status_mark.png` 32×32 | 8 | 반복 (청록 다이아몬드가 커졌다 작아짐) |
    | `Stun` | `status_stun.png` 32×32 | 10 | 반복 (노란 별이 타원으로 돎) |
    | `Blind` | `status_blind.png` 32×32 | 9 | 반복 (검은 연기) |
    | `Slow` | `status_slow.png` 48×48, 기준점은 얼음 밑동(아래에서 3px) | 18 | 앞 9장 한 번(얼음이 자라남) → 뒤 9장 반복(반짝임). `Play Once Count` 9 |
  - 시트 가져오기 설정·자르기·프리팹 5개는 에디터 메뉴 **Invoke → JW → 상태 이상 그림 프리팹 만들기**(`StatusEffectPrefabBuilder`, 에디터 전용)로 만든다. 시트를 바꾸면 다시 누른다 (프리팹에서 바꾼 값도 되돌아감)
  - 몬스터마다 다른 그림을 쓰려면 그 프리팹의 칸에 넣는다. 코드는 고치지 않는다
- 같은 상태를 다시 걸어도 효과는 늘지 않는다. 대상이 죽으면 `StatusEffects`가 모두 풀어서 효과도 모두 사라진다
- 한계: 색을 바꾸는 다른 코드(김도훈 `CloneGlitch` 등)와 같은 SpriteRenderer를 쓰면, 상태가 걸린 동안은 이 컴포넌트의 색이 덮는다

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `ShownStatuses` | - | `IReadOnlyCollection<StatusType>` | 지금 몸 효과를 보여 주는 상태 (테스트용) |
| `BleedFlashCount`, `IsFlashing` | - | `int` / `bool` | 출혈 번쩍임 횟수, 지금 번쩍이는 중 (테스트용) |
| `HasEffectObject(StatusType type)` | 상태 | `bool` | 그 상태의 그림 효과가 떠 있는지 (테스트용) |

### `SpriteFrameLoop` (#363)

스프라이트 프레임을 Animator 없이 반복 재생한다. 효과 프리팹의 SpriteRenderer 옆에 붙인다. 보여 주기만 하는 오브젝트에 쓴다.
- `Frames`: 순서대로 보여 줄 스프라이트, `Frames Per Second`: 초당 프레임(기본 12)
- `Play Once Count`: 앞의 이 장수는 처음에 한 번만, 그 뒤 나머지만 반복 (0이면 전부 반복)
- 켜질 때마다 처음부터 다시 재생한다

### 테스트
| 이름 | 설명 |
| --- | --- |
| `StatusIconSimTest` | Test_JW의 StatusIconTest. 켜고 Play → Host (몬스터가 나타날 때까지 기다린 뒤 약 4초). 몬스터에 `StatusEffects`가 미리 붙어 있어야 한다 (네트워크 컴포넌트라 실행 중에 붙이지 않음, #325. 없으면 실패). `StatusIconView`는 없으면 테스트 동안 붙인다. 머리 위 1개, 알맞은 그림, 3개 순서·가운데 정렬, 다시 걸어도 3개, 기절이 풀리면 2개로 다시 정렬, 몬스터가 죽으면 모두 사라짐을 확인. 「[StatusIconTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `StatusIconPlayTest` | (임시) Test_JW의 StatusPlayTest. 상태 이상 거는 스킬이 아직 없어서 만든 직접 해 보는 테스트. 켜고 Play → Host, Q(찌르기)로 몬스터를 맞힐 때마다 출혈 → 표식 → 기절 → 실명 → 이속 감소를 하나씩 8초 동안 건다. 상태 이상 스킬이 생기면 없앤다. 평소에는 꺼 둔다. |
| `StatusEffectsTest` | Test_JW의 StatusTest. 켜고 Play → Host (몬스터가 나타날 때까지 기다린 뒤 약 6초). 몬스터에 `StatusEffects`가 미리 붙어 있어야 한다 (없으면 실패). 출혈 총 데미지, 표식 2배, 기절·이속 감소·실명의 지속과 풀림, 다시 걸면 시간 새로 시작을 확인. 「[StatusTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `StatusEffectViewSimTest` | (#353) Test_JW의 StatusViewTest. 켜고 Play → Host (몬스터가 나타날 때까지 기다린 뒤 약 8초, 마지막에 5개를 4초 동안 보여 줌). 실명 어두운 틴트·풀리면 처음 색, 이속 감소 파란 틴트, 둘 다 섞임, 출혈 1초마다 빨간 번쩍임(약 2.3초에 2번 이상), 다시 걸어도 하나, 그림 효과 5개(기본 그림이 있으면 뜨고 없으면 없음, #363), 모두 풀리면 처음 색·그림 효과 모두 사라짐. `StatusEffectView`가 없으면 테스트 동안 붙인다. 「[StatusViewTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

- Test_JW의 `TestMonster`(김도훈 프리팹)에는 `StatusEffects`·`StatusIconView`가 없어서, Test_JW 씬의 TestMonster 세 마리에 씬에서 붙여 둔다 (#348). 실제 몬스터 프리팹(Leak, Loop, Worm, Pillar, Gateway, 허수아비)에는 이미 붙어 있다.

## 맞춘 결과
- 김도훈과 정할 것
  - 몬스터 프리팹(Leak, Loop, Worm, Pillar, Gateway, GatewayClone)에 `StatusEffectView` 붙이기 → #361에서 붙임. 그림 효과는 기본 그림(Resources)을 쓰므로 칸은 비워 둔다 (#363)
  - 기절·이속 감소·실명 반응은 형 이슈에서 `IsStunned`, `MoveSpeedMultiplier`, `IsBlinded`를 읽어서 처리
  - 몬스터 프리팹(Leak, Loop 등)에 `StatusEffects`, `StatusIconView` 붙이기 (형 폴더)
- 정한 것
  - 같은 상태를 다시 걸면 지속 시간만 새로 시작 (겹치지 않음)
- 정해야 할 것 (기획)
  - 수치 전부 (기절 1초 제외)
  - 출혈 도트가 표식 2배를 받는지 (지금은 안 받음)
