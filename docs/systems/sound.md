# Sound (BGM·효과음)

- 담당자: 김도훈 (#436)

## 하는 일
- 장소마다 BGM을 튼다 (`BgmPlayer`). 내 캐릭터가 있는 장소가 바뀌면 곡을 바꾼다 (페이드 1초). 같은 곡이면 이어서 튼다.
- 몬스터와 플레이어 스킬의 연출 이벤트에 효과음을 붙인다 (`SfxPlayer`).
- 어떤 상황에 어떤 소리를 낼지는 표 하나(`SoundTable` 에셋)에 적는다. **소리를 더할 때는 코드를 고치지 않고 표에 줄을 더한다.**
- 하지 않는 일
  - 음량 설정: 설정 화면(`GameSettings`, settings.md, 임혜원). BGM은 Mixer `BGM` 그룹, 효과음은 `SFX` 그룹으로 보내서 설정 음량을 따른다.

## 동작 규칙
- 파일 위치: `Assets/_DH/Audio/BGM`, `Assets/_DH/Audio/SFX`, 표 `Assets/_DH/Audio/SoundTable.asset`
- `Base` 씬의 `Sound` 오브젝트에 `BgmPlayer`, `SfxPlayer`를 붙이고 둘 다 같은 `SoundTable`을 넣는다. Base 씬은 늘 떠 있어서 어느 장소에서나 소리가 난다.
- BGM: 내 캐릭터의 씬 이름으로 표의 곡을 찾는다. 내 캐릭터가 없으면(로그인 화면) 곡을 끈다. 네트워크 없는 테스트는 열린 장소 씬 중 표에 있는 첫 씬의 곡.
- 효과음
  - 몬스터: `Monster.Appeared`(이 화면에 나타남, 모든 화면)를 받아, 프리팹 이름으로 표를 찾아 그 몬스터의 연출 이벤트에 소리를 붙인다.
    - 나타남 → `appear`, `Monster.HitEffectPlayed` → `hit`, `Monster.DeathEffectPlayed` → `death`, `GatewayAI.SlamEffectPlayed` → `slam`, `GatewayAI.SummonEffectPlayed` → `summon`, `ProjectileAttack.FireEffectPlayed` → `fire`
    - 보스 기믹 (#457): `GatewayPhases.ChargeEffectPlayed` → `charge`, `OverloadEffectPlayed` → `overload`, `GroggyEffectPlayed` → `groggy`
  - 공통 소리 (`Common`, #457)
    - 플레이어 피격·사망: `PlayerRoster.PlayerAdded`로 받은 플레이어의 `PlayerHealth.HitEffectPlayed` → `playerHit`, `DeathEffectPlayed` → `playerDeath`. 내 장소의 플레이어만
    - 스킬 해금: 내 캐릭터의 `PlayerProgress.UnlockAnswered`가 `UnlockResult.Unlocked`면 `skillUnlock`
    - UI 클릭: `Ui Scan Interval`(0.5초)마다 열린 씬의 `Button`(꺼진 것 포함)을 찾아, 처음 본 버튼의 `onClick`에 `uiClick`을 붙인다. 나중에 생기는 버튼(방 목록 줄 등)도 잡힌다
  - 플레이어 스킬: `PlayerRoster.PlayerAdded`를 받아 `SkillEffectEvents.Fired`(판정이 나간 순간, 스킬 ID)에 표의 스킬 소리를 붙인다. 진우 코드는 고치지 않는다.
  - 칸에 소리를 여러 개 넣으면 그중 하나를 랜덤으로. 비워 두면 소리 없음.
  - 내 캐릭터와 같은 장소의 소리만 낸다 (호스트는 다른 파티의 던전도 띄워 두므로).
  - 같은 소리가 `Min Gap`(0.06초) 안에 다시 나면 건너뛴다.
  - 연출 이벤트는 모든 화면에서 불리므로 네트워크를 따로 붙이지 않는다.

## 1차 표 (11/8)
| 곳 | 소리 |
| --- | --- |
| BGM 로비·사냥터(1·2·3)·연습장 | `hunting_bgm` (사냥터2·3 줄은 임혜원이 추가, 김도훈 허락 11/18) |
| BGM 레이드 던전 | `boss_bgm` |
| Gateway 내려찍기(패킷 낙하·때리기) | `boss_attack_3` |
| Gateway 패킷 던지기 | `packet_attack` |
| Gateway 사망 | `boss_death` |
| Pillar 나타남(기둥 생성) | `pillar_sound` |
| Leak 사망 | `leak_death_1`, `leak_death_2` (랜덤) |
| Worm 사망 | `worm_death` |
| 찌르기·베기·던지기 스킬 | `sword_swing_1_2` |
| 큰 스킬(깊게 찌르기, 넓게 베기, 다중 베기, 전장 휩쓸기, 검무) | `sword_swing_3` |

## 2차 (#457)
| 곳 | 소리 |
| --- | --- |
| Loop 사망 | `loop_death` |
| Gateway 전체 공격 폭발 | `boss_boom` |
| 플레이어 사망 | `player_death` |
| 스킬 트리 해금 성공 | `skill_unlock` |
| UI 버튼 클릭 | `ui_click` |

## 3차: 파이썬 스킬 (#568, 박진우)
- 파일: `Assets/_JW/Audio/SFX/Python/py_*.mp3`. Pixabay(무료, 출처 표기 불필요)에서 받아 앞부분 자르기, 길이 0.4~1.6초, 음량(평균 -20dB, 최고 -3dB 아래)을 맞춤
- 표에는 메뉴 Invoke → JW → 파이썬 스킬 효과음 넣기(`PythonSkillSounds`)가 없는 `py.*` 줄만 더한다. 기존 줄은 그대로

| 스킬 | 소리 (Pixabay 원본) |
| --- | --- |
| 매직 미사일 `py.magic_missile` | `py_magic_missile` (Magic_Spell_03) |
| 파이어볼 `py.fireball` | `py_fireball` (Fire Ball) |
| 얼음 창 `py.ice_spear` | `py_ice_spear` (Ice Spell Impact) |
| 체인 라이트닝 `py.chain_lightning` | `py_chain_lightning` (Lightning Spell) |
| 메테오 `py.meteor` | `py_meteor` (Epic Spell Impact) |
| 블리자드 `py.blizzard` | `py_blizzard` (Frost Spell Impact) |
| 독 안개 `py.venom_mist` | `py_venom_mist` (Steam Hissing) |
| 구슬 생성 `py.orb_create` | `py_orb_create` (Fairy Sparkle) |
| 구슬 발사 `py.orb_shot` | `py_orb_shot` (Magic_Spell_04, 조금 높게) |
| 구슬 난사 `py.orb_barrage` | `py_orb_barrage` (Zap Hit) |
| 구슬 폭발 `py.orb_burst` | `py_orb_burst` (Magic Burst) |
| 구슬 보호막 `py.orb_shield` | `py_orb_shield` (Sci-Fi Energy Shield Activation 1) |
| 저주 `py.hex` | `py_hex` (Dark Magic Curse Light Impact #1) |
| 뱀 속박 `py.snake_bind` | `py_snake_bind` (Snake hissing) |
| 섬광 `py.flash` | `py_flash` (Elemental Spell Impact (Light)) |
| 블링크 `py.blink` | `py_blink` (instant teleport) |
| 공중 부양 `py.levitate` | `py_levitate` (Magical Sparkle Rise FX) |

## 다른 시스템이 쓰는 것
| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `SfxPlayer.Play(AudioClip[] clips, Scene scene)` (static) | 소리 목록, 그 일이 일어난 씬 | - | 효과음 하나를 랜덤으로 낸다. 내 캐릭터의 장소가 아니면 안 낸다. 표에 없는 소리를 직접 낼 때 쓴다 |
| `SfxPlayer.Table` (static) | - | `SoundTable` | Base 씬의 소리 표 |
| `Monster.Appeared` (static event) | `Monster` | - | [연출] 몬스터가 이 화면에 나타난 순간(Start). 모든 화면 |
| `SoundTable.BgmOf(place)`, `TryGetMonster(name, out sounds)`, `SkillFiredOf(skillId)` | | | 표 읽기 |
