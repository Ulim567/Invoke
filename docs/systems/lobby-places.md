# 로비 입구와 스킬 트리 해금 장소

- 담당자: 임혜원

## 하는 일
- 로비의 입구(사냥터, 연습장, 레이드 던전)와 스킬 트리 해금 장소. 가까이 가서 ↑(상호작용)를 누르면 창을 연다.
- 입구 창에서 「들어가기」를 누르면 이동 요청을 이벤트로 알리고 로그를 남긴다.
- 창이 열린 동안 내 캐릭터의 Player·Attack 액션 맵을 끄고, 닫으면 다시 켠다.
- 하지 않는 일
  - 장소 사이 이동, 서버 요청, 도착 위치: 「장소 사이 이동하기」 (이우림)
  - 레이드 파티 모으기, 파티 전원 옮기기: 이우림
  - 스킬 트리 노드 보여 주기·해금: 「스킬 트리 화면 만들기」, 「재료 써서 스킬 트리 해금하기」 (임혜원)

## 동작 규칙
- 스크립트: `Assets/_HW/Scripts/Lobby/`
- 프리팹: `Assets/_HW/Lobby/` (입구, 해금 장소, 창). Lobby 씬에 놓는다.
- 가까이 있는지: 입구·해금 장소의 Collider 2D(Is Trigger)에 내 캐릭터(`isLocalPlayer`)가 들어와 있으면. 안내(↑ 표시)를 켠다
- 키: `PlayerInputReader.InteractPressed`(↑)만 읽는다
- 창이 열린 동안 Player·Attack 맵을 둘 다 끈다 (이우림과 정함). 슬롯 키가 눌리지 않으므로 SlotCaster가 대상 고르기를 끝내며 Player 맵을 다시 켜는 일이 없다
- 창은 버튼으로 닫는다. 창이 꺼지거나 사라질 때도 맵을 다시 켠다
- 창(`LobbyEntrancePanel`, `SkillTreePanel`)은 Lobby 씬에 하나씩 두고, 입구들이 같이 쓴다

| 입구 | `Destination` | 「들어가기」를 누르면 |
| --- | --- | --- |
| 사냥터 | `HuntingGround` | `TravelRequested("HuntingGround")`, 로그 |
| 연습장 | `TrainingRoom` | `TravelRequested("TrainingRoom")`, 로그 |
| 레이드 던전 | `RaidDungeon` | `RaidEntryRequested()`, 로그 "레이드 던전으로 가려고 함" |

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `LobbyEntrance.TravelRequested` | `string` 갈 씬 이름 (`PlaceScenes`) | - | static 이벤트. 사냥터·연습장 입구에서 「들어가기」를 눌렀을 때. 내 화면에서만 불린다 |
| `LobbyEntrance.RaidEntryRequested` | - | - | static 이벤트. 레이드 던전 입구에서 「들어가기」를 눌렀을 때. 내 화면에서만 불린다 |

- 받는 쪽(이우림)은 `LobbyEntrance.TravelRequested += 함수;`로 등록하고, 사라질 때 `-=`로 해제한다.

## 맞춘 결과
- 이우림과 정함
  - 이동 기능과 서버 요청은 이우림. 입구는 상호작용, 창, 버튼, 로그까지
  - 레이드 입구는 "레이드 던전으로 가려고 함"만 알린다. 파티는 이우림
  - 프리팹은 `_HW` 폴더에 만들고 Lobby 씬에 놓는다 (PR 「씬·프리팹 변경」에 적음)
  - 창이 열린 동안 Player·Attack 액션 맵을 둘 다 끈다
- 이동 함수를 만든 뒤에도 입구는 이벤트(`TravelRequested`, `RaidEntryRequested`) 그대로. 이우림의 `PlaceTravelClient`가 받아 서버에 요청 (#134, [place-scenes.md](place-scenes.md))
