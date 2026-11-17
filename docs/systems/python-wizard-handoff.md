# 파이썬 마법사 작업 인수인계 (2026-11-15 갱신)

## 기본 정보
- 프로젝트: Invoke (Unity 6000.6.0f1, URP 2D, Mirror). 저장소 `Ulim567/Invoke`, 기준 브랜치 `dev`
- 로컬: `/Users/losecow/Desktop/GameDev/Invoke` (device_bash에서는 `$HOME/mnt/Invoke`). 기준 커밋 `f51265b`
- 사용자: 박진우 (GitHub `Losecow`). **답변은 항상 한국어 반말.** 코드·명령어·경로만 영어
- 규칙: **로컬에서만 작업, Claude는 git 작업 안 함** (읽기 전용 `git --no-optional-locks`만). 나중에 사용자가 한 번에 올림
- 로컬 파일은 **직접 고쳐도 됨** (사용자 허락). `.unity/.prefab/.asset`도 이번 작업에서는 직접 고쳤음
- 파일 넣기: `device_commit_files(force)` 후 md5 다시 확인. 덮어쓰기가 조용히 안 될 때가 있음. PNG는 전송 뒤 md5가 달라질 수 있음(메타데이터)
- 스킬 기획 초안: https://claude.ai/artifact/GzGeThVys8Bq6N1J35ZRnq (중독은 아직 문서에 반영 안 함)
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
- `Assets/_JW/Effects/Magic/*.png` 25개, `Effects/Status/status_poison.png`, `Resources/StatusIcons/Poison.png`, `Resources/SkillIcons/py.*.png` 17개 — **전부 코드로 그린 임시 그림**. 프롬프트 문서대로 다시 만들어 같은 이름으로 덮어쓰면 됨

### 새 코드 `Assets/_JW/Scripts/Python/`
- `PythonSkills.cs`: MagicBolts 표, MagicSkill, MagicBoltSkill(오브젝트 없이 서버가 매 틱 판정), AimedBoltSkill, 원소 4개, MagicZoneSkill, 범위 3개
- `PythonOrbSkills.cs`: 구슬 생성, OrbSkill, 구슬 발사·난사·폭발·보호막
- `PythonUtilitySkills.cs`: 저주(표식), 뱀 속박(기절), 섬광(실명), 블링크, 공중 부양
- `ManaOrbs.cs`(ManaOrb, NoManaOrbError, 개수 SyncVar), `ManaOrbView.cs`, `OrbShield.cs`, `OrbShieldView.cs`, `MagicEffectView.cs`, `MagicBoltVisual.cs`, `PlayerCharacter.cs`(캐릭터 언어, CanUse)
- `Assets/_JW/Scripts/Editor/PythonWizardBuilder.cs`: 메뉴 **Invoke → JW → 파이썬 마법사 만들기**. 이펙트 프리팹, 모션 클립·`Py_Player.controller`, `LanguageRule_Python.asset`, SkillTable `py.*` 17줄, `Player_Python.prefab`(Player Variant), Base 씬 NetworkManager 등록까지

### 고친 파일
- `_JW`: StatusEffects(중독 Poison: 6초, 1초마다 3×겹, 최대 3겹), StatusEffectView, StatusIcon, SkillIconImporter, PlayerMovement(Blink, StartLevitate), SkillData(계열 Element/Zone/Orb/Curse), SlotCaster(py.* 여러 명 선택 = 구슬 수)
- `_UL`: HeapStorage(CollectsGarbage), LanguageRuleData(garbageCollected), FunctionExecutor, PredefinedFunctionIds(py.* 17개), PredefinedFunctionRegistry(py 분류), CharacterLanguages(All에 Python), AccountSession·AccountServer(Language, 임시 계정 언어), InvokeNetworkManager(Character Prefabs로 언어별 프리팹), InvokeAuthenticator(Skip Login Language)
- `_DH`: PlayerHealth(DamageBlocking)
- `_HW`: SkillMotionTable(py.* 줄), BlockEditor·BlockReference·SlotEquipPanel(내 언어 스킬만 보이기)
- `Assets/Scenes/Base.unity`: NetworkManager에 spawnPrefabs `Player_Python`, characterPrefabs (Python, Player_Python), `skipLoginLanguage: Python`
- 사용자가 메뉴를 한 번 돌려서 프리팹·애니메이션·SkillTable 줄·Player_Python이 만들어져 있음

## 상태
- 메뉴를 돌린 시점까지는 컴파일 통과. **그 뒤에 고친 것(InvokeAuthenticator·AccountServer의 Skip Login Language, 빌더의 NetworkManager 등록)은 컴파일 미확인**
- **게임에서 아직 한 번도 테스트 안 함.** 테스트: Base 씬 Play → Host (에디터) → 마법사로 들어감 → 장착 화면에서 슬롯에 py.* 스킬 넣기(기본은 C 스킬)

## 남은 일
1. Unity 컴파일·게임 테스트, 버그 수정
2. ~~이펙트·아이콘 PixelLab으로 다시 만들기~~ 완료(2026-11-13)
   - 이펙트 25개 + `status_poison`: PixelLab VFX Creator(1개당 1회)로 만들어 같은 이름으로 덮어씀
   - 스킬 아이콘 17개 + 중독 아이콘 `Poison.png`: Create image Pro(24×24, 64개 후보, 1개당 10회)에서 골라 가운데 20×20만 씀. 회색 테두리가 있던 ice_spear·chain_lightning·hex는 테두리를 걷어냄
   - 원본·변환본·미리보기: 바탕화면 `invoke pixelab/` (`ready/`, `icons/`, `_contact*.png`, `_icons_preview.png`)
   - 아쉬운 것: `py.chain_lightning`(갈라진 번개보다 지그재그), `py.blink`(어두움). 게임에서 보고 다시 뽑을지 정함
   - 메뉴 Invoke → JW → 파이썬 마법사 만들기를 다시 눌러야 새 그림이 프리팹에 들어감
   - 덤: 검사(C) 이펙트 시안 7개(Thrust_Flash, Skewer_Flash, SwordWave, SwordWave_Wide, Mark_Flash, Dance_Slash, Parry_Flash)를 만들어 다운로드 폴더에 받아 둠. `_HW/Effects`(임혜원 폴더)라 덮어쓰지 않음. 비교 후 혜원과 정하기
3. 파이썬용 스킬 트리(지금은 노드가 없어 전부 열림), 새 캐릭터 기본 슬롯 장착(지금 C 스킬), 캐릭터 선택 화면 소개(`_HW/Scripts/UI/AccountView.cs` IntroOf), HUD 구슬 개수
4. SimTest(`[XxxTest] 전체 통과` 형식), 문서 반영(status-effects.md 중독, player-movement 블링크·부양, account.md 언어별 프리팹, function-executor.md 가비지 컬렉션), 기획 문서에 중독
5. 올리기 전 정리
   - `Base.unity`의 `skipLoginLanguage`를 `C`로 되돌리기
   - `Base.unity`에는 Play로 생긴 변경이 섞여 있음. `git restore` 하면 등록도 날아가니 등록 부분만 남기기
   - `Library/pyw_bundle.tar`, `Library/pyw_x/`, `Library/pyw_list.txt`는 임시 파일(git 대상 아님, 지워도 됨)
   - 이슈·PR 글 쓰기 (제목 `[Feat/#N]: ...`, Claude 서명 줄 넣지 않음, 리뷰어 Ulim567, 다른 사람 코드 고쳤으니 bendy41·limenow도)
