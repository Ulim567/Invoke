# 자세 (PlayerStance)

- 담당자: 이우림

## 하는 일
- 플레이어 한 명의 자세 숫자(1~100, 자세가 없으면 0)를 가진다 (#538, Notion 「자세와 소환검 기획」 1. 자세). 자세 숫자는 `PlayerStance`만 바꾼다.
- 자세 숫자의 효과(주는 데미지, 계열별 데미지, 받는 피해, 쿨타임, 계열별 쿨타임, 계열별 상태 이상 지속 시간, 자해, 회복)를 캐릭터 언어의 자세 규칙(`StanceRule`)으로 알려 준다. C는 `CStanceRule`(#538), Python은 `PythonStanceRule`(#546, Notion 같은 문서 3. Python 자세).
- 하지 않는 일
  - 자세 숫자를 화면에 보이지 않는다. 플레이어는 `printf`로 출력해서 확인한다.
  - 데미지를 직접 계산하지 않는다. 스킬과 출혈·중독이 `DamageBonusOf`를 읽어 `DamageRequest.BonusRate`에 더한다.
  - 규칙의 효과를 켜고 끄지 않는다. 규칙은 처음부터 모두 적용된다. 스킬 트리의 규칙 노드(#545, skill-tree.md 「자세 노드」)는 규칙 내용을 보여 주는 일만 한다.

## 동작 규칙
- 자세 바뀜
  - `init_stance()`(predefined-function.md 「자세 function」)가 시전 시간 125 tick 뒤 `Roll()`로 1~100 무작위 숫자를 정한다
  - 장소를 옮기면 `InvokeNetworkManager.TravelPlayer`가 `Clear()`로 0을 만든다. 기획은 「전투 장소에 들어가면」이지만, 시전할 수 없는 로비에서는 자세를 쓸 일이 없어 모든 이동에서 없앤다
  - 다음 `init_stance()`까지 유지된다. 죽어도 그대로다
- 자세 규칙 (C, `CStanceRule`, 수치는 모두 임시)

| 숫자 조건 | 이득 | 손해 |
| --- | --- | --- |
| 홀수 | 찌르기 계열 데미지 +15% | 휘두르기 계열 데미지 -15% |
| 짝수 | 휘두르기 계열 데미지 +15% | 찌르기 계열 데미지 -15% |
| 3의 배수 | 쿨타임 -15% | 받는 피해 +10% |
| 5의 배수 | 데미지 +10% | 쿨타임 +10% |
| 40~60 | 스킬이 준 데미지의 5% 회복 | 데미지 -5% |
| 소수 | 데미지 +20% | 데미지를 준 스킬마다 자해(최대 체력의 1%, 11/18 2%에서 낮춤) |
| 피보나치 수 (1, 2, 3, 5, 8, 13, 21, 34, 55, 89) | 소환 계열 데미지 +20% | 찌르기 계열 데미지 -10% |
| 완전수 (6, 28) | 데미지 +30% | 없음 |

- 규칙 이름과 스킬 트리 규칙 노드 ID (#545, Notion 「자세 규칙」 표): 홀수 적중 `c.stance.odd`, 짝수 일망타진 `c.stance.even`, 3의 배수 조급함 `c.stance.three`, 5의 배수 신중함 `c.stance.five`, 40~60 흡혈 `c.stance.middle`, 소수 혹사 `c.stance.prime`, 피보나치 수 지휘 `c.stance.fibonacci`, 완전수 완벽 `c.stance.perfect`
- 규칙 표: 규칙마다 한 줄(`StanceRuleEntry`: 노드 ID, 이름, 조건 글, 숫자 조건, 효과 값). 효과는 조건에 맞는 줄의 효과를 모두 더한 것. 스킬 트리의 규칙 카드에 보이는 효과 글(좋은 효과·나쁜 효과)도 같은 효과 값에서 만든다(`StanceEffect.Describe`, 예: 「찌르기 계열 데미지 +15%」). 카드 문장은 「자세 숫자가 {조건 글}일 때 아래 효과가 붙습니다.」 수치를 고치면 화면 글도 같이 바뀐다 (#545)
- 「데미지」: 모든 스킬과 출혈·중독 도트. 「계열 데미지」: 스킬 표의 `family`가 그 계열인 스킬만 (이우림 결정 11/18). 던지기 계열은 계열 규칙이 없음
- 겹침: 여러 규칙에 해당하면 효과를 더한다. 예(Notion): 5는 데미지 +30%, 찌르기 +5%(합 +35%), 휘두르기 -15%(합 +15%), 소환 +20%(합 +50%), 쿨타임 +10%, 자해. 15는 쿨타임 -15% + 10% = -5%
- 0(자세 없음): 어떤 규칙도 적용하지 않음. 수학에서는 0이 짝수·3의 배수·5의 배수이지만 「자세 없음」에 효과가 붙지 않게 함
- 자세 규칙 (Python, `PythonStanceRule`, #546, 수치는 모두 임시)

| 숫자 조건 | 이득 | 손해 |
| --- | --- | --- |
| 큰 수 (51~100) | 원소 계열 데미지 +15% | 범위 계열 데미지 -15% |
| 작은 수 (1~50) | 범위 계열 데미지 +15% | 원소 계열 데미지 -15% |
| 4의 배수 | 구슬 계열 데미지 +20% | 원소 계열 데미지 -10% |
| 회문수 (1~9, 11, 22, …, 99) | 쿨타임 -15% | 받는 피해 +10% |
| 7의 배수 | 보조 계열 스킬이 거는 상태 이상(표식·기절·실명) 지속 시간 +30% | 보조 계열 쿨타임 +20% |
| 완전제곱수 (1, 4, 9, …, 100) | 받는 피해 -15% | 데미지 -5% |
| 세제곱수 (1, 8, 27, 64) | 데미지 +25% | 없음 |

- Python 규칙 이름과 노드 ID (#545): 큰 수 직격 `py.stance.big`, 작은 수 포화 `py.stance.small`, 4의 배수 응축 `py.stance.four`, 회문수 경솔 `py.stance.palindrome`, 7의 배수 집요함 `py.stance.seven`, 완전제곱수 비늘 `py.stance.square`, 세제곱수 각성 `py.stance.cube`
- Python 계열: 스킬 표의 `family`. 원소(매직 미사일·파이어볼·얼음 창·체인 라이트닝), 범위(메테오·블리자드·독 안개), 구슬(구슬 생성·발사·난사·폭발·보호막), 보조(저주·뱀 속박·섬광)
- Python 예(Notion): 1은 범위 +15%, 원소 -15%, 쿨타임 -15%, 받는 피해 -5%, 데미지 +20%. 64는 원소 +5%, 범위 -15%, 구슬 +20%, 받는 피해 -15%, 데미지 +20%. 7은 모든 스킬 쿨타임 -15%, 보조 계열 쿨타임은 -15% + 20% = +5%
- 효과가 쓰이는 곳
  - 주는 데미지: 스킬은 `DataDrivenSkill.StanceDamageBonus(caster)`(모든 스킬 + 그 스킬 계열 추가분), 출혈·중독 도트는 건 플레이어의 모든 스킬 추가분만(스킬 정보가 없음). 조건 배율처럼 `BonusRate`에 더함 (damage-calculation.md)
  - 받는 피해: `PlayerHealth.TakeDamage`가 피해를 줄이기 전에 `ApplyReceivedDamage` (player-health.md). 줄이는 규칙(Python 완전제곱수)도 같음
  - 쿨타임: `CooldownTracker`가 스킬이 끝날 때 능력치 감소를 적용한 값에 (1 + `CooldownRateOf(시전자, 끝난 스킬)`)를 곱함 (function-executor.md). 모든 스킬 증감 + 그 스킬 계열 증감(지금은 Python 보조 계열만). 스킬 표에 없는 함수(init_stance 등)는 모든 스킬 증감만. 능력치(`PlayerStats`)에는 넣지 않음: 능력치의 쿨타임 감소는 0~1로 잘려 쿨타임 증가를 표현할 수 없기 때문
  - 상태 이상 지속 시간 (#546): 보조 계열 스킬(저주·뱀 속박·섬광)이 상태를 걸 때 `DataDrivenSkill.StanceStatusDurationRate(caster)`를 `StatusEffects.TryApply`에 넘겨 기본 지속 시간 × (1 + 증감). 예: 표식 5초 → 6.5초 (status-effects.md)
  - 회복: 스킬이 맞힐 때마다(`DataDrivenSkill.NotifyDamageDealt`) 준 데미지 × 5%. 1 미만 부분은 모아 두었다가 1이 될 때마다 회복(이우림 결정 11/18, 공격력 10의 5%인 0.5를 버리거나 1로 올리지 않게). 자세가 바뀌면 모아 둔 부분은 버림. 다음 `Update`에 `PlayerHealth.Heal`(최대 체력을 넘지 않음). 도트는 회복 없음
  - 자해: 스킬 시전 하나가 처음 데미지(0보다 큼)를 준 순간 한 번. 최대 체력 × 1%를 데미지 계산과 같게 반올림(0보다 크면 최소 1). 다음 `Update`에 `PlayerHealth.TakeSelfDamage`로 깎음
    - 바로 바꾸지 않는 이유: 스킬 `OnTick` 안에서 자해로 죽으면 실행기가 모든 작업을 끊는데, 틱 도중에 그 스킬이 끊기면 남은 스킬 코드가 끊긴 스킬 위에서 돌기 때문
    - 경직·무적 없음, 무적 중에도 깎음, 받는 피해 증감·보호막을 거치지 않음, 체력이 0이 되면 사망
    - 같은 `Update`에 회복과 자해가 함께 있으면(41, 43 등) 회복을 먼저 함
    - 출혈·중독 도트는 스킬 시전이 아니므로 자해 없음
- 네트워크: 서버(또는 네트워크 없는 테스트)만 값을 가진다. 보내지 않는다(화면에 보이지 않는 값이고, 실행기와 데미지 판정이 서버에서 돎)

## 다른 시스템이 쓰는 것

공용 `Player.prefab` 최상위(`PlayerStats`, `PlayerHealth`와 같은 곳)에 `PlayerStance`가 붙는다. Python 플레이어 프리팹(`Player_Python.prefab`)은 `Player.prefab`의 변형이라 같이 붙어 있다. 붙지 않은 프리팹은 자세 효과가 없고, `init_stance`·`get_stance`는 콘솔 오류를 내고 0을 돌려준다.

### `PlayerStance`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Stance` | - | `int` | 지금 자세 숫자. 없으면 0 |
| `Effect` | - | `StanceEffect` | 지금 자세의 효과. 자세가 없거나 언어에 규칙이 없으면 효과 없음 |
| `Roll()` | - | `int` | [서버] 1~100 무작위로 다시 정하고 그 숫자를 돌려줌. `init_stance`가 부름 |
| `Clear()` | - | - | [서버] 자세를 없앰(0). 장소 이동 때 `InvokeNetworkManager`가 부름 |
| `ApplyReceivedDamage(int damage)` | 받을 피해 | `int` | [서버] 받는 피해 증감을 적용한 값. `PlayerHealth.TakeDamage`가 부름 |
| `NotifySkillDealtDamage(int damage, bool firstOfCast)` | 준 데미지, 이 시전의 첫 데미지인지 | - | [서버] 스킬이 데미지를 준 뒤. 회복이 있으면 데미지 × 비율을 모아 회복, `firstOfCast`이고 자해가 있으면 자해. 다음 `Update`에 바꿈. 보통은 `DataDrivenSkill.NotifyDamageDealt`를 거쳐 부름 |
| `DamageBonusOf(GameObject attacker, SkillFamily? family = null)` (static) | 때린 플레이어, 스킬 계열 | `float` | 주는 데미지 추가분(모든 스킬 + 그 계열). 계열 없이 부르면(도트) 모든 스킬 추가분만. `BonusRate`에 더함. 자세·컴포넌트가 없으면 0 |
| `CooldownRateOf(GameObject caster, PredefinedFunction skill)` (static) | 시전자, 끝난 스킬 | `float` | 쿨타임 증감(-0.15 = 15% 짧게). 모든 스킬 증감 + 그 스킬 계열 증감(#546). `CooldownTracker`가 곱함. 없으면 0 |
| `StatusDurationRateOf(GameObject caster, SkillFamily? family)` (static) | 시전자, 스킬 계열 | `float` | 그 계열 스킬이 거는 상태 이상의 지속 시간 증감(+0.3 = 30% 길게, #546). 보통은 `DataDrivenSkill.StanceStatusDurationRate`를 거쳐 부름. 없으면 0 |
| `NoStance`, `MinStance`, `MaxStance` | - | `int` | 0, 1, 100 |

### `StanceRule`, `StanceEffect`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `StanceRule.For(string language)` (static) | 캐릭터 언어 | `StanceRule` | 그 언어의 자세 규칙. C는 `CStanceRule`, Python은 `PythonStanceRule`(#546). 모르는 언어는 C |
| `EffectOf(int stance)` | 자세 숫자 | `StanceEffect` | 그 숫자의 효과(규칙 표에서 조건에 맞는 줄의 합). 0 이하이면 효과 없음 |
| `Rules` | - | `IReadOnlyList<StanceRuleEntry>` | 그 언어의 규칙 표 (#545). 화면에 보이는 순서 |
| `StanceRule.All` (static) | - | `IReadOnlyList<StanceRule>` | 모든 언어의 규칙 (C, Python, #545) |
| `StanceRule.TryGetRule(string nodeId, out StanceRuleEntry rule)` (static) | 규칙 노드 ID | `bool` | 모든 언어의 표에서 그 노드의 줄을 찾음. 없으면 false (#545) |
| `StanceRule.FunctionIds` (static) | - | `IReadOnlyList<string>` | 자세 함수 고정 ID 4개 (C·Python의 init_stance, get_stance). 스킬 트리 검사가 SkillTable 비교에서 뺄 때 씀 (#545) |
| `StanceRuleEntry.NodeId`, `Name`, `Condition`, `Effect` | - | - | 규칙 노드 ID(`c.stance.odd`), 이름(적중), 조건 글(2로 나누어떨어지지 않는 수), 효과 값 (#545) |
| `StanceRuleEntry.Gains`, `Losses` | - | `IReadOnlyList<string>` | 좋은 효과·나쁜 효과 글. 나쁜 효과가 없으면 Losses가 비어 있음. 화면에는 「이득·손해」라는 말을 쓰지 않음 (#545) |
| `StanceRuleEntry.Matches(int stance)` | 자세 숫자(1 이상) | `bool` | 이 규칙의 조건에 맞으면 true (#545) |
| `CStanceRule.OddNode` … `PerfectNode`, `PythonStanceRule.BigNode` … `CubeNode` | - | `string` | 규칙 노드 ID 상수 (#545) |
| `StanceEffect.Describe(List<string> gains, List<string> losses)` | 좋은 효과·나쁜 효과를 넣을 목록 | - | 효과 값을 화면 글로 나눔. 받는 피해·쿨타임은 줄어야 좋은 효과 (#545) |
| `StanceRule.IsPrime`, `IsFibonacci`, `IsPerfect` (static) | 숫자 | `bool` | 소수(1은 아님), 피보나치 수, 완전수이면 true |
| `StanceRule.IsPalindrome`, `IsSquare`, `IsCube` (static) | 숫자 | `bool` | 회문수, 완전제곱수, 세제곱수이면 true (#546). 0 이하는 false |
| `CStanceRule.MiddleMin`, `MiddleMax` | - | `int` | 40~60 규칙의 범위 40, 60 (양 끝 포함) |
| `PythonStanceRule.BigMin` | - | `int` | 큰 수 규칙의 가장 작은 숫자 51 (#546). 1~50은 작은 수 |
| `StanceEffect.DamageBonusFor(SkillFamily? family)` | 스킬 계열 | `float` | 모든 스킬 추가분 + 그 계열 추가분. null이면 모든 스킬 추가분만 |
| `StanceEffect.CooldownRateFor(SkillFamily? family)` | 스킬 계열 | `float` | 모든 스킬 쿨타임 증감 + 보조 계열 증감(보조 계열일 때만, #546). null이면 모든 스킬 증감만 |
| `StanceEffect.StatusDurationRateFor(SkillFamily? family)` | 스킬 계열 | `float` | 보조 계열이면 상태 이상 지속 시간 증감, 그 밖은 0 (#546) |
| `StanceEffect.DamageBonus` | - | `float` | 모든 스킬 데미지 추가분 (+0.1 = +10%) |
| `StanceEffect.ThrustDamageBonus`, `SlashDamageBonus`, `SummonDamageBonus` | - | `float` | 찌르기·휘두르기·소환 계열에만 붙는 추가분 |
| `StanceEffect.ElementDamageBonus`, `ZoneDamageBonus`, `OrbDamageBonus` | - | `float` | 원소·범위·구슬 계열에만 붙는 추가분 (#546) |
| `StanceEffect.ReceivedDamageRate` | - | `float` | 받는 피해 증감 (+0.1 = 10% 더 아픔) |
| `StanceEffect.CooldownRate` | - | `float` | 모든 스킬 쿨타임 증감 (-0.15 = 15% 짧게, +0.1 = 10% 길게) |
| `StanceEffect.CurseCooldownRate`, `CurseDurationRate` | - | `float` | 보조 계열에만 붙는 쿨타임 증감, 상태 이상 지속 시간 증감 (#546) |
| `StanceEffect.SelfDamageRate` | - | `float` | 자해량(최대 체력 비율) |
| `StanceEffect.LifestealRate` | - | `float` | 스킬이 준 데미지 가운데 회복하는 비율 |

## 맞춘 결과
- 이우림 결정 (11/18)
  - 자해로 체력이 0이 되면 사망
  - 규칙 표 최종본(11/18, Notion 「효과 수치 (임시)」): 홀수·짝수는 찌르기·휘두르기 계열의 선택, 3의 배수·5의 배수는 쿨타임, 40~60은 회복, 소수는 자해, 피보나치 수는 소환 계열, 완전수는 손해 없음. 처음 기획의 stack +1칸(3의 배수)은 빠짐
  - 「데미지」(5의 배수, 40~60, 소수)와 「모든 데미지」(완전수)는 같은 범위: 모든 스킬과 출혈·중독 도트
  - 출혈·중독 도트에는 계열이 없는 규칙만 적용(모든 스킬 추가분). 자해·회복은 없음
  - 회복은 스킬이 준 데미지만, 1 미만은 모아 두었다가 1이 되면 회복
  - 쿨타임 증감은 `CooldownTracker`에서 곱함(능력치에 넣지 않음)
  - ~~`init_stance`·`get_stance` 처음 개수 1개씩~~: #545에서 없앰. 스킬 트리의 자세 정하기·자세 확인 노드를 해금하면 1개씩 받음 (progress-store.md)
  - 이번에는 C만. 자세 상태·함수·효과 규칙은 Python에도 붙일 수 있게 언어별로 나눔
  - 해금은 정보: 규칙은 해금과 관계없이 처음부터 모두 적용. 스킬 트리에서 규칙을 해금하면 그 규칙의 내용(이름, 이득, 손해)을 볼 수 있음. 원하지 않는 자세는 다시 뽑음
  - 해금 전에 가리는 범위 (가): 효과가 있다는 사실과 규칙이 여덟 개라는 것만 보이고, 규칙 이름·수치는 「???」. `init_stance` 설명도 규칙 이름을 적지 않음(`ItemDescriptions`). 규칙 노드와 「???」 표시는 #545에서 만듦 (skill-tree.md 「자세 노드」)
- Python 자세 이우림 결정 (11/18, #546)
  - 규칙 표: Notion 3. Python 자세의 효과 수치(임시) 그대로. 함수 이름·시전 시간·쿨타임·처음 개수 1개는 C와 같음
  - 7의 배수의 지속 시간 증가는 보조 계열 스킬(저주·뱀 속박·섬광)의 표식·기절·실명만. 독 안개의 중독은 범위 계열이라 늘리지 않음 (Notion에는 「표식·기절·실명·중독」). 이유: 이득(지속 시간)과 손해(보조 계열 쿨타임)가 같은 스킬 묶음에 걸려야 선택이 되고, 독 안개는 안개 안에서 중독을 계속 다시 걸어 지속 시간 차이가 거의 없음
  - 규칙 해금 노드(「???」)는 C와 함께 #545에서 만듦. Python 자세 함수의 처음 개수 1개도 C와 같이 없앰 (이우림 결정 11/18)
- 스킬 트리 자세 노드 이우림 결정 (11/18, #545)
  - 규칙 노드는 규칙마다 1개(C 8개, Python 7개), 새 노드 종류 `SkillTreeNodeKind.Rule`. 스킬 트리의 새 「자세」 탭에 보이고, 규칙은 필요한 문법마다 한 줄 (처음에는 스킬 탭의 한 계열로 정했으나 바꿈, skill-tree.md 「자세 탭」)
  - init_stance·get_stance는 Skill 노드, get_stance의 선행은 init_stance. 처음 개수는 없앰
  - 규칙 노드의 선행: init_stance + 그 규칙을 판별하는 데 필요한 문법 (Notion 「해금」). 재료는 임시
  - 해금 전 규칙 노드는 이름·설명 「???」, 해금 뒤 이름과 「자세 숫자가 2로 나누어떨어지지 않는 수인 경우, 아래 효과가 적용됩니다.」·「효과:」 목록(좋은 효과 청록, 나쁜 효과 빨강, 화면에 「이득·손해」라는 말은 쓰지 않음). 설명은 #544의 설명 카드로 보임
- 다른 담당자 코드에 넣은 것 (모든 파일 수정 허락, 11/8)
  - 박진우: 스킬 데미지 자리(`ThrustSkills`, `SlashSkills`, `ThrowSkills`, `SummonDamage`, `MagicSkill.Deal`)에 `StanceDamageBonus` 더하기와 `NotifyDamageDealt`, `DataDrivenSkill`의 `StanceDamageBonus`·`NotifyDamageDealt`, `StatusEffects`의 출혈·중독 도트 추가분, `SlotLoadout.IsEquippableSkill`에서 자세 함수 제외. `SummonDamage.Deal`의 첫 인자가 스킬 ID에서 스킬(`DataDrivenSkill`)로 바뀜
  - 김도훈: `PlayerHealth.TakeDamage`의 받는 피해 증감, `TakeSelfDamage`·`Heal` 추가
  - 임혜원: 함수 탭에서 자세 함수를 기본 함수로(`BlockReference.IsSkill`), 한글 이름 「자세 정하기」·「자세 확인」
  - #546 박진우: `StatusEffects.TryApply`에 지속 시간 증감을 받는 오버로드, `PythonUtilitySkills`의 저주·뱀 속박·섬광이 시전자와 증감을 넘김, `DataDrivenSkill`의 `Family`·`StanceStatusDurationRate`, `SlotLoadout.IsEquippableSkill`에서 Python 자세 함수 제외
  - #546 임혜원: `BlockReference`에 Python 자세 함수(기본 함수, 한글 이름), `ItemDescriptions.Python`에 설명
