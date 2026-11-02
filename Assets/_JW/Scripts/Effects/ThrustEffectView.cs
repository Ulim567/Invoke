using UnityEngine;

/// <summary>
/// [표시] 찌르기 계열 스킬의 섬광을 튼다. 플레이어 프리팹에 SkillEffectEvents와 함께 붙인다 (#152).
/// <para>
/// - SkillEffectEvents.Fired(판정이 나간 순간)를 받아, 찌르는 방향으로 섬광 프리팹을 하나 만든다. 빗나가도 나온다.<br/>
/// - 검 찌르기·집중찌르기: Thrust Flash (임혜원 #114), 꼬챙이: Skewer Flash (더 긴 섬광).<br/>
/// - 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 각 화면이 직접 만들고, 섬광 프리팹의 OneShotEffect가 스스로 없앤다 (place-scenes.md).
/// </para>
/// </summary>
[RequireComponent(typeof(SkillEffectEvents))]
public class ThrustEffectView : MonoBehaviour
{
	[Tooltip("검 찌르기·집중찌르기 섬광 프리팹 (왼쪽 끝이 기준점, 오른쪽으로 뻗음)")]
	[SerializeField] GameObject thrustFlashPrefab;
	[Tooltip("꼬챙이 섬광 프리팹")]
	[SerializeField] GameObject skewerFlashPrefab;

	SkillEffectEvents events;

	void Awake() => events = GetComponent<SkillEffectEvents>();

	void OnEnable() => events.Fired += OnFired;

	void OnDisable() => events.Fired -= OnFired;

	void OnFired(string skillId, Vector2 origin, Vector2 direction)
	{
		GameObject prefab = skillId switch
		{
			PredefinedFunctionIds.Thrust => thrustFlashPrefab,
			PredefinedFunctionIds.FocusedThrust => thrustFlashPrefab,
			PredefinedFunctionIds.Skewer => skewerFlashPrefab,
			_ => null,
		};
		if (prefab != null) SpawnFlash(prefab, origin, direction.x < 0f ? -1 : 1);
	}

	// 섬광은 오른쪽으로 뻗는 그림이라, 왼쪽으로 찌를 때는 좌우를 뒤집는다
	void SpawnFlash(GameObject prefab, Vector2 origin, int dir)
	{
		GameObject flash = Instantiate(prefab, origin, Quaternion.identity, null);
		SceneLink(flash);
		Vector3 scale = flash.transform.localScale;
		scale.x = Mathf.Abs(scale.x) * dir;
		flash.transform.localScale = scale;
	}

	// 플레이어가 있는 장소 씬에 둔다 (Instantiate는 활성 씬에 만들기 때문)
	void SceneLink(GameObject obj)
	{
		if (obj.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, gameObject.scene);
	}
}
