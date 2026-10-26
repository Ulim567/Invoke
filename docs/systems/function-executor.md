# FunctionExecutor

- 담당자: 이우림

## 하는 일
- 캐릭터마다 `FunctionExecutor`가 하나씩 붙어, cast한 function을 tick마다 진행한다. function을 한 번 cast할 때마다 `FunctionTask`가 하나 생기고, memory가 허락하는 만큼 여러 `FunctionTask`가 동시에 진행된다.
- function이 실행되는 동안 stack memory를 쓰고, function이 끝나거나 cancel되는 즉시 돌려준다.
- 하지 않는 일
  - 키 입력을 읽지 않는다. slot을 뗐을 때 `Cast`를 부르는 것은 cast 담당 스크립트가 한다.
  - 이펙트, 소리, 애니메이션을 하지 않는다. 연출은 `TaskStarted`, `TaskEnded` event를 받은 쪽이 한다.
  - heap, code area, cooldown, 언어별 규칙(C의 모든 `FunctionTask` 중단 등)은 아직 다루지 않는다. 이후 이슈에서 추가한다.

## 동작 규칙
- 1 tick = `FixedUpdate` N회. N은 `FunctionExecutor`의 Inspector 값 `Fixed Updates Per Tick`이다(처음 값 1, 즉 0.02초).
- cast한 function은 다음 tick부터 진행한다.
- 같은 tick에서는 먼저 cast한 `FunctionTask`부터 진행한다.
- 모든 `Block`은 최소 1 tick을 쓴다.
  - `PredefinedFunction`: `TickCount` tick
  - `UserFunction`: 실제로 실행된 `Block`들의 tick 합
  - `ReturnBlock`: 1 tick
- `FunctionBlock`은 실행을 시작할 때 `FrameSize`칸의 stack을 쓰고, 끝나거나 cancel되는 즉시 돌려준다. `ReturnBlock` 같은 문법 `Block`은 stack을 쓰지 않는다.
- stack이 모자라면 그 `FunctionTask`가 `Overflow`로 끝난다. 이미 실행 중이던 바깥 `FunctionBlock`들은 cancel되며 stack을 돌려준다. 지금은 그 `FunctionTask`만 끝나고 다른 `FunctionTask`는 계속 진행된다.
- `FunctionTask`는 function을 끝까지 실행하면 `Completed`로 끝난다. `UserFunction` 안에서 `ReturnBlock`을 만나면, 그 `ReturnBlock`을 담은 `UserFunction`만 끝나고 바깥 `UserFunction`은 다음 `Block`으로 넘어간다.

## 다른 시스템이 쓰는 것

캐릭터(Player 프리팹)에 `FunctionExecutor`, `ProcessMemory`가 붙어 있다. `GetComponent`로 가져온다.

### `FunctionExecutor`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Cast(FunctionBlock function)` | cast할 function (`PredefinedFunction` 또는 `UserFunction`) | `FunctionTask` | function을 cast한다. 넘긴 객체는 원본으로 남고 복사본이 실행되므로, 같은 객체를 여러 번 넘겨도 된다. `FunctionTask`는 다음 tick부터 진행한다. |
| `TaskStarted` (event) | `FunctionTask` | - | `FunctionTask`가 첫 tick을 진행하기 직전에 불린다. 연출을 시작할 때 쓴다. |
| `TaskEnded` (event) | `FunctionTask` | - | `FunctionTask`가 끝나서 목록에서 빠질 때 불린다. 끝난 이유는 `task.EndReason`으로 확인한다. |
| `CurrentTick` | - | `int` | 지금까지 지난 tick 수. 첫 tick이 1이다. |

### `FunctionTask`

`FunctionTask`는 `FunctionExecutor`만 만든다. 다른 스크립트는 값을 읽기만 한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Caster` | - | `GameObject` | 이 `FunctionTask`를 cast한 캐릭터. `PredefinedFunction`은 이것으로 캐릭터의 위치, 방향, 컴포넌트를 찾는다. |
| `Name` | - | `string` | cast한 function의 이름 |
| `State` | - | `FunctionTaskState` | `Running`(진행 중), `WaitingInput`(입력 대기, 아직 쓰지 않음), `Ended`(끝남) |
| `EndReason` | - | `FunctionTaskEndReason` | `None`(진행 중), `Completed`(정상 종료), `Interrupted`(외부 중단, 아직 쓰지 않음), `Overflow`(stack 부족) |
| `IsEnded` | - | `bool` | 끝났으면 `true` |

### `ProcessMemory`

값은 `ProcessMemory`만 바꾼다. 다른 시스템은 읽기만 한다(예: memory 게이지 UI).

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Capacity` | - | `int` | memory 전체 칸 수 (Inspector 값) |
| `StackUsed` | - | `int` | 지금 stack이 쓰는 칸 수. 실행 중인 모든 `FunctionBlock`의 `FrameSize` 합 |
| `Free` | - | `int` | 남은 칸 수 |

