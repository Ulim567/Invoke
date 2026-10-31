# FunctionExecutor 개편 후보

- 담당자: 이우림
- 대상: `Assets/_UL/Scripts/FunctionExecutor/`
- 진행: #90 merge 뒤 refactor 이슈

## `Block`
- 상태 필드: `started`, `ended`, `cancelled` 세 `bool` → enum `BlockState` 하나
  - 값: `NotStarted`, `Running`, `Ended`, `Cancelled`
- 메모리: `AllocateMemory`, `ReleaseMemory`를 `Block`에서 빼고 `FunctionBlock`으로

## `FunctionTask`
- 작업 상태와 실행 문맥으로 나누기
  - `FunctionTask`(작업 상태): `Name`, `Caster`, `State`, `EndReason`, `IsEnded`, `IsFailed`, `Failure`, `Error`, `Step`, `Interrupt`, `End`
  - 실행 문맥(새 클래스, 블록이 받는 파라미터): `Caster`, `Memory`, 언어 규칙, 비동기 시전, 지역 변수 목록, `RecordFailure`
  - 이유: Mirror에서 서버만 실행하고 화면에는 작업 상태만 보낼 때 나누기 쉬움. 블록만 따로 테스트하기 쉬움
  - 영향: 모든 블록과 스킬의 함수 모양(`OnStart(FunctionTask task)` 등)이 바뀜. 박진우에게 알림
  - 함께 고칠 것: `Block` 개편과 같은 이슈에서
  - 블록 함수의 파라미터: 빼지 않음. 타입만 `FunctionTask`에서 실행 문맥으로
  - 비동기 시전: 실행 문맥의 함수로. 블록이 `task.Executor`에 직접 닿지 않음
- `Executor` 참조: `FunctionTask`에서 뺌. 실행 문맥만 가짐(블록에는 실행기 자체를 보여 주지 않음)
  - 실패 알림: `FunctionTask.Step`이 결과를 돌려주고, `FunctionExecutor`가 그 결과가 실패면 직접 처리
  - 이유: 실행기 → 작업 한 방향으로만 부름. 작업이 실행기를 거꾸로 부르지 않음

## `PredefinedFunction`
- heap 함수(`Malloc`, `Free` 등)
  - 규칙의 실제 코드(포인터 주인, 잘못된 포인터일 때의 에러): 실행 문맥으로. 플레이어의 `free` 블록(`FreeBlock`)과 같은 코드를 씀
  - `PredefinedFunction`에 남는 것: 스킬 작성자가 부르는 입구, `OnCancel` 중이면 막기, 실패면 `Fail`
  - 스킬 작성자가 쓰는 이름과 방법: 그대로 (파라미터 타입만 실행 문맥으로)

## `FunctionExecutor`
- 쿨타임 분리: `cooldownEndTicks`, `StartCooldown`, `GetRemainingCooldownTicks`, 실행 중 확인을 쿨타임 클래스로
  - `CooldownCondition`: `FunctionExecutor` 대신 쿨타임 클래스를 봄
- 시전 조건: 클래스 구조(`CastCondition`) 유지
  - 이유: 조건이 늘어날 수 있음
- 언어 규칙: `languageRule` 칸과 `GetFailureCategory`를 캐릭터의 언어 컴포넌트로
  - 이유: 편집 화면의 compile error 검사가 작업 없이 언어 규칙을 봄

## `FunctionFailure`
- 성격별로 나누기
  - 시전 거부: `OnCooldown`, `NotEnoughMemory`를 시전 거부 이유 enum으로 따로
  - 스킬 작성자의 실수: `MissingReturnValue`를 표에서 빼고, 콘솔 오류와 함께 작업 끝내기
  - 플레이어의 잘못된 포인터: 플레이어 코드 에러 `InvalidPointer`를 새로 두고, `FreeBlock`이 `SkillFailed` 대신 이것으로 실패
  - 언어 규칙 표에 남는 것: 플레이어 코드 에러와 `SkillFailed`
- 이유: 언어마다 달라지는 것(플레이어 코드 에러의 처리와 분류)만 표에 남김
- 영향: `CastRejected` 이벤트의 모양이 바뀜. 임혜원에게 알림
- 주의: enum 중간의 값을 빼면 뒤 값들의 번호가 당겨짐. `LanguageRule_C.asset`의 표를 Inspector에서 다시 채움

## 스킬 호출 블록
- `PredefinedCall`, `CallPredefinedBlock`, `PredefinedValueBlock` → `CallBlock : ValueBlock` 하나로 합치기
  - `ValueSlots`: 인자 칸. `AssignBlock`과 같은 모양
  - 반환 값: 반환 타입이 `Void`가 아니면 스킬이 끝난 뒤 `Result`에 넣음. 몸통에 놓이면 `Result`를 읽지 않아 버려짐
  - `FunctionDataConverter`: 몸통의 호출과 값 칸의 호출을 모두 `CallBlock`으로
- 이유: C에서 두 호출은 같은 함수 호출이고, 반환 값을 쓰는지만 다름
- 바뀌는 동작: `Return`을 빠뜨린 스킬을 몸통에서 불러도 `MissingReturnValue`로 실패
  - 반환 타입으로 맞출 수 없는 값을 넘긴 스킬: `TypeMismatch`(C에서 `CompileError`) 대신 `MissingReturnValue`. 몸통에서 불러도 같음
  - 이유: 반환 값은 스킬 작성자(개발자)가 정하므로 플레이어의 compile error로 보이지 않게 함
- 클래스 이름: `PredefinedCallBlock`. 사용자 정의 함수 호출과 합칠지는 블록 실행 3 이슈에서
- 비동기 스킬: 지금처럼 `FunctionDataConverter`가 값 칸에 넣지 않음
