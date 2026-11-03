using UnityEngine;

/// <summary>
/// 맞으면 받은 데미지를 머리 위에 숫자로 띄우는 컴포넌트. 연출만 한다. 연습장 허수아비에 붙인다 (잡몹·보스에도 붙여도 된다).
/// <para>
/// 사용법: Monster와 같은 오브젝트에 붙이고, Number Prefab에 DamageNumber 프리팹을 넣는다.<br/>
/// - <c>Monster.HitEffectPlayed</c>(받은 데미지, 김도훈)를 받는다. 체력이 줄지 않는 허수아비(invulnerable)도 이 event는 불린다 (#214).<br/>
/// - 숫자는 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 각 화면에서 이 컴포넌트가 프리팹으로 만든다 (place-scenes.md 「네트워크 오브젝트」).<br/>
/// - 만들기는 Spawn 한 곳, 없애기는 DamageNumber가 스스로 한다 (CLAUDE.md 코드 작성 규칙 3, 4).
/// </para>
/// </summary>
public class DamageNumberView : MonoBehaviour
{
	[SerializeField, Tooltip("데미지 숫자 프리팹 (TextMeshPro + DamageNumber)")]
	DamageNumber numberPrefab;

	[SerializeField, Min(0f), Tooltip("콜라이더 윗변에서 이만큼 위에 띄운다 (유닛). 상태 이상 아이콘(0.6)보다 조금 위")]
	float heightAboveTop = 1.4f;

	[SerializeField, Min(0f), Tooltip("연달아 맞을 때 숫자가 겹치지 않게 좌우로 흩뜨리는 폭 (유닛)")]
	float jitter = 0.4f;

	Monster monster;
	Collider2D body;

	void Awake()
	{
		monster = GetComponent<Monster>();
		body = GetComponent<Collider2D>();
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
	/// 데미지 숫자 하나를 머리 위에 띄운다.
	/// </summary>
	public void Spawn(int damage)
	{
		if (numberPrefab == null) return;
		float top = body != null ? body.bounds.max.y : transform.position.y;
		Vector3 position = new Vector3(transform.position.x + Random.Range(-jitter, jitter), top + heightAboveTop, 0f);
		DamageNumber number = Instantiate(numberPrefab, position, Quaternion.identity);
		number.Show(damage);
	}
}
