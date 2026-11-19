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
| py.orb_create | 구슬 생성 | 없음 | `orb_create()`: 마나 구슬 하나를 heap에 만들고 Orb로 돌려줌. 구슬이 5개(다 쓴 구슬 포함)면 None, heap 부족은 MemoryError (#532) |
| py.orb_shot | 구슬 발사 | 단일 | `orb_shot(target, orb)`: 넘긴 구슬을 써서 센 마법탄 (#532) |
| py.orb_barrage | 구슬 난사 | 여러 명 | `orb_barrage(targets, orbs)`: 구슬 리스트의 쓸 수 있는 구슬을 모두 써서, 대상 리스트의 몬스터들에게 하나씩 (#532) |
| py.orb_burst | 구슬 폭발 | 범위 | `orb_burst(orbs)`: 구슬 리스트의 쓸 수 있는 구슬을 모두 터뜨려 둘레 모두에게 배율 × 구슬 수 (#532) |
| py.orb_shield | 구슬 보호막 | 없음 | `orb_shield(orb)`: 넘긴 구슬을 써서 10초 동안 다음 피해 한 번 막음 (#532) |
| py.hex | 저주 | 단일 | 약한 마법탄 + 표식 |
| py.snake_bind | 뱀 속박 | 단일 | 약한 마법탄 + 기절 |
| py.flash | 섬광 | 범위 | 둘레 모두 실명, 데미지 없음 |
| py.blink | 블링크 | 없음 | 바라보는 쪽 5칸 순간이동 (벽 앞에서 멈춤) |
| py.levitate | 공중 부양 | 없음 | 6초 동안 ←→↑↓로 떠다님 |

구슬 스킬 (#532, 이우림이 Python 문법 맞추기에서 고침):
- 구슬을 해제하지 않고 「다 쓴 구슬」(`ManaOrb.IsSpent`)로만 바꾼다. 스킬은 넘겨받은 값이 변수에서 왔는지 리스트에서 왔는지 알 수 없기 때문
- 다 쓴 구슬 하나를 넘김: `UsedManaOrbError`. 리스트에 쓸 구슬 없음: `NoManaOrbError`. 구슬이 아닌 값: TypeError(파라미터 타입 검사, 리스트 원소는 `FailWith(TypeMismatch)`)
- 리스트의 다 쓴 구슬은 건너뛰고 리스트에서 빼지 않는다
- 다 쓴 구슬도 최대 5개에 든다(C의 누수 소환검과 같음). 변수·리스트가 놓아야 가비지 컬렉션이 해제해 자리가 돌아온다. 머리 위(`ManaOrbView`)와 HUD 칸에 빨갛게 보인다
- 슬롯에서 바로 시전해 orb·orbs 값이 없으면 쓸 수 있는 구슬 중 먼저 만든 것부터 (임시 계정)
- **에디터 Host로 시험할 때만**, 슬롯에서 바로 시전한 구슬 생성(사용자 함수 밖)은 돌려받을 곳이 없으므로 그 구슬을 숨은 시스템 전역에 둔다 (#593, 이우림 결정 11/19). 판단은 `Application.isEditor`(시전은 서버에서만 실행되므로 에디터가 서버라는 뜻). 함수 안에서 담지 않은 구슬은 그대로 가비지 컬렉션이 해제한다
  - 실제 서버(빌드)에서는 슬롯으로 시전해도 담지 않은 구슬은 사라진다. 그래서 실제 게임에서 구슬은 함수에서 변수·리스트에 담아야 쓸 수 있다
  - 이 구슬(`ManaOrb.KeptBySlot`)은 담은 곳이 없어 다 쓴 구슬로 남으면 영영 해제되지 않으므로, 구슬 스킬이 쓰는 순간 바로 해제한다 (빨간 구슬로 남지 않음)
- 구슬 확인: `is_valid_orb(orb)` (`py.is_valid_orb`, predefined-function.md)

## 구조
- **마법탄은 오브젝트가 아니다.** `MagicBoltSkill`이 서버에서 매 틱 `MagicBolts` 표(속도, 사거리, 판정 상자, 관통)대로 움직이며 판정한다. 날아가는 시간은 스킬 틱 수 안에 끝나야 한다.
- 그림은 `SkillEffectEvents.Fired/Hit/Ended`를 받은 각 화면의 `MagicEffectView`가 `Resources/Magic/{이름}_{bolt|hit|cast|zone|beam}` 프리팹으로 그린다. 프리팹이 없으면 그리지 않는다.
- **마나 구슬**: heap 객체 `ManaOrb`. `ManaOrbs`가 구슬 수와 다 쓴 구슬 표시를 SyncVar로 보내고(`TotalCount`, `Count`, `SpentCount`, `IsSpentAt(i)`, `Changed`, `OrbPosition`), `ManaOrbView`가 머리 위에 그린다. 네트워크 오브젝트 아님.
  - #532: 플레이어가 Orb 값으로 변수·리스트에 담는다. 숨은 시스템 전역에 두지 않는다(`MovesMallocedToSystemGlobals` false). 가리키는 이름이 없으면 가비지 컬렉션이 해제
  - `TotalCount`: 다 쓴 구슬도 센 수. 최대 5개(`CanCreate`), 머리 위 그림, HUD가 쓴다
  - `Count`: 쓸 수 있는 구슬만 센 수. 찍을 수 있는 대상 수(`SlotCaster`)가 쓴다. `UsablePointers()`
  - 자리 순서: heap 주소 순서. `IndexOf(포인터)`는 다 쓴 구슬도 포함한 자리라서, 구슬을 써도 다른 구슬이 움직이지 않는다
  - 다 쓴 구슬 표시: 클라이언트에는 구슬마다의 값이 없어서, i번째 비트가 i번째 구슬의 다 씀인 int(`syncedSpentMask`)로 보낸다
- **가비지 컬렉션**: `LanguageRuleData.GarbageCollected` → `HeapStorage.CollectsGarbage`. 도달할 수 없는 객체를 누수로 남기지 않고 해제하고 `ObjectFreed`로 알린다. 그래서 해제 스킬이 없다.
- **구슬 보호막**: `OrbShield.Activate(초)`, `PlayerHealth.DamageBlocking`(true를 돌려주면 그 피해를 받지 않음).
- **중독** (`StatusType.Poison`): 6초, 1초마다 3 × 겹친 수, 다시 걸면 한 겹 더(최대 3). `StatusEffects.PoisonTicked`, `PoisonStacks`. 아이콘은 `Resources/StatusIcons/Poison`.
- **이동**: `PlayerMovement.Blink(direction, distance)`, `StartLevitate(seconds)` (검 타기와 같은 움직임, `StopRide`로 끝냄).
- **캐릭터 언어**: `PlayerCharacter.LocalLanguage`, `SkillPrefixOf`, `CanUse`. 블록 편집기·블록 설명·장착 화면이 이것으로 내 언어의 스킬만 보여 준다.
- 스킬 계열 `SkillFamily.Element, Zone, Orb, Curse` 추가. 모션은 `SkillMotionTable`에서 전부 Cast.

## 테스트
| 이름 | 확인하는 것 |
| --- | --- |
| `PythonOrbTest` | (이우림 #532) 네트워크 없이 구슬 규칙: orb_create가 Orb를 돌려줌, 담지 않은 구슬 해제, 다 쓴 구슬·is_valid_orb, NoManaOrbError, UsedManaOrbError, TypeError, del, 다 쓴 구슬도 5개에 듦. Play 중 `PythonOrbTest.Spawn(기본 능력치 경로)` |
| `PythonWizardSimTest` | (#551) Test_JW의 PythonWizardTest. Test_JW NetworkManager의 Player Prefab을 `Player_Python`으로 잠깐 바꾸고(씬 저장 안 함) 켜고 Play → Host (약 12초). 실제 마법사 프리팹으로: 캐릭터 언어 Python·가비지 컬렉션 켜짐, 전역 리스트에 담은 구슬 2개와 머리 위 그림 2개(HUD가 있으면 「마나 구슬 2/5」. 레벨 1 메모리 10칸에는 리스트와 구슬 3개가 안 들어감), 구슬 값 없이 바로 시전한 구슬 발사가 먼저 만든 구슬을 써서 몬스터를 맞힘, 구슬 보호막이 첫 피해만 막음, 중독 3겹에서 멈춤, 공중 부양 6초 뒤 내려옴(경직이 풀린 뒤 시전), 슬롯에서 바로 만든 구슬은 남고 쓰면 바로 해제됨(#593). 근처(12칸)에 몬스터가 없으면 구슬 발사·중독은 건너뜀. 자세는 0으로 두고 봄. 「[PythonTest] 전체 통과」면 통과. 평소에는 꺼 둔다 |

## 남은 일
- ~~파이썬용 스킬 트리~~ (#523, skill-tree.md 「스킬 노드 (파이썬 마법사)」), ~~새 캐릭터의 처음 슬롯~~ (#520, progress-store.md), 캐릭터 선택 화면 설명, HUD 구슬 개수
- 이펙트·아이콘 그림 교체 (지금은 코드로 그린 임시 그림. `Assets/_JW/Effects/Magic/`의 같은 이름 파일을 바꾸고 메뉴를 다시 누르면 됨)
- 캐릭터 원본: PixelLab https://www.pixellab.ai/create-character/554a2c03-c766-41c6-b9f3-4c3f7445d523 (East 방향, 96×96 프레임의 (16,16)부터 64×64가 본체)
