# PlayerHealth

- 담당자: 김도훈

## 하는 일
- 플레이어의 체력, 피격, 사망, 부활을 맡는다. 체력은 `PlayerHealth`만 바꾼다.
- 경직·사망 중에 움직이거나 시전할 수 있는지(`CanAct`)를 알려 준다.
- 피격 깜빡임, 사망 후 숨김, 부활 후 다시 보이기를 한다(`PlayerHealthEffects`).
- 하지 않는 일
  - 이동·시전을 직접 막지 않는다. 이동(`PlayerMove`)과 시전 담당이 `CanAct`를 읽어 막는다.
  - 피격·사망 애니메이션을 재생하지 않는다(임혜원 「플레이어 상태에 맞춰 애니메이션 재생하기」).
  - 관전 카메라를 움직이지 않는다. 카메라 담당(박진우)이 `HideEffectPlayed`, `RespawnEffectPlayed`를 받아 움직인다.
  - 로비로 씬을 옮기지 않는다. `LobbyReturnRequested`로 요청만 하고, 씬 이동 담당(이우림)이 옮긴다.
  - 실제 HUD를 만들지 않는다. 지금 체력 바(`TempHealthBar`)는 확인용 임시다.

## 동작 규칙
- 최대 체력은 `PlayerStats.MaxHp`다. 처음 나타날 때와 부활할 때 최대 체력이 된다. 최대 체력이 줄어 지금 체력보다 작아지면 지금 체력을 맞춘다(늘어날 때는 회복하지 않는다).
- 피격 (임혜원과 정한 값, #28)
  - 맞으면 경직 0.3초(`Hit Stun Time`) 뒤 무적 1초(`Invincible Time`)가 이어진다. 피격 순간부터 둘이 끝날 때까지 다시 맞지 않는다.
  - 경직 동안은 피격 모션, 무적 동안은 깜빡인다.
  - 죽는 공격이면 피격 연출 없이 사망 연출만 한다.
- 사망
  - 체력이 0이 되면 사망한다. 사망 중에는 피해를 받지 않고 `CanAct`가 false다.
  - 1초(`Hide Delay`) 뒤 캐릭터를 숨기고 관전으로 넘어간다. Collider는 그대로 남는다(땅에 그대로 서 있게 하기 위함).
  - 사망 페널티는 알파에서 없다.
- 부활
  - 사망 3초(`Respawn Delay`) 뒤 최대 체력으로 부활하고, 무적 1초 동안 깜빡인다. 부활 전용 모션은 없다.
  - 부활할 때 `LobbyReturnRequested`로 로비(마을)로 보내 달라고 요청한다. 씬 이동이 생기기 전까지는 처음 나타난 위치(씬의 시작 위치)에서 부활한다.
  - `AutoRespawn`이 false면(레이드) 부활하지 않고 숨긴 채 관전만 한다.
- event는 몬스터와 같이 로직용과 연출용으로 나뉜다. 네트워크를 붙이면 로직용은 서버에서만, 연출용은 각 플레이어 화면에서만 불린다.
  - 순서(피격): `Damaged` → `HpChanged` → `HitEffectPlayed`
  - 순서(사망): `Damaged` → `HpChanged` → `Died` → `DeathEffectPlayed` → (1초 뒤) `HideEffectPlayed`
  - 순서(부활): `LobbyReturnRequested` → `Respawned` → `HpChanged` → `RespawnEffectPlayed`

## 다른 시스템이 쓰는 것

플레이어 프리팹 최상위 오브젝트에 `PlayerHealth`, `PlayerHealthEffects`가 붙는다. `PlayerHealth`는 `IDamageable`을 구현하므로 몬스터 공격(`ContactDamage`, `MonsterAttackResolver`)은 따로 고치지 않아도 플레이어에게 피해를 준다.

### `PlayerHealth`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TakeDamage(int damage)` | 데미지 | - | 체력을 줄인다. 0 이하이거나, 죽었거나, 경직·무적 중이면 무시한다. |
| `Hp` | - | `int` | 지금 체력 |
| `MaxHp` | - | `int` | 최대 체력 (`PlayerStats.MaxHp`) |
| `IsDead` | - | `bool` | 죽었으면 `true`. 레이드 실패 판정(파티 전원 사망)이 읽는다. |
| `IsHidden` | - | `bool` | 사망 후 숨겨진 상태(관전 중)면 `true` |
| `IsStunned` | - | `bool` | 경직 중이면 `true` |
| `IsInvincible` | - | `bool` | 경직·무적 중이면 `true` |
| `CanAct` | - | `bool` | 움직이거나 시전할 수 있으면 `true`. 경직 중이거나 죽었으면 `false`. 이동·시전 담당이 읽는다. |
| `AutoRespawn` | - | `bool` | 죽었을 때 자동으로 부활하는지. 기본 `true` |
| `SetAutoRespawn(bool value)` | 켤지 끌지 | - | 레이드에 들어갈 때 `false`, 나올 때 `true`로 바꾼다. |
| `HitStunTime`, `InvincibleTime` | - | `float` | 경직, 무적 시간(초). 연출 길이를 맞출 때 읽는다. |
| `Damaged` (event) | `int` | - | [로직] 체력이 줄었을 때 |
| `Died` (event) | - | - | [로직] 체력이 0이 된 순간 한 번 |
| `Respawned` (event) | - | - | [로직] 부활한 순간 |
| `LobbyReturnRequested` (event) | - | - | [로직] 부활할 때 로비로 보내 달라는 요청. 씬 이동 담당이 받는다. |
| `HpChanged` (event) | `int`, `int` (지금 체력, 최대 체력) | - | [표시] 체력 바에 쓴다. |
| `HitEffectPlayed` (event) | `int` | - | [연출] 피격 모션, 무적 깜빡임 시작 |
| `DeathEffectPlayed` (event) | - | - | [연출] 사망 모션 시작 (1초짜리, 1회 재생 후 마지막 장면 유지) |
| `HideEffectPlayed` (event) | - | - | [연출] 캐릭터를 숨기고 카메라를 관전으로 넘길 때 |
| `RespawnEffectPlayed` (event) | - | - | [연출] 캐릭터가 다시 보이고 카메라를 내 캐릭터로 돌릴 때 |

## 맞춘 결과
- 임혜원과 정한 것 (#28)
  - 피격: 경직 0.3초, 이후 무적 1초. 무적 깜빡임은 코드로 한다.
  - 사망: 1초짜리 사망 모션 1회 재생 후, 1초 뒤 캐릭터를 숨기고 관전으로 넘어간다.
  - 부활: 부활 전용 모션 없이 대기 모습으로 다시 나타난다.
- 박진우와 정한 것
  - `PlayerMovement`가 `CanAct`가 false면 이동·점프를 막는다 (#53)
  - `SlotCaster`가 `CanAct`가 false면 시전하지 않고 고르던 대상도 취소한다 (#48)
- 임혜원과 정한 것
  - `PlayerAnimator`가 `HitEffectPlayed`, `DeathEffectPlayed`, `RespawnEffectPlayed`를 받아 피격·사망 모션을 재생한다 (#49)
- 다른 시스템이 쓰는 곳
  - 몬스터 AI(`GroundChaseAI`)가 `IsDead`를 읽어 죽은 플레이어는 쫓지 않는다.
- 이우림과 정해야 할 것
  - `PlayerHealth`, `PlayerHealthEffects`를 공용 Player.prefab에 붙이는 것 (지금은 Player_DH에만 있다)
  - 씬 이동이 생기면 `LobbyReturnRequested`를 받아 로비로 옮기는지
- 박진우와 정해야 할 것
  - 관전 카메라가 `HideEffectPlayed`에 다른 파티원으로, `RespawnEffectPlayed`에 내 캐릭터로 돌아오는지
