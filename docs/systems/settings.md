# 설정 화면 (GameSettings, SettingsView)

- 담당자: 임혜원

## 하는 일
- 플레이어가 BGM·효과음 음량과 화면 모드(창 / 전체 화면)를 바꾸는 화면 (#292).
- 값은 `GameSettings`(static)가 갖고 저장·적용한다. 화면은 `SettingsView`가 그린다 (코드 작성 규칙 1).
- 하지 않는 일
  - BGM·효과음 재생: 소리를 내는 쪽이 AudioSource의 Output을 아래 Mixer 그룹으로 둔다.

## 동작 규칙
- 여는 법: 화면 오른쪽 위 「설정」 버튼. 창의 X 버튼으로 닫는다.
- `SettingsCanvas` 프리팹(`Assets/_HW/UI/Settings`)은 **Base 씬**에 둔다. Base 씬은 계속 떠 있어서 로비·사냥터·레이드 어디서나 열 수 있다. 정렬 순서 40 (편집 화면 10, 레이드 결과 30보다 위).
- 프리팹은 메뉴 Invoke → UI → 설정 화면 만들기(`SettingsUIBuilder`, 에디터 전용)가 만든다. 여러 번 눌러도 된다.
- 음량
  - 0~100%, 처음 값 80%. PlayerPrefs에 저장해서 다시 켜도 그대로다.
  - Audio Mixer `Assets/_HW/Audio/InvokeMixer`: Master 아래 `BGM`, `SFX` 그룹. 두 그룹의 Volume을 `BGMVolume`, `SFXVolume` 이름으로 노출했다.
  - 슬라이더 값을 데시벨로 바꿔 넣는다 (0% = -80dB, 100% = 0dB).
  - 시작할 때(SettingsView.Start) 저장된 음량을 Mixer에 넣는다. 파라미터가 노출되지 않았으면 Console에 경고를 남긴다.
- 화면 모드
  - 전체 화면: 테두리 없는 창, 모니터 크기 (`FullScreenWindow`). 창 모드: 1280x720 (`Windowed`).
  - Unity가 마지막 화면 모드를 스스로 기억하므로 따로 저장하지 않는다.
  - 에디터에서는 바뀌지 않는다. 빌드에서 확인한다.

## 다른 시스템이 쓰는 것
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `InvokeMixer`의 `BGM` / `SFX` 그룹 | - | - | BGM을 내는 AudioSource는 Output을 `BGM`, 효과음은 `SFX`로 둔다. 그러면 설정 음량을 따른다 |
| `GameSettings.BgmVolume`, `SfxVolume` | - | `float` (0~1) | 지금 음량. 읽기만 한다 |
| `GameSettings.Changed` (event) | - | - | [표시] 값이 바뀌었을 때 |

## 맞춘 결과
- 없음
