using UnityEngine;

/// <summary>
/// [표시] 몬스터 체력 바 그림 하나 (#388). 프리팹 Resources/MonsterHealthBar의 최상위에 붙는다 (메뉴 Invoke → JW → 몬스터 체력 바 프리팹 만들기로 만듦).
/// 테두리는 이 오브젝트의 SpriteRenderer, 빨간 채움과 흰 잔상은 자식이다. 채움·잔상 그림은 기준점이 왼쪽이라 가로 크기만 바꿔 줄인다.
/// 값은 MonsterHealthBarView가 정해서 넘긴다.
/// </summary>
public class MonsterHealthBar : MonoBehaviour
{
	/// <summary>안쪽 칸의 가로 픽셀 수. 채움·잔상이 꽉 찼을 때 너비</summary>
	public const int InnerPixels = 20;

	[SerializeField] Transform fill;
	[SerializeField] Transform trail;

	/// <summary>빨간 채움과 흰 잔상의 너비를 픽셀 단위(0~20)로 정한다.</summary>
	public void SetPixels(int fillPixels, int trailPixels)
	{
		SetWidth(fill, fillPixels);
		SetWidth(trail, trailPixels);
	}

	static void SetWidth(Transform part, int pixels)
	{
		if (part == null) return;
		pixels = Mathf.Clamp(pixels, 0, InnerPixels);
		part.gameObject.SetActive(pixels > 0);
		part.localScale = new Vector3((float)pixels / InnerPixels, 1f, 1f);
	}
}
