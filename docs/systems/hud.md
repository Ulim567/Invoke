# 게임 중 HUD (HudView)

- 담당자: 임혜원

## 하는 일
- 우림이 시안 8(사냥터)·8-1(로비)대로 게임 중 HUD를 그린다 (#324). 값은 읽기만 한다.
- 하지 않는 일: 레이드 남은 시간 (레이드 제한 시간이 아직 없음). 설정 창은 settings.md, 레벨업 창은 level-up.md, 스킬 트리 창은 `SkillTreePanel`(로비에만 있음)이 이벤트를 받아서 연다

## 동작 규칙
- 프리팹 `Assets/_HW/UI/Hud/HudCanvas`를 **Base 씬**에 둔다. 정렬 순서 5 (게임 화면 바로 위, 다른 창보다 아래)
  - 메뉴 Invoke → UI → 게임 HUD 만들기(`HudUIBuilder`, 에디터 전용)가 만든다
- 내 캐릭터(`NetworkClient.localPlayer`)가 생기면 보이고, 없으면 숨긴다
- 창이 열린 동안 숨긴다: `InputBlockingPanel.HudHidden` (로비 입구, 스킬 트리, 블록 편집, 스킬 장착, 레이드 방). 창 뒤로 HUD가 겹쳐 보이지 않게. 설정 창은 숨기지 않고 어둡게 덮는다 (settings.md)

| 자리 | 보여 주는 것 | 읽는 곳 |
| --- | --- | --- |
| 왼쪽 위 띠 | 설정(Esc) · Lv · 버그 조각 · 보스 코어 | `PlayerProgress.Level`, `GetMaterialCount("bug_fragment" / "boss_core")` |
| 그 아래 | 「> 사냥터」 지금 장소 이름 | 내 캐릭터가 있는 씬 이름 (`PlaceScenes` 상수 → 한글) |
| 아래 가운데 | HP 막대·숫자 | `PlayerHealth.Hp`, `MaxHp` |
| | Memory 막대: 왼쪽부터 Data(진한 청록)·Heap(청록), 오른쪽부터 Stack(보라), 사용 중 / 전체 | `ProcessMemory` |
| | 슬롯 8칸 Q W E R A S D F | 아래 「슬롯 상태」 |
| 오른쪽 아래 | 스킬 트리 · 레벨업 바로가기 | 누르면 `HudView.SkillTreeRequested` / `LevelUpRequested`. 스킬 트리는 내 캐릭터가 로비에 있을 때만, 레벨업은 받는 창이 있을 때만 켬 (꺼지면 흐리게) |

### 슬롯 상태
| 상태 | 모습 | 읽는 곳 |
| --- | --- | --- |
| 비어 있음 | 점선 테두리, 흐린 키 글자 | `SlotLoadout.GetEntry(i).IsEmpty` |
| 사용 가능 | 키 글자 | - |
| 실행 중 | 칸 전체를 밝은 청록으로 덮음 | `FunctionExecutorNetwork.CastAccepted(슬롯, 작업)` ~ `TaskEnded(작업)` |
| 쿨타임 | 어두운 덮개가 시계 방향으로 줄고, 키 글자 대신 남은 초 | `FunctionExecutor.GetRemainingCooldownTicks` × `SecondsPerTick`. 덮개 비율은 이번 쿨타임이 처음 보였을 때를 1로 |
| 시전 거부 | 0.5초 빨간 테두리 (쿨타임 거부는 제외) | `CastRejected` |
| 공격할 수 없는 장소 (로비) | 모든 칸 무채색 | `PlaceScenes.IsPlayerInNoCastPlace` |

- 장착된 스킬 이름 (#411): 슬롯에 마우스를 올린 동안 칸 위 말풍선에 스킬 이름 (`SlotHoverTip`). C 스킬을 넣은 칸은 그 스킬, 함수를 넣은 칸은 그 안의 C 스킬(스킬 트리에 있는 것), 여러 개면 「검 찌르기 +1」. 1초마다 다시 셈
  - 읽는 곳: `SlotLoadout.GetEntry(i)`, 실제 계정은 `FunctionExecutorNetwork.ReceivedFunctions`, 임시 계정은 `FunctionStorage.Load(GUID)`. 이름은 `SkillTree` 노드 이름

- Esc: 열린 창이 없으면 설정 열기 (`UIShortcut`이 HUD에 붙어 있음, 맨 위 창만 반응). `HudView.SettingsRequested`를 알린다
- 창(`InputBlockingPanel`을 물려받은 로비 입구, 스킬 트리, 편집, 장착, 레이드 방)은 ESC로 닫힌다. 로비 입구 창은 Enter로 들어간다. 창마다 바꾸려면 `OnCancelKey()` / `OnSubmitKey()`를 덮어쓴다
- 시안의 버프·디버프 칸은 아직 기획 전이라 넣지 않음

## 다른 시스템이 쓰는 것
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `HudView.SettingsRequested` (static event) | - | - | [표시] 설정 버튼·Esc. 설정 화면이 받아서 연다 |
| `HudView.SkillTreeRequested` (static event) | - | - | [표시] 스킬 트리 바로가기 |
| `HudView.LevelUpRequested` (static event) | - | - | [표시] 레벨업 바로가기 |
| `HudView.PlaceName(씬 이름)` (static) | 씬 이름 | `string` | 장소 한글 이름 |

## 맞춘 결과
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
- 쓰는 곳: 장소 이름, 레이드에서 숨기기, 스킬 트리 버튼(로비에서만), 파티원 목록, 설정 창의 레이드 포기, 레벨업 결과 창(로비에서), 스킬 트리 창

## 머리 위 닉네임 (#362)
- `NameTagView`(HudCanvas 안): 이 화면의 플레이어(`PlayerRoster`) 중 내 캐릭터와 같은 장소(`IsInMyPlace`)에 있는 플레이어 머리 위에 `PlayerPublicInfo.Nickname`을 띄운다. 내 이름은 청록, 다른 플레이어는 흰색
- 위치: 콜라이더 윗변 + 0.35칸을 화면 좌표로. 숨겨진(쓰러진 뒤) 플레이어, 닉네임이 빈 플레이어는 띄우지 않는다
- 이름표는 프리팹에 8개 미리 만들어 둔다. HUD와 같이 창이 열리면 숨는다
- 플레이어 프리팹은 고치지 않는다

## 소환검 (#365)
- 아래 띠, HP 막대 왼쪽: 「소환검 n/5」와 5칸. 내 캐릭터의 `SummonedSwords.Swords`(박진우)를 매 프레임 읽기만 한다 (클라이언트에서도 목록이 맞음, summoned-sword.md)
- 칸은 소환 순서대로: 사용 가능(`Ready`) 청록, 쓰는 중(던짐, 찌르기, 검무, 검 타기, 떨어짐 등) 어두운 청록, 누수(`Leaked`) 빨강, 없음은 바탕색
- 박진우의 임시 표시 `SummonedSwordHud`(OnGUI)를 대신한다

