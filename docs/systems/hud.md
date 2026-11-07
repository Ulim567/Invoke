# 게임 중 HUD (HudView)

- 담당자: 임혜원

## 하는 일
- 우림이 시안 8(사냥터)·8-1(로비)대로 게임 중 HUD를 그린다 (#324). 값은 읽기만 한다.
- 하지 않는 일: 레이드 HUD(시안 8-3: 남은 시간, 보스 체력, 파티원) (다음 이슈). 설정 창은 settings.md, 레벨업 창은 level-up.md, 스킬 트리 창은 `SkillTreePanel`(로비에만 있음)이 이벤트를 받아서 연다

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
