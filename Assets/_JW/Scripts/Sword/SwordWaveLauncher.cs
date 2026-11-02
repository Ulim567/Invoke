using UnityEngine;

/// <summary>
/// 검기를 만들고 없애는 컴포넌트 (#163). 플레이어 프리팹에 붙이고 Inspector에 검기 프리팹 두 개를 연결한다.
/// <para>
/// - 검기는 SpawnWave로만 만들고 DespawnWave로만 없앤다 (코드 작성 규칙 3). 둘 다 NetworkSpawner를 거친다.<br/>
/// - 검기 프리팹은 NetworkIdentity가 있어야 하고 NetworkManager의 Registered Spawnable Prefabs에 등록되어 있어야 한다 (place-scenes.md).
/// </para>
/// </summary>
public class SwordWaveLauncher : MonoBehaviour
{
	[Tooltip("검 휘두르기의 검기 (SwordWave)")]
	[SerializeField] SwordWave wavePrefab;
	[Tooltip("검 크게베기의 큰 검기 (SwordWave_Wide)")]
	[SerializeField] SwordWave wideWavePrefab;

	/// <summary>
	/// 검기를 플레이어가 속한 장소 씬에 만든다. wide면 큰 검기. 프리팹이 없거나 만들지 못하면 null (원인은 로그).
	/// </summary>
	public SwordWave SpawnWave(bool wide, Vector2 position)
	{
		SwordWave prefab = wide ? wideWavePrefab : wavePrefab;
		if (prefab == null)
		{
			Debug.LogError($"[SwordWaveLauncher] {(wide ? "Wide Wave Prefab" : "Wave Prefab")}이 비어 있습니다.", this);
			return null;
		}
		GameObject spawned = NetworkSpawner.Spawn(prefab.gameObject, position, Quaternion.identity, gameObject.scene);
		return spawned != null ? spawned.GetComponent<SwordWave>() : null;
	}

	/// <summary>검기를 없앤다. null이면 아무 일도 없다.</summary>
	public void DespawnWave(SwordWave wave)
	{
		if (wave != null) NetworkSpawner.Despawn(wave.gameObject);
	}
}
