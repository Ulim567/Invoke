# Invoke

코딩 언어가 되어 버그를 잡는 2D 실시간 액션 MMORPG

4조: 이우림(UL) · 박진우(JW) · 임혜원(HW) · 김도훈(DH)

---

## 개발 환경

| 항목 | 내용 |
|---|---|
| Unity | **6000.6.0f1** (팀원 모두 같은 버전을 씁니다) |
| 렌더 파이프라인 | URP 2D (Universal 2D 템플릿) |
| Git | Git LFS 필요 (Windows는 Git for Windows에 기본 포함, Mac은 따로 설치) |

- 에디터 버전이 다르면 Unity가 프로젝트 파일을 바꿔서 커밋마다 충돌이 생깁니다. 반드시 같은 버전을 씁니다.

### 처음 받는 방법

0. **Mac은 clone하기 전에 Git LFS를 설치합니다.** 이미지, 사운드, 폰트를 LFS로 관리하므로, LFS 없이 clone하면 이 파일들이 깨진 채로 받아집니다.
   ```bash
   brew install git-lfs
   git lfs install
   ```
1. 저장소를 clone합니다.
   ```bash
   git clone https://github.com/Ulim567/Invoke.git
   ```
2. 받은 폴더로 들어가서 dev 브랜치로 이동합니다. 모든 작업은 dev에서 시작합니다.
   ```bash
   cd Invoke
   git switch dev
   ```
3. Unity Hub에서 **Add → Add project from disk**로 clone한 폴더를 엽니다.
   - 처음 열 때는 Unity가 `Library` 폴더를 만드느라 시간이 걸립니다.

---

## 작업 순서

작업 하나를 시작해서 끝낼 때까지의 순서입니다. 각 규칙의 자세한 내용은 아래 절에 있습니다.

1. **이슈 만들기**: GitHub의 **Issues → New issue**에서 템플릿을 골라 이슈를 만들고, 이슈 번호를 확인합니다.
2. **dev를 최신으로 맞추기**
   ```bash
   git switch dev
   git pull
   ```
3. **작업 브랜치 만들기**: 이름은 `이슈라벨/#이슈번호`입니다.
   ```bash
   git switch -c feature/#12
   ```
4. **Unity에서 작업하고 커밋하기**
   ```bash
   git add -A
   git status
   git commit -m "[Feat/#12]: Add stack frame memory calculation"
   ```
   - `git status`로 올라갈 파일을 확인합니다. `Library/`, `Temp/`가 보이면 커밋하지 않습니다.
5. **push하기**: 처음 push할 때만 `-u origin 브랜치이름`을 붙입니다. 그다음부터는 `git push`만 하면 됩니다.
   ```bash
   git push -u origin feature/#12
   ```
6. **PR 열고 병합하기**: GitHub에서 PR을 엽니다.
   - 받는 쪽 브랜치(base)가 **dev**인지 확인합니다.
   - 제목을 커밋 규칙 형식으로 쓰고, 템플릿 항목을 채웁니다.
   - **Squash and merge**로 병합합니다. GitHub가 작업 브랜치를 자동으로 지웁니다.
7. **정리하기**: dev로 돌아와 최신 내용을 받고, 내 컴퓨터의 작업 브랜치를 지웁니다.
   ```bash
   git switch dev
   git pull
   git branch -D feature/#12
   ```
   - 소문자 `-d`가 아니라 대문자 `-D`를 씁니다. Squash로 병합하면 Git이 이 브랜치가 병합되지 않았다고 판단해서 `-d`로는 지워지지 않기 때문입니다.

### 자주 헷갈리는 것

- **GitHub에 새로 생긴 브랜치가 `git branch`에 안 보입니다.**
  - `git branch`는 내 컴퓨터의 브랜치만 보여 줍니다. `git fetch`로 GitHub의 브랜치 목록을 받아 온 뒤, `git branch -a`로 보면 `remotes/origin/브랜치이름`으로 보입니다.
  - 그 브랜치로 `git switch 브랜치이름`을 하면 내 컴퓨터에도 같은 브랜치가 만들어집니다.
- **브랜치를 바꾸기 전에는 Unity에서 씬을 저장합니다.**
  - 저장하지 않은 씬이 있는 채로 브랜치를 바꾸면, Unity가 바뀐 파일을 다시 읽으면서 작업 내용이 사라질 수 있습니다.

---

## 폴더 규칙

```
Assets/
├─ _DH/   김도훈 개인 작업 폴더
├─ _HW/   임혜원 개인 작업 폴더
├─ _JW/   박진우 개인 작업 폴더
├─ _UL/   이우림 개인 작업 폴더
├─ Scenes/
└─ Settings/
```

- 실험용 씬, 스크립트, 프리팹은 **자기 개인 폴더**에서 만듭니다. 다른 사람의 개인 폴더는 고치지 않습니다.
- `Assets` 안의 파일과 폴더 이름은 **영어로만** 짓습니다.
  - Mac과 Windows는 한글 파일 이름을 서로 다른 방식으로 저장해서, 같은 파일이 두 개로 보이거나 지워졌다 다시 생긴 것처럼 보일 수 있습니다.
