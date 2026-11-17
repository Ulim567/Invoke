using UnityEngine;

/// <summary>
/// [표시] 구슬 보호막의 그림을 보여 준다. OrbShield와 같은 오브젝트에 붙인다.
/// <para>
/// - OrbShield.Changed를 받아 켜져 있는 동안 몸 둘레에 보호막 그림을 띄우고, BlockEffectPlayed에 막는 번쩍임을 한 번 튼다.<br/>
/// - 프리팹은 비워 두면 Resources/Magic/orb_shield_zone(보호막), Resources/Magic/orb_shield_hit(번쩍임)을 쓴다. 없으면 그리지 않는다.<br/>
/// - 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 각 화면이 직접 만들고 없앤다.
/// </para>
/// </summary>
[RequireComponent(typeof(OrbShield))]
public class OrbShieldView : MonoBehaviour
{
	[Tooltip("보호막 그림 프리팹 (반복). 비워 두면 Resources/Magic/orb_shield_zone")]
	[SerializeField] GameObject shieldPrefab;
	[Tooltip("막는 순간 번쩍임 프리팹 (한 번). 비워 두면 Resources/Magic/orb_shield_hit")]
	[SerializeField] GameObject blockPrefab;

	OrbShield shield;
	Collider2D body;
	GameObject shown;

	/// <summary>보호막 그림이 떠 있으면 true. 테스트가 읽는다.</summary>
	public bool IsShown => shown != null;

	void Awake()
	{
		shield = GetComponent<OrbShield>();
		body = GetComponent<Collider2D>();
		if (shieldPrefab == null) shieldPrefab = Resources.Load<GameObject>("Magic/orb_shield_zone");
		if (blockPrefab == null) blockPrefab = Resources.Load<GameObject>("Magic/orb_shield_hit");
	}

	void OnEnable()
	{
		shield.Changed += OnChanged;
		shield.BlockEffectPlayed += OnBlocked;
		OnChanged(shield.IsActive);
	}

	void OnDisable()
	{
		shield.Changed -= OnChanged;
		shield.BlockEffectPlayed -= OnBlocked;
		OnChanged(false);
	}

	Vector3 BodyCenter => body != null ? body.bounds.center : transform.position + Vector3.up;

	// 보호막 그림은 캐릭터의 자식으로 두어 같이 움직인다
	void OnChanged(bool on)
	{
		if (on && shown == null && shieldPrefab != null)
		{
			shown = Instantiate(shieldPrefab, transform);
			shown.transform.position = BodyCenter;
		}
		else if (!on && shown != null)
		{
			Destroy(shown);
			shown = null;
		}
	}

	void OnBlocked()
	{
		if (blockPrefab == null) return;
		GameObject flash = Instantiate(blockPrefab, BodyCenter, Quaternion.identity, null);
		if (flash.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(flash, gameObject.scene);
	}
}
