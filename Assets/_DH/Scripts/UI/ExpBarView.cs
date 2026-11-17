using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 왼쪽 위 Lv 옆의 경험치 바. 내 캐릭터의 경험치를 읽어서 보여 주기만 한다 (값은 바꾸지 않는다).
/// <para>
/// - 비율: PlayerProgress.Exp(이우림) / LevelRules.ExpToNext(Level). 최대 레벨이면 가득 찬다.<br/>
/// - 경험치가 늘면 연한 막대(Ghost)가 먼저 새 위치까지 바로 차고, Delay초 뒤 색 막대(Fill)가 Speed로 서서히 따라 찬다 (HP 잔상의 반대).<br/>
/// - 레벨이 오르면 색 막대가 끝까지 찬 뒤 0부터 다시 남은 경험치까지 찬다. 여러 레벨이 한 번에 올라도 한 바퀴씩 돈다.<br/>
/// - 처음 보일 때, 내 캐릭터가 바뀐 뒤 Snap Time초(접속 직후 서버에서 진행도를 불러오는 동안), 경험치가 줄었을 때는 기다리지 않고 바로 맞춘다.
/// </para>
/// <para>
/// 사용법: HUD 빌더(HudUIBuilder)가 Lv 글자 옆 막대 오브젝트에 붙이고 Fill·Ghost를 연결한다. 두 Image는 Filled, Horizontal, Left이고 Ghost가 Fill 뒤에 그려진다.
/// </para>
/// </summary>
public class ExpBarView : MonoBehaviour
{
	[SerializeField, Tooltip("서서히 따라 차는 색 막대 (Filled, Horizontal, Left)")]
	Image fill;

	[SerializeField, Tooltip("먼저 바로 차는 연한 막대. Fill 뒤에 둔다 (Filled, Horizontal, Left)")]
	Image ghost;

	[SerializeField, Min(0f), Tooltip("경험치를 얻은 뒤 색 막대가 따라 차기 시작할 때까지 기다리는 시간(초)")]
	float delay = 0.3f;

	[SerializeField, Min(0.01f), Tooltip("색 막대가 차는 빠르기 (막대 한 개 = 1, 초당)")]
	float speed = 1.5f;

	[SerializeField, Min(0f), Tooltip("내 캐릭터가 생긴 뒤 이 시간(초) 동안은 바로 맞춘다. 접속할 때 불러온 레벨이 레벨업처럼 차오르지 않게")]
	float snapTime = 1f;

	[SerializeField, Tooltip("색 막대 색 (메이플스토리 경험치 바처럼 연두)")]
	Color fillColor = new Color32(0x9B, 0xE1, 0x5D, 0xFF);

	[SerializeField, Tooltip("먼저 차는 막대 색")]
	Color ghostColor = new Color32(0xEF, 0xFF, 0xD9, 0xFF);

	GameObject player;
	PlayerProgress progress;
	// 레벨 + 막대 비율로 센 위치 (예: 3.25 = 3레벨에서 25%). 보이는 위치가 목표를 따라간다
	float shown;
	float waitUntil;
	bool waiting;
	float snapUntil;

	void Awake()
	{
		if (fill != null) fill.color = fillColor;
		if (ghost != null) ghost.color = ghostColor;
	}

	void Update()
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (me != player)
		{
			player = me;
			progress = player != null ? player.GetComponent<PlayerProgress>() : null;
			snapUntil = Time.unscaledTime + snapTime;
		}
		if (progress == null) return;

		float target = Target();
		float now = Time.unscaledTime;

		if (target < shown || now < snapUntil) Snap(target);
		else if (target > shown)
		{
			// 처음 늘어난 순간부터 기다린다. 기다리는 중에 또 늘어도 시작 시각은 그대로
			if (!waiting)
			{
				waiting = true;
				waitUntil = now + delay;
			}
			if (now >= waitUntil)
			{
				shown = Mathf.MoveTowards(shown, target, speed * Time.unscaledDeltaTime);
				if (shown >= target) waiting = false;
			}
		}

		Draw(target);
	}

	// 지금 레벨 + 이번 레벨에서 찬 비율. 최대 레벨은 가득 (레벨 + 1)
	float Target()
	{
		int level = progress.Level;
		if (level >= LevelRules.MaxLevel) return level + 1f;
		int need = LevelRules.ExpToNext(level);
		return need > 0 ? level + Mathf.Clamp01((float)progress.Exp / need) : level + 1f;
	}

	void Snap(float target)
	{
		shown = target;
		waiting = false;
	}

	void Draw(float target)
	{
		int targetLevel = Mathf.FloorToInt(target);
		int shownLevel = Mathf.FloorToInt(shown);
		bool maxed = progress.Level >= LevelRules.MaxLevel;

		// 색 막대: 보이는 위치의 소수 부분. 레벨을 넘는 순간 0부터 다시 찬다. 목표에 닿은 최대 레벨은 가득
		float fillAmount = maxed && shown >= target ? 1f : shown - shownLevel;
		// 연한 막대: 아직 이전 레벨 바퀴면 끝까지, 같은 레벨이면 목표 위치
		float ghostAmount = maxed || shownLevel < targetLevel ? 1f : target - targetLevel;

		if (fill != null && fill.fillAmount != fillAmount) fill.fillAmount = fillAmount;
		if (ghost != null && ghost.fillAmount != ghostAmount) ghost.fillAmount = ghostAmount;
	}
}
