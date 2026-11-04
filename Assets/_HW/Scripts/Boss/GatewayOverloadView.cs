using Mirror;
using UnityEngine;

/// <summary>
/// 보스 Gateway의 전체 공격 화면 연출. 연출만 한다. Gateway 프리팹(김도훈) 최상위에 붙인다.
/// <para>
/// - <c>GatewayPhases.ChargeEffectPlayed</c>(모으는 시간) → 위쪽에 「전체 공격! 초록 안전지대로 피하세요」와 남은 시간.<br/>
/// - <c>GatewayPhases.OverloadEffectPlayed</c> → 화면 번쩍임. <c>Monster.DeathEffectPlayed</c> → 경고 닫기 (김도훈, monster.md).<br/>
/// - Charge 모션은 GatewayAnimator가 한다. 화면 연출(Screen Prefab)은 처음 쓸 때 한 번만 만들고, 보스가 없어질 때 함께 없앤다 (CLAUDE.md 코드 작성 규칙 3, 4).<br/>
/// - 내 캐릭터가 다른 씬(다른 던전)에 있으면 보이지 않는다.
/// </para>
/// </summary>
public class GatewayOverloadView : MonoBehaviour
{
	[SerializeField, Tooltip("화면 연출 프리팹 (OverloadScreen)")]
	OverloadScreen screenPrefab;

	GatewayPhases phases;
	Monster monster;
	OverloadScreen screen;

	void Awake()
	{
		phases = GetComponent<GatewayPhases>();
		monster = GetComponent<Monster>();
	}

	void OnEnable()
	{
		if (phases != null)
		{
			phases.ChargeEffectPlayed += OnCharge;
			phases.OverloadEffectPlayed += OnOverload;
		}
		if (monster != null) monster.DeathEffectPlayed += OnDeath;
	}

	void OnDisable()
	{
		if (phases != null)
		{
			phases.ChargeEffectPlayed -= OnCharge;
			phases.OverloadEffectPlayed -= OnOverload;
		}
		if (monster != null) monster.DeathEffectPlayed -= OnDeath;
		if (screen != null) screen.HideWarning();
	}

	void OnDestroy()
	{
		if (screen != null) Destroy(screen.gameObject);
	}

	void OnCharge(float seconds)
	{
		if (IsMyScene() && Screen() != null) screen.ShowWarning(seconds);
	}

	void OnOverload()
	{
		if (IsMyScene() && Screen() != null) screen.Flash();
	}

	void OnDeath()
	{
		if (screen != null) screen.HideWarning();
	}

	// 시험: Play 중 Gateway를 고르고 Inspector ⋮ 메뉴에서 누른다
	[ContextMenu("시험: 모으기 3초")]
	void TestCharge() => OnCharge(3f);

	[ContextMenu("시험: 터짐")]
	void TestOverload() => OnOverload();

	// 화면 연출. 처음 쓸 때 한 번만 프리팹에서 만든다
	OverloadScreen Screen()
	{
		if (screen == null && screenPrefab != null) screen = Instantiate(screenPrefab);
		return screen;
	}

	// 내 캐릭터가 이 보스와 같은 씬에 있으면 true (네트워크 없이 시험할 때도 true)
	bool IsMyScene()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		return localPlayer == null || localPlayer.gameObject.scene == gameObject.scene;
	}
}
