# 튜토리얼 (TutorialView, TutorialTracker, TutorialGuide, TutorialScript)

- 담당자: 김도훈
- 위치: `Assets/_DH/Scripts/UI/Tutorial/` (HUD 안에 둠, `HudUIBuilder`가 만듦)

## 하는 일
- 처음 접속한 사람에게 HUD 왼쪽 위(장소 이름 아래)에 대사 창을 띄워, 한 쪽씩 해 볼 일을 알려 준다. 해 볼 일을 하면 줄 앞 `[ ]`가 바로 `[x]`로 바뀌고, 그 쪽을 모두 하면 다음 대사로 넘어간다.
- 가야 할 곳을 눈에 보이게 가리킨다 (문, 허수아비, 편집 터미널). 레이드 문도 가리킨다.
- 값을 읽기만 한다. 게임 값(체력, 개수, 재료 등)은 바꾸지 않는다 (코드 규칙 2). 서버에 보내는 것도 없다.
- 하지 않는 일
  - 블록·함수 개수 지급: 개수는 서버(`PlayerProgress`, 이우림)가 가진 값이라 튜토리얼이 주지 않는다. 처음 상태(슬롯 `thrust`, `sword_throw`, 남은 개수 0, `progress-store.md`)로 할 수 있는 일만 둔다
  - 패링: 처음에는 쓸 수 없는 스킬(스킬 트리 해금 필요)이라 넣지 않았다

## 동작 규칙
- 대사와 해 볼 일은 `TutorialScript.Pages`에서 고친다. 한 쪽 = 대사 한 줄 + 해 볼 일 0~3개 + 가리킬 곳.
- 쪽 구성 (시작 연출 + 0~9쪽, 5쪽 뒤에 5-1쪽을 넣고 7쪽은 둘로 나눔)

