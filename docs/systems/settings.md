# 설정 화면 (GameSettings, SettingsView)

- 담당자: 임혜원

## 하는 일
- 플레이어가 전체·BGM·효과음 음량을 바꾸고, 로그아웃하거나 레이드를 포기하는 창 (#292, 우림이 시안 8-2, 8-4).
- 값은 `GameSettings`(static)가 갖고 저장·적용한다. 화면은 `SettingsView`가 그린다 (코드 작성 규칙 1).
- 하지 않는 일
  - BGM·효과음 재생: 소리를 내는 쪽이 AudioSource의 Output을 아래 Mixer 그룹으로 둔다.

## 동작 규칙
- 여는 법: HUD 왼쪽 위 「설정」 버튼이나 Esc (`HudView.SettingsRequested`, 다른 창이 열려 있지 않을 때). 닫는 법: 「돌아가기」, Esc.
- 창(`SettingsView`)은 `InputBlockingPanel`: 열린 동안 내 캐릭터의 Player·Attack 맵을 끈다. 화면 전체를 어둡게 덮고, 뒤의 HUD는 숨기지 않는다 (`HidesHud` false).
- 키: 방향키 위·아래로 줄 고르기, 왼쪽·오른쪽으로 음량 10%씩, Enter로 버튼 누르기 (EventSystem 이동). 열 때 「돌아가기」를 고른다.
- 아래 버튼
  - 로비·사냥터·연습장: 「로그아웃」. `SettingsView.LogoutRequested`를 알린다. 받는 쪽이 없으면 버튼이 꺼진다 (로그아웃은 아직 계정 쪽에 없음, account.md)
  - 레이드 던전 (내 캐릭터의 `PlayerPublicInfo.Place`가 `PlaceScenes.RaidDungeon`일 때, #362): 「레이드 포기」. `RaidParties.RequestGiveUp()` (place-scenes.md)
- `SettingsCanvas` 프리팹(`Assets/_HW/UI/Settings`)은 **Base 씬**에 둔다. Base 씬은 계속 떠 있어서 로비·사냥터·레이드 어디서나 열 수 있다. 정렬 순서 40 (편집 화면 10, 레이드 결과 30보다 위).
- 프리팹은 메뉴 Invoke → UI → 설정 화면 만들기(`SettingsUIBuilder`, 에디터 전용)가 만든다. 여러 번 눌러도 된다.
- 음량
  - 전체: `AudioListener.volume` (모든 소리). 처음 값 100%.
  - BGM·효과음: 0~100%, 처음 값 80%. PlayerPrefs에 저장해서 다시 켜도 그대로다.
  - Audio Mixer `Assets/_HW/Audio/InvokeMixer`: Master 아래 `BGM`, `SFX` 그룹. 두 그룹의 Volume을 `BGMVolume`, `SFXVolume` 이름으로 노출했다.
  - 슬라이더 값을 데시벨로 바꿔 넣는다 (0% = -80dB, 100% = 0dB).
  - 시작할 때(SettingsView.Start) 저장된 음량을 Mixer에 넣는다. 파라미터가 노출되지 않았으면 Console에 경고를 남긴다.
- 화면 모드: 시안 8-2에 없어 창에서 뺐다. `GameSettings.SetFullscreen`은 남아 있다 (전체 화면: 테두리 없는 창, 모니터 크기 / 창 모드: 1280x720. 빌드에서만 바뀜).

## 다른 시스템이 쓰는 것
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `InvokeMixer`의 `BGM` / `SFX` 그룹 | - | - | BGM을 내는 AudioSource는 Output을 `BGM`, 효과음은 `SFX`로 둔다. 그러면 설정 음량을 따른다 |
| `GameSettings.MasterVolume`, `BgmVolume`, `SfxVolume` | - | `float` (0~1) | 지금 음량. 읽기만 한다 |
| `SettingsView.LogoutRequested` (static event) | - | - | [표시] 설정 창의 「로그아웃」. 계정 쪽이 받아서 접속을 끊고 로그인 화면으로 돌아간다 |
| `GameSettings.Changed` (event) | - | - | [표시] 값이 바뀌었을 때 |

## 맞춘 결과
- 없음
