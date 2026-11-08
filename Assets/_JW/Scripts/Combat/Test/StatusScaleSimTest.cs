using UnityEngine;

/// <summary>
/// 상태 이상 그림 크기 테스트 (#456). 아무 씬의 아무 오브젝트에 붙이고 Play (Host 필요 없음). 콘솔에 「[StatusScaleTest] … 통과/실패」가 찍힌다.
/// <para>
/// 크기가 (1.5, 1)인 부모(Leak처럼) 아래에 StatusEffectView.InverseScale을 적용한 자식이 월드에서 (1, 1) 크기인지 본다. 크기 2, 0.5, 뒤집힌(-1.5) 부모도 확인한다.
/// </para>
/// </summary>
public class StatusScaleSimTest : MonoBehaviour
{
	int failures;

	void Start()
	{
		CheckScale(new Vector3(1.5f, 1f, 1f), "Leak처럼 (1.5, 1)");
		CheckScale(new Vector3(2f, 2f, 1f), "(2, 2)");
		CheckScale(new Vector3(0.5f, 0.75f, 1f), "(0.5, 0.75)");
		CheckScale(new Vector3(-1.5f, 1f, 1f), "뒤집힌 (-1.5, 1)");

		if (failures == 0) Debug.Log("[StatusScaleTest] 전체 통과");
		else Debug.LogError($"[StatusScaleTest] 실패 {failures}개");
	}

	void CheckScale(Vector3 parentScale, string label)
	{
		var parent = new GameObject("ScaleTestParent");
		parent.transform.localScale = parentScale;
		var child = new GameObject("ScaleTestChild");
		child.transform.SetParent(parent.transform, false);
		child.transform.localScale = StatusEffectView.InverseScale(parent.transform);
		Vector3 world = child.transform.lossyScale;
		Check($"{label} 부모 아래 그림이 원래 크기 (지금 {Mathf.Abs(world.x):0.###}, {Mathf.Abs(world.y):0.###})",
			Mathf.Abs(Mathf.Abs(world.x) - 1f) < 0.001f && Mathf.Abs(Mathf.Abs(world.y) - 1f) < 0.001f);
		Destroy(parent);
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[StatusScaleTest] {label}: 통과");
		else { failures++; Debug.LogError($"[StatusScaleTest] {label}: 실패"); }
	}
}
