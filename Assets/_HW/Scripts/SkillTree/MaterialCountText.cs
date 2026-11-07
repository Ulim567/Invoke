using Mirror;
using TMPro;
using UnityEngine;

/// <summary>
/// 내 캐릭터가 가진 재료 개수 글자 (스킬 트리 창 오른쪽 위 「버그 조각 34」, 시안 9). 이우림의 PlayerProgress를 읽기만 한다.
/// 내 캐릭터가 없으면(서버에 접속 전) 0으로 보인다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class MaterialCountText : MonoBehaviour
{
	[SerializeField, Tooltip("재료 ID (bug_fragment 등)")]
	string materialId = SkillTreeDefaults.BugFragment;

	[SerializeField, Tooltip("앞에 붙일 이름")]
	string label = "버그 조각";

	TMP_Text text;
	int shown = -1;

	void Awake() => text = GetComponent<TMP_Text>();

	void Update()
	{
		int count = 0;
		if (NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out PlayerProgress progress))
			count = progress.GetMaterialCount(materialId);
		if (count == shown) return;
		shown = count;
		text.text = $"{label}  <color=#FFFFFF>{count}</color>";
	}
}
