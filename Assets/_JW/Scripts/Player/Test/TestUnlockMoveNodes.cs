using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// [테스트 전용, #322] 서버(Host)에 들어온 모든 플레이어에게 더블점프 노드(선행인 대시와 함께)를 해금해 준다.
/// <para>
/// - 공용 Player에 PlayerProgress가 붙은 뒤(#303) 테스트 씬에서도 해금 목록을 따르게 되어, 해금 없이는 Space 더블점프를 확인할 수 없어서 둔다.<br/>
/// - 서버에서만 동작한다. 접속한 Client의 캐릭터도 서버가 해금해 주므로, 빌드 Client 쪽에는 아무것도 하지 않아도 된다.<br/>
/// - 재료(bug_fragment)를 노드 비용만큼 더한 뒤 PlayerProgress.TryUnlock으로 연다 (progress-store.md 「진행도 컴포넌트」, 「노드 해금 검사」). 이미 열린 노드는 건너뛴다.<br/>
/// - 「로그인 건너뛰기」 임시 계정은 메모리에만 두므로 DB가 바뀌지 않는다. 로그인한 실제 계정이면 DB에 저장되니 주의.<br/>
/// - 씬을 바꿔도(장소 이동) 계속 동작하게 DontDestroyOnLoad로 둔다. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class TestUnlockMoveNodes : MonoBehaviour
{
	[Tooltip("해금 비용으로 넉넉히 더해 줄 버그 조각 개수")]
	[SerializeField, Min(0)] int fragments = 50;
	[Tooltip("새 플레이어를 찾는 간격 (초)")]
	[SerializeField, Min(0.1f)] float interval = 1f;

	const string Fragment = "bug_fragment";

	// 해금을 시도한 플레이어와 그 결과(성공 true). 실패한 플레이어는 로그가 쌓이지 않게 다시 시도하지 않는다.
	// 성공했는데 나중에 해금 목록에서 사라지면(진행도를 다시 불러옴 등) 알리고 다시 연다
	readonly Dictionary<PlayerProgress, bool> tried = new Dictionary<PlayerProgress, bool>();
	// 내 캐릭터 화면에서 본 더블점프 해금 여부. 바뀔 때만 로그를 남긴다
	bool? lastLocalUnlocked;

	IEnumerator Start()
	{
		DontDestroyOnLoad(gameObject);
		var wait = new WaitForSeconds(interval);
		while (true)
		{
			if (NetworkServer.active)
			{
				foreach (PlayerProgress progress in FindObjectsByType<PlayerProgress>()) UnlockFor(progress);
			}
			LogLocalPlayer();
			yield return wait;
		}
	}

	void UnlockFor(PlayerProgress progress)
	{
		if (Has(progress, PredefinedFunctionIds.DoubleJump)) return;
		if (tried.TryGetValue(progress, out bool succeeded))
		{
			if (!succeeded) return;
			Debug.LogWarning($"[TestUnlock] {progress.name}: 열어 둔 더블점프가 해금 목록에서 사라짐 (진행도를 다시 불러온 듯). 다시 연다");
		}

		if (progress.GetMaterialCount(Fragment) < fragments) progress.AddMaterial(Fragment, fragments);
		Unlock(progress, PredefinedFunctionIds.Dash);
		Unlock(progress, PredefinedFunctionIds.DoubleJump);
		tried[progress] = Has(progress, PredefinedFunctionIds.DoubleJump);
	}

	// 내 캐릭터(Host 또는 빌드 Client)가 실제로 더블점프를 쓸 수 있다고 보는지. 서버에서 열어도 내 화면에 안 오면 여기서 false로 보인다
	void LogLocalPlayer()
	{
		if (NetworkClient.localPlayer == null || !NetworkClient.localPlayer.TryGetComponent(out PlayerMovement movement)) return;
		bool now = movement.DoubleJumpUnlocked;
		if (lastLocalUnlocked == now) return;
		lastLocalUnlocked = now;
		Debug.Log($"[TestUnlock] 내 캐릭터 {movement.name}: DoubleJumpUnlocked = {now}");
	}

	static void Unlock(PlayerProgress progress, string nodeId)
	{
		if (Has(progress, nodeId)) return;
		UnlockResult result = progress.TryUnlock(nodeId);
		Debug.Log($"[TestUnlock] {progress.name}: {nodeId} → {result}");
	}

	static bool Has(PlayerProgress progress, string nodeId)
	{
		foreach (string id in progress.Unlocked) if (id == nodeId) return true;
		return false;
	}
}
