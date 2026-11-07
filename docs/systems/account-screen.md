# 접속 화면 (AccountView)

- 담당자: 임혜원

## 하는 일
- 로그인·회원가입·캐릭터 선택 화면과 접속 끊김 팝업을 우림이 「로그인 화면 시안」(1~5, 14)대로 그린다 (UGUI Canvas).
- 그리기만 한다. 화면 상태·안내 문장·버튼 동작은 모두 이우림의 `AccountFlow` (account.md 「화면」). Mirror·메시지는 직접 쓰지 않는다.
- 하지 않는 일: 로그인·회원가입 검사, 캐릭터 만들기·들어가기 요청 (`AccountFlow`가 함)

## 동작 규칙
- 프리팹 `Assets/_HW/UI/Account/AccountCanvas`를 **Base 씬**에 둔다. 정렬 순서 110: 로딩 화면(`PlaceTravel`, 100)보다 위. 접속한 뒤 로그인·캐릭터 화면을 보는 동안 로딩 화면이 뒤에서 덮고 있어서 (place-scenes.md 「접속 직후」). 팝업도 게임 중 맨 위
  - 메뉴 Invoke → UI → 접속 화면 만들기(`AccountUIBuilder`, 에디터 전용)가 만든다. 여러 번 눌러도 된다
  - 이 화면을 쓰면 임시 OnGUI 화면(`AccountScreen`, Base NetworkManager)은 체크를 끈다
- `AccountFlow.Changed`마다 다시 그림
  - `IsVisible`: 배경과 세 화면을 보일지. 들어간 뒤에는 숨김
  - `Page`: 로그인 / 회원가입 / 캐릭터
  - `PopupMessage`가 있으면 팝업 (게임 중에도)
- 안내 문장 (시안 5)
  - A 빨간 문장: `Message` (확인 버튼 아래)
  - B 칸 강조: `MessageField`가 가리키는 입력칸 테두리를 빨갛게 (`acc_field_error`)
  - C 회색 안내: `IsWaiting`이면 「서버 응답을 기다리는 중...」, 버튼 막기
  - D 팝업: `PopupMessage` + 확인(`ClosePopup`)
- 입력: 입력칸이 글자를 들고 있다가 버튼을 누를 때 `Login`, `SignUp`에 넘김. 서버 주소 칸만 `ServerAddress`에 바로 씀 (로그인·회원가입 칸이 같은 값)
- 키: 입력칸에서 Enter = 그 화면의 확인 버튼. 입력칸 밖에서는 `UIShortcut`(#298): 로그인 Enter = 로그인, 회원가입 Enter = 가입하기 / ESC = 로그인으로, 캐릭터 Enter = 만들기·들어가기, 팝업 Enter·ESC = 확인
- 캐릭터 화면
  - 언어 카드 3장 = `Languages` 순서 (C, Java, Python). 준비 중이면 누를 수 없음. 고른 카드는 청록 테두리
  - 오른쪽 위: 「> 캐릭터 선택 · 닉네임」, 아이콘, 보유 중(청록)/미보유(주황), 소개 (임시 문장)
  - 오른쪽 아래: 「이 캐릭터로 게임에 들어갑니다」/「C 캐릭터를 만듭니다」 + `ConfirmLabel` 버튼
  - 컨셉 아트 자리: 지금은 큰 글자 「C」. 그림이 생기면 바꾼다
- 비밀번호 칸 오른쪽 눈: 누르면 글자를 보이거나 가림. 처음과 로그인한 뒤에는 가림
- 로그인한 뒤에는 비밀번호 칸을 비운다
- 로컬 시험: 서버 주소 `localhost`로 로그인·회원가입하려면 서버가 켜져 있어야 한다. Play 중에만 NetworkManager의 `Invoke Authenticator` → `Skip Login In Editor`를 끄고 Host 버튼 (account.md 「로그인 건너뛰기」)

## 다른 시스템이 쓰는 것
- 없음

## 맞춘 결과
- 이우림과 정한 것: 꾸민 화면(Canvas)은 임혜원 이슈 (account.md). 시안은 우림이 「Invoke 로그인 화면 시안」 아티팩트