| 쪽 | 해 볼 일 | 가리키는 곳 |
| --- | --- | --- |
| 시작 `[ . . . . ]  . . . . .` | (1.5초 뒤 저절로 넘어감) | - |
| 0 부팅 | (Enter) | - |
| 1 이동·점프·달리기 | 이동, 점프, 달리기 | - |
| 2 연습장 입장 | 연습장 진입 | 로비의 연습장 문 |
| 3 Q 스킬 | Q 스킬 쓰기 | 연습장 허수아비 |
| 4 메모리 | (Enter) | HUD Memory 막대 테두리가 깜빡임 |
| 5 편집 터미널 | 터미널 열기 | 연습장 편집 터미널 |
| 5-1 스킬 장착 | 장착대 열기 (실제 계정은 장착대가 없어 이 쪽을 건너뜀, #470) | 연습장 장착대 |
| 6 사냥터 | 사냥터 진입, 버그 조각 회수 | 로비면 사냥터 문, 연습장이면 연습장 출구 |
| 7-1 로비로 | 로비로 이동 | 사냥터·연습장 출구 |
| 7-2 업그레이드 | 1번 키, 2번 키 | - |
| 8 레이드 | (Enter) | 로비의 레이드 던전 문 |
| 9 완료 | (3초 뒤 저절로 끝남, Enter로도 끝남) | - |

- 설명만 하는 쪽은 Enter(`GameInput.UI.SubmitPressed`)로 넘긴다. 일이 있는 쪽도 Enter를 누르면 그 쪽을 넘긴다. 큰 창이 열려 있으면 반응하지 않는다.
  - 새 키 액션을 만들지 않으려고 UI 액션 맵의 확인 키를 쓴다 (입력은 `GameInput`을 거친다, 코드 규칙 5)
- 미리 해 둔 일은 기억한다. 다음 쪽에 이미 한 일이 있으면 처음부터 체크되어 있고, 모두 했으면 바로 이어서 넘어간다. 예외: 「로비로 이동」은 그 쪽에 와 있을 때만 센다 (앞에서 로비를 지나왔다고 미리 한 것이 되면 사냥터에서도 넘어가 버림)

| 해 볼 일 | 체크되는 때 | 읽는 곳 |
| --- | --- | --- |
| 이동 | ← →를 누름 | `PlayerInputReader.MoveX` |
| 달리기 | Shift를 누른 채 이동 | `PlayerInputReader.RunHeld` |
| 점프 | Space | `PlayerInputReader.JumpPressed` |
| 연습장·사냥터 진입 | 내 장소가 그 장소가 됨 | `PlayerPlace.Mine` |
| 로비로 이동 | 내 장소가 로비 | `PlayerPlace.Mine` |
| Q 스킬 쓰기 | 서버가 Q(슬롯 0) 시전을 받아 줌 | `FunctionExecutorNetwork.CastAccepted` |
| 편집 창 열기 | 연습장의 편집 창이 열림 | `BlockEditorPanel.IsOpen` (`InputBlockingPanel`) |
| 장착대 열기 | 연습장의 장착 창이 열림 | `SlotEquipPanel.IsOpen` (`InputBlockingPanel`) |
| (실제 계정인지) | 장착 쪽을 건너뜀 | `FunctionExecutorNetwork.UsesServerData` |
| 버그 조각 회수 | 내가 몬스터 보상을 받음 | `MonsterReward.LocalRewardReceived` |
| 1번·2번 키 | 스킬 트리·레벨업 바로가기를 누름 | `HudView.SkillTreeRequested`, `LevelUpRequested` |

### 가야 할 곳 표시 (`TutorialGuide`)
- 내 화면에만 그리는 연출이다. 가리킬 곳(`TutorialGoal`)은 쪽마다 정하고, 실제 오브젝트는 내 장소를 보고 찾는다 (같은 이름의 씬에 있는 것 중 가장 가까운 것, 0.4초마다 다시 찾음).
- 모양: 오브젝트 위에 주황 ▼(11×7픽셀, 월드 2배)가 1픽셀씩 통통 뜨고, 그 위에 이름표(「연습장」, 「사냥터」, 「레이드」, 「허수아비」 등)가 붙는다. 화면 밖이면 화면 가장자리에 그쪽을 가리키는 같은 모양의 화살표와 그 옆에 같은 이름표가 뜬다. 그림과 이름표는 코드로 만든다 (에셋 없음, 내 화면 전용 UI).
  - 마커는 정렬 레이어 `WorldUI`에 그린다 (`Default`는 하늘보다 뒤라 가려짐). HUD 캔버스 밖(씬 맨 위)에 두어 화면 해상도와 관계없이 크기가 같다.

### 대사 창 모양 (`TutorialView`)
- HUD 프리팹(`HudUIBuilder`가 만든 값)을 Play 때 덮어쓴다: 바탕 불투명도 `panelOpacity`(0.6), 글자 크기 `fontSize`(8), 창 너비 `panelWidth`(240). Inspector에서 고친다. 「Enter 넘기기」(`KeyHint`)도 글자 크기 8로 덮어쓴다.
- 글이 길면 띄어쓰기에서 줄을 바꾸고(낱말을 `<nobr>`로 묶음) 창 높이를 글에 맞춘다.
- 폰트 `Galmuri11 TMP`에 `← → ↑ …` 같은 기호가 없다. 대사와 해 볼 일에는 쓰지 않는다 (방향키는 「위 방향키」처럼 말로).
- 찾는 방법: 문 `LobbyEntrance.Target`(목적지), 출구 `PlaceExit`, 허수아비 `Monster.IsInvulnerable`, 편집 터미널 `PanelStation` 중 이름에 `Editor`가 들어간 것, 장착대 `PanelStation` 중 이름에 `Equip`이 들어간 것(씬의 `EditorTerminal`, `EquipStation`). **씬의 두 오브젝트 이름을 바꾸면 여기도 맞춘다.**
- 메모리 쪽(4쪽)에서는 HUD의 Memory 막대 뒤에 깔아 둔 민트 테두리(`TutorialMemoryGlow`, `HudUIBuilder`가 만듦)가 깜빡인다.

### 저장
- 끝내면 저장한다. `PlayerPrefs` 키 `tutorial.done.<닉네임>` (임시 계정은 `temp`). 서버에는 저장하지 않는다.
  - 한계: 다른 컴퓨터에서 접속하면 다시 뜬다 (Enter로 넘김). 언어 캐릭터가 늘면 캐릭터별 저장이 필요한데, 클라이언트는 캐릭터 key를 몰라 닉네임(계정)별이다
  - 진행 중인 쪽은 저장하지 않는다. 접속하면 처음부터
- 내 캐릭터가 있고 레이드 던전이 아닐 때만 보인다. HUD 안에 있어서 창이 열리면 HUD와 함께 숨는다.
- 시험용: Play 중 `TutorialView`의 점 세 개 메뉴 「튜토리얼 다시 보기」

## 다른 시스템이 쓰는 것
- 없다. 다른 시스템은 튜토리얼을 부르지 않는다. 튜토리얼이 위 표의 값을 읽는다.

## 맞춘 결과
- 임혜원: `HudUIBuilder`에 `BuildTutorial`과 Memory 테두리를 추가했다 (경험치 바 때와 같은 방식, HUD 프리팹은 메뉴로 다시 만듦). `LobbyEntrance`에 읽기 전용 `Target` 한 줄을 추가했다. `HudView`는 그대로
