# Monster

- 담당자: 김도훈

## 하는 일
- 모든 몬스터(잡몹, 보스)의 체력, 피격, 사망을 맡는다. 체력은 `Monster`만 바꾼다.
- 데미지를 받을 수 있는 모든 것이 쓰는 공통 인터페이스 `IDamageable`을 정한다. 몬스터, 플레이어, 보스, 기둥, 분신이 이것을 구현한다.
- 몬스터 몸에 닿은 플레이어에게 피해를 준다(`ContactDamage`). 몸은 서로 통과한다.
- 땅을 걷는 몬스터의 순찰·추적 AI(`GroundChaseAI`)를 맡는다. 잡몹 1 Leak이 쓴다.
- 공중을 떠다니며 멀리서 쏘는 몬스터의 AI(`FlyingShooterAI`)를 맡는다. 잡몹 2 Loop가 쓴다.
- 레이드 보스 Gateway의 패턴(`GatewayAI`)과 공격을 맡는다. 지금은 공격 1(패킷 던지기·패킷 낙하, #128), 공격 2(웜 소환·때리기, #203)와 페이즈 전환·기믹 1(기둥 파괴)·기믹 2(전체 공격)·기믹 3(진짜 찾기, `GatewayPhases`)까지 있다.
- 화면에 보스 체력 바(`BossHealthBar`)를 보여 준다 (#188).
- 레이드 던전에 보스를 만드는 일(`RaidBossSpawner`)과 클리어·실패 판정(`RaidJudge`)은 `raid.md`에 적는다.
- 사냥터에 몬스터를 만들고 다시 만드는 스포너(`MonsterSpawner`)를 맡는다.
- 몬스터가 죽으면 근처 플레이어에게 경험치·돈·재료(드롭)를 준다(`MonsterReward`).
- 몬스터 공격을 "공격 형태 + 효과"로 만드는 구조를 맡는다(버그 0차). 지금 있는 형태는 투사체(`ProjectileAttack`, `MonsterProjectile`)다.
- 하지 않는 일
  - 데미지 계산(공격력 × 배율, 치명타, 처형)은 하지 않는다. 계산이 끝난 정수 데미지를 받기만 한다.
  - 연출(이펙트, 소리, 애니메이션)을 하지 않는다. 연출은 `HitEffectPlayed`, `DeathEffectPlayed` event를 받은 쪽이 한다.
  - 경험치·돈·재료 값을 직접 바꾸지 않는다. `PlayerProgress`(이우림)의 함수를 부른다. 레벨업 규칙은 레벨 시스템 이슈에서 한다.
  - 공격 구조(`MonsterAttackData`, `ProjectileAttack`)는 언제, 어느 방향으로 공격할지 정하지 않는다. 몬스터 AI(`GroundChaseAI`, `FlyingShooterAI` 등)가 정한다.

## 동작 규칙
- 데미지는 `int`다. 0 이하의 데미지와 이미 죽은 몬스터에게 준 데미지는 무시한다.
- Inspector 값 `Invulnerable`을 켜면 맞아도 체력이 줄지 않고 죽지 않는다. `Damaged`, `HitEffectPlayed`는 그대로 받은 데미지를 넘겨 불린다 (연습장 허수아비, 임혜원과 정함).
- 체력이 0이 되면 `Died`를 한 번 알리고, Inspector 값 `Despawn Delay`초 뒤에 `NetworkSpawner.Despawn`으로 오브젝트를 없앤다(서버에서 없애면 모든 클라이언트에서도 사라짐, #310). `Despawn Delay`는 사망 모션 길이 이상으로 둔다(Leak 1.0초).
- event는 로직용과 연출용으로 나뉜다. 로직용은 서버(또는 네트워크 없는 테스트)에서만, 연출용은 그 몬스터를 보는 모든 화면에서 불린다 (#310, `MonsterNetwork`와 각 컴포넌트의 `[ClientRpc]`).
  - 로직(드롭, 스포너, 보스 페이즈, 레이드 판정)은 `Damaged`, `Died`를 받는다.
  - 연출(이펙트, 소리, 애니메이션, 데미지 숫자)은 `HitEffectPlayed`, `DeathEffectPlayed`를 받는다.
  - 순서: `Damaged` → `HitEffectPlayed` → (체력 0이면) `Died` → `DeathEffectPlayed`
- 접촉 피해는 `FixedUpdate`마다 몸 크기의 상자 안을 검사한다. Player와 Monster 레이어는 충돌이 꺼져 있어서 Trigger를 쓸 수 없기 때문이다.
  - 같은 대상에게는 Inspector 값 `Hit Interval`초에 한 번만 피해를 준다.
  - Trigger가 아닌 Collider(몸)에 닿았을 때만 피해를 준다.
  - 서버(또는 네트워크를 켜지 않은 테스트)에서만 검사한다. 클라이언트의 몬스터 복사본은 피해를 주지 않는다 (#310)
- 판정(접촉 피해 상자, 플레이어 탐지, 낭떠러지·장애물 검사)은 `Physics2D`가 아니라 몬스터가 속한 씬의 물리 세계(`gameObject.scene.GetPhysicsScene2D()`)에 한다. 서버는 장소 씬마다 물리 세계가 따로 있어서, `Physics2D`로 검사하면 사냥터 등에 있는 플레이어와 땅을 찾지 못한다(`place-scenes.md`). 물리 세계는 저장하지 않고 검사할 때마다 가져온다.
- 몬스터 공격 = 공격 형태 + 효과
  - 효과는 `MonsterAttackData` 파일에 목록으로 담는다. 맞으면 위에서부터 차례로 적용한다.
  - 효과 적용은 모두 `MonsterAttackResolver.Apply` 한 곳을 거친다. 체력 피해는 `DamageCalculator.Calculate(DamageRequest.Flat(Amount))`(박진우)를 거쳐 나온 정수로 `IDamageable.TakeDamage`를 부르고, 실행 중단은 맞은 대상의 `FunctionExecutor.InterruptAll()`(이우림)로 실행 중인 함수를 모두 끊는다(실행기가 없으면 무시). 메모리 효과(점유·탈취·회수 방해)는 받는 방식을 정하기 전까지 로그만 남긴다.
  - 투사체는 MonsterProjectile 레이어다. Player에 맞으면 효과를 주고 사라지고, Ground·Platform에 닿거나 `Lifetime`초가 지나면 효과 없이 사라진다.
  - 패링: Player에 맞으면 효과를 주기 전에 그 플레이어의 `ParryGuard.TryParry(투사체)`(박진우, `c-skills.md`)를 묻는다. `true`면 효과 없이 사라진다. 관통 투사체도 잡히면 사라진다. 관통은 이미 맞힌 대상에게는 다시 묻지 않는다.
  - 맞았는지 판단과 효과 적용은 서버(또는 네트워크 없는 테스트)에서만 한다 (#310).
  - 네트워크 (#310): 투사체 프리팹(`PacketThrow`, `MonsterBullet`)은 `NetworkIdentity`가 있고 Base NetworkManager에 등록돼 있다. `ProjectileAttack`이 `NetworkSpawner`로 쏜 몬스터의 씬에 만든다(서버의 `Base`가 아니라 던전·사냥터의 물리 세계에서 판정됨). 클라이언트는 서버가 `[ClientRpc]`로 보낸 출발 위치·속도로 그림만 날리고(검기와 같은 방식), 서버가 없애면 함께 사라진다. `Instantiate`로 직접 만든 투사체(시험용)는 그 자리에서만 동작한다

- 잡몹
  - 잡몹 1 **Leak**(메모리 누수 덩어리): 순찰형. `Monster` + `ContactDamage` + `GroundChaseAI`(`Chase Players` 끔) + `StatusEffects` + `StatusIconView`. 플레이어를 쫓지 않고 돌아다니며, 닿으면 접촉 피해를 준다. 체력 40, 접촉 피해 10, 순찰 1.5, 사망 대기 1.0초 (전부 임시, 프리팹 Inspector 값)
    - 몸 약 2.5×1.5칸, 그림 Pivot은 몸 바닥(임혜원 #101). 판정 BoxCollider2D와 `ContactDamage` 상자는 Size (2.4, 0.6), Offset (0, 0.6) (#107, #281에서 높이 1.2 → 0.6: 그림이 땅 위에 서도록 발 높이를 맞춤)
  - 잡몹 2 **Loop**(무한 루프 드론): 원거리 투사체형. `Monster` + `ContactDamage` + `FlyingShooterAI` + `ProjectileAttack` + `StatusEffects` + `StatusIconView`. 공중을 떠다니다 사거리 안의 플레이어를 멈춰서 겨냥해 쏜다. 쫓아가지는 않는다. 체력 30, 접촉 피해 5, 투사체 체력 피해 8(`Loop_Shot`), 사거리 6, 쏘는 간격 2초, 모으는 시간 0.25초, 사망 대기 0.75초 (전부 임시, 프리팹 Inspector 값)
    - 원형 판정(CircleCollider2D) 지름 1.1, 그림 Pivot은 가운데(임혜원 #101). Rigidbody2D Gravity Scale 0
- 몬스터끼리는 서로 통과한다(메이플 방식). 공용 Physics 2D Layer Collision Matrix에서 Monster ↔ Monster 충돌이 꺼져 있다(#81).
- 순찰·추적 (`GroundChaseAI`)
  - 순찰: "걷기"와 "멈추기"를 랜덤 시간씩 번갈아 한다.
    - 걷기 `Walk Time Range`(1.5~4초)가 끝나면 `Idle Chance`(50%) 확률로 멈추고, 아니면 계속 걷는다.
    - 멈추기 `Idle Time Range`(0.5~2초)가 끝나면 다시 걷는다. 다시 걸을 때 `Turn Chance`(40%) 확률로 반대로 돌아선다.
    - 걷는 중 앞이 낭떠러지(발 앞 아래에 Ground·Platform이 없음)거나 장애물(`Obstacle Layers`)이면 바로 돌아선다. 몬스터끼리 통과하게 하려면 `Obstacle Layers`에는 Ground만 넣는다.
    - 처음 방향과 상태는 랜덤이라 여러 마리가 똑같이 움직이지 않는다.
  - 발견: `Chase Players`가 켜져 있을 때만 한다. 끄면 플레이어를 무시하고 순찰만 한다. `Detect Interval`(0.2초)마다 `Detect Range` 안의 살아 있는(`PlayerHealth.IsDead`가 false) 플레이어 중 가장 가까운 플레이어를 쫓는다.
  - 놓침: 쫓던 플레이어가 죽거나 `Lose Range` 밖으로 나가면 잠깐 멈췄다가 순찰로 돌아간다.
  - 추적: 좌우로만 움직이고 점프하지 않는다. 앞이 낭떠러지나 장애물이면 그 자리에서 기다린다(떨어지지 않는다).
  - 몬스터가 죽으면 멈춘다. 그림은 오른쪽을 보고 그리고, 왼쪽을 볼 때 `SpriteRenderer.flipX`를 켠다(플레이어와 같은 규칙).
  - AI는 서버(또는 네트워크를 켜지 않은 테스트)에서만 돈다. 처음 방향을 랜덤으로 정하는 일도 서버만 한다 (#310)

- 스포너 (`MonsterSpawner`)
  - 구역(`Area Size`, `Area Offset`) 안에 몬스터를 `Max Count`(임시 3)마리까지 둔다. 한 마리가 죽으면(`Died`) `Respawn Delay`(임시 5초) 뒤 구역 안 새 랜덤 위치에 한 마리를 만든다.
  - 위치: 구역 안 땅·발판의 윗면 중 랜덤한 곳에 발이 닿게 만든다(몬스터의 BoxCollider2D 또는 CircleCollider2D 기준). 떠다니는 몬스터(`FlyingShooterAI`)는 생긴 자리에서 `Hover Height`만큼 떠오른다. 구역은 몬스터가 생길 땅과 발판을 덮기만 하면 된다.
    - 구역 안 랜덤한 x의 높은 하늘(구역 위 100칸)에서 아래로 선을 그어 땅(`Ground Layers`)을 찾는다. 타일맵 Collider는 테두리(Outlines)만 있어서 선이 윗면·아랫면을 번갈아 맞으므로, 짝수 번째만 윗면으로 써서 땅 속에는 만들지 않는다.
    - 고른 자리 위로 몸 높이만큼 비어 있지 않거나 구역 안에 윗면이 없으면 x를 다시 뽑는다(최대 10번).
  - 몬스터는 `NetworkSpawner.Spawn`(이우림)으로 스포너가 있는 씬에 만든다. 그래서 사냥터 씬의 물리 세계와 플레이어에게 맞춰진다.
  - 사냥터(`HuntingGround`, 임혜원 #176 맵 기준) 배치
    - `LeakSpawner`: 위치 (-10, 1), 구역 56×12 (X -38~18, 바닥과 발판 3층), 최대 6마리
    - `LoopSpawner`: 위치 (28.5, 0), 구역 17×10 (X 20~37, 위가 뚫린 구역), 최대 3마리
    - 마리 수는 씬에 놓은 스포너에서만 바꾼다. 프리팹 원본(Leak 3, Loop 2)은 테스트용으로 그대로 둔다
  - 서버(Host 포함)에서만 만든다. 서버가 켜지는 순간 모든 지점에 만들고, 클라이언트는 서버가 만든 몬스터를 받기만 한다. 네트워크를 켜지 않으면 만들지 않는다.
  - 죽은 몬스터는 `Despawn Delay` 뒤 `Destroy`로 없앤다. 서버에서 없애면 Mirror(`NetworkIdentity`)가 모든 클라이언트에서도 없앤다.
  - 몬스터 프리팹은 `NetworkIdentity`를 붙이고 NetworkManager의 Registered Spawnable Prefabs에 등록한다. 체력·피격·사망 연출은 `MonsterNetwork`로 맞춘다(#310). 위치는 `NetworkRigidbodyReliable2D`(Gateway는 `NetworkTransformReliable`, Sync Direction Server To Client)로 맞춘다(#310, 아래 「위치·방향은 모든 화면에서 같다」).

- 떠다니며 쏘기 (`FlyingShooterAI`)
  - 떠다니기: 중력 없이 생긴 자리보다 `Hover Height`(2.5)만큼 위에서 위아래로 살짝 둥실거린다.
  - 순찰: 움직이기·멈추기를 랜덤 시간씩 번갈아 하고 가끔 돌아선다. 벽(`Obstacle Layers`)이나 처음 자리에서 `Patrol Radius`(4)보다 멀어지면 돌아선다.
  - 공격: `Attack Range`(6) 안의 살아 있는 플레이어 중 가장 가까운 플레이어를 멈춰서 바라보고, 처음엔 `First Shot Delay`(0.5초) 뒤, 그다음엔 `Fire Interval`(2초)마다 몸 중심을 겨냥해 `ProjectileAttack.Fire`를 부른다. 대각선으로도 쏜다. `Lose Range`(8) 밖으로 나가거나 죽으면 순찰로 돌아간다.
  - 밀려나기: 맞으면(`Damaged`) 쏘던 플레이어 반대쪽(없으면 바라보는 반대쪽)으로 `Knockback Time`(0.15초) 동안 `Knockback Speed`(4)로 밀린다. `TakeDamage`에는 때린 쪽 정보가 없어서 쏘던 플레이어를 기준으로 한다.
  - AI(이동·탐지·발사)는 서버(또는 네트워크를 켜지 않은 테스트)에서만 돈다. 클라이언트의 복사본은 쏘지 않는다 (#310)
- 발사 (`ProjectileAttack`)
  - `Fire`를 부르면 연출 `FireEffectPlayed`가 바로 나가고, `Fire Delay`초(모으는 시간) 뒤에 투사체가 나간다. 모으는 중에는 `IsCharging`이 `true`이고, 다시 `Fire`를 불러도 무시한다.
  - 방향은 `Fire`를 부른 순간에 정해진다. 모으는 중에 죽거나 기절하면 투사체는 나가지 않는다.

- 보스 Gateway (`GatewayAI`, #128)
  - `Monster`(Is Boss 체크) + `ContactDamage` + `ProjectileAttack` + `GatewayAI` + `StatusEffects` + `StatusIconView`. 제자리에 서 있고 움직이지 않는다. 체력 3000, 접촉 피해 10, 사망 대기 1.5초 (전부 임시)
  - `Detect Range`(20) 안의 살아 있는 플레이어 중 가장 가까운 쪽을 바라본다(최상위 SpriteRenderer `flipX`). 처음 발견하면 `First Attack Delay`(3초) 뒤, 그다음엔 `Attack Interval`(5초)마다 공격 하나를 랜덤으로 고른다. 같은 공격은 연속 두 번 고르지 않는다. 범위 안에 아무도 없으면 공격하지 않는다.
  - 패킷 던지기(`Throw`): `ProjectileAttack.Fire`로 바라보는 쪽 수평으로 큰 패킷 덩어리(`Packet_Throw`, 체력 피해 15, 패링 가능)를 던진다. `Fire Delay` 1초, 속도 5 (#380: 피하기 어려워 0.58초·7에서 늘림). 모으는 동안 날아갈 줄을 빨간 띠로 깜빡여 보여 준다(`ThrowWarning`, `GatewayAI`가 시작할 때 붙임, 연출만). 관통(`Pierce`)이라 한 줄의 플레이어를 모두 맞히고 계속 날아간다. 높이는 `Fire Point Offset`으로 바닥 근처에 맞춘다.
  - 패킷 낙하(`Drop`): 연출 `SlamEffectPlayed`가 먼저 나가고 `Slam Delay`(0.42초 = Slam 모션 6번째 장면에 내려침) 뒤, 살아 있는 플레이어마다 그 발밑 땅에 하나, `Drop Area`(38×12, 보스방 가로 40칸에 맞춤) 안 랜덤한 땅에 `Extra Drops`(2)개 `FallingPacket`을 만든다.
  - 웜 소환(`Summon`, #203): 연출 `SummonEffectPlayed`(Throw 모션) 뒤 `Summon Delay`(0.58초)에 보스 양옆 `Worm Spawn Distance`(4.5칸) 아래 땅 위에 웜 2마리를 만든다. 살아 있는 웜이 `Max Worms`(4) 이상이면 고르지 않는다
  - 때리기(`Smash`, #203): 바라보는 쪽 몸 앞 끝에서 `Smash Range`(6칸) 안에 목표 플레이어가 있을 때만 고른다. 연출 `SlamEffectPlayed`(Slam 모션, 낙하와 같이 씀) 뒤 `Smash Delay`(0.42초 = Slam 6번째 장면)에 몸 앞 구역(`Smash Size` 6×4, 아래 변은 보스 아래 땅. 땅을 못 찾으면 보스 몸 판정 바닥)의 플레이어에게 한 번씩 `Gateway_Smash`(체력 피해 25 + 실행 중단). 맞은 플레이어가 실행 중이던 함수가 모두 끊긴다
  - 공격 고르기: 목록(던지기, 낙하, 소환, 때리기)에서 랜덤. 직전 공격과 지금 쓸 수 없는 공격(가까운 플레이어 없는 때리기, 웜이 가득 찬 소환)은 뺀다
  - 기절 중이나 `Pause(초)`로 멈춘 동안에는 공격을 고르지 않는다. 모으던 낙하·소환·때리기는 취소된다. 패턴은 서버(또는 네트워크 없는 테스트)에서만 돈다.
  - 위치·방향은 모든 화면에서 같다 (#310)
    - 위치: Gateway는 `NetworkTransformReliable`, Worm·Leak·Loop는 `NetworkRigidbodyReliable2D`(둘 다 Sync Direction Server To Client). 분신·기둥·안전지대·낙하 패킷은 움직이지 않아 생성 위치만
    - 진짜 찾기의 순간이동: 서버에서 `NetworkTransformBase.ServerTeleport`. 클라이언트 화면에서도 미끄러지지 않고 바로 옮겨진다
    - 바라보는 방향: `GatewayAI`, `GroundChaseAI`, `FlyingShooterAI`의 방향이 `[SyncVar]`. 바뀌면 각 화면에서 그림을 뒤집는다. 분신 방향은 만든 직후 `GatewayPhases`가 `[ClientRpc]`로 한 번 알린다
    - `GroundChaseAI.IsMoving`: 클라이언트에서는 서버 값(`[SyncVar]`). 클라이언트의 Rigidbody 속도는 서버와 다를 수 있다
  - 연출 이벤트는 모든 화면에서 불린다 (#310)
    - `GatewayAI`, `GatewayPhases`, `ProjectileAttack`: `NetworkBehaviour`. 연출 함수가 서버에서 `[ClientRpc]`를 보내 그 몬스터를 보는 모든 화면(호스트 포함)에서 이벤트를 부른다. 전용 서버에서는 불리지 않는다. 네트워크 없는 테스트는 그 자리에서
    - `ProjectileAttack`을 쓰는 프리팹에는 `NetworkIdentity`가 있어야 한다: Gateway, Loop, `TestMonster`(#310에서 `NetworkIdentity` 추가, Test_DH·Test_HW·Test_JW 씬에 놓여 있음)
    - 새 몬스터 부품의 연출 이벤트: 그 부품이 네트워크 프리팹에만 쓰이면 `GatewayAI`처럼 직접 `[ClientRpc]`, `NetworkIdentity` 없는 오브젝트도 쓰면 `Monster`처럼 네트워크 컴포넌트를 따로 둔다
    - `Monster`만 따로 두는 이유는 연습장 허수아비(`NetworkIdentity` 없는 씬 오브젝트). 허수아비를 네트워크 프리팹으로 바꾸면 `MonsterNetwork`를 `Monster`로 합친다
- 페이즈와 기믹 (`GatewayPhases`)
  - 1페이즈 100~70%(기믹 없음), 2페이즈 70% 이하(기둥), 3페이즈 50% 이하(전체 공격), 4페이즈 30% 이하(진짜 찾기) (`Phase Thresholds`, #380에서 시간 대신 체력으로). 기믹은 **페이즈마다 한 번**만 나온다.
  - 2~4페이즈: 문턱을 넘는 순간 보스가 `Transition Pause`(1.5초) 멈췄다가(`GatewayAI.Pause`) 그 페이즈 기믹. 연출은 없다. 한 번에 여러 문턱을 넘으면 가장 낮은 페이즈로 바로 간다
  - 기믹 1 기둥 파괴: 보스 기준 X -15, -8, +15의 땅에 기둥(`Pillar`, `Monster`, 체력 150) 3개를 세운다. 스킬로 부술 수 있다. 보스는 그동안 기본 공격을 계속한다
    - `Pillar Time Limit`(20초) 안에 모두 부수면 성공: 보스에게 기절 `Groggy Duration`(6초) = 그로기. 연출 `GroggyEffectPlayed`
    - 시간이 다 되면 실패: 남은 기둥이 터지고(체력 0) 보스가 패킷 던지기와 웜 소환을 한 번씩 한다(`GatewayAI.PunishAttack`, 웜 수 제한 무시)
    - 보스가 죽으면 남은 기둥도 터진다
  - 기믹 2 전체 공격: 3페이즈(50%) 기믹
    - 보스가 `Charge Time`(3초) 동안 기를 모은다(연출 `ChargeEffectPlayed`). 그동안 기본 공격은 멈춘다(`GatewayAI.Pause`, 모으는 시간 + 0.5초)
    - 보스 왼쪽·오른쪽에 하나씩, 보스에서 가로 `Safe Zone Distance`(6~15칸) 랜덤 거리의 땅 위에 안전지대(`SafeZone`, 가로 4 × 높이 5)를 만든다. 이동 속도 5라 3초면 어느 쪽이든 닿는다
    - 시간이 되면 안전지대 밖의 같은 씬 살아 있는 플레이어 모두에게 `Gateway_Overload`(체력 피해 60)를 적용하고(연출 `OverloadEffectPlayed`), 안전지대를 없앤다. 안전 여부는 플레이어 발 위치로 본다
    - 플레이어 무적(1초) 중이면 기존 피격 규칙대로 무시된다. 모으는 동안 보스가 기본 공격을 하지 않아 겹칠 일이 거의 없다
    - 보스가 죽으면 모으던 전체 공격은 취소하고 안전지대를 없앤다
  - 기믹 3 진짜 찾기: 4페이즈(30%) 기믹
    - 보스 기준 `Clone Slots`(X -10, 0, +10)에 진짜 1 + 분신 2를 세운다. 진짜는 그중 랜덤한 자리로 순간이동한다. 분신은 본체와 그림·크기가 같고 진짜와 같은 쪽을 본다
    - 구분 힌트: 분신만 2~3초마다 0.1초 색이 바뀐다(`CloneGlitch`, 연출 전용, 위치는 안 바꿈)
    - 기믹 동안 진짜도 공격하지 않는다(`GatewayAI.Pause`)
    - 분신(`GatewayClone`)은 `Monster`(보스 아님, 체력 99999)라 스킬로 맞고 죽지 않는다. 보스가 아니라 화면 위 체력 바에는 진짜만 나온다
    - 분신이 맞을 때마다 같은 씬의 살아 있는 플레이어 모두에게 `Gateway_Reflect`(체력 피해 15, 반사). `TakeDamage`에는 때린 사람 정보가 없어서 파티 전체가 받는다. 분신에 걸린 출혈 틱도 맞을 때마다 반사된다
    - 진짜가 기믹 동안 `Reveal Damage`(150) 이상 피해를 받거나 `Clone Duration`(20초)이 지나면 분신이 사라지고(체력 0 → 사망 연출) 진짜가 다시 공격한다(`GatewayAI.Resume`)
    - 보스가 죽으면 분신도 사라진다
  - 수치는 전부 임시
- 웜 (`Worm` 프리팹, #203)
  - 땅을 기어 플레이어를 쫓는 몬스터. 새 코드 없이 Leak과 같은 부품: `Monster`(체력 20, 사망 대기 1.2초) + `GroundChaseAI`(Chase Players 켬, 발견 거리 30, 놓치는 거리 40, 추적 속도 3) + `ContactDamage`(피해 12, 간격 1초) + `StatusEffects` + `StatusIconView`
  - 낭떠러지·벽에서 멈추고 떨어지지 않는다. 스스로 사라지지 않으므로 처치해야 한다(동시 최대 4마리)
  - 그림은 오른쪽을 보고 그리고 왼쪽은 flipX (GroundChaseAI). 연출은 잡몹처럼 `HitEffectPlayed`, `DeathEffectPlayed`, 이동은 `GroundChaseAI.IsMoving`
- 낙하 (`FallingPacket`)
  - 땅 위 착지 지점에 그림자를 `Warn Time`(1초) 동안 보여 주고, 패킷이 `Drop Height`(8)에서 `Fall Time`(0.3초) 동안 떨어진다.
  - 착지 순간 그림자 위 `Hit Size`(2×3, 아래 변이 땅) 상자 안의 플레이어에게 한 번씩 `Packet_Drop`(체력 피해 20, 패링 불가)을 적용한다. 그림자 밖으로 피하면 맞지 않는다.
  - `NetworkSpawner`로 만들고 없앤다(착지 `Despawn Delay` 0.3초 뒤). 판정·삭제는 서버(또는 네트워크 없는 테스트)에서만 한다.
- 보스 체력 바 (`BossHealthBar`, #188)
  - 위치는 임시로 화면 위쪽 가운데(프리팹의 Panel 위치만 바꾸면 옮겨진다. 그림·UI 배치 때 확정). 내 캐릭터와 같은 씬에 살아 있는 보스(`Monster.IsBoss`)가 있을 때만 보인다. 여러 마리면 최대 체력이 가장 큰 보스. `Search Interval`(0.5초)마다 찾는다.
  - 빨간 막대 = 지금 체력, 흰 막대 = 맞은 뒤 `Trail Delay`(0.4초) 기다렸다가 천천히 따라 줄어듦, 숫자 = 지금 / 최대, 이름 = 보스 오브젝트 이름("(Clone)" 뗌)
  - 페이즈 구분선을 보스의 `GatewayPhases.PhaseThresholds`(70%, 50%, 30%) 위치에 놓는다. 구분선(`Phase Marks`)은 3개 필요
  - 체력은 `MonsterNetwork`로 동기화되어 클라이언트 화면에서도 정확하다 (#310)
  - 지금은 색 막대(임시). 그림은 임혜원(테두리, 빨간 채움, 흰 채움, 구분선)
  - UI 프리팹 `BossHealthBar`(`_DH`). Base 씬(공용) 화면 UI에 놓는 일은 이우림과 정한다
- 스포너 설정 확인: `Monster Prefab`이나 `Ground Layers`가 비어 있으면 시작할 때 `[MonsterSpawner]` 경고를 남긴다 (#188)
- 관통 투사체: `MonsterProjectile`의 `Pierce`를 켜면 맞은 대상(Rigidbody 기준)마다 한 번씩 효과를 주고 계속 날아간다. 끄면 예전처럼 처음 맞은 대상에서 사라진다.

- 상태 이상 반응 (#103)
  - 상태 이상은 같은 오브젝트의 `StatusEffects`(박진우)에서 읽기만 한다. 몬스터 프리팹 최상위에 `StatusEffects`와 상태 이상 아이콘 `StatusIconView`(박진우, 머리 위 표시)를 붙인다. `StatusEffects`가 없으면 상태 이상에 반응하지 않는다.
  - 기절(`IsStunned`): 행동 불가. `GroundChaseAI`, `FlyingShooterAI`가 그 자리에 멈추고 탐지도 하지 않는다. `ContactDamage`는 피해를 주지 않고, `ProjectileAttack.Fire`는 쏘지 않는다. 발사를 모으는 중에 기절하면 투사체는 나가지 않는다.
  - 이속 감소(`MoveSpeedMultiplier`): `GroundChaseAI`의 순찰·추적 속도, `FlyingShooterAI`의 순찰 속도에 곱한다.
  - 실명(`IsBlinded`): 공격이 빗나간다. `ContactDamage`는 피해를 주지 않는다. 실명 중에 쏜 투사체는 `Misses`가 켜져 플레이어를 통과한다(날아가는 중에 실명이 풀려도 빗나감). 땅·발판에는 그대로 막힌다.

- 처치 보상 (`MonsterReward`)
  - 몬스터가 죽으면(`Died`) 죽은 자리에서 `Reward Range`(잡몹 20칸) 안의 살아 있는 플레이어 모두에게 경험치·돈·재료를 준다. 때린 사람은 따로 세지 않는다 (`TakeDamage`에 때린 사람 정보가 없어서, 알파에서는 근처 모두에게 준다).
  - `Whole Scene`을 켜면(레이드 보스) 거리·생사와 상관없이 같은 씬의 플레이어 모두가 받는다. 죽어서 관전 중인 파티원도 받는다.
  - 드롭은 땅에 떨어지지 않고 바로 가방에 들어간다. 재료는 죽을 때 한 번 굴려서 받는 플레이어 모두에게 같은 것을 준다. 줄마다 확률(`Chance`)을 따로 굴리고, 1이면 반드시 나온다.
  - 값은 `PlayerProgress.AddExp`, `AddMoney`, `AddMaterial`(이우림)로만 바꾼다. `PlayerProgress`가 없는 플레이어는 건너뛴다.
  - 서버(Host 포함)이거나 네트워크를 켜지 않은 테스트에서만 준다.
  - 재료 ID: `bug_fragment`(버그 조각), `boss_core`(보스 코어). 이름표는 `SkillTreeDefaults`(임혜원)와 같다.
  - 몬스터별 보상 (임시)

    | 몬스터 | 경험치 | 돈 | 재료 | 받는 플레이어 |
    | --- | --- | --- | --- | --- |
    | Leak | 10 | 5 | 버그 조각 1개 (50%) | 20칸 안, 살아 있는 플레이어 |
    | Loop | 15 | 8 | 버그 조각 1개 (60%) | 20칸 안, 살아 있는 플레이어 |
    | Gateway | 200 | 100 | 보스 코어 1개 (100%), 버그 조각 5개 (100%) | 같은 씬 모두 (`Whole Scene`) |

  - 기둥·분신·웜·허수아비에는 `MonsterReward`를 붙이지 않는다 (보상 없음).

## 다른 시스템이 쓰는 것

### `IDamageable`

데미지를 주는 쪽은 맞은 Collider2D에서 `GetComponentInParent<IDamageable>()`로 가져와 `TakeDamage`를 부른다. 맞은 대상이 몬스터인지 플레이어인지 알 필요가 없다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TakeDamage(int damage)` | 데미지 (배율·반올림까지 끝낸 정수) | - | 피해를 준다. 0 이하이거나 이미 죽은 대상이면 아무 일도 일어나지 않는다. |

### `Monster`

몬스터 프리팹 최상위 오브젝트에 붙어 있다. `IDamageable`을 구현한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TakeDamage(int damage)` | 데미지 | - | 체력을 줄인다. 0이 되면 사망한다. |
| `Hp` | - | `int` | 지금 체력. 읽기만 한다. |
| `MaxHp` | - | `int` | 최대 체력 (Inspector 값) |
| `IsDead` | - | `bool` | 죽었으면 `true` |
| `IsBoss` | - | `bool` | 보스면 `true` (Inspector 값). 처형처럼 보스를 빼는 규칙에서 읽는다. |
| `IsInvulnerable` | - | `bool` | 체력이 줄지 않는 몬스터면 `true` (Inspector 값 `Invulnerable`). 연습장 허수아비 |
| `Damaged` (event) | `int` (받은 데미지) | - | [로직] 데미지를 받았을 때 불린다 (`Invulnerable`이면 체력은 그대로). 보스 페이즈 전환처럼 체력에 따른 로직에 쓴다. |
| `Died` (event) | - | - | [로직] 체력이 0이 된 순간 한 번 불린다. 드롭, 스포너 집계, 레이드 판정에 쓴다. |
| `HitEffectPlayed` (event) | `int` (받은 데미지) | - | [연출] 피격 이펙트, 소리, 데미지 숫자 표시에 쓴다. |
| `DeathEffectPlayed` (event) | - | - | [연출] 사망 이펙트, 소리, 애니메이션에 쓴다. 한 번 불린다. |
| `ApplyServerHp(int)`, `PlayHitFromServer(int, int)`, `PlayDeathFromServer()` | - | - | [클라이언트] `MonsterNetwork`만 부른다. 다른 스크립트는 부르지 않는다 (#310) |

- 클라이언트에서는 `Hp`, `IsDead`, `HitEffectPlayed`, `DeathEffectPlayed`가 서버와 같다 (`MonsterNetwork`가 붙은 몬스터). `TakeDamage`, `Damaged`, `Died`는 서버에서만

### `MonsterNetwork` (#310)

네트워크 몬스터 프리팹 최상위에 `Monster`와 함께 붙인다 (Gateway, GatewayClone, Pillar, Worm, Leak, Loop). `NetworkIdentity`가 있어야 한다. 다른 스크립트가 부르는 함수는 없다.

- 서버 → 그 몬스터를 보는 클라이언트: 체력(`[SyncVar]`), 피격 알림(데미지, 맞은 뒤 체력, `[ClientRpc]`), 사망 알림(`[ClientRpc]`)
- 받은 클라이언트: `Monster`에 체력을 넣고 `HitEffectPlayed`·`DeathEffectPlayed`만 부른다
- 나중에 그 장소에 들어와 몬스터를 처음 본 클라이언트도 생성 때 지금 체력을 받는다
- 호스트: 서버의 `Monster`가 이미 연출 이벤트를 불러서 받은 알림은 무시. 네트워크를 켜지 않은 테스트: 아무것도 하지 않음
- `Monster`를 `NetworkBehaviour`로 바꾸지 않고 따로 둔 이유: 연습장 허수아비처럼 `NetworkIdentity` 없는 씬 오브젝트도 `Monster`를 쓴다

### `ContactDamage`

`Monster`와 같은 오브젝트에 붙인다. 다른 스크립트가 부르는 함수는 없고, Inspector 값만 정한다.

| Inspector 값 | 설명 |
| --- | --- |
| `Damage` | 닿았을 때 주는 데미지 |
| `Hit Interval` | 같은 대상에게 다시 피해를 주기까지 시간(초) |
| `Box Size`, `Box Offset` | 검사 상자 크기와 위치. Scene 창에서 빨간 상자로 보인다. |
| `Target Layers` | 피해를 줄 레이어. Player로 둔다. |

### `GroundChaseAI`

몬스터 프리팹에 `Monster`, `Rigidbody2D`, `Collider2D`와 함께 붙인다. Inspector에서 `Ground Layers`는 Ground·Platform, `Obstacle Layers`는 Ground(Monster를 넣으면 몬스터끼리 마주칠 때 통과하지 않고 돌아선다), `Target Layers`는 Player로 둔다. `Chase Players`를 끄면 순찰만 하고 탐지 값은 쓰지 않는다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Target` | - | `PlayerHealth` | 지금 쫓는 플레이어. 순찰 중이면 `null`. 연출(추적 중 표정 등)이나 공격 AI가 읽는다. |
| `Facing` | - | `int` | 바라보는 방향. 오른쪽 1, 왼쪽 -1 |
| `IsMoving` | - | `bool` | 지금 좌우로 움직이고 있으면 `true`. 이동·대기 애니메이션을 바꿀 때 읽는다. |

### `MonsterSpawner`

장소 씬(사냥터)에 놓는다. 다른 스크립트가 부르는 함수는 없고, Inspector 값만 정한다.

| Inspector 값 | 설명 |
| --- | --- |
| `Monster Prefab` | 만들 몬스터 프리팹. `NetworkIdentity`가 있고 등록돼 있어야 한다. |
| `Max Count` | 구역 안에 동시에 있을 최대 마리 수 |
| `Respawn Delay` | 죽은 뒤 다시 만들기까지 시간(초) |
| `Area Size`, `Area Offset` | 몬스터를 만들 구역. 몬스터가 생길 땅과 발판을 덮는다. 스포너를 선택하면 Scene 창에서 초록 상자로 보인다. |
| `Ground Layers` | 몬스터가 설 수 있는 레이어. Ground, Platform으로 둔다. |

- 구역 안에서 설 땅을 찾지 못하면 만들지 않고 경고 로그를 한 번 남긴 뒤 1초마다 다시 시도한다. 위가 막힌 실내(천장 아래)에서는 땅을 못 찾을 수 있다 (#319, 임혜원 알림)

### `AttackEffectType`, `AttackEffect`

| `AttackEffectType` | 뜻 | 쓰는 값 |
| --- | --- | --- |
| `HealthDamage` | 체력 피해 | `Amount` = 데미지 |
| `MemoryOccupy` | 메모리 점유: 힙을 원치 않는 객체가 차지 | `Amount` = 칸 수, `Duration` |
| `MemorySteal` | 메모리 탈취: 쓸 수 있는 남은 메모리 감소 | `Amount` = 칸 수, `Duration` |
| `ReclaimBlock` | 회수 방해: 힙이 회수되지 않음 | `Duration` |
| `Interrupt` | 실행 중단: 실행 중인 함수가 끊김 | - |

`AttackEffect`는 `Type`, `Amount`(int), `Duration`(초, float)을 읽기만 한다.

### `MonsterAttackData`

공격 하나의 데이터 파일. Project 창에서 Create → Invoke → Monster Attack으로 만든다. 게임 중에는 값을 바꾸지 않는다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Effects` | - | `IReadOnlyList<AttackEffect>` | 맞았을 때 적용할 효과 목록 |
| `Parryable` | - | `bool` | 패링으로 잡을 수 있는 공격이면 `true` |

### `ProjectileAttack`

몬스터 프리팹에 `Monster`와 함께 붙인다. Inspector에서 `Attack`(MonsterAttackData), `Projectile Prefab`, `Speed`, `Fire Point Offset`, `Fire Delay`(모으는 시간)를 정한다. `NetworkBehaviour`라서 프리팹에 `NetworkIdentity`가 있어야 한다 (#310).

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Fire(Vector2 direction)` | 쏠 방향 (길이 상관없음) | - | 그 방향으로 쏜다. 몬스터 AI가 부른다. 연출이 바로 나가고 `Fire Delay`초 뒤 투사체가 나간다. 죽었거나 기절했거나 방향이 (0, 0)이거나 모으는 중이면 쏘지 않는다. 실명 중에 나간 투사체는 빗나간다. |
| `IsCharging` | - | `bool` | 발사를 모으는 중이면 `true` |
| `FireDelay` | - | `float` | 모으는 시간(초). `ThrowWarning`이 이만큼 띠를 보여 준다 |
| `FirePointOffset` | - | `Vector2` | 발사 위치 (오른쪽 기준) || `FireEffectPlayed` (event) | `Vector2` (쏠 방향, 길이 1) | - | [연출] 발사를 시작할 때 불린다. 발사 모션, 소리에 쓴다. 투사체는 `Fire Delay`초 뒤에 나간다. |

### `FlyingShooterAI`

몬스터 프리팹에 `Monster`, `Rigidbody2D`(Dynamic, Gravity Scale 0, Freeze Rotation Z), `Collider2D`, `ProjectileAttack`과 함께 붙인다. Inspector에서 `Obstacle Layers`는 Ground, `Target Layers`는 Player로 둔다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Target` | - | `PlayerHealth` | 지금 쏘고 있는 플레이어. 순찰 중이면 `null` |
| `Facing` | - | `int` | 바라보는 방향. 오른쪽 1, 왼쪽 -1 |
| `IsMoving` | - | `bool` | 지금 좌우로 움직이고 있으면 `true`(밀려나는 중 포함) |

### `MonsterProjectile`

`ProjectileAttack`이 만드는 투사체. 다른 시스템은 값을 읽기만 한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Attack` | - | `MonsterAttackData` | 이 투사체의 공격 데이터. 패링(`ParryGuard`)이 `Attack.Parryable`로 잡을 수 있는지 확인한다. |
| `Misses` | - | `bool` | 쏜 몬스터가 실명 중이었으면 `true`. 플레이어를 통과한다. |

Inspector `Pierce`를 켜면 관통한다(맞은 대상마다 한 번, 사라지지 않음).

### `GatewayAI`

Gateway 프리팹 최상위에 `Monster`(Is Boss), `ProjectileAttack`과 함께 붙인다. Inspector에서 `Target Layers`는 Player, `Ground Layers`는 Ground·Platform, `Falling Packet Prefab`에 FallingPacket 프리팹을 넣는다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Target` | - | `PlayerHealth` | 지금 바라보는 가장 가까운 플레이어. 범위 안에 없으면 `null` |
| `Facing` | - | `int` | 바라보는 방향. 오른쪽 1, 왼쪽 -1 |
| `SlamEffectPlayed` (event) | - | - | [연출] 패킷 낙하·때리기를 시작할 때. Slam 모션에 쓴다. 그림자는 `Slam Delay`초 뒤, 때리기 판정은 `Smash Delay`초 뒤 |
| `SummonEffectPlayed` (event) | - | - | [연출] 웜 소환을 시작할 때. Throw 모션에 쓴다. 웜은 `Summon Delay`초 뒤 |
| `Pause(float seconds)` | 멈출 시간(초) | - | 그동안 공격을 멈춘다. 모으는 중인 낙하는 취소. `GatewayPhases`가 페이즈 전환 때 부른다 |
| `PunishAttack()` | - | - | 패킷 던지기와 웜 소환을 지금 한 번씩 (웜 수 제한 무시). 기둥 기믹 실패 때 `GatewayPhases`가 부른다. 서버에서만 |
| `Resume()` | - | - | `Pause`를 바로 풀고 한 간격 뒤부터 다시 공격한다 (분신 기믹이 일찍 끝날 때) |
| 던지기 연출 | - | - | 같은 오브젝트의 `ProjectileAttack.FireEffectPlayed`를 받는다 (Throw 모션) |

### `GatewayPhases`

Gateway 프리팹 최상위에 `Monster`, `GatewayAI`, `StatusEffects`와 함께 붙인다. Inspector에서 `Pillar Prefab`(Pillar), `Overload Attack`(Gateway_Overload), `Safe Zone Prefab`(SafeZone), `Clone Prefab`(GatewayClone), `Reflect Attack`(Gateway_Reflect), `Ground Layers`(Ground·Platform)를 넣는다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Phase` | - | `int` | 지금 페이즈 (1~4) |
| `PhaseThresholds` | - | `IReadOnlyList<float>` | 페이즈 비율 (0.7, 0.5, 0.3). 보스 체력 바가 읽는다 |
| `PhaseChanged` (event) | `int` (새 페이즈) | - | [로직] 페이즈가 바뀐 순간 |
| `GroggyEffectPlayed` (event) | `float` (그로기 시간) | - | [연출] 기둥을 모두 부숴 그로기에 들어갈 때. Groggy 모션에 쓴다 |
| `ChargeEffectPlayed` (event) | `float` (모으는 시간) | - | [연출] 전체 공격을 모으기 시작할 때. Charge 모션에 쓴다 |
| `OverloadEffectPlayed` (event) | - | - | [연출] 전체 공격이 터질 때. 화면 번쩍임·흔들림·소리 |

분신(`GatewayClone` 프리팹)은 `Monster`(체력 99999, 보스 아님, 사망 대기 0.75) + `ContactDamage` + `CloneGlitch` + `StatusEffects` + `StatusIconView` + Gateway와 같은 판정(BoxCollider2D) + `NetworkIdentity`. 진짜와 구분되는 컴포넌트는 `CloneGlitch`뿐이다.

안전지대(`SafeZone` 프리팹)는 `SafeZone`(가로 `Width` 4, 높이 `Height` 5, Pivot 바닥) + 자식 그림 + `NetworkIdentity`. `Contains(Vector2)`로 안에 있는지 알려 준다.

기둥(`Pillar` 프리팹)은 `Monster`(체력 150, 보스 아님) + BoxCollider2D 2×4칸(Pivot 바닥) + `NetworkIdentity`. 연출은 잡몹처럼 `HitEffectPlayed`, `DeathEffectPlayed`, `Hp`/`MaxHp`를 읽으면 된다.

### `MonsterReward`

보상을 줄 몬스터 프리팹 최상위에 `Monster`와 함께 붙인다. Inspector에서 `Player Layers`는 Player로 둔다. `NetworkBehaviour`라서 프리팹에 `NetworkIdentity`가 있어야 한다 (#404).

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `RewardEffectPlayed` (event) | `PlayerProgress`, `int` 경험치, `int` 돈, `IReadOnlyList<MaterialDrop>` 재료 | - | [연출] 내 캐릭터가 보상을 받았을 때. 받은 플레이어의 화면(호스트 포함)에서만 불린다 (`[TargetRpc]`, #404). 받은 플레이어 = 그 화면의 내 캐릭터. 네트워크 없는 테스트는 받은 플레이어마다 그 자리에서. "+경험치" 글자, 재료가 날아오는 연출에 쓴다. 재료 목록은 이벤트 안에서만 읽는다. 몬스터는 0.5초 뒤 없어지므로 띄운 그림을 몬스터 자식으로 두지 않는다 |

| Inspector 값 | 설명 |
| --- | --- |
| `Exp`, `Money` | 받는 플레이어마다 주는 경험치, 돈 |
| `Drops` | 재료 드롭 목록 (`Material Id`, `Count`, `Chance`) |
| `Reward Range` | 죽은 자리에서 이 거리 안의 살아 있는 플레이어가 받는다. 몬스터를 선택하면 노란 원으로 보인다 |
| `Player Layers` | Player |
| `Whole Scene` | 켜면 같은 씬의 플레이어 모두 (레이드 보스) |

### `FallingPacket`

`GatewayAI`가 만든다. 자식 `Shadow`(그림자), `Packet`(떨어지는 그림)을 Inspector에 넣는다. Collider는 필요 없다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `WarnEffectPlayed` (event) | - | - | [연출] 그림자가 나타날 때 |
| `LandEffectPlayed` (event) | - | - | [연출] 패킷이 땅에 닿을 때 (착지 이펙트·소리) |

## 맞춘 결과
- 이우림과 정한 것
  - 실행 중단 효과(`Interrupt`): `MonsterAttackResolver`가 맞은 대상에서 `GetComponentInParent<FunctionExecutor>()`를 찾아 `InterruptAll()`을 부른다. 없으면 아무것도 하지 않는다 (#143 제안대로). 판정은 서버에서만이라 서버의 실행기가 끊는다
  - 처치 보상은 `PlayerProgress.AddExp`(몬스터 처치), `AddMoney`, `AddMaterial`(드롭)로 준다 (`progress-store.md`). 레벨업 규칙은 김도훈의 레벨 시스템 이슈
  - 공용 Physics 2D Layer Collision Matrix에서 Monster ↔ Monster 충돌을 껐다(#81). 그래서 몬스터 프리팹 콜라이더의 Exclude Layers에 `Monster`를 넣지 않아도 몬스터끼리 통과한다.
  - 장소 씬마다 물리 세계가 따로 있어서, 몬스터 판정은 자기 씬의 물리 세계에 한다(#76, #83).
  - 스포너가 몬스터를 만들 수 있도록 김도훈이 Leak에 `NetworkIdentity`를 붙이고 `Base` 씬 NetworkManager의 Registered Spawnable Prefabs에 등록한다(#86, 이우림 허락).
  - 사냥터 씬(`HuntingGround`)에 스포너를 놓고 구역을 맵에 맞추는 일은 김도훈이 한다 (처음엔 이우림이 하기로 했으나 김도훈이 맡음).
  - 몬스터 생성은 스포너에서 `NetworkSpawner.Spawn`을 쓴다(#86). 몬스터 삭제와 투사체 생성·삭제는 #310에서 `NetworkSpawner`로 바꿨다.
  - 공용 Player의 Rigidbody2D Simulated를 모든 플레이어에서 켰다(#23). 그래서 서버의 `ContactDamage` 검사와 몬스터 투사체가 원격 플레이어에게도 닿는다.
- 박진우와 정한 것
  - 상태 이상(#97)은 `StatusEffects`가 걸고 풀고, 몬스터 반응(기절·이속 감소·실명)은 김도훈이 `IsStunned`, `MoveSpeedMultiplier`, `IsBlinded`를 읽어서 한다(#103). 몬스터 프리팹에 `StatusEffects`를 붙이는 일은 김도훈이 한다.
  - 상태 이상 아이콘(`StatusIconView`, #156)도 김도훈이 몬스터 프리팹(Leak, Loop, Gateway) 최상위에 붙인다.
  - 스킬이 맞힌 대상에게 `IDamageable.TakeDamage(int)`로 데미지를 준다.
  - 배율을 곱한 데미지는 데미지 계산 쪽에서 반올림해 정수로 바꾼다. 계산 값이 양수면 최소 1, 0 이하면 0이고 `TakeDamage`가 0을 무시한다.
  - 몬스터 공격의 체력 피해도 데미지 계산을 거친다. `MonsterAttackResolver`가 `DamageCalculator.Calculate(DamageRequest.Flat(effect.Amount))`를 부른다. 몬스터는 공격력 능력치가 없어서 `Amount`를 그대로 넘기고, 몬스터 공격은 처형하지 않으므로 대상 정보는 넘기지 않는다.
  - 처형(체력 2% 이하, 보스 제외)은 `DamageTarget.FromMonster`로 `Monster.IsBoss`, `Hp`, `MaxHp`를 읽는다 (#42).
  - 패링 (#245): `MonsterProjectile`이 플레이어에 닿으면 `Misses` 확인 다음, 효과 적용 전에 `ParryGuard.TryParry(this)`를 묻는다. `true`면 효과 없이 투사체를 없앤다(관통도). `Parryable`·`Misses`·판정 시간은 `ParryGuard`가 보고, 반격 검기는 박진우 스킬이 투사체 속도 반대 방향으로 쏘므로 투사체에 쏜 몬스터 정보는 넣지 않는다. 판정은 서버에서만 (`[ServerCallback]` 그대로)
- 박진우와 정해야 할 것
  - 공격 "성질"(폭탄, 불 등)을 `MonsterAttackData`에 어떻게 담을지
- 임혜원과 정한 것
  - 연습장 허수아비는 `Monster`를 쓰고 Inspector `Invulnerable`을 켠다. 체력은 그대로, `Damaged`·`HitEffectPlayed`(받은 데미지)는 그대로 불려서 데미지 숫자에 쓴다
  - Leak·Loop 그림과 애니메이션은 `_HW/Monsters`(#101). 오른쪽을 보고 그리고, 왼쪽은 최상위 SpriteRenderer의 `flipX`
  - `Despawn Delay`는 사망 모션 길이 이상 (Leak 1.0초, Loop 0.75초)
  - 프리팹 판정 크기·값은 김도훈이 먼저 바꾸고, 그림·Animator·MonsterAnimator(임혜원)는 그 뒤 임혜원이 프리팹 최상위에 붙인다
  - 몬스터 연출은 임혜원의 MonsterAnimator(`_HW`)가 `HitEffectPlayed`, `DeathEffectPlayed`, `GroundChaseAI.IsMoving`을 읽어서 한다. Leak은 멈출 때 Move를 천천히 틀고, 대기 모션은 따로 없다
  - Loop 발사 연출은 `ProjectileAttack.FireEffectPlayed`를 받는다. Shoot 모션 4번째 프레임(약 0.25초)에 맞춰 `Fire Delay` 0.25초 뒤 투사체가 나간다
  - Loop는 맞으면 쏘던 플레이어 반대쪽으로 살짝 밀린다(`FlyingShooterAI`). 그림의 휘청 모션과 함께 보인다
  - 투사체 그림 `Loop_Bullet`은 `MonsterBullet` 프리팹에 임혜원이 붙인다
  - 보스 Gateway·웜 그림과 애니메이션은 `_HW/Boss`(#126). 보스 프리팹도 김도훈이 판정을 먼저 만들고, 임혜원이 그림·Animator를 붙인다
  - Gateway 공격 타이밍: Throw 8번째 장면(0.58초)에 패킷이 손을 떠나고(`Fire Delay` 0.58), Slam 6번째 장면(0.42초)에 내려친다(`Slam Delay` 0.42). 두 모션 모두 9장면 0.75초
  - 패킷 공격 그림은 `_HW/Boss/Attack`(#146). `Packet_Throw` 2×2칸, `Packet_Fall` 1.5칸, `Packet_Shadow` 가로 2칸, 4장면 12fps 반복. 김도훈이 PacketThrow·FallingPacket 프리팹에 첫 장면과 컨트롤러를 붙였다(Scale 1). PacketThrow는 `ProjectileFacing`(임혜원)으로 왼쪽으로 날 때 flipX
- 임혜원과 정해야 할 것
  - 기둥 그림 2×4칸: 체력 50% 초과 멀쩡, 50% 이하 금 감, 사망(`DeathEffectPlayed`) 때 부서지는 모션 약 0.5초. 지금은 임시 그림
  - Gateway Groggy 모션은 `GatewayPhases.GroggyEffectPlayed`(그로기 시간을 넘김)를 받는다
  - 분신 프리팹(`GatewayClone`)에도 Gateway와 같은 그림·Animator·MonsterAnimator를 붙인다. 가짜가 사라지는 연출은 `DeathEffectPlayed`
  - 웜 프리팹(`Worm`)에 Animator(Worm.controller)·MonsterAnimator(Base State Crawl)를 붙인다. 땅을 기어 다닌다. 소환은 Gateway Throw 모션(`SummonEffectPlayed`), 때리기는 Slam 모션(`SlamEffectPlayed`, 낙하와 같이 씀)
  - 안전지대 바닥 그림(가로 4칸, 빛나는 바닥, 반복). Gateway Charge 모션은 `GatewayPhases.ChargeEffectPlayed`, 터지는 연출은 `OverloadEffectPlayed`
  - 보스 체력 바 그림: 테두리, 빨간 채움, 흰 채움(단색 사각형), 페이즈 구분선. 이름은 Galmuri 글자로 코드가 쓴다
  - Gateway 프리팹의 그림·Animator. Throw는 `ProjectileAttack.FireEffectPlayed`, Slam은 `GatewayAI.SlamEffectPlayed`를 받는다
