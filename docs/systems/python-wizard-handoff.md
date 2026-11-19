# 파이썬 마법사 작업 인수인계 (2026-11-18 갱신)

## 기본 정보
- 프로젝트: Invoke (Unity 6000.6.0f1, URP 2D, Mirror). 저장소 `Ulim567/Invoke`, 기준 브랜치 `dev`
- 로컬: `/Users/losecow/Desktop/GameDev/Invoke` (device_bash에서는 `$HOME/mnt/Invoke`)
- dev에 들어간 PR: #520(마법사 본체, #486), #522(인수인계·발판 테스트, #521), #525(스킬 트리, #523), #531(소개 글·HUD 구슬, #527), #542(스킬 설명·상점 가격, #539)
- 다음 작업은 새 이슈·새 브랜치(`git switch -c "feature/#N" origin/dev`). zsh에서는 브랜치 이름에 따옴표
- 사용자: 박진우 (GitHub `Losecow`). **답변은 항상 한국어 반말.** 코드·명령어·경로만 영어
- 규칙: **Claude는 git 작업 안 함** (읽기 전용 `git --no-optional-locks`만). 커밋·merge·push는 사용자가 하고, Claude는 명령어와 PR 글(음슴체, Claude 서명 줄 없음)을 줌
- 올리기 전 확인: GitHub dev가 바뀌었으면 겹치는 파일을 미리 3-way로 합쳐 보고, 충돌·의미상 어긋남(새 enum 값, 새 규칙 등)을 먼저 찾음
- 로컬 파일은 **직접 고쳐도 됨** (사용자 허락). `.unity/.prefab/.asset`도 이번 작업에서는 직접 고쳤음
- 파일 넣기: `device_commit_files(force)` 후 md5 다시 확인. 덮어쓰기가 조용히 안 될 때가 있음. PNG는 전송 뒤 md5가 달라질 수 있음(메타데이터)
- 스킬 기획 초안: https://claude.ai/artifact/GzGeThVys8Bq6N1J35ZRnq (중독은 아직 기획 문서에 반영 안 함)
- 시스템 문서: `docs/systems/python-skills.md` (구조, 스킬 표, 남은 일)
- 이펙트·아이콘 프롬프트: `docs/systems/python-effect-prompts.md`

## 캐릭터 (PixelLab, 사용자 계정)
- **지금 쓰는 마법사**: https://www.pixellab.ai/create-character/99a78933-02dc-47de-97de-c872e84b7aef
  - C 캐릭터 결에 맞춘 호리호리한 체형, 남색 로브 + 탁한 금색 테두리, 뱀 지팡이. 64×64, Sidescroller, East(옆모습) 사용
  - 모션(East, Template Skeleton v3): Breathing Idle 4, Running 8, Two-Footed Jump 7, Fireball(시전) 6, Taking Punch(피격) 6, Falling Back Death 7
- 버린 첫 디자인(통통한 3등신): 554a2c03-c766-41c6-b9f3-4c3f7445d523
- 생성 횟수: 이번 작업으로 40회 사용. 사용자가 100회 넘게 써도 된다고 함
- 프레임은 96×96, 본체 64×64는 대략 (16,14)부터. 조립 스크립트는 `assemble.py`(이 세션 작업 폴더에만 있음)

## 로컬에 들어간 것
### 그림
- `Assets/_JW/Python/Py_Idle(8칸) Py_Run(8) Py_Jump(7) Py_Cast(6) Py_Hit(4) Py_Death(7).png` — 64×64 칸, 24 PPU, 발밑 기준. 새 디자인으로 교체 완료
- `Assets/_JW/Effects/Magic/*.png` 25개, `Effects/Status/status_poison.png`, `Resources/StatusIcons/Poison.png`, `Resources/SkillIcons/py.*.png` 17개 — PixelLab으로 만든 그림 (2026-11-13). 원본·변환본은 바탕화면 `invoke pixelab/`

### 새 코드 `Assets/_JW/Scripts/Python/`
- `PythonSkills.cs`: MagicBolts 표, MagicSkill, MagicBoltSkill(오브젝트 없이 서버가 매 틱 판정), AimedBoltSkill, 원소 4개, MagicZoneSkill, 범위 3개
- `PythonOrbSkills.cs`: 구슬 생성, OrbSkill, 구슬 발사·난사·폭발·보호막
- `PythonUtilitySkills.cs`: 저주(표식), 뱀 속박(기절), 섬광(실명), 블링크, 공중 부양
- `ManaOrbs.cs`(ManaOrb, NoManaOrbError, 개수 SyncVar), `ManaOrbView.cs`, `OrbShield.cs`, `OrbShieldView.cs`, `MagicEffectView.cs`, `MagicBoltVisual.cs`, `PlayerCharacter.cs`(캐릭터 언어, CanUse)
- `Assets/_JW/Scripts/Editor/PythonWizardBuilder.cs`: 메뉴 **Invoke → JW → 파이썬 마법사 만들기**. 이펙트 프리팹, 모션 클립·`Py_Player.controller`, `LanguageRule_Python.asset`, SkillTable `py.*` 17줄, `Player_Python.prefab`(Player Variant), Base 씬 NetworkManager 등록까지

