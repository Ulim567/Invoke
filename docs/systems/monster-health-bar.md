# 몬스터 체력 바

- 담당자: 박진우

## 하는 일
- 일반 몬스터 머리 위에 작은 체력 바(테두리 30×8px, 안쪽 20×4px, 16 PPU)를 띄운다 (#388). 보여 주기만 하고 체력 값은 바꾸지 않는다.
- 빨간 채움은 `Monster.Hp / MaxHp` 비율로 1픽셀 단위로 줄어든다. 체력이 1이라도 남으면 1픽셀은 남긴다.
- 맞으면(`HitEffectPlayed`) 흰 잔상이 `Trail Delay`(0.3초) 동안 맞기 전 너비로 남았다가 `Trail Speed`(초당 30픽셀)로 빨간 쪽까지 줄어든다.
- 체력이 가득이면 숨기고, 줄어 있으면 보이고, 죽으면 숨긴다.
- 보스(`IsBoss`, 화면 위 BossHealthBar가 맡음)와 허수아비(`IsInvulnerable`)에는 띄우지 않는다. Gateway·GatewayClone에는 붙이지 않는다.
- 클라이언트에서도 `Hp`·`HitEffectPlayed`가 서버와 같아서(`MonsterNetwork`) 모든 화면에서 같은 바가 보인다. 바는 네트워크 오브젝트가 아니다.

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `MonsterHealthBarView` (컴포넌트) | - | - | 몬스터 프리팹 최상위(`Monster`와 같은 곳)에 붙인다. Inspector: `Bar Prefab`(비우면 `Resources/MonsterHealthBar`), `Height Above Top`(콜라이더 윗변에서 0.6, 상태 이상 아이콘(1.4) 아래), `Trail Delay`, `Trail Speed` |
| `MonsterHealthBarView.IsShown` | - | bool | 바가 보이는지 (테스트용) |
| `MonsterHealthBarView.FillPixels` / `TrailPixels` | - | int | 빨간 채움 / 흰 잔상 너비 (픽셀, 0~20, 테스트용) |
| 메뉴 Invoke → JW → 몬스터 체력 바 프리팹 만들기 | - | - | `Assets/_JW/Effects/MonsterHp` 그림 3장 가져오기 설정 후 `Assets/_JW/Resources/MonsterHealthBar.prefab`을 만든다(Sorting Layer WorldUI, 테두리 0·잔상 1·채움 2). 다시 눌러도 덮어쓴다 |

- 테스트: `MonsterHealthBarSimTest` (Test_JW, Play → Host, 「[MonsterHpBarTest] 전체 통과」). 몬스터 하나를 죽이므로 평소에는 꺼 둔다.

## 맞춘 결과
- 이도훈과 정함 (#388) - 체력 바는 박진우가 만들고, 도훈의 일반 몬스터 프리팹(Leak, Loop, Worm, Pillar)에 `MonsterHealthBarView`를 붙인다. 보스·분신은 제외.
