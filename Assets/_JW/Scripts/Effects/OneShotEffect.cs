using UnityEngine;

/// <summary>
/// [표시] 한 번 재생하고 사라지는 이펙트. 이펙트 프리팹에 붙인다. Lifetime(초)이 지나면 스스로 없앤다.
/// 보여 주기만 하는 오브젝트(네트워크 오브젝트 아님)에만 쓴다.
/// </summary>
public class OneShotEffect : MonoBehaviour
{
	[Tooltip("이 시간(초)이 지나면 없앤다. 애니메이션 길이에 맞춘다")]
	[SerializeField, Min(0.01f)] float lifetime = 0.25f;

	void Start() => Destroy(gameObject, lifetime);
}
