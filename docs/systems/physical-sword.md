# 물리 검 (PhysicalSword)

- 담당자: 박진우

## 하는 일
- 손에 든 물리 검(레이피어)의 상태를 관리한다: 손에 있음 / 날아가는 중 / 땅에 떨어짐.
- 다른 기능(C 스킬의 맨손 검사, 던지기 스킬, UI)이 맨손인지 읽을 수 있게 한다.
- 던진 검 오브젝트(떨어진 검 프리팹)를 만들고 없앤다.
- 하지 않는 일
  - 던지기·검 돌아오기 스킬의 데미지, 처형, 돌아오는 경로 판정: 던지기 스킬 이슈 (`Hit` 이벤트를 받아서 처리)
  - 소환검: 소환검 이슈
  - 네트워크 동기화: 「소환검·검기를 서버에서 생성하고 동기화하기」 이슈

## 규칙
- 처음에는 손에 있다.
- 던지기(`Throw`)
  - 대상이 있으면 대상의 콜라이더 가운데를 향해 날아간다(`Throw Speed`, 기본 20유닛/초). 닿으면 `Hit`을 알리고 그 자리에서 아래로 떨어진다.
  - 대상이 없거나(빗나감), 날아가는 사이 대상이 사라지거나, `Max Flight Time`(기본 1초) 안에 닿지 못하면 그 자리에서 바로 떨어진다.
  - 던지는 위치: 발밑 + `Hand Offset`(기본 (0.5, 2)). x는 바라보는 방향(`PlayerMovement.Facing`)으로 뒤집는다.
  - 떨어진 검은 아래로 `Ground Layers`(Ground, Platform)를 찾아 그 윗면에 멈춘다. 플레이어·몬스터와 부딪히지 않는다(물리를 쓰지 않고 직접 움직임).
- 손에 없으면(날아가는 중, 땅에 떨어짐) 맨손이다 (`IsUnarmed`).
  - 맨손이면 공격 스킬은 `UnarmedError`로 끝난다. 검 돌아오기, 소환(검 소환·해제), 이동 계열은 쓸 수 있다 (c-skills.md 표의 `usableUnarmed`).
- 회수
  - 줍기(`TryPickUp`): 땅에 떨어진 검과 `Pick Up Range`(기본 1.5유닛) 안에 있을 때만.
  - 검 돌아오기(`Recall`): 거리와 상관없이 (날아가는 중이어도) 바로 손으로 돌아온다.
- 해제(free)할 수 없다.
- 떨어진 검 오브젝트는 `SpawnDroppedSword` / `DespawnDroppedSword`로만 만들고 없앤다 (CLAUDE.md 코드 작성 규칙 3). 캐릭터가 사라지면 같이 없앤다.
- 줍기: 내 캐릭터에서 `PlayerInputReader.PickUpPressed`(Z)가 true이면 `SwordPickUpInput`이 `TryPickUp`을 부른다.

### 지금 값 (Player_JW)

| 항목 | 값 |
| --- | --- |
| Dropped Sword Prefab | `Assets/_JW/DroppedSword.prefab` (임시 그림: 흰 막대) |
| Ground Layers | Ground, Platform |
| Hand Offset | (0.5, 2) |
| Throw Speed | 20 |
| Max Flight Time | 1초 |
| Pick Up Range | 1.5 |

## 다른 시스템이 쓰는 것

플레이어 프리팹에 `PhysicalSword`가 붙어 있다. `GetComponent<PhysicalSword>()`로 가져온다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `State` | - | `SwordState` | `InHand`(손에 있음), `Flying`(날아가는 중), `OnGround`(땅에 떨어짐) |
| `IsUnarmed` | - | `bool` | 손에 없으면 `true`. 공격 스킬 시전 검사, 맨손 그림에 쓴다 |
| `CanPickUp` | - | `bool` | 땅에 떨어져 있고 줍기 거리 안이면 `true`. 「줍기」 안내 표시에 쓴다 |
| `Dropped` | - | `DroppedSword` | 던진 검 오브젝트. 손에 있으면 `null`. 위치 표시, 검 돌아오기 경로에 쓴다 |
| `Throw(GameObject target)` | 대상 (없으면 `null` = 빗나감) | `bool` | 검을 던진다. 손에 없으면 `false`. 검 던지기 스킬이 부른다 |
| `TryPickUp()` | - | `bool` | 근처의 떨어진 검을 줍는다. 주울 수 없으면 `false`. 줍기 키를 눌렀을 때 부른다 |
| `Recall()` | - | `bool` | 거리와 상관없이 손으로 되돌린다. 이미 손에 있으면 `false`. 검 돌아오기 스킬이 경로 판정 뒤에 부른다 |
| `StateChanged` (event) | `SwordState` | - | [표시] 상태가 바뀔 때. 손 그림, 맨손 표시 |
| `Hit` (event) | `GameObject` 맞은 대상 | - | [로직] 던진 검이 대상에 닿았을 때. 데미지·처형은 이 이벤트를 받는 쪽(검 던지기 스킬)이 준다 |

### 테스트
| 이름 | 설명 |
| --- | --- |
| `PhysicalSwordSimTest` | Test_JW의 SwordTest. 켜고 Play → Host, Game 창을 한 번 클릭(가짜 키 입력이 들어가게). 빗나감(바로 떨어져 맨손), 맨손 스킬(검 찌르기 `UnarmedError`, 대시 정상), 줍기(가짜 Z 키), 맞음(가장 가까운 몬스터에게 `Hit`, 근처에 떨어짐), 검 돌아오기를 확인. 「[SwordTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

## 맞춘 결과
- 이우림과 정함
  - 줍기 키는 Z (`PlayerInputReader.PickUpPressed`, 이우림 #73)
- 정해야 할 것
  - 검 그림 (지금은 흰 막대): 임혜원
  - 던진 검이 날아가는 속도, 줍기 거리: 기획
