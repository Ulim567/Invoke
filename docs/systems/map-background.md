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
| 하늘 (여러 장) | 0.95 | 0.95 | -100 ~ |
| 건물 5 (가장 뒤) | 0.8 | 0.9 | -50 |
| 건물 4 | 0.7 | 0.9 | -49 |
| 건물 3 | 0.6 | 0.9 | -48 |
| 건물 2·1 (가장 앞, 사냥터·레이드만) | 0.45 | 0.9 | -47, -46 |

- Factor: 0이면 땅과 같이 움직이고, 1이면 화면에 붙어 있다.
- 처음 카메라를 찾은 순간의 위치를 기준으로 삼는다. 그래서 내 캐릭터가 생겨서 카메라가 처음 움직인 뒤부터 따라온다.
- 가로로 끊기지 않게 Sprite Renderer의 Draw Mode를 Tiled로 하고 Width를 넉넉히 둔다. 하늘 그림은 좌우가 이어지게 만들어 두었다.

| 장소 | 하늘 | 건물 색 |
| --- | --- | --- |
| 로비 | 노을 (Clouds 6) | 갈색 |
| 사냥터 | 흐린 낮 | 올리브 회색 |
| 레이드 던전 | 폭풍 밤 (번개) | 남색 |
| 연습장 | 실내 (CyberLab 벽) | - |

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
