# 데미지 숫자

- 담당자: 임혜원 (처음 만듦, 연습장), 박진우 (#442 픽셀 숫자 그림·팝 연출·몬스터에 붙임)

## 하는 일
- 몬스터가 맞으면(`Monster.HitEffectPlayed`, 이도훈) 받은 데미지를 머리 위에 픽셀 숫자로 띄운다. 보여 주기만 하고 체력은 건드리지 않는다.
- 숫자는 툭 커졌다가 줄어드는 팝과 함께 좌우 랜덤으로 살짝 포물선을 그리며 튀었다가 흐려지고 사라진다 (0.9초).
- 맞을 때 대상에 표식(`StatusEffects`의 `Mark`, 데미지 2배)이 걸려 있으면 표식 맞힘(치명타) 숫자(마젠타 + 시안 글리치 테두리)로 더 크게 띄운다.
- 데미지가 클수록 숫자가 조금 크다 (`Big Damage` 200에서 최대 1.4배).
- 숫자는 네트워크 오브젝트가 아니다. 각 화면에서 만든다. `HitEffectPlayed`와 표식 상태가 클라이언트에도 오므로 모든 화면에서 보인다. 몬스터와 같은 씬에 만들고 몬스터 자식으로 두지 않는다.
- 붙인 곳: 연습장 허수아비(`TrainingDummy`), 잡몹(Leak, Loop, Worm, Pillar), 보스(Gateway).

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `DamageNumberView` (컴포넌트) | - | - | `Monster`와 같은 곳(몬스터 프리팹 최상위)에 붙인다. Inspector: `Number Prefab`(비우면 `Resources/DamageNumber`), `Height Above Top`(콜라이더 윗변에서 2.2, 체력 바 0.6·상태 아이콘 1.4보다 위), `Jitter`(0.4), `Critical Scale`(1.3), `Big Damage`(200), `Big Damage Scale`(1.4) |
| `DamageNumberView.Spawn(int damage)` | 데미지 | - | 숫자 하나를 띄운다. `HitEffectPlayed`가 부르고, 테스트도 직접 부른다 |
| `DamageNumber` (컴포넌트) | - | - | 숫자 하나. Inspector: `Digits`/`Critical Digits`(0~9 그림), `Digit Spacing Pixels`(8), `Life Time`(0.9), `Pop Scale`(1.7), `Pop Time`(0.12), `Launch Speed X`(-1.2~1.2), `Launch Speed Y`(4.5), `Gravity`(9), `Fade Start`(0.55). Sorting Layer WorldUI, Order 20 |
| `DamageNumber.Show(int damage, bool critical, float scale)` | 데미지, 표식 맞힘, 크기 배율 | - | `DamageNumberView`가 부른다 |
| 메뉴 Invoke → JW → 데미지 숫자 프리팹 만들기 | - | - | `Assets/_JW/Effects/Damage`의 `damage_digits.png`, `damage_digits_crit.png`(9×10px 칸 10개)를 잘라 `Assets/_JW/Resources/DamageNumber.prefab`을 만든다 |

- 테스트: `DamageNumberSimTest` (Test_JW, Play → Host, 「[DmgNumTest] 전체 통과」). 몬스터를 때리지 않고 `Spawn`을 직접 부르며, 표식을 잠깐 걸었다 푼다.

## 맞춘 결과
- 임혜원과 정함 (#442) - `DamageNumber`, `DamageNumberView`(_HW) 코드를 박진우가 픽셀 숫자로 바꾼다. 그림·빌더·새 프리팹은 _JW. 예전 TextMeshPro 프리팹(`_HW/Training/DamageNumber.prefab`)은 쓰지 않는다.
- 이도훈과 정함 (#442) - 도훈 몬스터 프리팹(Leak, Loop, Worm, Pillar, Gateway)에 `DamageNumberView`를 붙인다.