- 완성된 파일을 공용 폴더로 옮길 때는 **Unity Project 창 안에서** 옮깁니다.
  - Windows 탐색기에서 옮기면 `.meta` 파일이 따로 남아 씬과 프리팹의 연결이 끊어집니다.
- 새 파일을 커밋할 때는 **`.meta` 파일도 함께** 커밋합니다.
- 빈 폴더는 Git에 올라가지 않으므로, 각 개인 폴더에 `DeleteThis.txt`를 넣어 두었습니다.
  - 자기 폴더에 다른 파일이 생기면 `DeleteThis.txt`는 **Unity Project 창에서** 지웁니다. 그래야 `.meta` 파일도 함께 지워집니다.

---

## 브랜치 규칙

```
main  ← 발표·제출용
 └─ dev  ← 팀 작업을 모으는 곳
     ├─ feature/#12
     ├─ bug/#15
     └─ design/#20
```

- **main과 dev에는 직접 push할 수 없습니다.** 두 브랜치는 PR로만 바뀝니다.
- 작업할 때는 **dev에서 새 브랜치**를 만듭니다.
- 브랜치 이름은 **`이슈라벨/#이슈번호`** 입니다.
  - 예: `feature/#12`, `bug/#15`
  - 라벨: `feature`, `enhancement`, `bug`, `refactor`, `chore`, `design`
- push는 **자기가 만든 작업 브랜치에만** 합니다.
- 이슈 하나에 브랜치 하나를 씁니다. PR이 병합되면 브랜치는 자동으로 지워집니다.
- **병합된 브랜치에서 이어서 작업하지 않습니다.** 추가 작업이 필요하면 dev에서 새 브랜치를 만듭니다.
  - dev에는 Squash로 병합되므로, 병합된 브랜치에서 다시 PR을 열면 충돌이 생깁니다.

---

## 이슈 규칙

모든 작업은 이슈를 먼저 만들고 시작합니다. 이슈는 아래 6가지 템플릿 중 하나로 만듭니다.

| 종류 | 언제 쓰는가 | 예시 |
|---|---|---|
| Feature | 게임에 없던 기능이나 시스템을 새로 만들 때 | 연습장 함수 시전 |
| Enhancement | 이미 있는 기능을 더 좋게 바꿀 때 | 블록을 끌 때 놓을 위치 강조 |
| Bug | 의도와 다르게 동작하는 것을 발견했을 때 | 피격 후 메모리 게이지 초기화 |
| Refactor | 동작은 그대로 두고 코드 구조만 바꿀 때 | 메모리 계산 코드를 별도 클래스로 분리 |
| Chore | 설정, 패키지, 폴더, 문서, 빌드처럼 게임 내용이 아닌 작업 | Cinemachine 추가, README 수정 |
| Design | 애니메이션, 맵, 그림, 소리 같은 게임 콘텐츠를 만들 때 | C 캐릭터 걷기 애니메이션, 첫 사냥터 맵 |

- **Enhancement와 Refactor 구분**: 플레이어가 차이를 느끼면 Enhancement, 코드만 바뀌면 Refactor입니다.
- **Enhancement와 Bug 구분**: 기획대로 동작하지만 아쉬우면 Enhancement, 기획대로 동작하지 않으면 Bug입니다.

---

## 커밋 규칙

```
[타입/#이슈번호]: 영어 한 줄 요약
```

- 예: `[Feat/#12]: Add stack frame memory calculation`

| 이슈 라벨 | 커밋 타입 |
|---|---|
| feature | `Feat` |
| enhancement | `Enhance` |
| bug | `Bug` |
| refactor | `Refactor` |
| chore | `Chore` |
| design | `Design` |

- 커밋 하나에는 **한 가지 변경만** 담습니다.

---

## PR 규칙

### 작업 브랜치 → dev

1. 작업이 끝나면 dev로 PR을 엽니다. PR 템플릿의 항목을 채웁니다.
2. **PR 제목은 커밋 규칙 형식**으로 씁니다.
   - 예: `[Feat/#12]: Add stack frame memory calculation`
   - Squash로 병합하면 PR 제목이 dev에 남는 커밋 메시지가 되기 때문입니다.
3. 승인 없이 **작성자가 직접 병합**합니다. 병합 방식은 **Squash and merge**입니다.
   - 병합하기 전에 PR 템플릿의 체크리스트를 작성자가 스스로 확인합니다.

### dev → main

- 알파(12주차), 베타(16주차), 발표 직전처럼 **정해진 시점에만** 병합합니다.
- **다른 팀원 1명이 승인**해야 병합할 수 있습니다.
  - 작업 브랜치에서 main으로 PR을 잘못 열어도, 승인 없이는 병합되지 않게 막기 위해서입니다.
  - PR을 열 때 받는 쪽 브랜치가 **dev**인지 꼭 확인합니다.
- 병합 방식은 **Merge commit**입니다.
  - Squash로 병합하면 main과 dev의 기록이 달라져서, 다음번 dev → main 병합 때 충돌이 생깁니다.
