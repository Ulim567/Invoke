# 파이썬 마법사 (캐릭터, 스킬, 마나 구슬, 중독)

담당: 박진우. 코드 `Assets/_JW/Scripts/Python/`, 그림 `Assets/_JW/Python/`, `Assets/_JW/Effects/Magic/`.
**상태: 1차 구현. Unity에서 아직 검증 중이고 수치·이펙트 그림·아이콘은 전부 임시다.**

## 만드는 법 (에디터)
1. 메뉴 **Invoke → JW → 파이썬 마법사 만들기** (`PythonWizardBuilder`). 여러 번 눌러도 된다.
   - `Assets/_JW/Resources/Magic/*.prefab` (이펙트), `Resources/StatusEffects/Poison.prefab`
   - `Assets/_JW/Python/Animations/Py_*.anim`, `Py_Player.controller` (Idle, Run, Jump, Cast, Hit, Death)
   - `Assets/_JW/Python/LanguageRule_Python.asset` (없을 때만. C 규칙 복사 + 가비지 컬렉션, 문자열 == 내용 비교, 0은 null 아님, float % 허용, int→float만 자동 변환, 선언 안 한 변수·타입 불일치는 runtime error)
   - `SkillTable.asset`에 없는 `py.*` 줄 17개 (있는 줄은 건드리지 않음)
   - `Assets/_JW/Python/Player_Python.prefab` (Player의 Variant + ManaOrbs, ManaOrbView, OrbShield, OrbShieldView, MagicEffectView, PlayerCharacter(Python))
2. 사람이 할 일: `Assets/Prefabs/NetworkManager` 프리팹의 **Registered Spawnable Prefabs**에 `Player_Python` 추가, **Character Prefabs**에 (language `Python`, prefab `Player_Python`) 추가.
3. 캐릭터 선택에서 Python으로 들어가면 서버가 `Player_Python`으로 만든다 (`InvokeNetworkManager.PlayerPrefabFor`, `AccountSession.Language`).

## 스킬 (ID는 `PredefinedFunctionIds.Py*`, 수치는 SkillTable)
| ID | 이름 | 대상 | 동작 |
|---|---|---|---|
| py.magic_missile | 매직 미사일 | 단일 | 마법탄, 처음 닿은 적 |
| py.fireball | 파이어볼 | 단일 | 닿은 적 + Area Radius 안 다른 적에게 0.5배 |
| py.ice_spear | 얼음 창 | 관통 | 지나가는 적 모두 + 이속 감소 |
| py.chain_lightning | 체인 라이트닝 | 단일 | 바로 닿음(9칸), 4칸 안 다른 적으로 2번 튐(0.6배) |
| py.meteor | 메테오 | 단일 | 대상 발밑에 20틱 뒤 떨어져 반경 안 모두 |
| py.blizzard | 블리자드 | 단일 | 대상 자리에 깔림, 10틱마다 반경 안 모두 + 이속 감소 |
| py.venom_mist | 독 안개 | 단일 | 대상 자리에 깔림, 10틱마다 약한 데미지 + 중독 한 겹 |
| py.orb_create | 구슬 생성 | 없음 | 마나 구슬 하나를 heap에 만듦 (최대 5) |
| py.orb_shot | 구슬 발사 | 단일 | 구슬 1개 소비, 센 마법탄 |
| py.orb_barrage | 구슬 난사 | 여러 명 | 구슬 전부 소비, 고른 대상들에게 하나씩 (찍을 수 있는 수 = 구슬 수) |
| py.orb_burst | 구슬 폭발 | 범위 | 구슬 전부 소비, 둘레 모두에게 배율 × 구슬 수 |
| py.orb_shield | 구슬 보호막 | 없음 | 구슬 1개 소비, 10초 동안 다음 피해 한 번 막음 |
| py.hex | 저주 | 단일 | 약한 마법탄 + 표식 |
| py.snake_bind | 뱀 속박 | 단일 | 약한 마법탄 + 기절 |
| py.flash | 섬광 | 범위 | 둘레 모두 실명, 데미지 없음 |
| py.blink | 블링크 | 없음 | 바라보는 쪽 5칸 순간이동 (벽 앞에서 멈춤) |
| py.levitate | 공중 부양 | 없음 | 6초 동안 ←→↑↓로 떠다님 |

구슬이 필요한 스킬은 구슬이 없으면 `NoManaOrbError`로 끝난다.

## 구조
- **마법탄은 오브젝트가 아니다.** `MagicBoltSkill`이 서버에서 매 틱 `MagicBolts` 표(속도, 사거리, 판정 상자, 관통)대로 움직이며 판정한다. 날아가는 시간은 스킬 틱 수 안에 끝나야 한다.
- 그림은 `SkillEffectEvents.Fired/Hit/Ended`를 받은 각 화면의 `MagicEffectView`가 `Resources/Magic/{이름}_{bolt|hit|cast|zone|beam}` 프리팹으로 그린다. 프리팹이 없으면 그리지 않는다.
- **마나 구슬**: heap 객체 `ManaOrb`. `ManaOrbs`가 개수를 SyncVar로 보내고(`Count`, `Changed`, `OrbPosition`), `ManaOrbView`가 머리 위에 그린다. 네트워크 오브젝트 아님.
- **가비지 컬렉션**: `LanguageRuleData.GarbageCollected` → `HeapStorage.CollectsGarbage`. 도달할 수 없는 객체를 누수로 남기지 않고 해제하고 `ObjectFreed`로 알린다. 그래서 해제 스킬이 없다.
- **구슬 보호막**: `OrbShield.Activate(초)`, `PlayerHealth.DamageBlocking`(true를 돌려주면 그 피해를 받지 않음).
- **중독** (`StatusType.Poison`): 6초, 1초마다 3 × 겹친 수, 다시 걸면 한 겹 더(최대 3). `StatusEffects.PoisonTicked`, `PoisonStacks`. 아이콘은 `Resources/StatusIcons/Poison`.
- **이동**: `PlayerMovement.Blink(direction, distance)`, `StartLevitate(seconds)` (검 타기와 같은 움직임, `StopRide`로 끝냄).
- **캐릭터 언어**: `PlayerCharacter.LocalLanguage`, `SkillPrefixOf`, `CanUse`. 블록 편집기·블록 설명·장착 화면이 이것으로 내 언어의 스킬만 보여 준다.
- 스킬 계열 `SkillFamily.Element, Zone, Orb, Curse` 추가. 모션은 `SkillMotionTable`에서 전부 Cast.

## 남은 일
- Unity에서 컴파일·동작 확인, SimTest 작성
- 파이썬용 스킬 트리(지금은 노드가 없어 전부 열려 있음), 새 캐릭터의 기본 슬롯 장착(지금은 C 스킬이 들어 있음), 캐릭터 선택 화면 설명, HUD 구슬 개수
- 이펙트·아이콘 그림 교체 (지금은 코드로 그린 임시 그림. `Assets/_JW/Effects/Magic/`의 같은 이름 파일을 바꾸고 메뉴를 다시 누르면 됨)
- 캐릭터 원본: PixelLab https://www.pixellab.ai/create-character/554a2c03-c766-41c6-b9f3-4c3f7445d523 (East 방향, 96×96 프레임의 (16,16)부터 64×64가 본체)
