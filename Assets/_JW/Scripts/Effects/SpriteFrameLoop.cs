using UnityEngine;

/// <summary>
/// [표시] 스프라이트 프레임을 정해진 속도로 돌려 보여 준다 (#363). Animator 없이 쓰는 간단한 반복 재생. 효과 프리팹의 SpriteRenderer 옆에 붙인다.
/// <para>
/// - Frames를 순서대로 Frames Per Second로 보여 준다. 끝나면 처음으로 돌아간다.<br/>
/// - Play Once Count가 0보다 크면 앞의 그 장수는 처음에 한 번만 보여 주고, 그 뒤 나머지만 반복한다 (예: 이속 감소 얼음이 자라난 뒤 반짝임만 반복).<br/>
/// - 켜질 때마다 처음부터 다시 재생한다. 보여 주기만 하는 오브젝트에 쓴다.
/// </para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameLoop : MonoBehaviour
{
	[Tooltip("순서대로 보여 줄 프레임 (스프라이트 시트를 잘라 넣는다)")]
	[SerializeField] Sprite[] frames;
	[Tooltip("초당 프레임 수")]
	[SerializeField, Min(1f)] float framesPerSecond = 12f;
	[Tooltip("앞의 이 장수는 처음에 한 번만 보여 주고, 그 뒤 나머지만 반복한다. 0이면 전부 반복")]
	[SerializeField, Min(0)] int playOnceCount;

	SpriteRenderer spriteRenderer;
	float startTime;

	/// <summary>지금 보여 주는 프레임 번호 (0부터). 테스트가 읽는다.</summary>
	public int CurrentFrame { get; private set; }

	void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

	void OnEnable()
	{
		startTime = Time.time;
		CurrentFrame = -1;
		UpdateFrame();
	}

	void Update() => UpdateFrame();

	void UpdateFrame()
	{
		if (frames == null || frames.Length == 0) return;
		int once = Mathf.Clamp(playOnceCount, 0, frames.Length - 1);
		int step = Mathf.FloorToInt((Time.time - startTime) * framesPerSecond);
		int index = step < once ? step : once + (step - once) % (frames.Length - once);
		if (index == CurrentFrame) return;
		CurrentFrame = index;
		spriteRenderer.sprite = frames[index];
	}
}
