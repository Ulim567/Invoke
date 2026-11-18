# 상점

- 담당자: 이우림

## 하는 일
- 로비 상점: 문법 블록 4칸과 함수 4칸에 올라온 항목을 버그 조각으로 사서 그 항목의 가진 개수를 1 늘림 (#534)
- 사고 싶은 것이 없으면 버그 조각을 내고 8칸을 새로 받음 (새로고침)
- 상점 칸은 서버 DB에 저장. 구매·새로고침 때만 바뀌고, 상점을 열 때는 바뀌지 않음
- 하지 않는 일
  - 가진 개수의 저장·남은 개수 계산: 진행도 저장소 (progress-store.md, #500)
  - 잡몹 블록·함수 드랍: `MonsterReward`(김도훈, monster.md)
  - 상점 문 그림: 임혜원에게 부탁

## 동작 규칙

### 정한 기획
| 항목 | 결정 |
| --- | --- |
| 칸 | 문법 블록 4칸(칸 번호 0~3) + 함수 4칸(4~7). 후보가 적어도 칸 수는 그대로 |
| 문법 블록 후보 | 해금한 문법 블록 중 캐릭터 언어에서 쓸 수 있는 것. 상점 무게로 뽑음 |
| 함수 후보 | 해금한 함수: 스킬, get_bug, is_valid_target(Python은 is_valid_orb도), wait, malloc·free(C만). 희귀도 무게로 뽑음 |
| 뺄 항목 | 99개(`ItemCountRules.MaxCount`) 가진 항목, 대시·더블점프(`common.*`, 나중에 키보드 고정 이동기로 바뀜), 테스트 함수 |
| 중복 | 허용. 같은 항목이 여러 칸에 올라올 수 있음 |
| 후보가 없을 때 | 빈 칸. 예: 새 C 캐릭터는 문법 블록 4칸이 빈 칸 (처음 해금한 순차 실행은 함수 블록과 wait만 엶) |
| 문법 블록 가격 | 칸에 올라올 때 버그 조각 7~13 중 무작위 |
| 함수 가격 | 칸에 올라올 때 희귀도의 범위에서 무작위 (임시 값, 박진우가 정함) |
| 보스 코어 | 지금은 가격에 넣지 않음. 가격이 재료 목록이라 규칙 값만 바꾸면 넣을 수 있음 |
| 구매 | 가격만큼 재료 빼기 + 가진 개수 +1 + 산 칸에 새 상품. 새 상품은 산 뒤의 개수로 뽑음 |
| 새로고침 | 버그 조각 10개. 8칸을 모두 새로 뽑음 |
| 칸 저장 | 서버 DB(`CharacterShopOffers`). 처음 상점을 열 때 칸이 없으면 서버가 8칸을 뽑아 저장 |
| 임시 계정 | 상점은 열림. 모든 노드를 해금한 것으로 보고 뽑음 (편집 화면과 같음). 사면 재료만 빠지고 가진 개수는 그대로. DB에 쓰지 않음 |

### 상점 숫자 (`ShopRules`)
- 코드: `Assets/_UL/Scripts/Shop/ShopRules.cs`. 서버의 뽑기·판정과 화면이 함께 읽음
- 문법 블록 상점 무게 (Notion 「블록 표」의 「상점 무게」)

| 항목 ID | 무게 | 항목 ID | 무게 |
| --- | --- | --- | --- |
| `If` | 10 | `Assign` | 10 |
| `ElseIf` | 3 | `ArrayDeclare` | 3 |
| `Else` | 6 | `ArrayGet` | 3 |
| `For` | 6 | `ArraySet` | 3 |
| `While` | 2 | `Arithmetic` | 6 |
| `Break` | 1 | `Compare` | 6 |
| `Continue` | 1 | `Logic` | 3 |
| `Declare` | 10 | `Not` | 3 |
| `Return` | 2 | 그 밖 (Python의 `Global`, `Del`, `ListNew`, `ListAppend`, `ListPop`, `ListLen`) | 임시 1 |

- 희귀도 (`ShopRarity`, 임시 값, 박진우가 정함)

| 희귀도 | 무게 | 가격 (버그 조각) | 보스 코어 |
| --- | --- | --- | --- |
| `Common` | 60 | 8~12 | 0 |
| `Rare` | 30 | 15~20 | 0 |
| `Epic` | 10 | 25~30 | 0 |

- 함수의 희귀도: `ShopRules.FunctionRarities`. 표에 없는 함수는 `Common`
  - 임시 기준: 처음 스킬·기본 함수는 Common, 스킬 트리에서 재료를 많이 쓰는 스킬과 malloc은 Rare, 보스 코어가 드는 마지막 스킬(전장 휩쓸기, 검무, 메테오, 구슬 폭발)은 Epic
- 새로고침 비용: `ShopRules.RefreshCost` (버그 조각 10)

### 상점 칸 (`ShopOffer`)
- 칸 하나: 항목 ID(`ItemId`) + 가격(`Price`, `ShopPrice` 목록)
  - 항목 ID: 가진 개수와 같은 ID (`If`, `c.thrust`, `input.get_bug`, `Wait`, `Malloc`). 빈 글자면 빈 칸
  - `ShopPrice`: 재료 ID(`MaterialId`)와 개수(`Count`)
- 칸 번호: 목록 안의 위치 (0~3 문법 블록, 4~7 함수)

### DB (`CharacterShopOffers`, progress-store.md)
| column | 타입 | 규칙 |
| --- | --- | --- |
| `Id` | 정수 | 기본 키. SQLite가 자동으로 붙임 |
| `CharacterKey` | 문자열 | `Characters.Key` |
| `SlotIndex` | 정수 | 칸 번호 0~7 |
| `ItemId` | 문자열 | 항목 ID. 빈 글자면 빈 칸 |
| `Price` | 문자열 | 가격 `재료ID:개수`를 쉼표로 이은 글자. 예: `bug_fragment:7,boss_core:1` |

- (`CharacterKey`, `SlotIndex`) unique
- 칸이 하나라도 빠졌으면 불러올 때 빈 목록 → 상점을 열 때 서버가 8칸을 다시 뽑음
- 테스트용 「진행도 넣기」(`ReplaceAll`)는 상점 칸을 지우기만 함 (바뀐 해금 목록으로 다시 뽑게)

### 서버 판정 (`PlayerProgress`)
- 구매 검사 순서: 먼저 걸린 이유 하나만 돌려줌

| 순서 | 검사 | 걸리면 |
| --- | --- | --- |
| 1 | 칸 번호 0~7 | `InvalidSlot` |
| 2 | 올라온 항목이 있음 | `EmptySlot` |
| 3 | 가진 개수가 99 미만 (실제 계정만) | `MaxCount` |
| 4 | 가격의 재료마다 가진 개수가 가격 이상 | `MaterialShort` |

- 통과하면 `ProgressStore.SaveShop`으로 재료·개수·칸을 한 트랜잭션으로 먼저 쓰고, 성공한 뒤에 메모리를 바꿈
- 클라이언트는 칸 번호만 보냄. 상품과 가격은 서버가 가진 칸에서 읽음

### 화면 (`ShopPanel`, 게임 UI 캔버스 18~18-2)
- 여는 법: 로비 상점 자리(`ShopStation`, `PanelStation`)에서 ↑. 닫기는 ESC 또는 「닫기」
  - 상점 자리: 가까이 가면 「[↑] 상점」 안내판 (문과 같은 모양)
- 위 줄: 「> 상점」, 버그 조각·보스 코어 가진 개수, 닫기
- 카드: 첫 줄 이름(함수는 한글 이름 「상처 가르기」, 한글 이름이 없으면 영문), 둘째 줄 표기(문법 블록은 형식, 함수는 원형. 코드 색, 길면 두 줄), 분류와 관계없는 어두운 바탕 `#241B36` + 왼쪽 세로 띠와 제목 글자만 분류 색(`BlockLabels`. 분류 색을 넓게 깔면 노랑이 탁해져 코드 색 글자가 읽히지 않음), 가격 줄(재료 그림 + 이름·개수), 가진 개수(임시 계정은 숨김). 빈 칸은 「빈 칸」
  - 함수의 영문 이름은 원형에 들어 있으므로 따로 적지 않음
- 카드 누르기
  - 재료가 모자라면: 카드가 흔들리고 테두리와 모자란 재료가 잠깐 빨갛게 (18-2), 아래 줄에 「재료가 모자랍니다.」
  - 99개면: 카드가 흔들리고 아래 줄에 「If: 이미 99개를 가지고 있습니다.」
  - 아니면 구매 확인 창 (18-1): 「For 블록 1개를 구매하시겠습니까?」, 소비하는 재료(재료마다 그림 + 「버그 조각 12」 한 줄. 게임 글꼴에 ×·→ 글자가 없어 기호 없이 숫자만), 「가진 개수 3 > 4」. 예(Enter, 오른쪽 주황) / 아니오(ESC, 왼쪽 테두리): 게임 전체 버튼 규칙 (#430)
- 새로고침 버튼: 비용 표시. 버그 조각이 비용보다 적으면 비활성화 (흐리게)
- 칩: 구매·새로고침이 서버에서 끝나면(`Done`) 위 줄 바로 아래 가운데에 둥근 민트 칩. 0.15초 동안 나타나고, 1.5초 머문 뒤, 0.3초 동안 사라짐
  - 구매: 「상처 가르기 함수를 구매했습니다.」, 「If 블록을 구매했습니다.」 (이름은 구매를 요청할 때 적어 둠. 답이 올 때는 산 칸에 새 상품이 올라와 있음)
  - 새로고침: 「새로고침했습니다.」
- 서버가 거부하면 아래 줄에 이유를 빨갛게
- 창이 열린 동안 Player·Attack 액션 맵 비활성화 (`InputBlockingPanel`). HUD는 숨기지 않음 (로비 창과 같음)
- 모양: 메뉴 Invoke → UI → 상점 창 만들기(`ShopUIBuilder`)가 LobbyUI 프리팹 안에 만듦. 다시 누르면 `ShopPanel` 오브젝트는 두고 안쪽만 다시 만듦
- 자리: 메뉴 Invoke → 맵 → 로비 상점 자리 놓기가 Lobby 씬에 `ShopStation`을 만들고 `ShopPanel`을 연결, 안내판을 붙임. 이미 있으면 자리는 그대로

## 다른 시스템이 쓰는 것
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `PlayerProgress.ShopOffers` | - | `IReadOnlyList<ShopOffer>` | 상점 칸 8개 (칸 번호 순서). 상점을 처음 열기 전에는 비어 있을 수 있음. 바뀌면 `Changed` |
| `PlayerProgress.RequestOpenShop()` | - | - | [소유 클라이언트] 상점 창을 열 때. 칸이 없으면 서버가 뽑음 |
| `PlayerProgress.RequestBuyShopItem(int 칸 번호)` | 칸 번호 | - | [소유 클라이언트] 구매 요청 |
| `PlayerProgress.RequestRefreshShop()` | - | - | [소유 클라이언트] 새로고침 요청 |
| `PlayerProgress.ShopAnswered` (event) | `ShopRequest`, `int` 칸 번호, `ShopResult` | - | [표시] 위 요청의 결과. 성공(`Done`)도 알림 |
| `PlayerProgress.PrepareShop()`, `TryBuyShopItem(int)`, `TryRefreshShop()` | | `ShopResult` | [서버] 위 요청을 처리하는 함수. 테스트가 직접 부름 |
| `ShopRules` | - | - | 칸 수, 무게, 희귀도, 가격, 새로고침 비용 |
| `ShopRoller.RollAll`, `RollSlot`, `SyntaxCandidates`, `FunctionCandidates` | 해금 목록(null이면 모두), 가진 개수, 캐릭터 언어 | 칸, 후보 | 뽑기. 네트워크·DB 없이 부를 수 있음 |

- `ShopResult`: `Done`, `NotAllowed`(서버가 아닌 곳에서 부름), `InvalidSlot`, `EmptySlot`, `MaxCount`, `MaterialShort`. 새 값은 맨 끝에만 더함 (네트워크로 보냄)
- `ShopRequest`: `Open`, `Buy`, `Refresh`
- 테스트: `ShopTest` (Play → 호스트로 시작한 뒤 아무 오브젝트에 붙임, 「[ShopTest] 35/35 통과」). 테스트 전용 `ShopTest.db`

## 맞춘 결과
- 이우림이 정함 (11/18): 위 「정한 기획」
- 박진우와 맞출 것
  - 함수의 희귀도(`ShopRules.FunctionRarities`)와 희귀도별 무게·가격(`ShopRules.RarityRules`)
- 김도훈과 맞출 것
  - 잡몹 드랍 후보에 get_bug, is_valid_target 넣기 (`MonsterReward.FillItemPool`은 노드 없는 함수를 처음 개수에만 넣음)
- 임혜원과 맞출 것
  - 로비 상점 문 그림 (`ShopStation`의 SpriteRenderer는 그림 없이 그리기 층만 맞춰 둠)
  - `DoorSignBuilder.MakeSignFor`(안내판 만들기를 상점 자리에도 쓰도록 더함), `KeyBadgeInstaller`의 상점 버튼 세 줄
  - `KeyBadge.Attach`: 한 글자 버튼을 아이콘 버튼(키 표시를 버튼 왼쪽 바깥에)으로 보던 규칙에서 한글 한 글자는 뺌. 상점 구매 확인 창의 「예」가 「예 Enter」로 보이게
