# 소환 계열 스킬 이펙트 (SkillHitEffectView)

- 담당자: 임혜원

## 하는 일
- 표식 남기기, 검무, 패링이 맞는(막는) 순간 작은 이펙트를 한 번 틀고 없앤다. 보여 주기만 하고 값은 바꾸지 않는다 (#290, 박진우 요청 초안).
- 찌르기 섬광(검 찌르기·집중찌르기·꼬챙이)은 하지 않는다. `ThrustEffectView`(박진우)가 한다 (c-skills.md).

## 동작 규칙
| 스킬 | 받는 것 | 이펙트 |
| --- | --- | --- |
| 표식 남기기 | `SkillEffectEvents.Hit` (소환검이 찌를 때 검마다 한 번, 좌우 한 번씩) | `Mark_Flash`: 맞은 자리에 작은 찌르기 섬광. Thrust_Flash보다 작다. 대상 쪽을 향한다 |
| 검무 | `SkillEffectEvents.Hit` (소환검 하나가 벨 때마다) | `Dance_Slash`: 맞은 자리에 작은 베기 궤적. 대상 바깥쪽으로 휜다 |
| 패링 | `ParryGuard.ParryEffectPlayed` (잡은 위치) | `Parry_Flash`: 번쩍임 (Thrust_Flash 크기). 반격 검기는 원래 SwordWave |

- 공통: 맞는 순간 생겨서 0.25초 뒤 사라진다 (5장면, 20fps). 첫 장면이 가장 강하다.
- 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 각 화면이 만들고 `OneShotEffect`(0.25초)가 없앤다. 플레이어가 있는 장소 씬에 둔다.
- 붙이는 곳: 공용 `Assets/Prefabs/Player.prefab` 최상위 (`SkillEffectEvents`, `ParryGuard`와 같은 곳).
- 메뉴 Invoke → 이펙트 → 스킬 이펙트 붙이기(`SkillEffectBuilder`, 에디터 전용)가 그림으로 클립·컨트롤러·프리팹(`Assets/_HW/Effects/Mark_Flash.prefab` 등)을 만들고, Player.prefab에 이 컴포넌트를 붙여 프리팹을 넣는다. 그림을 고치면 메뉴를 다시 누른다.
- 표식·기절 아이콘은 원래 있는 상태 아이콘(StatusIcons)을 그대로 쓴다.

## 다른 시스템이 쓰는 것
- 없음. 다른 시스템은 이 컴포넌트를 부르지 않는다.

## 맞춘 결과
- 박진우와 정한 것 (#290)
  - 이펙트는 `SkillEffectEvents.Hit`, 패링은 `ParryGuard.ParryEffectPlayed`를 받아 튼다. 지금 그림은 초안이고 박진우가 바꿀 수 있다.
