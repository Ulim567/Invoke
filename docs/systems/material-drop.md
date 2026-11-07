# 재료 드롭 효과

- 담당자: 박진우

## 하는 일
- 몬스터가 재료를 떨구면, 죽은 자리(몬스터 콜라이더 가운데)에서 재료 아이콘이 톡 튀어 오른 뒤 받은 플레이어 몸 가운데로 점점 빨라지며 날아가 사라진다 (#404).
- 보여 주기만 한다. 재료 지급은 `MonsterReward`(이도훈)가 하고, 이 효과는 `MonsterReward.RewardEffectPlayed`를 받아서 띄운다.
- 받은 플레이어마다, 재료마다 개수만큼 띄운다 (한 재료당 최대 `Max Per Material` 10개). Gateway(`Whole Scene`)면 같은 씬의 플레이어 모두에게 날아간다.
- 아이콘은 흰 빛줄기가 위에서 아래로 훑는 반짝임을 반복한다(반짝임 뒤 잠깐 쉼).
- 아이콘은 몬스터의 씬에 만든다(몬스터 자식 아님). 몬스터가 먼저 없어져도 끝까지 날아간다. 네트워크 오브젝트가 아니다.
- 지금 `RewardEffectPlayed`는 서버(Host)에서만 불려서 **호스트 화면에서만 보인다**. 다른 플레이어 화면에 보이려면 `MonsterReward`가 이 이벤트를 클라이언트에도 불러 줘야 한다 (이도훈에게 요청).

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `MaterialDropView` (컴포넌트) | - | - | `MonsterReward`와 같은 곳(몬스터 프리팹 최상위)에 붙인다. Inspector: `Overrides`(재료 ID별 아이콘, 비우면 `Resources/MaterialDrops/{재료 ID}`), `Max Per Material`(10), `Pop Speed X`(-2.5~2.5), `Pop Speed Y`(6~8) |
| `MaterialDropView.Play` | `PlayerProgress` 받은 플레이어, `IReadOnlyList<MonsterReward.MaterialDrop>` 재료 | - | 재료 아이콘을 띄워 그 플레이어에게 날린다. `RewardEffectPlayed`가 부르고, 테스트도 직접 부른다 |
| `MaterialDropView.SpawnedCount` | - | int | 지금까지 띄운 아이콘 수 (테스트용) |
| `MaterialDropFlight` (컴포넌트) | - | - | 아이콘 하나의 움직임. Inspector: `Pop Time`(0.4초), `Pop Gravity`(18), `Fly Start Speed`(3), `Fly Acceleration`(45), `Arrive Distance`(0.25), `Max Lifetime`(3초) |
| 메뉴 Invoke → JW → 재료 드롭 프리팹 만들기 | - | - | `Assets/_JW/Effects/Materials/material_{재료 ID}.png`(가로 한 줄, 정사각 칸) 시트를 잘라 `Assets/_JW/Resources/MaterialDrops/{재료 ID}.prefab`을 만든다. Sorting Layer Effect, Order 10. 시트가 없는 재료는 건너뛴다 |

| 재료 ID | 아이콘 | 크기 |
| --- | --- | --- |
| `bug_fragment` (버그 조각) | `material_bug_fragment.png`, 8프레임 | 16×16 (1칸) |
| `boss_core` (보스 코어) | `material_boss_core.png`, 8프레임 | 24×24 (1.5칸) |

- 새 재료: 시트를 `material_{재료 ID}.png`로 넣고 빌더의 `Materials` 표에 한 줄 추가한 뒤 메뉴를 다시 누른다.
- 테스트: `MaterialDropSimTest` (Test_JW, Play → Host, 「[MaterialDropTest] 전체 통과」). 몬스터를 죽이지 않는다.

## 맞춘 결과
- 이도훈과 정함 (#404) - 재료 드롭 효과는 박진우가 만들고 `RewardEffectPlayed`를 받아서 쓴다. 도훈의 보상 몬스터 프리팹(Leak, Loop, Gateway)에 `MaterialDropView`를 붙인다.
