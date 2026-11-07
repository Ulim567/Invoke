using UnityEngine;

/// <summary>
/// [표시] 소환 계열 스킬이 맞는 순간의 작은 이펙트를 튼다. 플레이어 프리팹에 SkillEffectEvents와 함께 붙인다 (#290, 박진우 요청 초안).
/// <para>
/// - 표식 남기기: <c>SkillEffectEvents.Hit</c>마다 (소환검이 좌우에서 찌를 때 검마다 한 번) 맞은 자리에 작은 찌르기 섬광. 대상 쪽을 향한다.<br/>
/// - 검무: <c>SkillEffectEvents.Hit</c>마다 (소환검 하나가 벨 때마다) 맞은 자리에 작은 베기 궤적. 대상 바깥쪽으로 휜다.<br/>
/// - 패링: <c>ParryGuard.ParryEffectPlayed</c>(잡은 위치)에 번쩍임. 반격 검기는 원래 있는 SwordWave가 나온다.<br/>
/// - 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 각 화면이 직접 만들고, 이펙트 프리팹의 OneShotEffect가 0.25초 뒤 스스로 없앤다 (ThrustEffectView와 같은 방식).<br/>
/// - 이펙트 프리팹과 이 컴포넌트는 메뉴 Invoke → 이펙트 → 스킬 이펙트 붙이기(SkillEffectBuilder)가 만들고 붙인다.
/// </para>
/// </summary>
[RequireComponent(typeof(SkillEffectEvents))]
public class SkillHitEffectView : MonoBehaviour
{
	[Tooltip("표식 남기기: 소환검이 찌른 순간 작은 섬광 (오른쪽을 향한 그림, 가운데가 기준점)")]
	[SerializeField] GameObject markFlashPrefab;
	[Tooltip("검무: 소환검이 벤 순간 작은 궤적 (오른쪽으로 휜 그림, 가운데가 기준점)")]
	[SerializeField] GameObject danceSlashPrefab;
	[Tooltip("패링: 막은 순간 번쩍임")]
	[SerializeField] GameObject parryFlashPrefab;

	[Tooltip("켜면 이펙트를 만들 때마다 Console에 남긴다 (확인용)")]
	[SerializeField] bool logEffects;

	SkillEffectEvents events;
	ParryGuard guard;

	void Awake()
	{
		events = GetComponent<SkillEffectEvents>();
		guard = GetComponent<ParryGuard>();
	}

	void OnEnable()
	{
		events.Hit += OnHit;
		if (guard != null) guard.ParryEffectPlayed += OnParried;
	}

	void OnDisable()
	{
		events.Hit -= OnHit;
		if (guard != null) guard.ParryEffectPlayed -= OnParried;
	}

	void OnHit(string skillId, GameObject target, Vector2 point, DamageResult result)
	{
		if (logEffects && (skillId == PredefinedFunctionIds.Mark || skillId == PredefinedFunctionIds.SwordDance))
			Debug.Log($"[SkillHitEffectView] {skillId} 맞음 → 이펙트 ({point})");
		switch (skillId)
		{
			case PredefinedFunctionIds.Mark:
				// 대상 왼쪽에서 찔렀으면 오른쪽을 향하고, 오른쪽에서 찔렀으면 뒤집는다
				int dir = target != null && BodyCenter(target).x < point.x ? -1 : 1;
				Spawn(markFlashPrefab, point, 0f, dir);
				break;
			case PredefinedFunctionIds.SwordDance:
				// 대상 가운데에서 맞은 자리 쪽으로 궤적이 휘게 돌린다
				Vector2 outward = target != null ? point - BodyCenter(target) : Vector2.right;
				float angle = outward.sqrMagnitude > 0.0001f ? Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg : 0f;
				Spawn(danceSlashPrefab, point, angle, 1);
				break;
		}
	}

	void OnParried(Vector2 point)
	{
		if (logEffects) Debug.Log($"[SkillHitEffectView] 패링 → 이펙트 ({point})");
		Spawn(parryFlashPrefab, point, 0f, 1);
	}

	void Spawn(GameObject prefab, Vector2 point, float angle, int dir)
	{
		if (prefab == null) return;
		GameObject effect = Instantiate(prefab, point, Quaternion.Euler(0f, 0f, angle), null);
		// 플레이어가 있는 장소 씬에 둔다 (Instantiate는 활성 씬에 만들기 때문)
		if (effect.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
		Vector3 scale = effect.transform.localScale;
		scale.x = Mathf.Abs(scale.x) * dir;
		effect.transform.localScale = scale;
	}

	static Vector2 BodyCenter(GameObject obj)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)obj.transform.position;
	}
}
