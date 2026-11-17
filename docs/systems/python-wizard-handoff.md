# 파이썬 마법사 작업 인수인계 (2026-11-17 갱신)

## 기본 정보
- 프로젝트: Invoke (Unity 6000.6.0f1, URP 2D, Mirror). 저장소 `Ulim567/Invoke`, 기준 브랜치 `dev`
- 로컬: `/Users/losecow/Desktop/GameDev/Invoke` (device_bash에서는 `$HOME/mnt/Invoke`)
- 이슈 #486, PR #520으로 dev에 들어감 (2026-11-17). 다음 작업은 새 이슈·새 브랜치(`git switch -c feature/#N origin/dev`)
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
- PR #520으로 dev에 들어감 (2026-11-17). 컴파일 통과(경고만, 원래 있던 FindObjectsByType 경고)
- 마법사 처음 슬롯·개수: `slot1` = `orb_create();`, `slot2` = `orb_shot(GetBug());`, 개수 py.orb_create 1, py.orb_shot 1, get_bug 1. 나머지 py.* 15개는 개수 0이라 실제 계정은 함수에 넣어 저장할 수 없음 (#500). 임시 계정(로그인 건너뛰기)은 개수를 안 써서 다 쓸 수 있음
- Host 테스트: Base 씬 NetworkManager의 Skip Login Language를 Python으로(저장 안 함) → Play → Host

## 남은 일
1. **파이썬 스킬 트리** (다음 작업): py.* 노드를 만들어 해금하면 개수를 받게 함. 스킬 트리 코드·화면은 임혜원(`_HW/Scripts/SkillTree`)이라 같이 정함. 트리를 언어별로 나눌지, 처음 노드(StartingNodes)를 언어별로 둘지도 정해야 함
2. 캐릭터 선택 화면 소개(`_HW/Scripts/UI/AccountView.cs` IntroOf)와 Python 컨셉 아트. #486으로 실제 계정에서도 Python 카드가 열림
3. HUD 구슬 개수
4. 마법사 SimTest(`[XxxTest] 전체 통과` 형식): 구슬 최대 5개, 가비지 컬렉션(끊긴 구슬 생성 누수 없음), 보호막 한 번 막기, 중독 겹침, 공중 부양 타이머
5. 몬스터 드롭 후보 언어별: 김도훈 `MonsterReward`가 `ItemCountRules.Initial()`(C)을 씀 → `Initial(언어)`. PR #520에 적어 둠
6. 기획 문서에 중독 추가
7. 그림
   - 아쉬운 아이콘: `py.chain_lightning`(지그재그), `py.blink`(어두움). 게임에서 보고 다시 뽑을지 정함
   - 검사(C) 이펙트 시안: 7개 중 3개(Thrust_Flash, Skewer_Flash, SwordWave)만 받음(`invoke pixelab/c_effects/`, PixelLab 그대로). SwordWave_Wide, Mark_Flash, Dance_Slash, Parry_Flash는 크레딧이 없어 못 만듦(저장도 안 돼서 다시 만들어야 함). `_HW/Effects`라 비교 후 혜원과 정하기
   - PixelLab 크레딧 소진. 대안으로 pixelfx(github.com/patch-gg/pixelfx, MIT, v0.3.1, PNG+JSON 시트) 세팅 요청 받음: 저장소 `.claude/skills/pixelfx`에 스킬, 고정 버전, Unity 가져오기 메뉴, 서드파티 고지, Patch Pixel 폰트는 쓰지 않음
- 임시 파일: `Library/pyw_bundle.tar`, `Library/pyw_x/`, `Library/pyw_list.txt` (git 대상 아님, 지워도 됨)