### `PredefinedFunction` 만들기 (C 스킬, 공통 function, 특수 이동 function)

`PredefinedFunction`을 상속한 클래스 하나가 function 하나다.

| 이름 | 종류 | 설명 |
| --- | --- | --- |
| `Name` | 반드시 정함 (`string`) | function 이름 |
| `TickCount` | 반드시 정함 (`int`) | 동작 시간(tick 수). 1 이하이면 1 tick으로 진행한다. |
| `FrameSize` | 필요하면 덮어씀 (`int`, 기본 1) | 실행하는 동안 쓰는 stack 칸 수 |
| `OnStart(FunctionTask task)` | 필요하면 덮어씀 | 첫 tick에 `OnTick`보다 먼저 한 번 불린다. |
| `OnTick(FunctionTask task, int tick)` | 필요하면 덮어씀 | 매 tick 불린다. `tick`은 1부터 `TickCount`까지 센다. 몇 번째 tick에 무엇을 할지(예: 자세잡기 → 차징 → 발사) 여기서 정한다. |
| `OnEnd(FunctionTask task)` | 필요하면 덮어씀 | 마지막 tick에 `OnTick` 다음으로 한 번 불린다. `Completed`일 때만 불린다. |
| `OnCancel(FunctionTask task)` | 필요하면 덮어씀 | 도중에 cancel됐을 때(`Overflow`, `Interrupted`) `OnEnd` 대신 한 번 불린다. 만들어 둔 오브젝트를 없애는 등의 정리를 한다. |

- 한 tick에서 부르는 순서: `OnStart`(첫 tick만) → `OnTick` → `OnEnd`(마지막 tick만)
- 실행되는 것은 언제나 복사본이므로, 실행 중에 쓰는 값(예: 차징한 양)을 필드에 저장해도 된다.
  - 다만 `List` 같은 참조 형식 필드는 원본과 복사본이 함께 쓰게 된다. 실행 중에 쓰는 참조 형식 값은 `OnStart`에서 새로 만든다.
- `OnStart` 등에서 `base`를 부르지 않아도 된다. stack memory는 `FunctionExecutor`가 따로 처리한다.
- 값을 바꾸는 일(데미지, 이동)만 하고, 연출은 하지 않는다(`CLAUDE.md` 코드 작성 규칙 1).

```csharp
// 예시. 실제로 있는 function이 아니다
public class Thrust : PredefinedFunction
{
	public override string Name => "Thrust";
	public override int TickCount => 20;

	protected override void OnTick(FunctionTask task, int tick)
	{
		// 10 tick째에 cast한 캐릭터 앞쪽으로 판정을 낸다
		if (tick == 10) { /* task.Caster를 기준으로 판정 */ }
	}
}
```

### `UserFunction`, `ReturnBlock`

블록 코딩으로 만든 function이다. 블록 코딩 화면이 플레이어가 짠 블록들을 이 형식으로 바꿔 `Cast`에 넘긴다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `new UserFunction(string name, params Block[] blocks)` | 이름, 위에서부터 실행할 `Block`들 | `UserFunction` | `Block`에는 `PredefinedFunction`, `UserFunction`, `ReturnBlock`을 넣을 수 있다. 같은 객체를 여러 `UserFunction`의 `Block`으로 넣어도 된다. |
| `new ReturnBlock()` | - | `ReturnBlock` | 남은 `Block`을 실행하지 않고 이 `ReturnBlock`을 담은 `UserFunction`을 끝낸다. 1 tick을 쓴다. |

```csharp
// 예시. Dash, Thrust는 실제로 있는 function이 아니다
UserFunction dashThrust = new UserFunction("DashThrust", new Dash(), new Thrust());
executor.Cast(dashThrust);
```

## 맞춘 결과
- 아직 다른 사람과 정한 것은 없다.
- 박진우와 정해야 할 것
  - C 스킬마다 `FrameSize`를 몇 칸으로 할지
  - 스킬별로 stack 밖에서 따로 쓰는 memory(예: 검 소환의 heap)를 어떻게 다룰지
  - 대상 지정·파라미터 입력 방식 (`WaitingInput`을 쓰는 이슈에서 정한다)
  - cooldown을 실행 시간과 같게 둘지, 따로 둘지
