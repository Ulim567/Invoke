# 로비 입구

- 담당자: 임혜원

## 하는 일
- 로비의 입구(사냥터, 연습장, 레이드 던전). 가까이 가서 ↑(상호작용)를 누르면 창을 연다.
- 스킬 트리 창은 HUD의 「스킬 트리」 버튼으로 연다 (hud.md). 로비의 스킬 트리 해금 장소(`SkillTreeStation`)는 없앴다 (#378)
- 입구 창에서 「들어가기」를 누르면 이동 요청을 이벤트로 알리고 로그를 남긴다.
- 레이드 던전 입구는 입구 창 없이, ↑를 누르면 바로 이벤트로 알린다 (#242).
- 창이 열린 동안 내 캐릭터의 Player·Attack 액션 맵을 끄고, 닫으면 다시 켠다.
- 하지 않는 일
  - 장소 사이 이동, 서버 요청, 도착 위치: 「장소 사이 이동하기」 (이우림)
  - 레이드 파티 모으기, 파티 전원 옮기기: 이우림
  - 스킬 트리 노드 보여 주기·해금: 「스킬 트리 화면 만들기」, 「재료 써서 스킬 트리 해금하기」 (임혜원)

## 동작 규칙
- 스크립트: `Assets/_HW/Scripts/Lobby/`
- 프리팹: `Assets/_HW/Lobby/` (입구, 창). Lobby 씬에 놓는다.
- 가까이 있는지: 입구의 Collider 2D(Is Trigger)에 내 캐릭터(`isLocalPlayer`)가 들어와 있으면. 안내(↑ 표시)를 켠다
- 내 캐릭터가 다른 장소로 가면 놓아준다: 내 캐릭터의 물리 세계(`scene.GetPhysicsScene2D()`)가 로비와 다르면 안내를 끄고 ↑에 반응하지 않는다. 호스트는 로비 씬이 남아 있어 `OnTriggerExit2D`가 불리지 않기 때문 (이우림 #190 알림, #209)
- 키: `PlayerInputReader.InteractPressed`(↑)만 읽는다
- 공통 부모 `PlaceInteractable`(#285, 이전 이름 `LobbyInteractable`): 범위·안내·물리 세계 확인·↑ 읽기를 한다. 입구·`PanelStation`과 출구 `PlaceExit`(이우림)가 물려받아 `Interact()`만 구현한다
  - 표시 (#434): ↑ 안내 자리에 작은 민트 마름모(7×7픽셀)가 늘 1픽셀씩 천천히 위아래로 뜨며 살짝 깜빡인다. 가까이 가면 마름모가 밝아지고 위에 ↑ 키 그림(9×9)이 뜬다. 예전 ↑ 안내(노란 세모, `prompt`)는 켜지 않고 자리만 쓴다. 물려받는 모든 것(입구, 연습장 터미널·장착대, 출구)에 저절로 붙는다
    - 그림은 코드로 만든다 (에셋 없음). ↑ 안내의 Sorting Layer·Order를 따라간다. 끄려면 Inspector의 `Show Idle Marker` (끄면 예전 ↑ 안내만)
    - ↑ 키를 켜고 끌 때 Renderer가 아니라 오브젝트를 켜고 끈다 (호스트의 `HostPlaceVisibility`가 다른 장소 그림의 Renderer를 끄므로)
  - 검 타기 중(`PlayerMovement.IsRiding`)이거나 죽었거나 경직 중(`PlayerHealth.CanAct` false)이면 ↑를 무시한다
- 창이 열린 동안 Player·Attack 맵을 둘 다 끈다 (이우림과 정함). 슬롯 키가 눌리지 않으므로 SlotCaster가 대상 고르기를 끝내며 Player 맵을 다시 켜는 일이 없다
- 창은 버튼으로 닫는다. 창이 꺼지거나 사라질 때도 맵을 다시 켠다
- 로비 밖에서도 같은 방식을 쓴다 (#225): `PanelStation`(PlaceInteractable)이 ↑에 `InputBlockingPanel.OpenFor(player)`를 부른다. 연습장의 편집 터미널(`BlockEditorPanel`), 장착대(`SlotEquipPanel`)
- 창(`LobbyEntrancePanel`, `SkillTreePanel`)은 Lobby 씬에 하나씩 둔다. 입구 창은 입구들이 같이 쓰고, 스킬 트리 창은 HUD 버튼이 연다
- 입구 창 모양 (#434): 다른 창(계정 화면, 스킬 트리)과 같게. 보라 바탕 + 민트 테두리(`acc_panel`), 제목 「> 사냥터」 민트, 설명, 왼쪽 아래 「Enter 들어가기 Esc 닫기」, 오른쪽 아래 닫기(테두리 버튼)·들어가기(주황). 메뉴 Invoke → UI → 로비 입구 창 시안대로(`LobbyEntranceUIBuilder`)가 LobbyUI 프리팹의 EntrancePanel/Window 모양만 바꾼다 (버튼 연결은 그대로)

| 입구 | `Destination` | 알리는 때 | 알리는 것 |
| --- | --- | --- | --- |
| 사냥터 | `HuntingGround` | 입구 창의 「들어가기」 | `TravelRequested("HuntingGround")`, 로그 |
| 연습장 | `TrainingRoom` | 입구 창의 「들어가기」 | `TravelRequested("TrainingRoom")`, 로그 |
| 레이드 던전 | `RaidDungeon` | ↑ (입구 창 없음) | `RaidEntryRequested(player)`, 로그 "레이드 던전으로 가려고 함" |

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `LobbyEntrance.TravelRequested` | `string` 갈 씬 이름 (`PlaceScenes`) | - | static 이벤트. 사냥터·연습장 입구에서 「들어가기」를 눌렀을 때. 내 화면에서만 불린다 |
| `LobbyEntrance.RaidEntryRequested` | `PlayerInputReader` ↑를 누른 내 캐릭터 | - | static 이벤트. 레이드 던전 입구에서 ↑를 눌렀을 때 (입구 창 없음). 내 화면에서만 불린다. 이우림의 `RaidLobbyOpener`가 받아 레이드 방 화면(`RaidLobbyUI.OpenFor`, `raid.md`)을 연다 |

- 받는 쪽(이우림)은 `LobbyEntrance.TravelRequested += 함수;`로 등록하고, 사라질 때 `-=`로 해제한다.

## 맞춘 결과
- 이우림과 정함
  - 이동 기능과 서버 요청은 이우림. 입구는 상호작용, 창, 버튼, 로그까지
  - 레이드 입구는 "레이드 던전으로 가려고 함"만 알린다. 파티는 이우림
  - 프리팹은 `_HW` 폴더에 만들고 Lobby 씬에 놓는다 (PR 「씬·프리팹 변경」에 적음)
  - 창이 열린 동안 Player·Attack 액션 맵을 둘 다 끈다
- 이동 함수를 만든 뒤에도 입구는 이벤트(`TravelRequested`, `RaidEntryRequested`) 그대로. 이우림의 `PlaceTravelClient`가 받아 서버에 요청 (#134, [place-scenes.md](place-scenes.md))
- 레이드 방 (#242, 이우림 제안, 임혜원 확인 11/4)
  - 레이드 입구는 입구 창 없이 ↑에 `RaidEntryRequested(player)`. 레이드 방 화면이 입구 창 역할 (방 목록, 파티방, [raid.md](raid.md))
  - `RaidEntryRequested`는 `PlaceTravelClient`가 아니라 `RaidLobbyOpener`가 받음. 레이드 던전 이동은 파티장이 시작할 때 (다음 이슈)
  - `LobbyEntrance.cs` 변경 (이우림, 임혜원 허락)
    - `LobbyEntrance.Interact`: 레이드 입구만 `panel.Open` 대신 `RaidEntryRequested?.Invoke(player)`
    - `RaidEntryRequested`: `Action` → `Action<PlayerInputReader>`
    - `RequestEnter`의 레이드 입구 처리, `Description`의 레이드 입구 글: 쓰이지 않아 지움
  - 프리팹·씬: `_HW` 프리팹은 바꾸지 않음. 레이드 입구 프리팹의 `Panel` 칸은 쓰이지 않음