### 고친 파일
- `_JW`: StatusEffects(중독 Poison: 6초, 1초마다 3×겹, 최대 3겹), StatusEffectView, StatusIcon, SkillIconImporter, PlayerMovement(Blink, StartLevitate, RideDuration), SwordRideTimerView(RideDuration 기준), SkillData(계열 Element/Zone/Orb/Curse), SlotCaster(py.* 여러 명 선택 = 구슬 수), PlayerDropInputSimTest(몸이 발판에 걸쳐 있으면 IsDropping 유지가 정상)
- `_UL`: HeapStorage(CollectsGarbage), LanguageRuleData(garbageCollected), FunctionExecutor, PredefinedFunctionIds(py.* 17개), PredefinedFunctionRegistry(py 분류), CharacterLanguages(All에 Python), AccountSession·AccountServer(Language, 임시 계정 언어), InvokeNetworkManager(Character Prefabs로 언어별 프리팹), InvokeAuthenticator(Skip Login Language), 진행도 처음 값 언어별(CharacterProgressData.NewSlotFunction(슬롯, 언어), ItemCountRules.Initial(언어), ProgressStore.LanguageOf, ProgressTestData)
- `_DH`: PlayerHealth(DamageBlocking)
- `_HW`: SkillMotionTable(py.* 줄), BlockEditor·BlockReference(함수 탭에 내 언어 스킬만, py.*는 스킬 구역. `BlockReference.IsSkill`), SlotEquipPanel(내 언어 스킬만)
- `Assets/Scenes/Base.unity`: NetworkManager에 spawnPrefabs `Player_Python`, characterPrefabs (Python, Player_Python), `skipLoginLanguage: C` (마법사 테스트는 Play 중에만 Python으로 바꾸고 저장 안 함)
- `LanguageRule_Python.asset`: C 규칙 복사본이라 C에 실패 종류가 늘면(예: #504 NoTarget·DoubleFree) 여기도 줄을 더해야 함
- docs/systems: account, function-executor, player-health, player-movement, status-effects, predefined-function, slot-caster, skill-icons, player-animation, progress-store
- 사용자가 메뉴를 한 번 돌려서 프리팹·애니메이션·SkillTable 줄·Player_Python이 만들어져 있음

## 상태
- 컴파일 통과(경고만, 원래 있던 FindObjectsByType 경고)
- 구슬 방식 (#533, 이우림 Python 문법 맞추기): `orb = orb_create()`가 구슬(Orb)을 돌려주고, 구슬 스킬에 넘김 — `orb_shot(target, orb)`, `orb_shield(orb)`, `orb_barrage(targets, orbs)`, `orb_burst(orbs)`
  - 쓴 구슬은 빨간 「다 쓴 구슬」로 남고 다시 쓰면 `UsedManaOrbError`. `is_valid_orb(orb)`로 확인. 변수·list에 담지 않은 구슬은 바로 사라짐(가비지 컬렉션)
  - 구슬이 5개(다 쓴 구슬 포함)면 `orb_create()`는 None, heap이 모자라면 MemoryError
- 처음 슬롯·개수 (#533, #548 확정): `slot1` = `magic_missile(get_bug())`, `slot2` = `hex(get_bug())`. 처음부터 열린 노드도 순차 실행·매직 미사일·저주
- 스킬 트리 (#523, #533·#548): 구슬 생성은 변수 문법 다음, 구슬 난사는 list 문법 다음. 표는 skill-tree.md 「스킬 노드 (파이썬 마법사)」
- 자세 (#546, 이우림): Python 자세 규칙 `PythonStanceRule`, `py.init_stance`·`py.get_stance`. 계열별 데미지·쿨타임·상태 이상 시간이 바뀜 (stance.md)
- 스킬 설명 41개 (#539): `Assets/Scripts/Descriptions/ItemDescriptions.Skills.cs`. 수치가 바뀌면 문장도 같이 고침
- 상점 (#539): Epic(구슬 폭발, 메테오 등)만 보스 코어 1. shop.md
- Host 테스트: Base 씬 NetworkManager의 Skip Login Language를 Python으로(저장 안 함) → Play → Host. 임시 계정은 개수를 안 써서 모든 py.* 스킬을 쓸 수 있음

## 남은 일
1. 마법사 SimTest(`[XxxTest] 전체 통과` 형식, #533 구슬 방식 기준): 구슬 최대 5개(다 쓴 구슬 포함), 담지 않은 구슬은 사라짐, 다 쓴 구슬 → UsedManaOrbError, is_valid_orb, 구슬 보호막 한 번 막기, 중독 겹침, 공중 부양 타이머(RideDuration 6초)
2. ~~몬스터 드롭 후보 언어별~~: 고침. 처음 항목은 #550, 해금 노드가 여는 블록은 상점과 같은 후보로 뽑게 바꿈 (`ShopRoller.RollDrop`, monster.md)
3. 기획 문서에 중독 추가
4. 그림
   - 아쉬운 아이콘: `py.chain_lightning`(지그재그), `py.blink`(어두움). 게임에서 보고 다시 뽑을지 정함
   - 검사(C) 이펙트 시안: 7개 중 3개(Thrust_Flash, Skewer_Flash, SwordWave)만 받음(`invoke pixelab/c_effects/`, PixelLab 그대로). SwordWave_Wide, Mark_Flash, Dance_Slash, Parry_Flash는 크레딧이 없어 못 만듦. `_HW/Effects`라 비교 후 혜원과 정하기
   - PixelLab 크레딧 소진. 대안으로 pixelfx(github.com/patch-gg/pixelfx, MIT, v0.3.1, PNG+JSON 시트) 세팅 요청 받음: 저장소 `.claude/skills/pixelfx`에 스킬, 고정 버전, Unity 가져오기 메뉴, 서드파티 고지, Patch Pixel 폰트는 쓰지 않음
5. 파이썬 캐릭터 컨셉 아트 (캐릭터 선택 화면은 지금 글자 「Py」)
- 임시 파일: `Library/pyw_bundle.tar`, `Library/pyw_x/`, `Library/pyw_list.txt` (git 대상 아님, 지워도 됨)
