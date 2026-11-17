# 파이썬 마법사 이펙트·아이콘 프롬프트 (PixelLab용)

만든 그림은 아래 **파일 이름·칸 크기·장 수**에 맞춰 가로 한 줄 시트로 저장하고, 같은 이름의 임시 그림을 덮어쓴 뒤 Unity 메뉴 **Invoke → JW → 파이썬 마법사 만들기**를 다시 누르면 된다.
- 이펙트 시트: `Assets/_JW/Effects/Magic/{이름}.png` (16 PPU, 투명 배경)
- 장 수는 달라도 된다 (칸 너비로 자동으로 자른다). 칸 크기는 지켜야 한다.
- 마법탄(`_bolt`)은 **오른쪽으로 날아가는 모습**으로 그린다. 게임이 방향에 맞게 돌린다.

## 공통 문구
모든 이펙트 프롬프트 앞에 붙인다:
```
pixel art game effect, clean crisp pixels, dark outline, limited color palette, muted cool tones with bright core, transparent background, 2D side-scroller game VFX, no character, no text
```
캐릭터 색: 가라앉은 남색 로브 + 탁한 금색 테두리, 지팡이 구슬은 옅은 노랑·하늘색.

## 마나 구슬·구슬 스킬
| 파일 | 칸 | 장 | 종류 | 프롬프트 |
|---|---|---|---|---|
| mana_orb | 16×16 | 4 | 반복 | `small floating magic orb, glowing pale gold core with soft blue rim, gentle pulsing glow, tiny sparkle orbiting, 4 frame loop` |
| orb_create_cast | 32×32 | 5 | 한 번 | `magic orb forming from converging gold sparkles, small burst of light as it appears, 5 frame animation` |
| orb_shot_bolt | 32×32 | 4 | 반복 | `fast flying golden energy orb moving right, bright white-gold core, short comet trail to the left, 4 frame loop` |
| orb_shot_hit | 32×32 | 5 | 한 번 | `golden magic impact burst, expanding ring and scattering gold sparks, fading out, 5 frame animation` |
| orb_burst_cast | 128×128 | 6 | 한 번 | `large circular golden shockwave explosion centered, expanding ring of light with radial sparks, fading at the edge, 6 frame animation` |
| orb_shield_zone | 64×64 | 4 | 반복 | `translucent round magic barrier bubble, thin pale blue edge with faint gold runes rotating, mostly transparent inside, 4 frame loop` |
| orb_shield_hit | 64×64 | 5 | 한 번 | `magic barrier shattering, blue-white flash and glass-like shards flying outward, 5 frame animation` |

## 원소
| 파일 | 칸 | 장 | 종류 | 프롬프트 |
|---|---|---|---|---|
| magic_missile_bolt | 32×32 | 4 | 반복 | `small arcane missile flying right, bright blue-white core, thin blue trail to the left, 4 frame loop` |
| magic_missile_hit | 32×32 | 5 | 한 번 | `small blue arcane impact, star-shaped flash and blue sparks, fading, 5 frame animation` |
| fireball_bolt | 32×32 | 4 | 반복 | `fireball flying right, yellow-white core, orange flames, dark red flame tail to the left, flickering, 4 frame loop` |
| fireball_hit | 64×64 | 6 | 한 번 | `fire explosion, bright yellow center expanding to orange and dark red, smoke puffs at the end, 6 frame animation` |
| ice_spear_bolt | 48×32 | 4 | 반복 | `long sharp ice spear flying right, pale blue crystal with white highlight, frost particles trailing to the left, 4 frame loop` |
| ice_spear_hit | 32×32 | 5 | 한 번 | `ice shard impact, burst of pale blue crystal fragments and frost mist, 5 frame animation` |
| chain_lightning_beam | 64×32 | 4 | 한 번 | `horizontal lightning bolt spanning the full width from left edge to right edge, jagged yellow-white electric arc with blue glow, flickering then fading, 4 frame animation` |
| chain_lightning_hit | 32×32 | 4 | 한 번 | `electric spark burst, yellow-white crackling arcs in all directions, 4 frame animation` |

## 범위
| 파일 | 칸 | 장 | 종류 | 프롬프트 |
|---|---|---|---|---|
| meteor_cast | 96×144 | 10 | 한 번 | `flaming meteor falling diagonally from the top-left to the bottom center, then a big fiery explosion at the ground with debris; frames 1-5 the meteor falling, frames 6-10 the explosion fading; ground impact point at bottom center, 10 frame animation` |
| blizzard_zone | 96×96 | 6 | 반복 | `circular blizzard area, swirling snowflakes and icy wind streaks inside a round region, pale blue and white, semi-transparent, seamless 6 frame loop` |
| venom_mist_zone | 96×96 | 6 | 반복 | `circular toxic mist cloud, sickly green fog with rising bubbles, semi-transparent, slowly churning, seamless 6 frame loop` |

