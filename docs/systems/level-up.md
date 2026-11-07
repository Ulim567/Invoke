# 레벨업 창 (LevelUpView)

- 담당자: 임혜원

## 하는 일
- 내 캐릭터의 레벨·경험치·능력치를 보여 주는 창 (#332, 우림이 시안 10, 10-1). 값은 읽기만 한다.
  - 상태 창 (시안 10): HUD의 「레벨업」 버튼(`HudView.LevelUpRequested`)으로 연다. 지금 레벨, 최대 레벨, 경험치 막대, 다음 레벨까지 남은 경험치, 능력치 6종
  - 결과 창 (시안 10-1): 레벨이 오른 뒤 로비에 오면 저절로 뜬다. 이전 → 지금 레벨, 능력치의 이전·지금·늘어난 값
- 하지 않는 일
  - 레벨 올리기: `PlayerProgress.AddExp`(이우림)가 `LevelRules`(김도훈)로 올린다. 레벨 능력치는 `PlayerLevelStats`(김도훈)
  - 레벨업 순간의 연출(빛, 소리): 나중 이슈

## 동작 규칙
- `LevelUpCanvas` 프리팹(`Assets/_HW/UI/LevelUp`)을 **Base 씬**에 둔다. 정렬 순서 10. 프리팹은 메뉴 Invoke → UI → 레벨업 화면 만들기(`LevelUpUIBuilder`, 에디터 전용).
- 창은 `InputBlockingPanel`: 열린 동안 캐릭터 조작을 막고 HUD를 숨긴다.
- 닫기: 상태 창은 X·Esc. 결과 창은 「확인」·Enter·Esc.
- 레벨업 감지 (내 컴퓨터에서)
  - 매 프레임 내 캐릭터의 `PlayerProgress.Level`과 `PlayerStats` 능력치를 읽어 둔다.
  - 레벨이 오르면 바로 전 프레임에 읽어 둔 레벨·능력치를 「이전」으로 기억한다. 사냥터에서 여러 번 올라도 처음 「이전」 그대로 모은다.
  - 내 캐릭터가 로비(`PlaceScenes.Lobby`)에 있고 다른 창이 열려 있지 않으면 결과 창을 연다. 한 번 보여 주면 잊는다.
  - 접속해서 불러온 레벨은 레벨업으로 치지 않는다 (서버가 플레이어를 만든 직후 불러온 값을 넣어 첫 동기화에 들어 있음, progress-store.md).
- 경험치: `Exp` / `LevelRules.ExpToNext(Level)`. 최대 레벨이면 막대를 꽉 채우고 「MAX」.
- 능력치 표기: 공격 속도 `1.0`, 쿨타임 감소 `0%`, 이동 속도 `5`, 나머지 정수. 늘어난 값은 초록 `+10`, 그대로면 `-`.
- 상태 창의 「다음 레벨」 칸: `PlayerLevelStats`(김도훈 #340)의 `MaxHpPerLevel`, `AttackPowerPerLevel`, 메모리는 `MemoryBonusAt(레벨 + 1) - MemoryBonusAt(레벨)`. 나머지와 최대 레벨은 `-`.
- 결과 창이 뜨는 조건의 「로비」는 `PlayerPublicInfo.Place`로 본다 (#362).

## 다른 시스템이 쓰는 것
- 없음

## 맞춘 결과
- 김도훈이 `PlayerLevelStats`에 레벨당 오르는 값을 열어 줌 (#340). 상태 창 「다음 레벨」에 씀
