# Account (회원가입·로그인·캐릭터)

- 담당자: 이우림

## 하는 일
- 회원가입·로그인 (#174): 게임 서버(EC2)가 SQLite DB로 계정 확인 (Mirror `NetworkAuthenticator`)
- 닉네임: 계정마다 하나, 회원가입 때 정함. 그 계정의 모든 언어 캐릭터가 같이 씀
- 캐릭터: 계정마다 언어별 하나. 만들기, 들어가기. 들어가기에 성공하면 서버가 로비에 플레이어 생성
- 같은 계정의 중복 접속: 먼저 접속한 쪽을 끊음
- 전송 암호화: `EncryptionTransport`, 서버 키 자동 생성, 클라이언트의 서버 키 지문 확인
- 화면용 묶음 (`AccountFlow`): 화면 상태·안내 문장·버튼 함수를 한 컴포넌트에. 화면은 이것만 사용
- 임시 화면: OnGUI 로그인·회원가입·캐릭터 화면 (`AccountScreen`, `AccountFlow`를 그리기만 함). `NetworkManagerHUD` 대체
- 에디터 전용 로그인 건너뛰기
- 하지 않는 일
  - 진행도 저장: [progress-store.md](progress-store.md). 캐릭터 key(`AccountSession.CharacterKey`) 기준
  - 닉네임 머리 위 표시, 로그아웃·캐릭터 바꾸기, 비밀번호 찾기·변경, 계정 삭제
  - 조작한 요청 막기: 보안 작업 때
  - 캐릭터 언어에 따른 플레이어 프리팹 선택: Java 캐릭터가 생길 때. 지금은 모든 캐릭터가 `Base` NetworkManager의 Player Prefab
  - 꾸민 화면(Canvas): 임혜원 이슈 (「맞춘 결과」)

## 동작 규칙

### 흐름
1. 로그인·회원가입: 클라이언트 → 서버 `AccountAuthRequest { loginId, password, isSignUp, nickname }` (`nickname`은 회원가입만)
   - 접속 전이면 클라이언트가 먼저 접속(`StartClient`)하고, 접속을 마친 뒤 전송
2. 서버: 입력 규칙 검사 → `AccountStore` → 서버 → 클라이언트 `AccountAuthResult { failure, nickname, characters }`
   - 성공: 서버·클라이언트 모두 인증 완료(`ServerAccept`, `ClientAccept`). 회원가입 성공도 로그인으로 처리
   - 실패: 접속 유지. 다시 시도 가능
3. 캐릭터 만들기: 클라이언트 → 서버 `CreateCharacterRequest { language }` → 서버 → 클라이언트 `CreateCharacterResultMessage { failure, characters }`
   - 성공·실패 모두 그 계정의 지금 캐릭터 목록 포함
4. 들어가기: 클라이언트 → 서버 `EnterGameRequest { language }` → 서버 → 클라이언트 `EnterGameResultMessage { failure }`
   - 성공: 서버가 `InvokeNetworkManager.SpawnPlayer(conn)`로 로비에 플레이어 생성 (장소 씬을 다 불러온 뒤)
   - 받을 수 없는 요청 (이미 들어감, 임시 계정의 만들기, 쫓겨난 연결): 결과 `RequestNotAllowed` (무시하면 화면이 결과를 기다리며 멈춤)
- 플레이어 생성 시점: 들어가기 성공 뒤
  - `InvokeNetworkManager.Awake`에서 `autoCreatePlayer`를 끔 (Inspector 값 무시). 접속 직후에는 Ready만
  - `OnServerAddPlayer`: 경고 로그만. 플레이어는 `SpawnPlayer`로만 생성
- 클라이언트가 받는 정보: 계정 닉네임(`AccountAuthResult.nickname`), 캐릭터마다 언어(`CharacterSummary`). 캐릭터 key는 서버에만
- 인증 전 메시지: `AccountAuthRequest`, `AccountAuthResult`, `AccountKickedMessage`만. 캐릭터 메시지는 로그인한 연결만

### 입력 규칙 (`AccountRules`)
| 항목 | 규칙 | 대소문자 |
| --- | --- | --- |
| 아이디 | 영문·숫자, 4~16자 | 구분 없음 (소문자로 저장) |
| 비밀번호 | 8~64자 | 구분 |
| 닉네임 | 한글·영문·숫자, 2~10자, 서버 전체에서 중복 불가. 회원가입 때 검사 | 구분 없음 (입력한 그대로 저장) |
- 검사 위치: 서버. 거부 시 이유(`AccountFailure`)를 결과 메시지로 전송
- 로그인 실패: 규칙 위반도 `WrongLoginOrPassword` (어느 칸이 틀렸는지 알리지 않음)

### 저장 (`AccountStore`, 서버)
- DB: SQLite (`com.gilzoide.sqlite-net` 1.3.3, Windows·Linux native 라이브러리 포함 → EC2에 SQLite 설치 불필요)
- 위치: `Application.persistentDataPath/Accounts.db`
  - 에디터: `C:/Users/<사용자>/AppData/LocalLow/DefaultCompany/Invoke`
  - EC2: 서버를 실행하는 사용자(`ubuntu`)의 `~/.config/unity3d/` 아래. 서버 빌드 폴더 밖 → 새 빌드로 덮어써도 유지
- 테이블
  - `Accounts`: 계정 key(GUID), 아이디, 닉네임, 비밀번호 해시, salt, 해시 반복 횟수, 만든 시각. 아이디 unique, 닉네임 unique
  - `Characters`: 캐릭터 key(GUID), 계정 key, 언어, 만든 시각. (계정 key, 언어) unique
  - 닉네임을 캐릭터에서 계정으로 옮김(2026-11-03): 예전 DB 파일과 구조가 달라 `Accounts.db`를 새로 시작 (에디터는 `Accounts.before-account-nickname.db`로 보관)
- 비밀번호: salt + PBKDF2-SHA256 해시 (`PasswordHasher`). 반복 횟수 10000, 계정마다 저장 (나중에 횟수를 올려도 기존 계정 로그인 유지)
- DB 코드는 `#if UNITY_SERVER || UNITY_EDITOR` 안: 클라이언트 빌드에 없음. 클라이언트 빌드를 서버로 켜면 오류 로그
- DB 코드는 `AccountStore` 한 클래스에 모음 (나중에 다른 DB로 옮길 때 이 클래스만)

### 같은 계정의 중복 접속
- 이미 접속 중인 계정으로 로그인하면: 새 쪽은 로그인 성공. 먼저 접속한 쪽은
  1. 세션에 쫓겨남 표시 (`AccountSession.IsKicked`): 캐릭터 요청은 `RequestNotAllowed`, 다시 로그인은 무시
  2. `AccountKickedMessage { reason = LoggedInElsewhere }`
  3. 플레이어 바로 제거 (`InvokeNetworkManager.DespawnPlayer`): 같은 캐릭터가 둘이 되지 않게
  4. 0.5초 뒤 끊기 (`kickDisconnectDelay`)
- 인증은 풀지 않음: 풀면 그 연결이 보내는 다음 게임 메시지에서 Mirror가 바로 끊어, 끊김 알림이 도착하지 못함
- 끊긴 쪽의 화면: 로그인 화면으로 돌아가 이유 표시

### 로그인 건너뛰기 (에디터)
- `InvokeAuthenticator`의 `Skip Login In Editor` (기본 켜짐)
- 동작: Host 버튼으로 시작하면 로그인·캐릭터 고르기 없이 임시 계정의 C 캐릭터로 들어감
  - 임시 계정: DB 안 씀, 닉네임 `Editor{connId}`, `AccountSession.IsTemporary` true
  - 로그인·회원가입 버튼으로 접속하면 건너뛰지 않음
- 빌드: 무시 (에디터 코드만)
- 로그인 테스트: Play 중에만 끔 (끈 채로 `Base`를 저장하면 Host만으로 들어갈 수 없음)

### 전송 암호화
- 구성: `Base` NetworkManager의 Transport = `EncryptionTransport` (Inner = `KcpTransport`, 포트 7777), `EncryptionKeySetup`이 설정
- 서버 키
  - 위치: `Application.persistentDataPath/server-keys.json` (`EncryptionKeySetup.KeyPath`)
  - 없으면 서버가 켜질 때 새로 만들어 저장. 서버를 켤 때마다 지문을 로그에 남김 (`[EncryptionKeySetup] 서버 키 지문`)
  - 개인키 포함: 레포·빌드에 넣지 않음 (`.gitignore`에 `server-keys.json`)
  - 에디터·팀원 컴퓨터: 각자 다른 키 생성
- 클라이언트의 서버 키 확인 (`Callback`)
  - 접속 주소 `localhost`, `127.0.0.1`, `::1`: 지문 확인 없이 받음 (내 컴퓨터 안의 통신)
  - 그 밖의 주소: `Encryption Transport`의 Trusted Pub Key Signatures 목록에 지문이 있어야 받음. 없으면 오류 로그, 접속 실패
  - Inspector의 Validate Server Public Key: `List` (목록 칸 표시용. 실행할 때 코드가 `Callback`으로 바꿈)
- EC2 서버 지문: `Base`의 지문 목록에 들어 있음
  - EC2 인스턴스 종료(Terminate): 디스크와 함께 키·DB 삭제 → 새 서버의 지문을 목록에 넣고 클라이언트 다시 빌드
  - 중지(Stop)·다시 시작: 키·DB 유지, 지문 그대로
- EC2 주소: 다시 시작할 때마다 퍼블릭 IP 바뀜. 로그인 화면의 서버 주소 칸에 입력 (Elastic IP·DNS 안 씀)

### 서버 빌드·배포
- Linux Server 빌드 프로필: 전역 씬 목록 사용 (Override Global Scenes 끔). 0번 `Base`
  - 예전: `Assets/_UL/Scenes/ServerScene.unity`(#13 테스트 씬)만 빌드되어 EC2에서 `Base`·장소 씬이 실행되지 않았음
- `Base` NetworkManager의 `headlessStartMode`: 서버 빌드가 켜지면 서버 자동 시작
- EC2
  - 서버 폴더: `/home/ubuntu/InvokeServer`
  - systemd 서비스: `invoke-server` (EC2가 켜지면 자동 실행, `-logFile -`)
  - 로그: `journalctl -u invoke-server`
  - 배포: `Builds/LinuxServer`를 `InvokeServer.tar.gz`로 압축 → `scp` → 서비스 멈춤 → 서버 폴더에 압축 풀기 → 실행 권한(`chmod +x`) → 서비스 시작

### 화면 (`AccountFlow`, `AccountScreen`)
- 나눈 방식: 상태와 동작은 `AccountFlow`, 그리기는 화면 (`AccountScreen` 임시본, 나중에 Canvas)
  - 화면은 `AccountFlow.Changed`를 받을 때마다 `AccountFlow`의 값대로 다시 그림. 버튼은 `AccountFlow`의 함수만 부름
  - 화면은 Mirror, `InvokeAuthenticator`, 메시지를 직접 쓰지 않음
- 화면 (`AccountPage`): 로그인 → (링크) 회원가입 → 캐릭터 → 숨김(들어가기 성공 뒤)
  - 로그인·회원가입 사이 이동: 로그인 전에만
  - 서버 주소 칸: 접속 전에만 (`CanEditServerAddress`)
  - 에디터 전용 Host 버튼: `CanStartHost`일 때만
- 캐릭터 화면 (#197)
  - 언어 카드: `Languages` 순서 (C, Java, Python). 카드마다 상태 (`CharacterState`)
    - 보유 중 (`Owned`): 캐릭터를 만들어 둔 언어
    - 미보유 (`NotOwned`): 만들 수 있지만 아직 만들지 않은 언어
    - 준비 중 (`Locked`): 서버가 만들 수 없는 언어 (`CharacterLanguages.Shown`에만 있고 `All`에 없음). 지금은 Java, Python. 카드 누를 수 없음
  - 선택 (`SelectedLanguage`): 언제나 하나. 카드를 누르면 `Select`. 준비 중인 언어는 선택 불가
    - 로그인했을 때: 화면 순서에서 보유한 첫 캐릭터, 없으면 C
    - 응답을 기다리는 동안: 선택 바꾸기 무시
  - 버튼 하나 (`ConfirmLabel`, `CanConfirm`, `Confirm()`): 선택한 언어의 상태에 따라
    - 보유 중: 「들어가기」 → 들어가기 요청
    - 미보유: 「만들기」 → 만들기 요청. 성공하면 같은 언어가 보유 중, 버튼은 「들어가기」
  - 선택한 캐릭터의 컨셉 아트: 화면 전체 배경 (Canvas 화면 몫, `AccountScreen`은 그리지 않음). 준비 중인 언어의 컨셉 아트는 그 언어를 열 때까지 나오지 않음
- 안내 문장 (`Message`, `MessageField`)
  - 서버 결과의 실패 이유: `AccountFailure.ToMessage()`, 강조할 칸은 실패 이유로 정함
  - 회원가입 먼저 검사: 가입하기를 누르면 아이디 → 닉네임 → 비밀번호 규칙(`AccountRules`) → 비밀번호 확인 순서로 검사. 실패하면 보내지 않음. 서버도 다시 검사
  - 비밀번호 확인이 다름: 「비밀번호가 서로 다릅니다.」 (`AccountFlow.PasswordMismatchMessage`, 화면에서만)
  - 로그인 실패: 어느 칸이 틀렸는지 알리지 않음 (`MessageField` = `None`)
- 기다림 (`IsWaiting`): 요청을 보낸 뒤 결과가 올 때까지. 그동안 같은 요청은 다시 보내지 않음. 요청을 보내지 못하면 바로 끝냄
- 접속이 끊김
  - 캐릭터 화면·게임 중: 팝업 (`PopupMessage` = 쫓겨난 이유 또는 「서버와 연결이 끊겼습니다.」), 로그인 화면으로. 확인 → `ClosePopup()`
  - 로그인·회원가입 중 접속 실패: 「서버에 접속하지 못했습니다.」, 서버 주소 칸 강조
  - 끊김 감지: `AccountFlow`가 매 프레임 `NetworkClient.active` 확인 (`NetworkClient.OnDisconnectedEvent`는 NetworkManager가 접속할 때마다 덮어씀)
- 상황별 안내 문장 표: 시안 페이지 「5. 안내 문장」
- 임시 화면 `AccountScreen`: OnGUI. 칸 강조는 이름표를 빨갛게. 예전 `NetworkManagerHUD`의 `Client`, `Server Only` 버튼, 포트 칸 없음

## 다른 시스템이 쓰는 것

### `AccountFlow` (화면)

`Base` 씬 NetworkManager의 컴포넌트. `AccountFlow.Current`로 가져온다. 로그인·회원가입·캐릭터 화면은 이것만 쓴다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Current` | - | `AccountFlow` | `Base`의 `AccountFlow`. 없으면 `null` |
| `Changed` | - | - | event. 아래 값이 바뀔 때마다. 화면은 이때 다시 그림 |
| `Page` | - | `AccountPage` | 지금 화면: `Login`, `SignUp`, `Characters`, `Hidden` |
| `IsVisible` | - | `bool` | 화면을 그려야 하면 true. 들어간 뒤, 서버만 켠 컴퓨터에서는 false |
| `IsWaiting` | - | `bool` | 서버 응답을 기다리는 중. 버튼 막기, 「서버 응답을 기다리는 중...」 표시 |
| `Message` | - | `string` | 화면 아래 실패 이유. 없으면 빈 문자열 |
| `MessageField` | - | `AccountField` | `Message`가 가리키는 칸 (강조할 칸): `None`, `ServerAddress`, `LoginId`, `Nickname`, `Password`, `PasswordConfirm` |
| `Nickname` | - | `string` | 로그인한 계정의 닉네임. 캐릭터 화면에 표시 |
| `Languages` | - | `IReadOnlyList<string>` | 캐릭터 화면의 언어 카드 순서 (`CharacterLanguages.Shown`) |
| `GetCharacterState(string language)` | 언어 | `CharacterState` | 그 카드의 상태: `Owned`(보유 중), `NotOwned`(미보유), `Locked`(준비 중, 카드 누를 수 없음) |
| `SelectedLanguage` | - | `string` | 선택한 언어. 언제나 `Languages` 가운데 준비 중이 아닌 하나. 이 카드를 밝게, 배경에 이 캐릭터의 컨셉 아트 |
| `ConfirmLabel` | - | `string` | 오른쪽 아래 버튼의 글자: 「들어가기」, 「만들기」 |
| `CanConfirm` | - | `bool` | 버튼을 누를 수 있는지. 응답 기다리는 중이면 false |
| `PopupMessage` | - | `string` | 팝업 문장. 팝업이 없으면 `null`. `IsVisible`과 상관없이 표시 |
| `ServerAddress` | - | `string` (읽기·쓰기) | 서버 주소 칸의 값. 쓰면 다음 접속에 사용 |
| `CanEditServerAddress` | - | `bool` | 서버 주소 칸을 보여 줄지 (접속 전에만 true) |
| `CanStartHost` | - | `bool` | 에디터 전용 Host 버튼을 보여 줄지 (빌드에서는 늘 false) |
| `ShowLogin()`, `ShowSignUp()` | - | - | 로그인·회원가입 화면 바꾸기 (링크). 로그인한 뒤에는 무시 |
| `Login(string loginId, string password)` | 아이디, 비밀번호 | - | 로그인 버튼. 접속 전이면 먼저 접속. 성공하면 `Characters` |
| `SignUp(string loginId, string password, string passwordConfirm, string nickname)` | 아이디, 비밀번호, 비밀번호 확인, 닉네임 | - | 가입하기 버튼. 먼저 검사 → 통과하면 전송. 성공하면 바로 로그인, `Characters` |
| `Select(string language)` | 언어 | - | 언어 카드. 그 언어 선택. 준비 중인 언어, 응답 기다리는 중이면 무시 |
| `Confirm()` | - | - | 오른쪽 아래 버튼. 보유 중이면 들어가기(성공하면 `Hidden`, 서버가 로비에 플레이어 생성), 미보유면 만들기 |
| `ClosePopup()` | - | - | 팝업의 확인 버튼 |
| `StartHost()` | - | - | 에디터 전용 Host 버튼. `Skip Login In Editor`가 켜져 있으면 로그인을 건너뜀 |
| `PasswordMismatchMessage` | - | `string` 상수 | 「비밀번호가 서로 다릅니다.」 |
- 입력 중인 글자(아이디, 비밀번호 등)는 화면이 들고 있다가 버튼을 누를 때 넘김
- 입력칸 글자 수 제한: `AccountRules`의 최대 글자 수 (「결과 데이터」)

### `InvokeAuthenticator` (`AccountFlow`가 사용)

`Base` 씬 NetworkManager의 Authenticator. 화면은 직접 쓰지 않고 `AccountFlow`를 쓴다. `AuthenticatorTest`처럼 화면 없이 확인할 때 사용.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Current` | - | `InvokeAuthenticator` | `NetworkManager.singleton`의 Authenticator. 없으면 `null` |
| `RequestLogin(string loginId, string password)` | 아이디, 비밀번호 | `bool` (보냈으면 true) | 로그인 요청. 접속 전이면 먼저 접속. 이미 로그인했으면 경고 로그, 무시. 결과: `AuthResultReceived` |
| `RequestSignUp(string loginId, string password, string nickname)` | 아이디, 비밀번호, 닉네임 | `bool` | 회원가입 요청. 닉네임은 계정의 닉네임. 성공하면 바로 로그인. 그 밖은 `RequestLogin`과 같음 |
| `RequestCreateCharacter(string language)` | 언어(`CharacterLanguages`) | `bool` | 캐릭터 만들기 요청. 로그인 전이면 경고 로그, 무시. 결과: `CreateCharacterResultReceived` |
| `RequestEnter(string language)` | 언어(`CharacterLanguages`) | `bool` | 그 언어의 캐릭터로 들어가기 요청. 로그인 전이면 경고 로그, 무시. 결과: `EnterGameResultReceived` |
| `AuthResultReceived` | `AccountAuthResult` | - | event. 로그인·회원가입 결과. 성공이면 이 이벤트 다음에 인증 완료 |
| `CreateCharacterResultReceived` | `CreateCharacterResultMessage` | - | event. 캐릭터 만들기 결과 |
| `EnterGameResultReceived` | `EnterGameResultMessage` | - | event. 들어가기 결과. 성공이면 이어서 서버가 로비에 플레이어 생성 |
| `Kicked` | `AccountFailure` | - | event. 서버가 곧 끊는다는 알림 (예: `LoggedInElsewhere`). 실제 끊김은 0.5초 뒤 |
- 접속이 끊긴 것: 이벤트 없음. `NetworkClient.active`가 false로 바뀌는 것으로 확인

### 결과 데이터

| 이름 | 내용 | 설명 |
| --- | --- | --- |
| `CharacterSummary` | `language` | 화면에 보여 줄 캐릭터 하나 (만들어 둔 언어) |
| `AccountAuthResult` | `failure`, `nickname`, `characters`, `Success` | 로그인·회원가입 결과. 성공이면 계정 닉네임과 캐릭터 목록 (없으면 빈 배열 또는 `null`) |
| `CreateCharacterResultMessage` | `failure`, `characters`, `Success` | 캐릭터 만들기 결과와 지금 캐릭터 목록 |
| `EnterGameResultMessage` | `failure`, `Success` | 들어가기 결과 |
| `AccountFailure` | `None`, `InvalidLoginId`, `InvalidPassword`, `InvalidNickname`, `LoginIdTaken`, `WrongLoginOrPassword`, `UnknownLanguage`, `LanguageTaken`, `NicknameTaken`, `LoggedInElsewhere`, `CharacterNotFound`, `RequestNotAllowed` | 실패 이유. 새 값은 맨 뒤에만 추가 (메시지로 숫자가 오가므로) |
| `AccountFailure.ToMessage()` | - | 실패 이유의 한국어 문장 |
| `AccountRules` | `LoginIdMinLength`(4), `LoginIdMaxLength`(16), `PasswordMinLength`(8), `PasswordMaxLength`(64), `NicknameMinLength`(2), `NicknameMaxLength`(10) | 입력칸 글자 수 제한에 사용 |
| `AccountRules.CheckLoginId(string)`, `CheckPassword(string)`, `CheckNickname(string)` | 돌려주는 값: `AccountFailure` (통과하면 `None`) | 서버와 같은 규칙. 화면이 보내기 전에 먼저 검사할 때 사용 |
| `CharacterLanguages` | `C`, `Java`, `Python`, `All`, `Shown`, `IsKnown(string)` | 언어 이름 상수. `All`: 서버가 만들 수 있는 언어 (지금은 C 하나). `Shown`: 캐릭터 화면에 보여 줄 언어 (C, Java, Python) |

### `AccountSession` (서버)

로그인한 연결의 `conn.authenticationData`. `conn.authenticationData as AccountSession`으로 가져온다. 값은 `InvokeAuthenticator`·`AccountServer`만 바꾼다.

| 이름 | 돌려주는 값 | 설명 |
| --- | --- | --- |
| `AccountKey` | `string` | 계정 key (GUID) |
| `LoginId` | `string` | 아이디 (소문자) |
| `IsTemporary` | `bool` | 에디터 로그인 건너뛰기의 임시 계정. DB에 없음 → 진행도 저장하지 않음 |
| `IsKicked` | `bool` | 같은 계정이 다른 곳에서 로그인해 곧 끊길 연결. 플레이어는 이미 제거됨 |
| `HasEntered` | `bool` | 들어가기 성공 여부 |
| `CharacterKey` | `string` | 들어간 캐릭터의 key. 들어가기 전에는 `null`. 진행도 저장의 기준 ([progress-store.md](progress-store.md)) |
| `LoadedProgress` | `CharacterProgressData` | 들어가기 때 DB에서 불러온 진행도. 플레이어를 만든 직후 `AccountServer.ApplyProgress`가 넣고 비움. 임시 계정은 `null` (#205) |
| `Nickname` | `string` | 계정의 닉네임. 로그인할 때부터 있음 |

## 맞춘 결과
- 함수 시전 동기화와 확인함 (2026-11-02)
  - 시전 동기화는 `InvokeNetworkManager`를 고치지 않음
  - `FunctionExecutorNetwork`: 들어가기 뒤 생긴 내 캐릭터에서 함수 데이터 전송, 「데이터 준비됨」 알림 → 로딩 화면은 이 신호를 기다림
  - 함수·슬롯·전역 변수: 실제 계정은 서버가 `CharacterKey`로 DB에서 읽어 `FunctionExecutorNetwork`에 넣음 (#205). 임시 계정은 컴퓨터마다 하나인 파일
- 팀원에게 알릴 것
  - `NetworkManagerHUD` 없어짐. 에디터에서는 `Host (에디터)` 버튼으로 지금처럼 바로 들어감
  - EC2 서버 접속: 서버 주소 칸에 EC2 IP, 회원가입 뒤 캐릭터 만들기·들어가기
  - 클라이언트 빌드를 나눠 줄 때: EC2 지문이 들어간 `Base`로 빌드
- 임혜원에게 넘길 것 (별도 이슈)
  - Canvas 로그인·회원가입·캐릭터 화면: `AccountFlow`만 사용 (「다른 시스템이 쓰는 것」). 화면 구성과 상황별 안내 문장은 시안 페이지 (https://claude.ai/artifact/JQn9USgio8xZSojTWKYeMe, 이우림이 공유)
    - 화면이 하는 일: `Changed`를 받으면 `Page`에 맞는 패널 보이기, `Message`·`MessageField`·`IsWaiting`·`PopupMessage`·`Nickname`·언어 카드(`Languages`, `GetCharacterState`, `SelectedLanguage`)·버튼(`ConfirmLabel`, `CanConfirm`) 그리기, 버튼에서 `AccountFlow` 함수 부르기
    - `AccountFlow`가 하는 일 (화면에서 할 필요 없음): 회원가입 먼저 검사, 비밀번호 확인, 끊김 감지와 팝업 문장, 실패 이유 → 강조할 칸
    - 참고 구현: `AccountScreen` (OnGUI)
  - 로딩 화면과 겹침: 로그인한 뒤 캐릭터를 고르는 동안, 그리고 접속을 시도하는 동안 로딩 화면(「Loading...」)이 로그인·캐릭터 화면 뒤에 깔림. 접속 직후 로딩 화면이 로컬 플레이어가 생길 때까지 덮기 때문 (`place-scenes.md`). Canvas 화면의 Sort Order와 로딩 화면을 언제 보일지 함께 정함
  - Canvas 화면으로 바꾼 뒤: 이우림이 `AccountScreen` 삭제
