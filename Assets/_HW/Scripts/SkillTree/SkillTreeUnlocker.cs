using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 스킬 트리 「해금」 요청을 서버에 보내고 결과를 받는다 (「재료 써서 스킬 트리 해금하기」). 플레이어 프리팹 최상위에 PlayerProgress와 함께 붙인다.
/// <para>
/// - 내 캐릭터(소유 클라이언트)만 SkillTreeView.UnlockRequested(노드 ID)를 받아 서버에 보낸다 ([Command]).<br/>
/// - 서버는 PlayerProgress.TryUnlock으로 선행·재료를 검사하고, 되면 재료를 빼고 노드(같이 열리는 노드 포함)를 넣어 DB에 저장한다.<br/>
/// - 결과는 요청한 클라이언트에만 알린다 (UnlockAnswered). 해금 목록은 PlayerProgress의 동기화로 오고, 화면은 PlayerProgress.Changed로 다시 그린다.
/// </para>
/// </summary>
public class SkillTreeUnlocker : NetworkBehaviour
{
	/// <summary>[표시] 내 해금 요청의 결과 (노드 ID, 해금했으면 true). 내 화면에서만 불린다</summary>
	public static event Action<string, bool> UnlockAnswered;

	PlayerProgress progress;

	void Awake() => progress = GetComponent<PlayerProgress>();

	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();
		SkillTreeView.UnlockRequested += RequestUnlock;
	}

	public override void OnStopLocalPlayer()
	{
		base.OnStopLocalPlayer();
		SkillTreeView.UnlockRequested -= RequestUnlock;
	}

	void OnDestroy() => SkillTreeView.UnlockRequested -= RequestUnlock;

	void RequestUnlock(string nodeId)
	{
		if (string.IsNullOrEmpty(nodeId)) return;
		CmdUnlock(nodeId);
	}

	// 노드 ID는 클라이언트가 보낸 값이라 TryUnlock이 선행·재료를 다시 검사한다
	[Command]
	void CmdUnlock(string nodeId)
	{
		bool ok = progress != null && progress.TryUnlock(nodeId);
		Debug.Log($"[SkillTreeUnlocker] {name}: {nodeId} 해금 {(ok ? "성공" : "실패")}");
		TargetUnlockAnswered(connectionToClient, nodeId, ok);
	}

	[TargetRpc]
	void TargetUnlockAnswered(NetworkConnectionToClient target, string nodeId, bool ok) => UnlockAnswered?.Invoke(nodeId, ok);
}
