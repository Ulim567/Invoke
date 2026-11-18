using System.Collections.Generic;

// 스킬 41개의 설명 (박진우). C 24개, Python 17개. 칸의 뜻과 쓰는 규칙은 ItemDescription과 docs/systems/item-descriptions.md를 본다.
// - Slots(슬롯)를 비워 두면 파라미터 이름의 뜻(ItemDescriptions.ParameterMeaningOf)이 보인다. 스킬마다 다르게 쓰고 싶으면 적는다
// - 예시 코드는 그 스킬을 쓰는 캐릭터의 언어로 적는다: C 스킬은 한 줄 C, Python 스킬은 Python (줄바꿈은 \n, 들여쓰기는 칸 4개)
// - 수치(사거리, 시간, 배율)는 SkillTable과 각 스킬 코드의 임시 값이다. 값을 바꾸면 여기 문장도 같이 고친다
// - 대시·더블점프는 키보드 고정 이동기로 바뀔 예정이라 하는 일만 둔다
public static partial class ItemDescriptions
{
	static readonly Dictionary<string, ItemDescription> SkillEntries = new Dictionary<string, ItemDescription>
	{
		// 찌르기 ---------------------------------------------------------
		[PredefinedFunctionIds.Thrust] = new ItemDescription
		{
			Summary = "대상 한 명을 검으로 찌릅니다.",
			Purpose = "가까운 몬스터 한 마리를 가장 기본적인 공격으로 때리고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터가 쓰러질 때까지 계속 찌르도록 만들 수 있습니다.",
			ExampleCode = "while (is_valid_target(get_bug())) { thrust(get_bug()); }",
			ExampleResult = "고른 몬스터가 쓰러질 때까지 검 찌르기를 반복합니다.",
			Caution = "대상이 사거리(2.5칸)보다 멀면 대상 앞까지 달려가서 찌릅니다. 위·아래 발판에 있는 대상은 맞지 않습니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.FocusedThrust] = new ItemDescription
		{
			Summary = "대상 한 명을 찌르고, 대상이 출혈 중이면 더 큰 피해를 줍니다.",
			Purpose = "출혈을 건 몬스터에게 피해를 몰아주고 싶을 때 씁니다.",
			Example = "예를 들면, 검 크게베기로 출혈을 건 뒤 바로 집중찌르기로 이어 갈 수 있습니다.",
			ExampleCode = "wide_slash(); focused_thrust(get_bug());",
			ExampleResult = "앞쪽 몬스터들에 출혈이 걸리고, 고른 몬스터는 1.5배 피해를 받습니다.",
			Caution = "출혈이 없는 대상에게는 검 찌르기와 같은 피해입니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.VitalStrike] = new ItemDescription
		{
			Summary = "미니게임으로 급소를 노려 큰 피해를 줍니다.",
			Purpose = "강한 한 방과 함께 실명이나 이동 속도 감소를 걸고 싶을 때 씁니다.",
			Example = "예를 들면, 슬롯 키로 대상을 고르는 동안 미니게임에서 눈을 맞힌 뒤 찌를 수 있습니다.",
			ExampleCode = "vital_strike(get_bug());",
			ExampleResult = "눈을 맞히면 3배 피해와 실명, 발목을 맞히면 3배 피해와 이동 속도 감소가 걸립니다.",
			Caution = "미니게임은 슬롯 키로 대상을 고르는 동안에만 합니다. 미니게임을 안 하거나 실패해도 기본 피해는 줍니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.Skewer] = new ItemDescription
		{
			Summary = "대상과 그 뒤에 선 적들을 한 번에 꿰뚫어 찌릅니다.",
			Purpose = "한 줄로 늘어선 몬스터 여러 마리를 한꺼번에 맞히고 싶을 때 씁니다.",
			Example = "예를 들면, 맨 앞의 몬스터를 골라 뒤에 줄 선 몬스터까지 함께 찌를 수 있습니다.",
			ExampleCode = "skewer(get_bug());",
			ExampleResult = "고른 몬스터와, 찌르는 방향으로 그 뒤 3칸 안의 몬스터가 모두 맞습니다.",
			Caution = "한 마리가 받는 피해는 검 찌르기의 절반입니다. 처음 고른 대상이 맞아야 뒤쪽도 맞습니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.SummonedThrust] = new ItemDescription
		{
			Summary = "소환검을 날려 대상을 찌르게 합니다.",
			Purpose = "캐릭터는 그 자리에 둔 채 소환검으로 공격하고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검을 하나 만들어 그 검으로 고른 몬스터를 찌를 수 있습니다.",
			ExampleCode = "Sword* s = summon_sword(); summoned_thrust(get_bug(), s);",
			ExampleResult = "새로 만든 소환검이 날아가 몬스터를 찌르고 곁으로 돌아옵니다.",
			Caution = "sword를 비워 두면 가장 먼저 소환한 쓸 수 있는 검을 씁니다. 넘긴 검이 던져져 있거나 다른 일을 하는 중이면 아무것도 하지 않습니다. 쓸 수 있는 소환검이 없으면 NoSummonedSwordError로 실패합니다.",
		},
		[PredefinedFunctionIds.WoundExploit] = new ItemDescription
		{
			Summary = "대상 한 명을 찌르고, 출혈 중이면 집중찌르기보다 더 큰 피해를 줍니다.",
			Purpose = "출혈 중인 몬스터에게 가장 센 찌르기를 하고 싶을 때 씁니다.",
			Example = "예를 들면, 검 크게베기로 출혈을 건 뒤 상처를 노려 찌를 수 있습니다.",
			ExampleCode = "wide_slash(); wound_exploit(get_bug());",
			ExampleResult = "출혈 중인 몬스터가 2배 피해를 받습니다.",
			Caution = "출혈이 없는 대상에게는 검 찌르기와 같은 피해입니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.DeepThrust] = new ItemDescription
		{
			Summary = "대상 한 명을 깊이 찌르고, 가끔 치명타로 큰 피해를 줍니다.",
			Purpose = "운에 따라 한 번에 큰 피해를 노리고 싶을 때 씁니다.",
			Example = "예를 들면, 같은 몬스터를 세 번 깊이찔러 치명타를 노릴 수 있습니다.",
			ExampleCode = "for (int i = 0; i < 3; i++) { deep_thrust(get_bug()); }",
			ExampleResult = "찌를 때마다 30% 확률로 치명타가 나서 2.5배 피해를 줍니다.",
			Caution = "치명타가 안 나면 검 찌르기와 같은 피해입니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},

		// 던지기 ---------------------------------------------------------
		[PredefinedFunctionIds.SwordThrow] = new ItemDescription
		{
			Summary = "들고 있는 검을 던져 대상 한 명을 맞힙니다.",
			Purpose = "조금 떨어진 몬스터를 맞히거나, 체력이 거의 없는 몬스터를 바로 처치하고 싶을 때 씁니다.",
			Example = "예를 들면, 거의 쓰러진 몬스터를 골라 검을 던져 마무리할 수 있습니다.",
			ExampleCode = "sword_throw(get_bug());",
			ExampleResult = "5칸 안의 대상이면 맞고, 체력이 아주 조금 남은 일반 몬스터는 바로 처치됩니다.",
			Caution = "던진 검은 그 자리에 떨어지고, Z로 다시 주울 때까지 손에 검이 없어 검이 필요한 스킬을 쓸 수 없습니다. 대상이 5칸보다 멀면 빗나갑니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.SummonedThrow] = new ItemDescription
		{
			Summary = "소환검을 던져 더 먼 적까지 맞힙니다.",
			Purpose = "손에 든 검을 놓지 않고 먼 몬스터를 맞히고 싶을 때 씁니다.",
			Example = "예를 들면, 먼 몬스터에게 소환검을 던지고 검 돌아오기로 돌려받을 수 있습니다.",
			ExampleCode = "summoned_throw(get_bug()); sword_return();",
			ExampleResult = "소환검이 몬스터에 꽂혔다가, 돌아오는 길에 지나가는 몬스터를 한 번 더 벱니다.",
			Caution = "8칸보다 먼 대상은 맞지 않고 소환검이 벽이나 땅에 꽂힙니다. 던진 소환검은 돌려받기 전까지 다른 스킬에 쓸 수 없습니다. 쓸 수 있는 소환검이 없으면 NoSummonedSwordError로 실패합니다.",
		},
		[PredefinedFunctionIds.SwordReturn] = new ItemDescription
		{
			Summary = "던진 소환검을 먼저 던진 것부터 하나씩 돌려받습니다.",
			Purpose = "던져 둔 소환검을 다시 쓰고 싶을 때 씁니다.",
			Example = "예를 들면, 던져 둔 소환검 두 개를 모두 돌려받을 수 있습니다.",
			ExampleCode = "sword_return(); sword_return();",
			ExampleResult = "소환검이 하나씩 돌아오며 길에 있는 몬스터를 한 번씩 벱니다.",
			Caution = "들고 있던 검(물리 검)은 돌려받지 않습니다. 물리 검은 Z로 줍습니다. 돌려받을 소환검이 없으면 아무 일도 없이 끝납니다. 손에 검이 없어도 쓸 수 있습니다.",
		},

		// 휘두르기 -------------------------------------------------------
		[PredefinedFunctionIds.SwordSlash] = new ItemDescription
		{
			Summary = "검을 휘둘러 대상 쪽으로 검기를 날립니다.",
			Purpose = "다가가지 않고 먼 몬스터를 맞히고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터가 살아 있을 때만 검기를 날릴 수 있습니다.",
			ExampleCode = "if (is_valid_target(get_bug())) { sword_slash(get_bug()); }",
			ExampleResult = "검기가 날아가 처음 닿은 몬스터 한 마리를 맞힙니다.",
			Caution = "검기는 날아가는 동안 heap 1칸을 쓰고, 사라지면 돌려줍니다. heap이 모자라면 검기를 날리지 않습니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.WideSlash] = new ItemDescription
		{
			Summary = "앞쪽으로 넓게 베어 맞은 적들에게 출혈을 겁니다.",
			Purpose = "앞에 모인 몬스터 여러 마리에 출혈을 걸고 싶을 때 씁니다.",
			Example = "예를 들면, 넓게 벤 뒤 출혈 보너스를 받는 찌르기를 이어서 쓸 수 있습니다.",
			ExampleCode = "wide_slash(); focused_thrust(get_bug());",
			ExampleResult = "바라보는 쪽 몬스터들이 약한 피해와 출혈을 받고, 고른 몬스터는 1.5배 찌르기에 맞습니다.",
			Caution = "대상을 고르지 않고 바라보는 방향으로 날립니다. 피해는 검 휘두르기의 절반입니다. heap이 모자라면 검기를 날리지 않습니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.SummonedSlash] = new ItemDescription
		{
			Summary = "쓸 수 있는 소환검마다 검기를 하나씩 대상에게 날립니다.",
			Purpose = "소환검이 많을 때 검기 여러 개를 한꺼번에 날리고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검 두 개를 만든 뒤 함께 검기를 날릴 수 있습니다.",
			ExampleCode = "summon_sword(); summon_sword(); summoned_slash(get_bug());",
			ExampleResult = "소환검 두 개가 각자 고른 몬스터 쪽으로 검기를 날립니다.",
			Caution = "검기 하나가 heap 1칸을 따로 씁니다. heap이 모자라면 날릴 수 있는 만큼만 날립니다. 쓸 수 있는 소환검이 없으면 NoSummonedSwordError로 실패합니다.",
		},
		[PredefinedFunctionIds.WoundSlash] = new ItemDescription
		{
			Summary = "검기를 날려, 맞은 적의 출혈 피해를 키웁니다.",
			Purpose = "출혈이 걸린 몬스터의 출혈 피해를 더 키우고 싶을 때 씁니다.",
			Example = "예를 들면, 검 크게베기로 출혈을 건 뒤 상처를 갈라 출혈 피해를 두 배로 만들 수 있습니다.",
			ExampleCode = "wide_slash(); wound_slash(get_bug());",
			ExampleResult = "맞은 몬스터의 출혈 피해가 그 출혈이 끝날 때까지 2배가 됩니다.",
			Caution = "검기 피해 자체는 커지지 않습니다. 출혈이 없는 대상에게는 검 휘두르기와 같습니다. heap이 모자라면 검기를 날리지 않습니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.MultiSlash] = new ItemDescription
		{
			Summary = "쓸 수 있는 소환검이 차례로 날아가 대상을 한 번씩 벱니다.",
			Purpose = "소환검으로 한 몬스터에게 여러 번 피해를 주고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검 세 개를 만든 뒤 한 몬스터를 연달아 벨 수 있습니다.",
			ExampleCode = "for (int i = 0; i < 3; i++) { summon_sword(); } multi_slash(get_bug());",
			ExampleResult = "소환검 세 개가 짧은 간격으로 날아가 각각 한 번씩 벱니다.",
			Caution = "소환검이 많을수록 여러 번 벱니다. 스킬이 끝날 때까지 닿지 못한 검은 피해 없이 돌아옵니다. 쓸 수 있는 소환검이 없으면 NoSummonedSwordError로 실패합니다.",
		},
		[PredefinedFunctionIds.BattlefieldSweep] = new ItemDescription
		{
			Summary = "고른 적마다 소환검이 하나씩 날아가 벱니다.",
			Purpose = "여러 몬스터를 소환검으로 나눠 공격하고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터 두 마리를 골라 소환검을 하나씩 보낼 수 있습니다.",
			ExampleCode = "Target bugs[2]; bugs[0] = get_bug(); bugs[1] = get_bug(); battlefield_sweep(bugs);",
			ExampleResult = "먼저 소환한 검부터 각 몬스터에게 날아가 한 번씩 벱니다.",
			Caution = "대상이 소환검보다 많으면 남는 대상은 베지 않습니다. 같은 몬스터를 두 번 넣어도 한 번만 벱니다. 슬롯에서 쓸 때는 대상을 여러 명 찍습니다. 쓸 수 있는 소환검이 없으면 NoSummonedSwordError로 실패합니다.",
		},

		// 소환 -----------------------------------------------------------
		[PredefinedFunctionIds.SummonSword] = new ItemDescription
		{
			Summary = "검 하나를 불러내 소환검으로 만들고, 그 검의 포인터를 돌려줍니다.",
			Purpose = "소환검이 필요한 스킬을 쓰기 전에 검을 준비할 때 씁니다.",
			Example = "예를 들면, 소환검을 만들어 variable에 담아 두고 그 검으로 찌를 수 있습니다.",
			ExampleCode = "Sword* s = summon_sword(); summoned_thrust(get_bug(), s);",
			ExampleResult = "곁에 소환검이 하나 생기고, 그 검이 고른 몬스터를 찌릅니다.",
			Returns = "만든 소환검의 포인터입니다. 만들지 못하면 NULL입니다.",
			Caution = "소환검은 누수된 검까지 합쳐 최대 5개입니다. 5개거나 heap이 모자라면 만들지 않고 NULL을 돌려줍니다. 스킬이 도중에 끊기면 만든 검은 누수됩니다. 손에 검이 없어도 쓸 수 있습니다.",
		},
		[PredefinedFunctionIds.FreeSword] = new ItemDescription
		{
			Summary = "소환검 하나를 해제해 heap을 돌려받습니다.",
			Purpose = "다 쓴 소환검을 치워 heap과 소환검 자리를 비우고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검으로 찌른 뒤 그 검을 바로 해제할 수 있습니다.",
			ExampleCode = "Sword* s = summon_sword(); summoned_thrust(get_bug(), s); free_sword(s);",
			ExampleResult = "소환검이 몬스터를 찌른 뒤 사라지고, 그 검이 쓰던 heap이 돌아옵니다.",
			Caution = "sword를 비워 두면 마지막에 소환한 검부터 해제합니다. 들고 있는 검과 누수된 검은 해제할 수 없습니다. 이미 해제한 포인터를 넘기면 InvalidPointerError로 실패합니다. 해제할 소환검이 없으면 NoSummonedSwordError로 실패합니다.",
		},
		[PredefinedFunctionIds.Parry] = new ItemDescription
		{
			Summary = "잠깐 동안 날아오는 공격을 받아쳐 되돌려 줍니다.",
			Purpose = "몬스터가 쏜 투사체를 막고 반격하고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터가 투사체를 쏘기 직전에 패링을 쓸 수 있습니다.",
			ExampleCode = "parry();",
			ExampleResult = "0.3초 안에 투사체가 닿으면 막고, 날아온 쪽으로 검기를 날려 반격합니다.",
			Caution = "누른 뒤 0.3초 동안만 막고, 한 번 막으면 끝납니다. 몸통 박치기와 낙하 공격은 막지 못합니다. 반격 검기는 heap 1칸을 씁니다. 손에 검이 없으면 UnarmedError로 실패합니다.",
		},
		[PredefinedFunctionIds.Mark] = new ItemDescription
		{
			Summary = "소환검 두 개로 대상 양옆을 찔러 표식을 남기고 잠시 기절시킵니다.",
			Purpose = "몬스터를 잠깐 멈추고 다음 공격의 피해를 키우고 싶을 때 씁니다.",
			Example = "예를 들면, 표식을 남긴 뒤 바로 찔러 2배 피해를 줄 수 있습니다.",
			ExampleCode = "mark(get_bug()); thrust(get_bug());",
			ExampleResult = "몬스터가 1초 동안 기절하고, 이어지는 검 찌르기가 2배 피해를 줍니다.",
			Caution = "쓸 수 있는 소환검이 2개 있어야 합니다. 모자라면 NoSummonedSwordError로 실패합니다. 두 검이 모두 닿아야 표식과 기절이 걸립니다. 표식은 이번 찌르기가 아니라 다음 공격부터 붙습니다.",
		},
		[PredefinedFunctionIds.SwordDance] = new ItemDescription
		{
			Summary = "소환검들이 대상 둘레를 돌며 베어 출혈을 겁니다.",
			Purpose = "소환검이 많을 때 한 몬스터에게 출혈과 여러 번의 피해를 주고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검을 모두 보내 보스 둘레를 돌며 벨 수 있습니다.",
			ExampleCode = "sword_dance(get_bug());",
			ExampleResult = "소환검마다 약 세 번씩 베고 출혈을 겁니다.",
			Caution = "한 번 베는 피해는 검 찌르기의 절반입니다. 스킬이 끝나면 검이 곁으로 돌아옵니다. 쓸 수 있는 소환검이 없으면 NoSummonedSwordError로 실패합니다.",
		},

		// 이동 -----------------------------------------------------------
		[PredefinedFunctionIds.Dash] = new ItemDescription
		{
			Summary = "바라보는 쪽으로 짧은 거리를 빠르게 이동합니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.DoubleJump] = new ItemDescription
		{
			Summary = "공중에서 한 번 더 점프합니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.SwordRide] = new ItemDescription
		{
			Summary = "소환검을 타고 공중을 날아다닙니다.",
			Purpose = "높은 곳으로 올라가거나 낭떠러지를 건너고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검을 하나 만든 뒤 바로 타고 날 수 있습니다.",
			ExampleCode = "summon_sword(); sword_ride();",
			ExampleResult = "발밑에 소환검이 붙고 30초 동안 ←→↑↓로 날 수 있습니다.",
			Caution = "대시, 장소 이동, 피격으로 타기가 먼저 끝나도 검은 곁으로 돌아옵니다. 발판 아래로는 ↓ + Space로 내려갑니다. 쓸 수 있는 소환검이 없으면 NoSummonedSwordError로 실패합니다.",
		},

		// 파이썬 마법사 (#523, 박진우) ----------------------------------------------
		// 구슬
		[PredefinedFunctionIds.PyOrbCreate] = new ItemDescription
		{
			Summary = "마나 구슬 하나를 만들어 머리 위에 띄우고, 그 구슬을 돌려줍니다.",
			Purpose = "구슬 스킬에 넘길 구슬을 만들 때 씁니다.",
			Example = "예를 들면, 구슬을 만들어 variable에 담아 둘 수 있습니다.",
			ExampleCode = "orb = orb_create()",
			ExampleResult = "머리 위에 마나 구슬이 하나 생기고, orb에 담겨 있는 동안 사라지지 않습니다.",
			Returns = "만든 구슬 (Orb). 구슬이 이미 5개면 None (다 쓴 구슬도 셈)",
			Caution = "돌려받은 구슬을 variable이나 list에 담지 않으면 바로 사라집니다. heap이 모자라면 MemoryError가 발생합니다.",
		},
		[PredefinedFunctionIds.PyOrbShot] = new ItemDescription
		{
			Summary = "넘긴 마나 구슬 하나를 써서 대상에게 쏩니다.",
			Purpose = "만들어 둔 구슬로 매직 미사일보다 센 마법탄을 쏘고 싶을 때 씁니다.",
			Example = "예를 들면, 구슬을 만들어 바로 고른 몬스터에게 쏠 수 있습니다.",
			ExampleCode = "orb = orb_create()\norb_shot(get_bug(), orb)",
			ExampleResult = "구슬이 떠 있던 자리에서 마법탄이 날아가 처음 닿은 몬스터를 맞히고, 그 구슬은 빨간 다 쓴 구슬이 됩니다.",
			Caution = "쓴 구슬은 빨간 「다 쓴 구슬」이 되어 다시 쓸 수 없습니다(UsedManaOrbError). is_valid_orb(orb)로 확인합니다. 다 쓴 구슬도 5개에 들어가므로, 담은 variable이나 list에서 놓아야 사라집니다.",
		},
		[PredefinedFunctionIds.PyOrbBarrage] = new ItemDescription
		{
			Summary = "구슬 list의 쓸 수 있는 구슬을 모두 써서, 대상 list의 몬스터들에게 하나씩 쏩니다.",
			Purpose = "구슬을 모아 두었다가 여러 몬스터에게 한꺼번에 쏘고 싶을 때 씁니다.",
			Example = "예를 들면, 구슬 세 개를 list에 모은 뒤 몬스터 두 마리에게 나눠 쏠 수 있습니다.",
			ExampleCode = "orbs = []\nfor i in range(0, 3):\n    orbs.append(orb_create())\ntargets = []\ntargets.append(get_bug())\ntargets.append(get_bug())\norb_barrage(targets, orbs)",
			ExampleResult = "구슬 세 개가 짧은 간격으로 하나씩 날아갑니다. 구슬이 대상보다 많으면 처음 대상부터 다시 돌아가며 쏩니다.",
			Caution = "다 쓴 구슬은 건너뛰고 list에서 빼지 않습니다. 쏘기 전에 쓰러진 대상은 건너뜁니다. 쓸 구슬이 없으면 NoManaOrbError가 발생합니다.",
		},
		[PredefinedFunctionIds.PyOrbBurst] = new ItemDescription
		{
			Summary = "구슬 list의 쓸 수 있는 구슬을 모두 터뜨려 둘레의 적을 모두 맞힙니다.",
			Purpose = "둘러싸였을 때 모아 둔 구슬로 주변을 한 번에 쓸어버리고 싶을 때 씁니다.",
			Example = "예를 들면, 구슬 다섯 개를 모아 두었다가 한꺼번에 터뜨릴 수 있습니다.",
			ExampleCode = "orbs = []\nfor i in range(0, 5):\n    orbs.append(orb_create())\norb_burst(orbs)",
			ExampleResult = "캐릭터 둘레 4칸 안의 몬스터가 모두 맞습니다. 터뜨린 구슬이 다섯 개라 피해도 다섯 배입니다.",
			Caution = "구슬이 많을수록 셉니다. 쓸 구슬이 없으면 NoManaOrbError가 발생합니다.",
		},
		[PredefinedFunctionIds.PyOrbShield] = new ItemDescription
		{
			Summary = "넘긴 마나 구슬 하나를 써서 다음 피해 한 번을 막는 보호막을 두릅니다.",
			Purpose = "보스의 큰 공격을 한 번 버티고 싶을 때 씁니다.",
			Example = "예를 들면, 구슬을 만들어 바로 보호막으로 쓸 수 있습니다.",
			ExampleCode = "orb = orb_create()\norb_shield(orb)",
			ExampleResult = "몸에 보호막이 생기고, 10초 안에 받는 첫 피해 한 번을 막습니다.",
			Caution = "보호막은 10초 동안 유지되고, 이미 있으면 시간만 새로 시작합니다. 다 쓴 구슬을 넘기면 UsedManaOrbError가 발생합니다.",
		},
		// 원소
		[PredefinedFunctionIds.PyMagicMissile] = new ItemDescription
		{
			Summary = "마법탄을 쏴 처음 닿은 적을 맞힙니다.",
			Purpose = "구슬 없이 쓸 수 있는 가장 기본적인 공격을 하고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터가 쓰러질 때까지 계속 쏘도록 만들 수 있습니다.",
			ExampleCode = "b = get_bug()\nwhile is_valid_target(b):\n    magic_missile(b)",
			ExampleResult = "고른 몬스터가 쓰러질 때까지 매직 미사일을 반복해서 쏩니다.",
			Caution = "가는 길에 다른 몬스터가 있으면 그 몬스터가 먼저 맞습니다. heap이 모자라면 쏘지 않고 끝납니다.",
		},
		[PredefinedFunctionIds.PyFireball] = new ItemDescription
		{
			Summary = "불덩이를 쏴 닿은 적과 그 둘레의 적을 맞힙니다.",
			Purpose = "모여 있는 몬스터들을 한 번에 맞히고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터 무리의 가운데를 골라 불덩이를 쏠 수 있습니다.",
			ExampleCode = "fireball(get_bug())",
			ExampleResult = "닿은 몬스터가 맞고, 그 둘레 2칸 안의 다른 몬스터도 절반 피해를 받습니다.",
			Caution = "처음 닿은 몬스터에서 터집니다. heap이 모자라면 쏘지 않고 끝납니다.",
		},
		[PredefinedFunctionIds.PyIceSpear] = new ItemDescription
		{
			Summary = "얼음 창을 쏴 지나가는 적을 모두 맞히고 느리게 만듭니다.",
			Purpose = "한 줄로 다가오는 몬스터들을 맞히고 발을 묶고 싶을 때 씁니다.",
			Example = "예를 들면, 다가오는 몬스터 줄을 향해 얼음 창을 쏠 수 있습니다.",
			ExampleCode = "ice_spear(get_bug())",
			ExampleResult = "얼음 창이 몬스터를 꿰뚫고 날아가며, 맞은 몬스터는 모두 3초 동안 느려집니다.",
			Caution = "한 마리가 받는 피해는 매직 미사일보다 약합니다. heap이 모자라면 쏘지 않고 끝납니다.",
		},
		[PredefinedFunctionIds.PyChainLightning] = new ItemDescription
		{
			Summary = "번개가 대상에게 바로 닿고 가까운 다른 적으로 튑니다.",
			Purpose = "흩어진 몬스터 여러 마리를 빠르게 맞히고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터 무리 중 하나를 골라 번개를 튀길 수 있습니다.",
			ExampleCode = "chain_lightning(get_bug())",
			ExampleResult = "고른 몬스터가 바로 맞고, 4칸 안의 다른 몬스터로 두 번 튀며 약한 피해를 줍니다.",
			Caution = "9칸보다 먼 대상에게는 닿지 않습니다. 튈 몬스터가 없으면 고른 대상만 맞습니다.",
		},
		// 범위
		[PredefinedFunctionIds.PyMeteor] = new ItemDescription
		{
			Summary = "대상 발밑에 운석을 떨어뜨려 둘레의 적을 모두 맞힙니다.",
			Purpose = "잘 움직이지 않는 몬스터 무리에게 큰 피해를 주고 싶을 때 씁니다.",
			Example = "예를 들면, 뱀 속박으로 묶어 둔 몬스터에게 운석을 떨어뜨릴 수 있습니다.",
			ExampleCode = "b = get_bug()\nsnake_bind(b)\nmeteor(b)",
			ExampleResult = "몬스터가 기절한 사이 운석이 떨어져, 그 둘레 3칸 안의 몬스터가 모두 2배 피해를 받습니다.",
			Caution = "운석은 시전하고 조금 뒤에 떨어집니다. 그 사이 대상이 움직이면 빗나갈 수 있습니다.",
		},
		[PredefinedFunctionIds.PyBlizzard] = new ItemDescription
		{
			Summary = "대상 자리에 눈보라를 깔아 안의 적을 계속 맞히고 느리게 만듭니다.",
			Purpose = "몬스터가 지나갈 자리를 막아 두고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터 무리가 있는 자리에 눈보라를 깔 수 있습니다.",
			ExampleCode = "blizzard(get_bug())",
			ExampleResult = "눈보라가 1초 동안 깔리고, 그 안의 몬스터는 0.2초마다 약한 피해를 받고 느려집니다.",
			Caution = "눈보라는 대상을 따라가지 않고 처음 자리에 남습니다. 한 번에 주는 피해는 약합니다.",
		},
		[PredefinedFunctionIds.PyVenomMist] = new ItemDescription
		{
			Summary = "대상 자리에 독 안개를 깔아 안의 적을 중독시킵니다.",
			Purpose = "시간이 지날수록 커지는 피해를 몬스터 여럿에게 걸고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터 무리에 독 안개를 깔아 두고 다른 공격을 이어 갈 수 있습니다.",
			ExampleCode = "b = get_bug()\nvenom_mist(b)\nmagic_missile(b)",
			ExampleResult = "안개 안의 몬스터가 중독되고, 1초마다 중독 피해를 받는 동안 매직 미사일도 맞습니다.",
			Caution = "중독은 다시 걸리면 겹쳐서(최대 3겹) 더 아픕니다. 안개는 대상을 따라가지 않습니다.",
		},
		// 보조
		[PredefinedFunctionIds.PyHex] = new ItemDescription
		{
			Summary = "저주탄을 쏴 맞은 적에게 표식을 남깁니다.",
			Purpose = "다음 공격의 피해를 크게 키우고 싶을 때 씁니다.",
			Example = "예를 들면, 표식을 남긴 뒤 바로 구슬을 쏠 수 있습니다.",
			ExampleCode = "b = get_bug()\nhex(b)\norb_shot(b, orb_create())",
			ExampleResult = "몬스터에 표식이 남고, 이어지는 구슬 발사가 2배 피해를 줍니다.",
			Caution = "저주탄 자체의 피해는 약합니다. 표식은 5초 동안 남고, 다음 공격부터 붙습니다.",
		},
		[PredefinedFunctionIds.PySnakeBind] = new ItemDescription
		{
			Summary = "뱀을 날려 맞은 적을 기절시킵니다.",
			Purpose = "몬스터를 잠깐 멈춰 공격을 피하거나 큰 마법을 맞히고 싶을 때 씁니다.",
			Example = "예를 들면, 다가오는 몬스터를 묶어 두고 물러날 수 있습니다.",
			ExampleCode = "snake_bind(get_bug())",
			ExampleResult = "처음 닿은 몬스터가 1초 동안 기절합니다.",
			Caution = "뱀이 가는 길에 다른 몬스터가 있으면 그 몬스터가 먼저 맞습니다. 뱀 자체의 피해는 약합니다.",
		},
		[PredefinedFunctionIds.PyFlash] = new ItemDescription
		{
			Summary = "빛을 터뜨려 둘레의 적을 모두 실명시킵니다.",
			Purpose = "둘러싸였을 때 몬스터들의 공격을 잠깐 막고 싶을 때 씁니다.",
			Example = "예를 들면, 섬광으로 주변을 실명시킨 뒤 구슬 폭발을 쓸 수 있습니다.",
			ExampleCode = "flash()",
			ExampleResult = "캐릭터 둘레 5칸 안의 몬스터가 모두 3초 동안 실명해 공격이 빗나갑니다.",
			Caution = "피해는 주지 않습니다.",
		},
		// 이동
		[PredefinedFunctionIds.PyBlink] = new ItemDescription
		{
			Summary = "바라보는 쪽으로 순간이동합니다.",
			Purpose = "몬스터의 공격을 피하거나 빠르게 거리를 벌리고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터를 묶은 뒤 순간이동해서 떨어질 수 있습니다.",
			ExampleCode = "snake_bind(get_bug())\nblink()",
			ExampleResult = "몬스터가 기절한 사이 바라보는 쪽으로 5칸 순간이동합니다.",
			Caution = "벽 앞에서는 멈춥니다. 발판은 지나갑니다. 공중에서도 쓸 수 있습니다.",
		},
		[PredefinedFunctionIds.PyLevitate] = new ItemDescription
		{
			Summary = "잠시 동안 공중에 떠서 위아래 양옆으로 움직입니다.",
			Purpose = "높은 곳으로 올라가거나 낭떠러지를 건너고 싶을 때 씁니다.",
			Example = "예를 들면, 공중에 떠서 높은 발판으로 올라갈 수 있습니다.",
			ExampleCode = "levitate()",
			ExampleResult = "6초 동안 중력 없이 ←→↑↓로 움직입니다. 시간이 다 되면 내려옵니다.",
			Caution = "대시나 피격으로 먼저 내려올 수 있습니다.",
		},
	};
}
