# FunctionExecutor 개편 후보

- 담당자: 이우림
- 대상: `Assets/_UL/Scripts/FunctionExecutor/`
- 진행: #118에서 구현 완료

## `Block`
- 상태 필드: `started`, `ended`, `cancelled` 세 `bool` → enum `BlockState` 하나
  - 값: `NotStarted`, `Running`, `Ended`, `Cancelled`
- 메모리: `AllocateMemory`, `ReleaseMemory`를 `Block`에서 빼고 `FunctionBlock`으로

## `FunctionTask`
- 작업 상태와 실행 문맥으로 나누기
  - `FunctionTask`(작업 상태): `Name`, `Caster`, `State`, `EndReason`, `IsEnded`, `IsFailed`, `Failure`, `Error`, `Step`, `Interrupt`, `End`
  - 실행 문맥(새 클래스 `FunctionContext`, 블록이 받는 파라미터): `Caster`, `Memory`, 언어 규칙, 비동기 시전, 지역 변수 목록, `RecordFailure`
  - 이유: Mirror에서 서버만 실행하고 화면에는 작업 상태만 보낼 때 나누기 쉬움. 블록만 따로 테스트하기 쉬움
  - 영향: 모든 블록과 스킬의 함수 모양(`OnStart(FunctionTask task)` 등)이 바뀜. 박진우에게 알림
  - 함께 고칠 것: `Block` 개편과 같은 이슈에서
  - 블록 함수의 파라미터: 빼지 않음. 타입만 `FunctionTask`에서 실행 문맥으로
  - 비동기 시전: 실행 문맥의 함수로. 블록이 `task.Executor`에 직접 닿지 않음
- `Executor` 참조: `FunctionTask`에서 뺌. 실행 문맥만 가짐(블록에는 실행기 자체를 보여 주지 않음)
  - 실패 알림: `FunctionTask.Step`이 결과를 돌려주고, `FunctionExecutor`가 그 결과가 실패면 직접 처리
  - 비동기 시전: 실행 문맥도 실행기를 모름. 블록은 실행 문맥에 요청만 기록(`RequestAsyncCast`), 실행기가 `Step` 다음에 꺼내 시전
    - 시점: 지금과 같음. 새 작업은 다음 틱부터 진행
    - 원래 작업이 그 틱에 끝나도(마지막 줄의 비동기 호출) 요청은 시전
  - 이유: 실행기 → 작업 한 방향으로만 부름. 작업이 실행기를 거꾸로 부르지 않음

## `PredefinedFunction`
- heap 함수(`Malloc`, `Free` 등)
  - 포인터 주인: 실행 문맥이 채움(`FunctionContext.Malloc`). heap 규칙(루트 집합, 옮기기, 누수)은 `HeapStorage`에 그대로
    - 이유: 실행 문맥은 작업마다 하나이고, heap과 앞으로의 전역 변수는 캐릭터에 하나. 실행 문맥은 상태와 규칙의 주인이 아니라 닿는 길만 줌
  - 잘못된 포인터일 때의 에러: 부른 쪽이 냄. `FreeBlock`은 `InvalidPointer`, `PredefinedFunction`은 `Fail(InvalidPointerError)` (`FunctionFailure` 항목의 책임 구분)
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
- 책임으로 나누기

| 책임 | 예 | 실행기가 하는 일 | 화면 |
| --- | --- | --- | --- |
| 없음 | 쿨타임 중, 시전 전 메모리 부족 | 작업을 만들지 않고 `CastRejected` | 아직 쓸 수 없음 |
| 플레이어 | 선언하지 않은 변수, 0으로 나누기, 잘못된 포인터 `free`, 맨손으로 찌르기 호출 | 작업을 `Failed`로 끝냄 | compile error 또는 runtime error |
| 개발자 | 스킬의 `Return` 누락·반환 타입 불일치, 언어 규칙 표에 그 실패 종류의 줄이 없음, 언어 규칙 파일 미연결 | 콘솔 오류, 작업을 `InternalError`로 끝냄 | 플레이어의 에러로 보여 주지 않음 |

  - `FunctionTaskEndReason.InternalError`: 새 값. `IsFailed` false, `Failure` null
  - 스킬 실패(`SkillFailed`): 플레이어 책임, `RuntimeError`. 이유: 스킬을 함수로 부르는 컨셉이므로, 잘못된 때에 스킬 함수를 부른 runtime error
    - 해당: `UnarmedError`, `NoSummonedSwordError`, 스킬의 heap 함수(`Free`, `TryGetHeapObject`)가 낸 `InvalidPointerError`
    - 스킬의 `InvalidPointerError`: 개발자의 스킬 코드가 포인터를 확인하지 않아 나는 경우도 플레이어 책임과 구분하지 않음. 개발자 버그는 테스트에서 찾아 고침
- 분류(`FailureCategory`): `CompileError`, `RuntimeError`만. `NotCodeError` 지움
  - 이유: 표를 거쳐 `Failed`로 끝나는 작업은 모두 플레이어 책임이고, 언제나 표에 분류가 있음
- C 표의 `InvalidPointer`: `EndTask`, `RuntimeError`
- 영향: `CastRejected` 이벤트의 모양이 바뀜. 화면은 `EndReason`으로 책임을 나눔. 임혜원에게 알림
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
