# 스킬 아이콘

- 담당자: 박진우

## 하는 일
- C 스킬·공통 이동 스킬 24개의 픽셀 아이콘(24×24)을 스킬 ID로 찾아 준다 (#444). 보여 주는 곳은 HUD 슬롯(hud.md)과 스킬 트리 노드(skill-tree.md, 둘 다 임혜원).
- 계열마다 판 색: 찌르기 청록, 던지기 주황, 휘두르기 빨강, 소환 보라, 이동 초록. 소환검이 들어간 스킬은 시안 칼날, 맨손 검 스킬은 은색 칼날.
- 그림은 PixelLab으로 만들었다. 문법 노드, 테스트 함수 등 아이콘이 없는 ID는 `null`.

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `SkillIcons.Get(string skillId)` | 스킬 ID (`c.thrust` 등) | `Sprite` (없으면 `null`) | 아이콘. `Resources/SkillIcons/{스킬 ID}`에서 불러오고 한 번 찾은 것은 기억한다 |
| `SkillIcons.Has(string skillId)` | 스킬 ID | `bool` | 아이콘이 있으면 `true` |

- 그림: `Assets/_JW/Resources/SkillIcons/{스킬 ID}.png` (24×24, 판은 가운데 약 20px). 넣기만 하면 `SkillIconImporter`(에디터 전용)가 가져오기 설정(Sprite Single, 16 PPU, Point, 압축 없음)을 자동으로 한다.
- 새 스킬: 같은 스타일로 24×24 아이콘을 만들어 스킬 ID 이름으로 넣는다.
- 테스트: `SkillIconsSimTest` (아무 씬, Play, Host 필요 없음, 「[SkillIconTest] 전체 통과」). 스킬 트리의 스킬 노드마다 아이콘이 있는지, 24×24·Point인지, 문법 노드·없는 ID는 `null`인지 본다.

| 계열 | 스킬 (ID) |
| --- | --- |
| 찌르기 | 검 찌르기 `c.thrust`, 검 집중찌르기 `c.focused_thrust`, 급소공략 `c.vital_strike`, 꼬챙이 `c.skewer`, 소환검 찌르기 `c.summoned_thrust`, 상처 공략 `c.wound_exploit`, 검 깊이찌르기 `c.deep_thrust` |
| 던지기 | 검 던지기 `c.sword_throw`, 소환검 던지기 `c.summoned_throw`, 검 돌아오기 `c.sword_return` |
| 휘두르기 | 검 휘두르기 `c.sword_slash`, 검 크게베기 `c.wide_slash`, 소환검 휘두르기 `c.summoned_slash`, 상처 가르기 `c.wound_slash`, 검 다중베기 `c.multi_slash`, 전장 휩쓸기 `c.battlefield_sweep` |
| 소환 | 검 소환 `c.summon_sword`, 해제 `c.free_sword`, 패링 `c.parry`, 표식 남기기 `c.mark`, 검무 `c.sword_dance` |
| 이동 | 대시 `common.dash`, 더블점프 `common.double_jump`, 검 타기 `c.sword_ride` |

## 맞춘 결과
- 임혜원과 정함 (#444) - 아이콘 파일과 `SkillIcons`는 박진우(_JW). HUD 슬롯(`HudView`, `HudUIBuilder`)과 스킬 트리(`SkillTreeNodeView`, `SkillTreeView`)에 아이콘을 넣는 코드는 박진우가 혜원 허락을 받고 고쳤다.
