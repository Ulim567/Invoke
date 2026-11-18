# 게임 중 HUD (HudView)

- 담당자: 임혜원

## 하는 일
- 우림이 시안 8(사냥터)·8-1(로비)대로 게임 중 HUD를 그린다 (#324). 값은 읽기만 한다.
- 하지 않는 일: 레이드 남은 시간 (레이드 제한 시간이 아직 없음). 설정 창은 settings.md, 레벨업 창은 level-up.md, 스킬 트리 창은 `SkillTreePanel`(Base 씬의 `SkillTreeCanvas`)이 이벤트를 받아서 연다

## 동작 규칙
- 프리팹 `Assets/_HW/UI/Hud/HudCanvas`를 **Base 씬**에 둔다. 정렬 순서 5 (게임 화면 바로 위, 다른 창보다 아래)
  - 메뉴 Invoke → UI → 게임 HUD 만들기(`HudUIBuilder`, 에디터 전용)가 만든다
- 내 캐릭터(`NetworkClient.localPlayer`)가 생기면 보이고, 없으면 숨긴다
- 창이 열린 동안 숨긴다: `InputBlockingPanel.HudHidden` (블록 편집, 스킬 장착, 레이드 방, 레벨업). 창 뒤로 HUD가 겹쳐 보이지 않게. 설정 창은 숨기지 않고 어둡게 덮는다 (settings.md)
- 로비 입구 창과 스킬 트리 창은 HUD를 숨기지 않는다 (#431, `HidesHud` false). 창이 열린 동안(`InputBlockingPanel.AnyOpen`) HUD는 보이기만 하고 누를 수 없다 (`CanvasGroup` interactable·blocksRaycasts 끔)
  - 창이 HUD 위에 그려지도록 `LobbyUI` 캔버스의 Sort Order는 HUD(5)보다 큰 8

| 자리 | 보여 주는 것 | 읽는 곳 |
| --- | --- | --- |
| 왼쪽 위 띠 | 설정(Esc) · Lv · 버그 조각 · 보스 코어. 재료 앞에 재료 그림 16×16 (#431, 박진우의 `material_{ID}.png` 첫 장) | `PlayerProgress.Level`, `GetMaterialCount("bug_fragment" / "boss_core")` |
| Lv 바로 옆 | 경험치 바 40×5 (김도훈 `ExpBarView`). 경험치가 늘면 연한 막대(EFFFD9)가 바로 차고 0.3초 뒤 연두 막대(9BE15D, 메이플스토리 경험치 바 색)가 서서히 따라 찬다. 레벨업하면 끝까지 찬 뒤 0부터 다시. 최대 레벨은 가득. 접속 직후 1초와 경험치가 줄었을 때는 바로 맞춤. 레이드에서는 Lv와 같이 숨김 | `PlayerProgress.Exp`, `Level`, `LevelRules.ExpToNext` |
| 그 아래 | 「> 사냥터 1」 지금 장소 이름 | 내 캐릭터가 있는 씬 이름 (`PlaceScenes` 상수 → 한글. 사냥터는 1~3, #518) |
| 아래 가운데 | HP 막대·숫자. 닳으면 흰 잔상이 0.3초 남았다가 줄어든다 (#431, 몬스터·보스 체력 바와 같은 방식. 회복은 바로) | `PlayerHealth.Hp`, `MaxHp` |
| | Memory 막대: 왼쪽부터 Data(진한 청록)·Heap(청록), 오른쪽부터 Stack(보라), 사용 중 / 전체 | `ProcessMemory` |
| | 슬롯 8칸 Q W E R A S D F | 아래 「슬롯 상태」 |
| 왼쪽 아래 띠 위 | 알림 목록: 오류와 출력을 최대 5개, 알림마다 3초 (#480, #536). 아래 「알림 목록」 | `FunctionExecutorNetwork.CastRejected`, `OwnTaskEnded`, `OwnOutput` |
| 오른쪽 아래 | 스킬 트리(1) · 레벨업(2) 바로가기. 아이콘 자리에 숫자 키 | 누르거나 숫자 1·2를 누르면 `HudView.SkillTreeRequested` / `LevelUpRequested`. 스킬 트리는 내 캐릭터가 로비·연습장에 있을 때만(`PlaceScenes.CanOpenSkillTree`, #586), 레벨업은 받는 창이 있을 때만 켬 (꺼지면 흐리게, 키도 안 먹음). 숫자 키는 HUD의 `UIShortcut` On Shortcut 1·2 (#431, player-input.md)라 다른 창이 열려 있으면 반응하지 않음 |

### 슬롯 상태
| 상태 | 모습 | 읽는 곳 |
| --- | --- | --- |
| 비어 있음 | 점선 테두리, 흐린 키 글자 | `SlotLoadout.GetEntry(i).IsEmpty` |
| 스킬 아이콘 (#444) | 칸 가운데 24×24 아이콘, 키 글자는 오른쪽 아래 구석에 작게 (쿨타임 초는 가운데). C 스킬 칸은 그 스킬, 함수 칸은 함수 안의 첫 C 스킬(말풍선 이름과 같은 것). 스킬이 없으면 아이콘 없이 키 글자만 가운데. 공격할 수 없는 장소에서는 어둡게 | `SkillIcons.Get(스킬 ID)` (박진우, skill-icons.md) |
| 사용 가능 | 키 글자 | - |
| 실행 중 | 칸 전체를 밝은 청록으로 덮음 | `FunctionExecutorNetwork.CastAccepted(슬롯, 작업)` ~ `TaskEnded(작업)` |
| 쿨타임 | 어두운 덮개가 12시부터 시계 방향으로 걷히고, 키 글자 대신 「1.5 sec」 (#431) | `FunctionExecutor.GetRemainingCooldownTicks` × `SecondsPerTick`. C 스킬 칸은 그 스킬, 함수 칸은 안의 C 스킬(`PredefinedFunctionRegistry.TryGet`) 중 가장 긴 것 (함수 자체는 쿨타임이 없음). 덮개 비율은 이번 쿨타임이 처음 보였을 때를 1로 |
| 시전 거부 | 0.5초 빨간 테두리 (쿨타임 거부는 제외) | `CastRejected` |
| 공격할 수 없는 장소 (로비) | 모든 칸 무채색 | `PlaceScenes.IsPlayerInNoCastPlace` |

- 장착된 스킬 이름 (#411): 슬롯에 마우스를 올린 동안 칸 위 말풍선에 스킬 이름 (`SlotHoverTip`). C 스킬을 넣은 칸은 그 스킬, 함수를 넣은 칸은 그 안의 C 스킬(스킬 트리에 있는 것), 여러 개면 「검 찌르기 +1」. 1초마다 다시 셈
  - 읽는 곳: `SlotLoadout.GetEntry(i)`, 실제 계정은 `FunctionExecutorNetwork.ReceivedFunctions`, 임시 계정은 `FunctionStorage.Load(GUID)`. 이름은 `SkillTree` 노드 이름

- Esc: 열린 창이 없으면 설정 열기 (`UIShortcut`이 HUD에 붙어 있음, 맨 위 창만 반응). `HudView.SettingsRequested`를 알린다
- 창(`InputBlockingPanel`을 물려받은 로비 입구, 스킬 트리, 편집, 장착, 레이드 방)은 ESC로 닫힌다. 로비 입구 창은 Enter로 들어간다. 창마다 바꾸려면 `OnCancelKey()` / `OnSubmitKey()`를 덮어쓴다
- 시안의 버프·디버프 칸은 아직 기획 전이라 넣지 않음

### 알림 목록 (#480, #536)
- 목록: 오류 알림과 출력 알림을 한 목록에 최대 5개(`HudView` Inspector의 `Max Notices`) 쌓음. 새 알림은 맨 아래, 넘치면 가장 오래된 알림부터 뺌
- 시간: 알림마다 나타난 뒤 3초(`Notice Time`)가 지나면 빠짐. 마지막 0.4초(`Notice Fade Time`)는 제자리에서 흐려짐(상자의 `CanvasGroup`)
- 영역: 왼쪽 아래 띠 바로 위(왼쪽에서 12, 아래에서 64)의 정해진 크기 영역 `Notices`(150×110, 너비는 `HudView` Inspector의 `Notice Area Width`로 시작할 때 정함). 알림이 하나도 없으면 숨김
  - 영역 밖은 `RectMask2D`가 잘라 냄. 영역 위쪽 45만큼(`HudView` Inspector의 `Notice Fade Height`, 시작할 때 마스크에 넣음)은 경계가 흐려(softness) 밀려 올라간 알림이 위로 갈수록 흐려지며 사라짐
  - softness는 위아래에 함께 걸리므로, 마스크 아래 경계를 같은 만큼 영역 밖으로 내려(padding 음수) 아래쪽은 흐려지지 않게 함
- 쌓기: 영역 안의 목록 `Content`(`VerticalLayoutGroup`, 간격 1, 아래 기준)에 알림마다 상자 하나. 맨 아래가 가장 새 알림
  - 새 알림이 오면 목록을 늘어난 높이(붙이기 전과 후의 목록 높이 차이)만큼 내렸다가 부드럽게 0으로 되돌림(`Notice Rise Sharpness`). 그래서 위 알림들이 처음 자리에서 그대로 밀려 올라감. 다시 쓰는 상자의 이전 글 크기로 재지 않도록 글을 넣은 직후 글자 크기를 다시 계산함
  - 상자는 견본 `NoticeTemplate`(바탕 `hud_gauge` 반투명 70%, `CanvasGroup`, 글자)을 `HudView`가 복사해 쓰고, 빠진 상자는 다시 씀
  - 새 상자를 붙인 그 자리에서 목록을 내린 위치까지 적용함. 다음 프레임까지 미루면 그 사이 한 번 그려질 때 위 알림들이 먼저 튀어 올랐다가 내려와, 아래로 내려가는 것처럼 보였음
  - 최대 너비: 글이 영역 너비(150에서 상자 안쪽 여백을 뺀 너비)보다 넓으면 그 너비에서 줄을 바꿈. 짧은 글은 줄바꿈 없이 글자 길이에 맞춤(글자의 `LayoutElement.preferredWidth`)
  - 구조는 `HudUIBuilder`가 만듦. 바꾸면 메뉴 「Invoke → UI → 게임 HUD 만들기」로 프리팹을 다시 만듦
- 같은 알림 합치기: 바로 앞 알림과 글이 같으면 새로 쌓지 않고 첫 줄 끝에 `(x4)`처럼 횟수를 붙이고 3초를 다시 셈. 반복문 안의 실패처럼 같은 오류가 이어질 때 목록이 같은 줄로 차지 않게 함. 값이 매번 다른 출력은 합쳐지지 않음
- 오류 알림: 두 줄, 왼쪽 정렬. 첫 줄 `[분류] 이름`(분류 글자만 빨강), 둘째 줄 `슬롯 > 함수 이름 : 현상 설명`(`슬롯 > 함수 이름`만 하늘색 `#5EB7F2`, Inspector의 `Notice Location Color`). 예: `[Runtime Error] UnarmedError` / `Q > slot1 : 손에 검이 없음`
  - 「·」, 「—」를 쓰지 않는 이유: HUD 글꼴(Galmuri)에 없어 빈칸으로 보임
  - 슬롯·함수 이름 (#536): 끝난 작업은 `TaskEndInfo.Slot`, `FunctionName`. 시전 거부는 HUD가 슬롯 내용에서 찾음(스킬을 바로 넣었으면 스킬 이름, 함수를 넣었으면 그 함수 이름). 슬롯이 없는 작업은 함수 이름만
  - 문장: `FailureNotice`(이우림, [function-executor.md](function-executor.md))가 만들고, 슬롯·함수 이름은 HUD가 둘째 줄 앞에 붙임
- 출력 알림 (#536): 한 줄. `[Q] n = 3`(`[Q]`만 청록, Inspector의 `Output Label Color`). 슬롯이 없는 작업은 `[함수 이름] …`. 출력 글은 `printf`·`print`가 만든 것(predefined-function.md 「출력 function」)
- 플레이어가 정한 글(출력 값, 함수 이름)은 `<noparse>`로 감싸 TMP 서식으로 읽히지 않게 함
- 설정 창의 「콘솔 출력 표시」를 끄면(`GameSettings.ShowConsoleOutput`이 false) 알림을 띄우지 않고, 끄는 순간 떠 있던 알림도 지움 (#536, settings.md)

| 알리는 것 | 받는 곳 |
| --- | --- |
| 시전 거부 중 `NotEnoughMemory`, `CompileError`(빌드 실패, 문장 `[Cast Rejected] CompileError` / `컴파일 오류가 있는 함수`, #506)만 (그 밖의 거부는 슬롯 덮개·빨간 테두리·무채색이 이미 보여 줌) | `CastRejected` → `FailureNotice.ForRejection` |
| 몬스터의 실행 중단 공격으로 끊긴 내 작업(`Interrupted`, 원인 `MonsterAttack`). 문장 `[Interrupted] 실행 중단 공격` / `함수가 모두 멈춤` (#504) | `OwnTaskEnded` → `FailureNotice.ForTaskEnd` |
| runtime error로 분류된 실패(스킬 에러 포함). Python 캐릭터는 Python 이름(NameError, TypeError 등, #532) | `OwnTaskEnded` → `FailureNotice.ForTaskEnd` |
| 내 작업의 출력 (#536) | `OwnOutput` |

- 알리지 않는 것: 정상 종료, 취소(`Cancelled`), 사망·장소 이동·재시작으로 끊긴 작업(원인 `System`, #504), 개발자 책임(`InternalError`), 실행 중에 난 compile error(시전 전에 빌드가 `CompileError` 거부로 잡음, #506)

- 아래 띠 (#431): 띠 전체를 칠하지 않는다 (바닥을 너무 가림). 가운데 막대·슬롯 뒤에만 아래에서 위로, 가운데에서 양옆으로 흐려지는 그림자(`hud_band_fade`)

## 다른 시스템이 쓰는 것
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `HudView.SettingsRequested` (static event) | - | - | [표시] 설정 버튼·Esc. 설정 화면이 받아서 연다 |
| `HudView.SkillTreeRequested` (static event) | - | - | [표시] 스킬 트리 바로가기 |
| `HudView.LevelUpRequested` (static event) | - | - | [표시] 레벨업 바로가기 |
| `HudView.PlaceName(씬 이름)` (static) | 씬 이름 | `string` | 장소 한글 이름 |

## 맞춘 결과
- 이우림이 고친 것 (#480): `HudView`에 실패 알림, `HudUIBuilder`에 알림 칸(`Notice`). HUD 프리팹은 메뉴로 다시 만듦
- 김도훈이 고친 것 (경험치 바): `HudUIBuilder`에 Lv 옆 `ExpBar`(+`ExpBarView`, `Assets/_DH/Scripts/UI`)와 레이드에서 숨기기. `HudView`는 그대로. HUD 프리팹은 메뉴로 다시 만듦
- 김도훈이 고친 것 (튜토리얼): `HudUIBuilder`에 장소 이름 아래 왼쪽 `Tutorial` 대사 창(`TutorialView`·`TutorialTracker`·`TutorialGuide`, `Assets/_DH/Scripts/UI/Tutorial`, [tutorial.md](tutorial.md)), 가야 할 곳을 가리키는 화면 가장자리 화살표 `TutorialEdgeArrow`, Memory 막대 뒤의 깜빡이는 테두리 `TutorialMemoryGlow`. 스스로 켜고 끄므로 `hideInRaid`에는 넣지 않음. `HudView`는 그대로
- 소환검 개수(`SummonedSwordHud`, 박진우)와 체력 바(`TempHealthBar`, 김도훈)의 임시 표시는 이 HUD가 대신할 예정. 그때 각자 끈다

## 레이드 던전 (시안 8-3, #337)
- 왼쪽 위 띠는 「설정 Esc」만 남기고 Lv, 재료를 숨긴다. 오른쪽 아래 바로가기도 숨긴다 (`HudView.hideInRaid`)
- 파티원 목록 (`RaidPartyView`): 장소 이름 아래 「파티」와 줄. 나를 뺀 같은 장소의 플레이어 (`PlayerRoster.Players`에서 `IsLocal`이 아니고 `IsInMyPlace`인 것). 레이드 던전이고 파티원이 있을 때만 보임
  - 줄마다 이름, HP 막대 (`PlayerHealth.Hp / MaxHp`). 쓰러지면(`IsDead`) 어두운 줄, 이름 흐리게, 「사망」, 막대 비움
  - 줄은 2개 (레이드 최대 3명). 이름은 `PlayerPublicInfo.Nickname` (비어 있으면 「[파티원 1]」)
  - 관전 중 표시 (#419): 내 카메라가 따라가는 파티원(`PlayerCameraFollow.SpectateTarget`, 박진우)의 이름 뒤에 「관전 중」. 예: 「[파티원 1] 관전 중」. 관전 중이 아니면 붙이지 않음
    - 따라가는 대상은 죽지 않은 파티원만 되므로 「사망」 표시와 겹치지 않음
- 보스 체력 바: 위쪽 가운데. 움직임은 김도훈의 `BossHealthBar` 컴포넌트를 HUD 프리팹 안에 붙여 그대로 씀 (보스가 있을 때만, 빨강 = 지금, 흰색 = 따라 줄어듦, 구분선 70%, 40%). 이름은 빨간 글자, 오른쪽 위 퍼센트는 `BossBarPercent`가 빨간 막대 비율을 읽어 씀
  - Base 씬의 `BossHealthBar` 프리팹(임시 색 막대)은 꺼 둠 (두 개가 겹치지 않게)

## 장소 판정 (#362)
- 내 캐릭터가 어느 장소에 있는지는 `PlayerPublicInfo.Place`(이우림 #359)로 본다 (`PlayerPlace.Of(player)`, `PlayerPlace.Mine`). 클라이언트에서는 Mirror가 플레이어를 늘 `Base` 씬에 만들어서 `scene.name`으로는 알 수 없다
- 쓰는 곳: 장소 이름, 레이드에서 숨기기, 스킬 트리 버튼(로비·연습장에서만), 파티원 목록, 설정 창의 레이드 포기, 레벨업 결과 창(로비에서), 스킬 트리 창

## 머리 위 닉네임 (#362)
- `NameTagView`(HudCanvas 안): 이 화면의 플레이어(`PlayerRoster`) 중 내 캐릭터와 같은 장소(`IsInMyPlace`)에 있는 플레이어 머리 위에 `PlayerPublicInfo.Nickname`을 띄운다. 내 이름은 청록, 다른 플레이어는 흰색
- 위치: 콜라이더 윗변 + 0.35칸을 화면 좌표로. 숨겨진(쓰러진 뒤) 플레이어, 닉네임이 빈 플레이어는 띄우지 않는다
  - 기준 (#426): 보간된 캐릭터 위치(`transform.position`) + 몸 기준점에서 콜라이더 윗변까지의 거리. 콜라이더 `bounds`는 물리 단계마다만 바뀌어 그대로 쓰면 끊김
  - 시점 (#426): 카메라 이동(`PlayerCameraFollow`, `CameraBounds`)이 끝난 뒤 UI를 그리기 직전(`Canvas.willRenderCanvases`)
  - 화면 좌표 (#426): `Camera.WorldToScreenPoint`를 쓰지 않고 카메라 `transform` 위치(`PixelPerfectCamera.RoundToPixel`로 맞춤)로 직접 계산. Pixel Perfect Camera가 카메라 행렬을 그리기 직전에만 넣어서 `WorldToScreenPoint`는 지난 프레임 카메라 위치로 계산됨
- 이름표는 프리팹에 8개 미리 만들어 둔다. HUD와 같이 창이 열리면 숨는다
- 플레이어 프리팹은 고치지 않는다

## 소환검 (#365)
- 아래 띠, HP 막대 왼쪽: 「소환검 n/5」와 5칸. 내 캐릭터의 `SummonedSwords.Swords`(박진우)를 매 프레임 읽기만 한다 (클라이언트에서도 목록이 맞음, summoned-sword.md)
- 칸은 소환 순서대로: 사용 가능(`Ready`) 청록, 쓰는 중(던짐, 찌르기, 검무, 검 타기, 떨어짐 등) 어두운 청록, 누수(`Leaked`) 빨강, 없음은 바탕색
- 박진우의 임시 표시 `SummonedSwordHud`(OnGUI)를 대신한다
- 파이썬 마법사 (#527): 내 캐릭터에 `ManaOrbs`(박진우)가 있으면 같은 자리에 「마나 구슬 n/5」와 5칸. `ManaOrbs.TotalCount`와 `IsSpentAt(i)`를 매 프레임 읽기만 한다(클라이언트에서는 서버가 보낸 값). n은 다 쓴 구슬도 센 수(최대 5개 제한과 같은 기준). 칸은 쓸 수 있는 구슬 사용 가능 색, 다 쓴 구슬 누수 빨강, 없음은 바탕색 (#532)

