using Mirror;
using UnityEngine;

/// <summary>
/// 부딪힌 상대가 자기와 같은 씬에 있는지 콘솔에 남기는 테스트 컴포넌트. 씬마다 물리 세계가 분리되었는지 확인할 때 쓴다.
/// <para>
/// 사용법<br/>
/// - 테스트 오브젝트(Rigidbody2D, Collider2D가 붙은 프리팹)에 붙인다.<br/>
/// - 서버에서만 기록한다. 같은 씬의 Collider와 부딪히면 일반 로그, 다른 씬의 Collider와 부딪히면 오류 로그를 남긴다.
/// </para>
/// </summary>
public class SceneCollisionLogger : MonoBehaviour
{
	void OnCollisionEnter2D(Collision2D collision)
	{
		// 씬마다 물리 세계를 나누는 곳은 서버뿐이므로 서버의 충돌만 확인한다
		if (!NetworkServer.active) return;

		GameObject other = collision.collider.gameObject;
		string myScene = gameObject.scene.name;

		if (other.scene == gameObject.scene)
			Debug.Log($"[SceneCollisionLogger] [{myScene}] {name}이(가) 같은 씬의 {other.name}와(과) 부딪혔습니다.");
		else
			Debug.LogError($"[SceneCollisionLogger] [{myScene}] {name}이(가) 다른 씬({other.scene.name})의 {other.name}와(과) 부딪혔습니다. 물리 세계가 분리되지 않았습니다.");
	}
}
