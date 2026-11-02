# 맵 배경 (하늘, 폐허 건물, 패럴랙스)

- 담당자: 임혜원

## 하는 일
- 장소 씬(로비, 사냥터, 레이드 던전)의 하늘과 폐허 건물 배경을 카메라보다 느리게 움직여서 멀리 있는 것처럼 보이게 한다 (`Parallax`).
- 보여 주기만 한다. 판정·네트워크와 상관없다. 서버처럼 카메라가 없으면 아무것도 하지 않는다.
- 하지 않는 일
  - 카메라가 내 캐릭터를 따라가기: `PlayerCameraFollow` (박진우)
  - 땅 타일(Grid의 Ground·Platform) 판정: 장소 씬 (이우림), 이동 (박진우)

## 동작 규칙
- 스크립트: `Assets/_HW/Scripts/Map/Parallax.cs`
- 그림: `Assets/_HW/Map` (하늘 `Sky`, 건물 `Buildings`, 타일 `Tiles`, 소품 `Props`). 전부 PPU 16
- 씬마다 `Background` 오브젝트 아래에 겹을 둔다. 앞에 있는 겹일수록 Order in Layer가 크고 Factor가 작다.

| 겹 | Factor | Vertical Factor | Order in Layer |
| --- | --- | --- | --- |
| 하늘 (여러 장, Sorting Layer Sky) | 0.95 | 0.95 | 1 ~ 6 |
| 건물 5 (가장 뒤, Sorting Layer FarBackground) | 0.8 | 0.9 | 1 |
| 건물 4 | 0.7 | 0.9 | 2 |
| 건물 3 | 0.6 | 0.9 | 3 |
| 건물 1·2 (가장 앞, 사냥터 1, 레이드 2) | 0.45 | 0.9 | 4 |

- Factor: 0이면 땅과 같이 움직이고, 1이면 화면에 붙어 있다.
- 처음 카메라를 찾은 순간의 위치를 기준으로 삼는다. 그래서 내 캐릭터가 생겨서 카메라가 처음 움직인 뒤부터 따라온다.
- 가로로 끊기지 않게 Sprite Renderer의 Draw Mode를 Tiled로 하고 Width를 넉넉히 둔다. 하늘 그림은 좌우가 이어지게 만들어 두었다.

| 장소 | 하늘 | 건물 색 |
| --- | --- | --- |
| 로비 | 노을 (Clouds 6) | 갈색 |
| 사냥터 | 흐린 낮 | 올리브 회색 |
| 레이드 던전 | 폭풍 밤 (번개) | 남색 |
| 연습장 | 실내 (CyberLab 벽) | - |

### 맵 모양과 소품 (에디터 도구, `Assets/_HW/Scripts/Map/Editor`)
- `MapPainter`: 메뉴 Invoke → 맵 → 「지금 씬 땅 칠하기」. 씬 이름과 같은 글자 설계도대로 Grid의 `Ground`·`Platform` 타일맵을 비우고 다시 칠한다. 맵 모양을 바꿀 때는 이 설계도를 고친 뒤 다시 누른다
- `MapDecorator`: 메뉴 Invoke → 맵 → 「지금 씬 소품 놓기」. 배치표대로 `Props`(스프라이트 소품), Grid의 `Decor`(타일 소품), 연습장은 `BackWall`(벽 무늬)을 다시 놓는다. 셋 다 충돌 없음
- 점프 높이가 4칸이라 발판·단은 바로 아래 땅보다 3칸 이하로 높게 둔다

| 장소 | 가로 | 바닥 윗면 Y | 모양 |
| --- | --- | --- | --- |
| 로비 | 72칸 (X -36~35) | -4 (오른쪽 높은 단 -2) | 시작 X -30 → 광장(해금 장소 X 0) → 높은 단(입구 X 21, 25, 29). 발판 3개 |
| 사냥터 | 90칸 (X -45~44) | -4 (언덕 -2, 둔덕 -3, 오른쪽 단 -1) | 높낮이 + 발판 5개 |
| 연습장 | 50칸 (X -25~24) | -4, 천장 아랫면 9 | 실내, 발판 3개 |
| 레이드 던전 | 50칸 (X -25~24) | -4, 벽 Y 8까지 | 보스방 평지 + 발판 2개 (김도훈과 맞출 것) |

- 시작 위치(`PlayerStartPosition`)는 로비만 옮김 (X -30, Y -3). 다른 장소의 도착 위치는 「장소 사이 이동하기」(이우림)에서 정한다

## 다른 시스템이 쓰는 것
없음. 씬에 붙여서 쓰기만 한다.

## 에셋 출처
| 에셋 | 만든 사람 | 크레딧 |
| --- | --- | --- |
| Free Sky Backgrounds, New free backgrounds | CraftPix | 선택 |
| Wasteland City | owmyknees | 선택 |
| No Man's Land | logaboga | 선택 |
| Origin Series - Urban Decay | Josee | 선택 |
| Abandoned City Assets | g.lerf | **필수** |
| CyberLab | NicoPardo | 선택 |
