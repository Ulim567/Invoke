# Raid (레이드)

- 담당자: 김도훈

## 하는 일
- 레이드 방 목록, 방 만들기(이름·비밀번호), 파티방(정비: 파티원 목록, 준비 상태, 시작)과 레이드 시작 알림
  - 데이터: `RaidRooms` (방·파티원 값은 여기서만 바꾼다)
  - 화면: `RaidLobbyUI`, `RaidRoomRowUI`, `RaidMemberRowUI`
  - 시작·끝 접점: `RaidFlow`
- 방 목록의 원본은 서버에 있다 (#242). 클라이언트는 서버가 보낸 방 목록으로 화면을 그리고, 방을 바꾸는 일은 서버에 요청한다
- 앞으로 이 문서에 추가할 것: 레이드 던전에 보스 배치, 클리어·실패 판정과 "레이드 끝"
- 하지 않는 일
  - 레이드 던전 씬을 파티마다 불러오고 내리기, 파티원 옮기기: 이우림 (`place-scenes.md`)
  - 네트워크(요청 보내기, 방 목록 보내기), `RaidRooms`의 요청 함수 안쪽: 이우림
  - 로비 레이드 입구에서 ↑를 눌렀을 때 이 화면 열기: 이우림 (`RaidLobbyOpener`가 `LobbyEntrance.RaidEntryRequested`를 받음, `lobby-places.md`)
  - 레이드 던전 맵: 임혜원

## 동작 규칙
- 흐름: 로비 레이드 입구에서 ↑ → 방 목록 → (방 만들기 또는 입장) → 파티방 → 파티장이 시작 → 서버에서 `RaidFlow.RequestStart(파티원 이름 목록)`
- 방
  - 이름은 앞뒤 공백을 빼고 비어 있으면 안 된다. 비밀번호가 비어 있으면 공개 방
  - 최대 3명(`RaidRooms.MaxMembers`). 한 사람은 한 방에만 있을 수 있다. 파티원은 이름으로 구분한다
  - 파티원 이름: 계정 닉네임 (서버 `AccountSession.Nickname`, 겹치지 않음)
  - 0번이 파티장(방을 만든 사람). 파티장이 나가면 다음 사람이 파티장이 되고 준비가 풀린다. 아무도 없으면 방이 없어진다
- 준비와 시작
  - 파티원은 「준비」 / 「준비 취소」를 누른다. 파티장은 준비가 없다
  - 「시작」은 파티장에게만 보이고, 다른 파티원이 모두 준비하면 눌린다 (혼자여도 시작 가능)
  - 시작하면 방이 목록에서 없어지고, 그 방 파티원 모두의 화면이 닫힌다
- 입장: 꽉 찬 방은 「입장」을 누르면 "인원이 다 찼습니다"를 알린다 (인원 글자가 빨갛게 보임). 비밀번호 방은 「입장」을 누르면 비밀번호 창이 뜨고, 공개 방은 바로 들어간다
- 화면
  - 열린 동안 내 캐릭터의 Player·Attack 액션 맵 비활성화 (`InputBlockingPanel`, `lobby-places.md`와 같은 방식)
  - 화면을 닫으면 방에서 나감. 그래서 방에 들어간 채로 다른 장소에 갈 수 없다
- 실패하면 이유를 안내 글자에 보여 준다 (방 이름 없음, 인원 참, 비밀번호 틀림, 이미 다른 방에 있음, 파티장 아님, 준비 안 됨)
- 네트워크 (#242)
  - 서버: 방 목록의 원본. 요청 검사는 `RaidRooms.TryCreate` 등 서버용 함수가 한다
  - 방 목록 보내기: 방 목록이 바뀔 때마다 서버 → 접속한 모든 클라이언트에 전체 목록. 게임에 들어올 때 한 번
    - 보내는 값: 방마다 `Id`, `Name`, `HasPassword`, 파티원(`Name`, `IsReady`). 비밀번호는 보내지 않음
    - 클라이언트: 받은 목록을 자기 `RaidRooms`에 넣고 `RoomsChanged`, `RoomChanged`를 부름
  - 요청: 클라이언트 → 서버. 이름은 담지 않음 (서버가 요청이 온 연결의 닉네임으로 앎)
  - 결과
    - 성공: 서버가 새 방 목록을 모두에게 보냄. 화면은 받은 목록으로 판단 (`FindRoomOf(MyName)`이 생기면 파티방. 없어지면, 내가 「나가기」를 눌렀으면 방 목록, 아니면(시작) 닫기)
    - 실패: 서버 → 요청한 클라이언트에만 이유. `RequestFailed`
  - 접속이 끊기면 서버가 그 플레이어를 방에서 뺌
- 서버에 접속하지 않았을 때: `Request…` 함수가 내 컴퓨터 안의 `RaidRooms`에서 바로 처리 (#227의 임시 버전과 같음). `MyName`은 `"나"`
- 임시 테스트 버튼: 가짜 방 만들기(방장봇, 짝수 번째는 비밀번호 1234, 3의 배수 번째는 꽉 찬 방), 가짜 파티원 넣기, 가짜 파티원 준비 바꾸기
  - 서버에 접속하지 않았을 때만 동작. 접속했을 때는 숨김 (클라이언트에서 `RaidRooms`를 직접 바꾸므로 서버를 거치지 않음)
- 테스트 씬: `Assets/_DH/RaidRoomTest.unity` (Canvas + EventSystem + `RaidLobbyUI`, Open On Start)

## 다른 시스템이 쓰는 것

### `RaidFlow`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `RequestStart(IReadOnlyList<string>)` | 파티원 이름 목록 (0번 파티장) | - | 레이드 시작 함수. 서버에서 `RaidRooms.TryStart`가 부른다 |
| `StartRequested` (static event) | `IReadOnlyList<string>` | - | [로직] 레이드 시작 요청. 서버에서만 불린다. 받는 쪽(이우림)이 던전을 불러오고 파티원을 옮긴다. 지금은 받는 쪽이 없어서 로그만 남는다 |

### `RaidRooms`

화면(클라이언트)이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Rooms` | - | `IReadOnlyList<RaidRoom>` | 지금 있는 방 목록 (서버가 보낸 목록) |
| `MyName` | - | `string` | 내 계정 닉네임. 서버에 접속하지 않았으면 `"나"` |
| `FindRoomOf(memberName)` | 이름 | `RaidRoom` | 그 사람이 있는 방, 없으면 `null` |
| `RequestCreate(name, password)` | 방 이름, 비밀번호 | - | 방 만들기 요청 |
| `RequestJoin(room, password)` | 방, 비밀번호 | - | 방 들어가기 요청 |
| `RequestSetReady(ready)` | 준비 여부 | - | 준비 바꾸기 요청 (파티장은 무시) |
| `RequestLeave()` | - | - | 방 나가기 요청. 방에 없으면 무시 |
| `RequestStart()` | - | - | 레이드 시작 요청 (파티장만, 모두 준비했을 때) |
| `RoomsChanged` (static event) | - | - | 방이 생기거나 없어지거나 인원이 바뀜 |
| `RoomChanged` (static event) | `RaidRoom` | - | 한 방의 파티원·준비가 바뀜 |
| `RequestFailed` (static event) | `string` 이유 | - | 서버가 내 요청을 거절함. 화면에 보여 줄 문장 |

- 요청 함수는 결과를 바로 돌려주지 않는다. 성공은 `RoomsChanged`·`RoomChanged`로, 실패는 `RequestFailed`로 온다

서버 코드(이우림)가 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TryCreate(name, password, leaderName, out room, out reason)` | 방 이름, 비밀번호, 파티장 이름 | `bool` | 방 만들기 |
| `TryJoin(room, password, memberName, out reason)` | 방, 비밀번호, 이름 | `bool` | 방 들어가기 |
| `Leave(room, memberName)` | 방, 이름 | - | 방 나가기 |
| `SetReady(room, memberName, ready)` | 방, 이름, 준비 여부 | - | 준비 바꾸기 (파티장은 무시) |
| `TryStart(room, requesterName, out reason)` | 방, 누른 사람 이름 | `bool` | 레이드 시작 (파티장만, 모두 준비했을 때) |

`RaidRoom`: `Id`, `Name`, `HasPassword`, `Members`(`RaidMember`: `Name`, `IsReady`), `Leader`, `IsFull`, `AllReady`. 모두 읽기만 한다.

### `RaidLobbyUI`

`InputBlockingPanel`을 물려받는다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `OpenFor(PlayerInputReader)` | ↑를 누른 내 캐릭터 | - | 레이드 화면 열기 (방 목록). 그 캐릭터의 Player·Attack 액션 맵 비활성화. `RaidLobbyOpener`가 부른다 |
| `Close()` | - | - | 화면 닫기. 방에 있으면 방에서 나감 (`RequestLeave`). 액션 맵 다시 활성화 |

## 맞춘 결과
- 이우림과 정한 것 (11/4)
  - 레이드는 방 파기 → 정비하는 파티방(로스트아크처럼) → 레이드 시작 흐름이다
  - 김도훈이 1~4를 멀티 없이 임시로 만들고, 네트워크는 이우림이 나중에 붙인다
    1. 레이드 방 만들기 UI (방 이름·비번, 방 목록)
    2. 파티방(정비) UI (파티원 목록, 준비 상태, 시작 버튼 → "레이드 시작 함수"에 파티원 목록을 넘김)
    3. 레이드 씬 안의 내용 (맵, 보스·몬스터 배치, 보스 패턴·페이즈)
    4. 클리어·실패 판정 (끝나면 "레이드 끝 함수"를 부름)
  - "레이드 시작 함수"는 `RaidFlow.RequestStart`, 받는 쪽은 `RaidFlow.StartRequested`
- 네트워크 붙이기 (#242, 이우림 제안, 김도훈 확인 전)
  - 범위: 방까지. 시작하면 서버에서 `RaidFlow.StartRequested`까지. 파티 던전 열기·옮기기는 다음 이슈
  - 파티장 내보내기: 이번에 넣지 않음
  - 김도훈에게 요청
    - `RaidLobbyUI`: `InputBlockingPanel` 물려받기. 네 창(방 목록, 파티방, 방 만들기 창, 비밀번호 창)을 한 부모 오브젝트 아래에 둠. 열기는 `OpenFor(player)`, 지금의 `Open()`·`Close()`는 지움
    - `Close()`(닫기 버튼 포함): 방에 있으면 `RaidRooms.RequestLeave()`
    - `TryCreate`·`TryJoin`·`SetReady`·`TryStart`·`Leave` 호출 → `Request…` 함수로 바꿈
    - 화면 전환: `RoomsChanged`·`RoomChanged`를 받으면 `FindRoomOf(MyName)`으로 판단. 내 방이 생기면 파티방, 「나가기」를 누르지 않았는데 내 방이 목록에서 사라지면(시작) 화면 닫기
    - `RequestFailed`를 받으면 안내 글자에 이유
    - `localName`(`"나"`) → `RaidRooms.MyName`
    - 테스트 버튼: 서버에 접속했을 때(`NetworkClient.isConnected`) 숨기기
