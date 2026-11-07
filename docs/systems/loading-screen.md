# 로딩 화면 그림 (LoadingCanvas, LoadingBarView)

- 담당자: 임혜원

## 하는 일
- 장소를 옮기는 동안과 접속 직후에 덮는 로딩 화면을 우림이 시안 11대로 그린다 (#330): 어두운 바탕, 「Loading...」, 청록 조각이 계속 지나가는 막대.
- 하지 않는 일
  - 언제 보이고 숨길지: 이우림의 `LoadingScreen`·`PlaceTravelClient`가 정한다 ([place-scenes.md](place-scenes.md)). 이 프리팹은 `LoadingScreen` 컴포넌트를 그대로 붙여 쓴다.
  - 진행률 표시: 씬을 얼마나 불러왔는지 알 수 없으므로 막대는 진행률이 아니라 움직임만 보여 준다.

## 동작 규칙
- 프리팹 `Assets/_HW/UI/Loading/LoadingCanvas`를 **Base 씬**에 두고, `PlaceTravel`의 `Loading Screen` 칸에 이 프리팹의 `LoadingScreen`을 넣는다. `PlaceTravel` 프리팹 안의 임시 Canvas는 쓰지 않는다 (꺼진 채로 남음).
- 정렬 순서 100: 모든 게임 화면 위, 접속 화면(110) 아래 (account-screen.md).
- 바탕 Image의 Raycast Target을 켜서 덮는 동안 뒤의 버튼이 눌리지 않는다.
- 막대(`LoadingBarView`): 조각이 1.2초마다 왼쪽에서 오른쪽으로 지나간다. `RectMask2D`로 막대 밖은 자른다. `Time.unscaledTime`을 쓴다.
- 프리팹은 메뉴 Invoke → UI → 로딩 화면 만들기(`LoadingUIBuilder`, 에디터 전용)가 만든다. 여러 번 눌러도 된다. 그림은 같은 폴더의 `load_frame`(테두리), `load_white`.

## 다른 시스템이 쓰는 것
- 없음 (`LoadingScreen`의 `Show`·`ShowImmediate`·`Hide`는 그대로 `PlaceTravelClient`가 부른다)

## 맞춘 결과
- 없음
