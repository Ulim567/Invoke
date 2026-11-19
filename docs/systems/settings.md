# 설정 화면 (GameSettings, SettingsView)

- 담당자: 임혜원

## 하는 일
- 플레이어가 전체·BGM·효과음 음량을 바꾸고, 튜토리얼을 건너뛰거나(#597) 캐릭터 화면으로 돌아가거나(#570) 로그아웃하거나 레이드를 포기하는 창 (#292, 우림이 시안 8-2, 8-4).
- 값은 `GameSettings`(static)가 갖고 저장·적용한다. 화면은 `SettingsView`가 그린다 (코드 작성 규칙 1).
- 하지 않는 일
  - BGM·효과음 재생: 소리를 내는 쪽이 AudioSource의 Output을 아래 Mixer 그룹으로 둔다.

## 동작 규칙
- 여는 법: HUD 왼쪽 위 「설정」 버튼이나 Esc (`HudView.SettingsRequested`, 다른 창이 열려 있지 않을 때). 닫는 법: 「돌아가기」, Esc.
- 창(`SettingsView`)은 `InputBlockingPanel`: 열린 동안 내 캐릭터의 Player·Attack 맵을 끈다. 화면 전체를 어둡게 덮고, 뒤의 HUD는 숨기지 않는다 (`HidesHud` false).
- 키: 방향키 위·아래로 줄 고르기, 왼쪽·오른쪽으로 음량 10%씩, Enter로 버튼 누르기 (EventSystem 이동). 열 때 「돌아가기」를 고른다.
- 「튜토리얼 건너뛰기」 (#597): 「돌아가기」 바로 아래 한 줄. 누르면 확인창 「튜토리얼을 건너뛰시겠습니까? / 건너뛴 튜토리얼은 다시 표시되지 않습니다.」 「취소 (Esc)」 / 「건너뛰기 (Enter)」
  - 「건너뛰기」: 설정 창을 닫고 `SettingsView.TutorialSkipRequested`를 알린다. 튜토리얼(`TutorialView`, 김도훈, tutorial.md)이 받아 끝낸 것으로 저장한다
  - 튜토리얼이 진행 중일 때만 보인다. 진행 중이 아니면(`TutorialView.IsRunning`이 false: 건너뜀, 이미 끝냄, 접속 전) `SettingsView`가 버튼을 숨기고, 빈 줄이 남지 않게 아래 줄(캐릭터 선택·로그아웃, 안내 글)을 35 올리고 창 높이를 267에서 232로 줄인다
  - 확인창은 `ConfirmDialog`(블록 편집기와 같은 컴포넌트)를 설정 창 안에 하나 더 둔 것. 모양은 설정 창 그림(`set_panel`, `set_button`, `set_danger`)
- 아래 버튼: 「튜토리얼 건너뛰기」 아래 한 줄에 왼쪽 「캐릭터 선택」, 오른쪽 「로그아웃」(레이드 던전에서는 「레이드 포기」)이 반씩 나란히 (#570)
  - 「캐릭터 선택」 (#570): `SettingsView.CharacterSelectRequested`를 알린다. 계정 쪽(`AccountFlow`)이 받아 접속은 둔 채 캐릭터 화면으로 돌아간다 (account.md). 받는 쪽이 없거나 레이드 던전에서는 꺼진다. 에디터 Host에서도 켜진다
  - 로비·사냥터·연습장: 「로그아웃」. `SettingsView.LogoutRequested`를 알린다. 받는 쪽이 없으면 버튼이 꺼진다 (에디터 Host는 꺼짐, account.md)
  - 레이드 던전 (내 캐릭터의 `PlayerPublicInfo.Place`가 `PlaceScenes.RaidDungeon`일 때, #362): 「레이드 포기」. `RaidParties.RequestGiveUp()` (place-scenes.md)
  - 방향키: 돌아가기 ↓ 튜토리얼 건너뛰기 ↓ 캐릭터 선택, 캐릭터 선택 ← → 로그아웃. 꺼진 버튼은 건너뛴다
- `SettingsCanvas` 프리팹(`Assets/_HW/UI/Settings`)은 **Base 씬**에 둔다. Base 씬은 계속 떠 있어서 로비·사냥터·레이드 어디서나 열 수 있다. 정렬 순서 40 (편집 화면 10, 레이드 결과 30보다 위).
- 프리팹은 메뉴 Invoke → UI → 설정 화면 만들기(`SettingsUIBuilder`, 에디터 전용)가 만든다. 여러 번 눌러도 된다.
- 음량
  - 전체: `AudioListener.volume` (모든 소리). 처음 값 100%.
  - BGM·효과음: 0~100%, 처음 값 80%. PlayerPrefs에 저장해서 다시 켜도 그대로다.
  - Audio Mixer `Assets/_HW/Audio/InvokeMixer`: Master 아래 `BGM`, `SFX` 그룹. 두 그룹의 Volume을 `BGMVolume`, `SFXVolume` 이름으로 노출했다.
  - 슬라이더 값을 데시벨로 바꿔 넣는다 (0% = -80dB, 100% = 0dB).
  - 시작할 때(SettingsView.Start) 저장된 음량을 Mixer에 넣는다. 파라미터가 노출되지 않았으면 Console에 경고를 남긴다.
- 콘솔 출력 표시 (#536): 소리 상자 아래 「콘솔 출력 표시」 체크 칸. 끄면 HUD 왼쪽 아래 콘솔 출력(printf·print 출력, 오류 알림)을 띄우지 않고, 떠 있던 알림도 바로 지운다. 처음 값 켜짐, PlayerPrefs에 저장
  - 체크 칸은 `SettingsToggle`(Toggle). Enter·클릭으로 켜고 끄고, 음량 줄처럼 방향키 왼쪽으로 끄고 오른쪽으로 켠다(EventSystem의 이동을 받음, 키를 직접 읽지 않음)
  - 체크 그림은 블록 편집기의 「시전 가능」 표시와 같은 `Assets/_HW/UI/BlockCoding/Shapes/ui_check.png`를 청록으로 칠해 쓴다. Galmuri 글꼴에 「✓」가 없기 때문
  - 끄는 것은 내 화면의 표시뿐이다. 출력 함수는 그대로 실행되고 개수도 그대로 센다. 시전 거부 빨간 테두리 같은 슬롯 표시는 그대로
  - 오류까지 끌 수 있게 한 이유: 알림이 화면을 가리는 것이 싫은 플레이어에게 선택지를 주기 위함 (이우림 결정 11/18)
- 화면 모드: 시안 8-2에 없어 창에서 뺐다. `GameSettings.SetFullscreen`은 남아 있다 (전체 화면: 테두리 없는 창, 모니터 크기 / 창 모드: 1280x720. 빌드에서만 바뀜).

## 다른 시스템이 쓰는 것
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `InvokeMixer`의 `BGM` / `SFX` 그룹 | - | - | BGM을 내는 AudioSource는 Output을 `BGM`, 효과음은 `SFX`로 둔다. 그러면 설정 음량을 따른다 |
| `GameSettings.MasterVolume`, `BgmVolume`, `SfxVolume` | - | `float` (0~1) | 지금 음량. 읽기만 한다 |
| `GameSettings.ShowConsoleOutput` | - | `bool` | HUD 콘솔 출력(출력·오류 알림)을 띄우는지 (#536). `HudView`가 읽는다. 바꾸는 것은 `SetShowConsoleOutput(bool)` |
| `SettingsView.LogoutRequested` (static event) | - | - | [표시] 설정 창의 「로그아웃」. 계정 쪽이 받아서 접속을 끊고 로그인 화면으로 돌아간다 |
| `SettingsView.CharacterSelectRequested` (static event) | - | - | [표시] 설정 창의 「캐릭터 선택」 (#570). 계정 쪽이 받아서 접속은 둔 채 캐릭터 화면으로 돌아간다. 받는 쪽이 없으면 버튼이 꺼진다 |
| `SettingsView.TutorialSkipRequested` (static event) | - | - | [표시] 설정 창의 「튜토리얼 건너뛰기」를 확인창에서 골랐을 때 (#597). 튜토리얼(`TutorialView`)이 받아 끝낸 것으로 저장한다. 받는 쪽이 없으면 버튼이 꺼진다 |
| `GameSettings.Changed` (event) | - | - | [표시] 값이 바뀌었을 때 |

## 맞춘 결과
- 없음
