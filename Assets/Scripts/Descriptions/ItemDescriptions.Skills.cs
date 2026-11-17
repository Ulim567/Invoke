using System.Collections.Generic;

// 스킬 41개의 설명 (박진우). C 24개, Python 17개(#523). 칸의 뜻과 쓰는 규칙은 ItemDescription과 docs/systems/item-descriptions.md를 본다.
// - Summary(하는 일)는 예전 스킬 트리 짧은 설명을 옮긴 것이다. 확인하고 고친다
// - 「예전 자세한 설명」 주석은 스킬 트리 설명 칸에 보이던 문단이다. 하는 일·용도·주의할 점으로 나누어 옮긴 뒤 주석을 지운다
// - Slots(슬롯)를 비워 두면 파라미터 이름의 뜻(ItemDescriptions.ParameterMeaningOf)이 보인다. 스킬마다 다르게 쓰고 싶으면 적는다
// - 예시(Example, ExampleCode, ExampleResult)를 비워 두면 참고 카드에 호출 한 줄(skewer(get_bug());)만 보인다
public static partial class ItemDescriptions
{
	static readonly Dictionary<string, ItemDescription> SkillEntries = new Dictionary<string, ItemDescription>
	{
		// 찌르기 ---------------------------------------------------------------
		[PredefinedFunctionIds.Thrust] = new ItemDescription
		{
			// 예전 자세한 설명: 가장 기본이 되는 찌르기입니다. 대상 한 명을 골라 찌릅니다.
			Summary = "대상 한 명을 검으로 찌릅니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.FocusedThrust] = new ItemDescription
		{
			// 예전 자세한 설명: 대상 한 명을 찌릅니다. 대상이 출혈 상태면 더 큰 피해를 줍니다.
			Summary = "출혈 중인 대상을 더 세게 찌릅니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.VitalStrike] = new ItemDescription
		{
			// 예전 자세한 설명: 미니게임에 성공하면 큰 피해를 줍니다. 눈을 맞히면 대상이 실명하고, 발목을 맞히면 대상의 이동 속도가 느려집니다.
			Summary = "미니게임으로 급소를 노려 큰 피해를 줍니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.Skewer] = new ItemDescription
		{
			// 예전 자세한 설명: 찌르기가 대상을 관통해 여러 명을 동시에 찌릅니다. 한 명이 받는 피해는 약합니다.
			Summary = "여러 적을 한꺼번에 꿰뚫어 찌릅니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.SummonedThrust] = new ItemDescription
		{
			// 예전 자세한 설명: 소환해 둔 검이 대상 한 명을 찌릅니다.
			Summary = "소환검에게 명령해 대상을 찌르게 합니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.WoundExploit] = new ItemDescription
		{
			// 예전 자세한 설명: 대상 한 명을 찌릅니다. 대상이 출혈 상태면 검 집중찌르기보다 더 큰 피해를 줍니다.
			Summary = "출혈 중인 대상의 상처를 노려 찌릅니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.DeepThrust] = new ItemDescription
		{
			// 예전 자세한 설명: 대상 한 명을 깊이 찔러 치명타로 큰 피해를 줍니다.
			Summary = "깊숙이 찔러 치명타 피해를 줍니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},

		// 던지기 ---------------------------------------------------------------
		[PredefinedFunctionIds.SwordThrow] = new ItemDescription
		{
			// 예전 자세한 설명: 들고 있는 검을 던집니다. 체력이 아주 조금 남은 적은 바로 처치합니다(보스 제외). 던진 검을 다시 주울 때까지는 손에 검이 없어 공격 스킬을 쓸 수 없습니다.
			Summary = "검을 던져 대상 한 명을 맞춥니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.SummonedThrow] = new ItemDescription
		{
			// 예전 자세한 설명: 소환검을 대상 한 명에게 던집니다. 검 던지기보다 더 멀리 있는 적도 맞힙니다.
			Summary = "소환검을 던져 더 먼 적까지 맞춥니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.SwordReturn] = new ItemDescription
		{
			// 예전 자세한 설명: 던진 소환검을 먼저 던진 것부터 하나씩 되돌려 받습니다. 돌아오는 길에 있는 적에게 피해를 줍니다. 손에 검이 없을 때도 쓸 수 있습니다.
			Summary = "던진 소환검을 차례대로 되돌려 받습니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},

		// 휘두르기 --------------------------------------------------------------
		[PredefinedFunctionIds.SwordSlash] = new ItemDescription
		{
			// 예전 자세한 설명: 대상 한 명에게 검기를 날립니다. 검기는 heap 메모리를 씁니다.
			Summary = "검을 휘둘러 검기를 날립니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.WideSlash] = new ItemDescription
		{
			// 예전 자세한 설명: 넓은 범위에 검기를 날립니다. 피해는 낮지만 맞은 적에게 출혈을 겁니다.
			Summary = "넓게 베어 주변 적에게 출혈을 겁니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.SummonedSlash] = new ItemDescription
		{
			// 예전 자세한 설명: 소환해 둔 검 하나마다 검기를 하나씩 대상에게 날립니다.
			Summary = "소환된 검의 수만큼 검기를 날립니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.WoundSlash] = new ItemDescription
		{
			// 예전 자세한 설명: 대상 한 명을 벱니다. 대상이 출혈 상태면 출혈로 받는 피해가 커집니다.
			Summary = "상처를 갈라 출혈 피해를 키웁니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.MultiSlash] = new ItemDescription
		{
			// 예전 자세한 설명: 소환해 둔 검이 각각 대상 한 명을 한 번씩 벱니다. 검이 많을수록 더 여러 번 벱니다.
			Summary = "소환된 검들이 대상을 한 번씩 벱니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.BattlefieldSweep] = new ItemDescription
		{
			// 예전 자세한 설명: 여러 대상을 고르면, 소환해 둔 검들이 각 대상에게 날아가 벱니다.
			// 대상 array를 받아서 호출 한 줄을 자동으로 만들 수 없다. 예시 코드를 직접 적어야 참고 카드에 예시가 보인다
			Summary = "소환된 검들이 여러 적에게 흩어져 벱니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},

		// 소환 ----------------------------------------------------------------
		[PredefinedFunctionIds.SummonSword] = new ItemDescription
		{
			// 예전 자세한 설명: malloc처럼 검 하나를 만들고 그 검을 가리키는 포인터를 돌려줍니다. 소환검은 최대 5개까지 둘 수 있습니다.
			Summary = "검을 불러내 소환검으로 만듭니다.",
			Returns = "만든 소환검의 포인터입니다. 만들지 못하면 NULL입니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.FreeSword] = new ItemDescription
		{
			// 예전 자세한 설명: free처럼 소환검을 없앱니다. 거리에 상관없이 해제할 수 있지만, 들고 있는 물리 검은 해제할 수 없습니다.
			Summary = "소환검 하나를 해제해 거둡니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.Parry] = new ItemDescription
		{
			// 예전 자세한 설명: 막는 데 성공하면 적의 공격을 검 이펙트로 한 번 되돌려 줍니다. 손에 검이 있어야 쓸 수 있습니다.
			Summary = "적의 공격을 받아쳐 되돌려 줍니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.Mark] = new ItemDescription
		{
			// 예전 자세한 설명: 검이 적의 좌우를 찔러 표식을 남기고 1초 동안 기절시킵니다. 소환검이 있어야 쓸 수 있습니다.
			Summary = "적에게 표식을 남기고 잠시 기절시킵니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.SwordDance] = new ItemDescription
		{
			// 예전 자세한 설명: 검 개수만큼 적 근처에 검을 두고 휘두릅니다. 피해는 낮지만 출혈을 겁니다.
			Summary = "소환검들이 적 곁에서 휘둘러 출혈을 겁니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},

		// 이동 ----------------------------------------------------------------
		[PredefinedFunctionIds.Dash] = new ItemDescription
		{
			// 예전 자세한 설명: 짧은 거리를 빠르게 이동합니다. 모든 캐릭터가 함께 쓰는 이동 기술입니다.
			Summary = "짧은 거리를 빠르게 이동합니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.DoubleJump] = new ItemDescription
		{
			// 예전 자세한 설명: 점프한 뒤 공중에서 한 번 더 점프합니다. 모든 캐릭터가 함께 쓰는 이동 기술입니다.
			Summary = "공중에서 한 번 더 점프합니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.SwordRide] = new ItemDescription
		{
			// 예전 자세한 설명: 소환검 위에 올라타 이동합니다. 쓸 수 있는 소환검이 없으면 쓸 수 없습니다.
			Summary = "소환검을 타고 이동합니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},

		// 파이썬 마법사 (#523, 박진우) ----------------------------------------------
		// 구슬
		[PredefinedFunctionIds.PyOrbCreate] = new ItemDescription
		{
			Summary = "마나 구슬 하나를 만들어 머리 위에 띄우고, 그 구슬을 돌려줍니다.",
			Purpose = "구슬 스킬에 넘길 구슬을 만들 때 씁니다.",
			Example = "",
			ExampleCode = "orb = orb_create()",
			ExampleResult = "",
			Returns = "만든 구슬 (Orb). 구슬이 이미 5개면 None (다 쓴 구슬도 셈)",
			Caution = "돌려받은 구슬을 variable이나 list에 담지 않으면 바로 사라집니다. heap이 모자라면 MemoryError가 발생합니다.",
		},
		[PredefinedFunctionIds.PyOrbShot] = new ItemDescription
		{
			Summary = "넘긴 마나 구슬 하나를 써서 대상에게 쏩니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "orb_shot(get_bug(), orb)",
			ExampleResult = "",
			Caution = "쓴 구슬은 빨간 「다 쓴 구슬」이 되어 다시 쓸 수 없습니다(UsedManaOrbError). is_valid_orb(orb)로 확인합니다. 다 쓴 구슬도 5개에 들어가므로, 담은 variable이나 list에서 놓아야 사라집니다.",
		},
		[PredefinedFunctionIds.PyOrbBarrage] = new ItemDescription
		{
			Summary = "구슬 list의 쓸 수 있는 구슬을 모두 써서, 대상 list의 몬스터들에게 하나씩 쏩니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "orb_barrage(targets, orbs)",
			ExampleResult = "",
			Caution = "다 쓴 구슬은 건너뛰고 list에서 빼지 않습니다. 쓸 구슬이 없으면 NoManaOrbError가 발생합니다.",
		},
		[PredefinedFunctionIds.PyOrbBurst] = new ItemDescription
		{
			Summary = "구슬 list의 쓸 수 있는 구슬을 모두 터뜨려 둘레의 적을 모두 맞힙니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "orb_burst(orbs)",
			ExampleResult = "",
			Caution = "구슬이 많을수록 셉니다. 쓸 구슬이 없으면 NoManaOrbError가 발생합니다.",
		},
		[PredefinedFunctionIds.PyOrbShield] = new ItemDescription
		{
			Summary = "넘긴 마나 구슬 하나를 써서 다음 피해 한 번을 막는 보호막을 두릅니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "orb_shield(orb)",
			ExampleResult = "",
			Caution = "보호막은 10초 동안 유지됩니다. 다 쓴 구슬을 넘기면 UsedManaOrbError가 발생합니다.",
		},
		// 원소
		[PredefinedFunctionIds.PyMagicMissile] = new ItemDescription
		{
			Summary = "마법탄을 쏴 처음 닿은 적을 맞힙니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.PyFireball] = new ItemDescription
		{
			Summary = "불덩이를 쏴 닿은 적과 그 둘레의 적을 맞힙니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.PyIceSpear] = new ItemDescription
		{
			Summary = "얼음 창을 쏴 지나가는 적을 모두 맞히고 느리게 만듭니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.PyChainLightning] = new ItemDescription
		{
			Summary = "번개가 대상에게 바로 닿고 가까운 다른 적으로 튑니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		// 범위
		[PredefinedFunctionIds.PyMeteor] = new ItemDescription
		{
			Summary = "대상 발밑에 운석을 떨어뜨려 둘레의 적을 모두 맞힙니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "운석은 조금 뒤에 떨어집니다.",
		},
		[PredefinedFunctionIds.PyBlizzard] = new ItemDescription
		{
			Summary = "대상 자리에 눈보라를 깔아 안의 적을 계속 맞히고 느리게 만듭니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.PyVenomMist] = new ItemDescription
		{
			Summary = "대상 자리에 독 안개를 깔아 안의 적을 중독시킵니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "중독은 다시 걸리면 겹쳐서 더 아픕니다.",
		},
		// 보조
		[PredefinedFunctionIds.PyHex] = new ItemDescription
		{
			Summary = "저주탄을 쏴 맞은 적에게 표식을 남깁니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "표식이 남은 적은 다음 공격에 더 큰 피해를 받습니다.",
		},
		[PredefinedFunctionIds.PySnakeBind] = new ItemDescription
		{
			Summary = "뱀을 날려 맞은 적을 기절시킵니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
		[PredefinedFunctionIds.PyFlash] = new ItemDescription
		{
			Summary = "빛을 터뜨려 둘레의 적을 모두 실명시킵니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "피해는 주지 않습니다.",
		},
		// 이동
		[PredefinedFunctionIds.PyBlink] = new ItemDescription
		{
			Summary = "바라보는 쪽으로 순간이동합니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "벽 앞에서는 멈춥니다.",
		},
		[PredefinedFunctionIds.PyLevitate] = new ItemDescription
		{
			Summary = "잠시 동안 공중에 떠서 위아래 양옆으로 움직입니다.",
			Purpose = "",
			Example = "",
			ExampleCode = "",
			ExampleResult = "",
			Caution = "",
		},
	};
}
