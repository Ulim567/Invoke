using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 맞으면 받은 데미지를 머리 위에 숫자로 띄우는 컴포넌트. 연출만 한다. 연습장 허수아비, 잡몹, 보스에 붙인다 (#442).
/// <para>
/// 사용법: Monster와 같은 오브젝트에 붙인다. Number Prefab을 비워 두면 Resources/DamageNumber(박진우, 픽셀 숫자)를 쓴다.<br/>
/// - <c>Monster.HitEffectPlayed</c>(받은 데미지, 김도훈)를 받는다. 체력이 줄지 않는 허수아비(invulnerable)도 이 event는 불린다 (#214).<br/>
/// - 맞을 때 같은 오브젝트의 StatusEffects에 표식이 있으면 표식 맞힘(치명타) 숫자로, Critical Scale배 크게 띄운다 (#442).<br/>
/// - 데미지가 클수록 숫자를 조금 크게 띄운다 (Big Damage에서 최대 Big Damage Scale배).<br/>
/// - 숫자는 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 각 화면에서 이 컴포넌트가 프리팹으로 만든다 (place-scenes.md 「네트워크 오브젝트」). 몬스터와 같은 씬에 만든다(몬스터 자식 아님).<br/>
/// - 만들기는 Spawn 한 곳, 없애기는 DamageNumber가 스스로 한다 (CLAUDE.md 코드 작성 규칙 3, 4).
/// </para>
/// </summary>
public class DamageNumberView : MonoBehaviour
{
	[SerializeField, Tooltip("데미지 숫자 프리팹 (DamageNumber). 비워 두면 Resources/DamageNumber")]
	DamageNumber numberPrefab;

	[SerializeField, Min(0f), Tooltip("콜라이더 윗변에서 이만큼 위에 띄운다 (유닛). 체력 바(0.6)·상태 아이콘(1.4)보다 위")]
	float heightAboveTop = 2.2f;

	[SerializeField, Min(0f), Tooltip("연달아 맞을 때 숫자가 겹치지 않게 좌우로 흩뜨리는 폭 (유닛)")]
	float jitter = 0.4f;

	[SerializeField, Min(1f), Tooltip("표식 맞힘(치명타) 숫자 크기 배율")]
	float criticalScale = 1.3f;

	[SerializeField, Min(1f), Tooltip("이 데미지 이상이면 숫자가 가장 크다")]
	int bigDamage = 200;

	[SerializeField, Min(1f), Tooltip("큰 데미지 숫자 크기 배율 (데미지에 따라 1 ~ 이 값)")]
	float bigDamageScale = 1.4f;

	Monster monster;
	Collider2D body;
	StatusEffects status;

	void Awake()
	{
		monster = GetComponent<Monster>();
		body = GetComponent<Collider2D>();
		if (body == null) body = GetComponentInChildren<Collider2D>();
		status = GetComponent<StatusEffects>();
		if (numberPrefab == null) numberPrefab = Resources.Load<DamageNumber>("DamageNumber");
	}

	void OnEnable()
	{
		if (monster != null) monster.HitEffectPlayed += Spawn;
	}

	void OnDisable()
	{
		if (monster != null) monster.HitEffectPlayed -= Spawn;
	}

	/// <summary>
	/// 데미지 숫자 하나를 머리 위에 띄운다. 표식이 걸려 있으면 표식 맞힘(치명타) 숫자.
	/// </summary>
	public void Spawn(int damage)
	{
		if (numberPrefab == null) return;
		float top = body != null ? body.bounds.max.y : transform.position.y;
		float centerX = body != null ? body.bounds.center.x : transform.position.x;
		Vector3 position = new Vector3(centerX + Random.Range(-jitter, jitter), top + heightAboveTop, 0f);
		DamageNumber number = Instantiate(numberPrefab, position, Quaternion.identity);
		if (gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(number.gameObject, gameObject.scene);

		bool critical = status != null && status.Has(StatusType.Mark);
		float scale = Mathf.Lerp(1f, bigDamageScale, Mathf.Clamp01((float)damage / bigDamage));
		if (critical) scale *= criticalScale;
		number.Show(damage, critical, scale);
	}
}