메테오는 앞 5장(12fps, 약 0.4초)이 떨어지는 장면이어야 데미지 시점과 맞는다. 땅에 닿는 자리는 칸 아래에서 20픽셀 위 가운데.

## 보조·이동
| 파일 | 칸 | 장 | 종류 | 프롬프트 |
|---|---|---|---|---|
| hex_bolt | 32×32 | 4 | 반복 | `dark curse orb flying right, purple-black core with violet wisps and a tiny skull hint, smoky trail to the left, 4 frame loop` |
| hex_hit | 32×32 | 5 | 한 번 | `curse mark appearing, purple sigil flash with dark smoke, 5 frame animation` |
| snake_bind_bolt | 32×32 | 4 | 반복 | `small green magic snake flying right, slithering wave motion, glowing yellow eyes, 4 frame loop` |
| snake_bind_hit | 32×32 | 5 | 한 번 | `green snake coiling into a tight binding ring then squeezing, green sparkle, 5 frame animation` |
| flash_cast | 160×160 | 5 | 한 번 | `blinding flash of light, large white-yellow radial burst from the center filling the frame then fading to thin rays, 5 frame animation` |
| blink_cast | 48×48 | 5 | 한 번 | `teleport puff, purple-blue arcane smoke swirl collapsing into a point with sparkles, 5 frame animation` |
| levitate_cast | 48×48 | 5 | 한 번 | `soft blue levitation ring under the feet, expanding magic circle with rising light particles, 5 frame animation` |

## 중독
| 파일 | 칸 | 장 | 프롬프트 |
|---|---|---|---|
| `Assets/_JW/Effects/Status/status_poison.png` | 32×32 | 6 (반복) | `poison status effect, small green toxic bubbles rising and popping, seamless 6 frame loop` |
| `Assets/_JW/Resources/StatusIcons/Poison.png` | 16×16 | 1 | `tiny 16x16 pixel art status icon, green poison droplet with a skull hint, dark outline, same style as simple RPG status icons, transparent background` |

## 스킬 아이콘 (17개)
`Assets/_JW/Resources/SkillIcons/{스킬 ID}.png`, 24×24 캔버스 가운데에 **20×20 정사각 그림**(자체 테두리 없음, 꽉 찬 배경). HUD 슬롯 테두리는 게임이 그린다.

공통 문구:
```
pixel art skill icon, 20x20 square, filled dark background, no border, no frame, no text, single clear symbol in the center, limited palette, crisp pixels, fantasy RPG mage skill
```
| 파일 | 프롬프트 (배경색, 그림) |
|---|---|
| py.magic_missile | `dark navy background, a blue-white arcane missile with a short trail` |
| py.fireball | `dark navy background, an orange fireball with flames` |
| py.ice_spear | `dark navy background, a pale blue ice spear pointing up-right` |
| py.chain_lightning | `dark navy background, a forked yellow lightning bolt linking two dots` |
| py.meteor | `dark red-brown background, a flaming meteor falling diagonally` |
| py.blizzard | `dark red-brown background, a white snowflake with swirling wind` |
| py.venom_mist | `dark red-brown background, a green toxic cloud with bubbles` |
| py.orb_create | `dark gold-brown background, a single glowing golden orb with sparkles` |
| py.orb_shot | `dark gold-brown background, a golden orb flying with a speed trail` |
| py.orb_barrage | `dark gold-brown background, three golden orbs flying in a fan` |
| py.orb_burst | `dark gold-brown background, a golden orb exploding into a ring` |
| py.orb_shield | `dark gold-brown background, a blue round barrier around a small gold orb` |
| py.hex | `dark purple background, a purple curse eye sigil` |
| py.snake_bind | `dark purple background, a green snake coiled in a ring` |
| py.flash | `dark purple background, a white-yellow starburst flash` |
| py.blink | `dark teal background, a mage silhouette dissolving into sparkles with an arrow` |
| py.levitate | `dark teal background, a feather above a glowing ring` |

배경색은 계열 구분이다: 원소 = 남색, 범위 = 적갈색, 구슬 = 금갈색, 보조 = 보라, 이동 = 청록.
