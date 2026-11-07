using Mirror;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 내 캐릭터가 죽으면 내 화면을 무채색으로 바꾸고 어둡게 하는 컴포넌트 (#424). 보여 주기만 한다.
/// <para>
/// - Base 씬의 Global Volume 오브젝트에 붙인다. 그 Volume의 Profile(Color Adjustments: Saturation -100, Post Exposure를 낮춤)이 실제 색을 바꾸고,
///   이 컴포넌트는 Volume의 weight만 0(평소) ↔ 1(사망)로 바꾼다.<br/>
/// - 내 캐릭터(NetworkClient.localPlayer)의 PlayerHealth.IsDead만 읽는다. 그래서 다른 플레이어가 죽어도 내 화면은 바뀌지 않는다.
///   IsDead는 서버가 정하고 클라이언트에도 맞춰져 있으므로(#310) 네트워크 작업은 없다.<br/>
/// - 레이드 던전에서 죽었으면: 죽은 지 PlayerHealth.RespawnDelay(3초)가 지나면 끈다. 레이드는 부활하지 않고 관전하므로, 관전하는 동안은 정상 색이다.<br/>
/// - 그 밖의 장소에서 죽었으면: 부활할 때까지 켠다. 부활은 로비로 옮겨진 뒤 로딩 화면이 덮인 채로 일어나므로(#391), 로딩 화면이 걷히면 정상 색이다.<br/>
/// - Canvas(HUD 등)는 모두 Screen Space - Overlay라 후처리가 걸리지 않아 그대로 밝게 보인다.<br/>
/// - 레이드 실패 결과 화면이 떠 있는 동안(RaidResultView.MyFailureShowing)은 살아 있든 죽었든 화면 효과를 켠다. 먼저 죽어 관전하던 파티원, 레이드를 포기한 파티원,
///   마지막에 죽은 파티원의 화면이 모두 같게 무채색이 된다.<br/>
/// - 사망 글자(「사망했습니다」): Death Text 칸에 넣은 CanvasGroup의 alpha. 화면 효과와 같은 때에 나타나고 사라지지만, 레이드 실패 결과 화면이 뜨면 숨긴다(결과 화면과 겹치지 않게). 비워 두면 화면 효과만 건다.<br/>
/// - 메인 카메라(Camera 프리팹)의 Post Processing이 켜져 있어야 보인다.
/// </para>
/// </summary>
[RequireComponent(typeof(Volume))]
public class DeathScreenEffect : MonoBehaviour
{
	[SerializeField, Min(0.01f), Tooltip("효과가 켜지고 꺼지는 데 걸리는 시간(초). 로딩 화면 페이드와 같은 값")]
	float fadeTime = 0.25f;

	[SerializeField, Tooltip("사망 글자(「사망했습니다」)의 부모에 붙인 CanvasGroup. 화면 효과와 같이 나타나고 사라진다. 비워 두면 글자 없이 화면 효과만")]
	CanvasGroup deathText;

	Volume volume;
	// 사망 글자가 보이는 정도(0~1). 레이드 실패 결과 화면 동안은 화면 효과와 따로 줄이므로 weight와 따로 센다
	float textAmount;
	// 지금 보고 있는 내 캐릭터. 다시 로그인해서 내 캐릭터가 바뀌면 새로 찾는다
	NetworkIdentity localPlayer;
	PlayerHealth health;
	// 직전 프레임에 죽어 있었는지. 살아 있다가 죽은 순간을 찾기 위함
	bool wasDead;
	// 이번 죽음의 시각과, 그때 레이드 던전에 있었는지
	float deathTime;
	bool diedInRaid;

	void Awake()
	{
		volume = GetComponent<Volume>();
		volume.weight = 0f;
		textAmount = 0f;
		ShowText(0f);
	}

	void Update()
	{
		bool wasDeadBefore = wasDead;
		bool deathShown = ShouldShow();
		bool failureShown = RaidResultView.MyFailureShowing;
		bool respawnedNow = wasDeadBefore && health != null && !health.IsDead;

		if (respawnedNow && !failureShown)
		{
			// 부활한 순간에는 서서히 줄이지 않고 바로 끈다. 일반 장소의 부활은 로딩 화면이 덮인 채로 일어나고 곧바로 로딩 화면이 걷히므로,
			// 서서히 줄이면 걷히는 로딩 화면 사이로 사라지는 중인 글자와 회색이 비쳐 보인다.
			// 레이드 실패 결과 화면 동안은 살아 있어도 무채색이어야 하므로 여기서 끄지 않는다
			volume.weight = 0f;
			textAmount = 0f;
		}
		else
		{
			// 그 밖(죽은 직후, 레이드에서 3초가 지나 관전으로 넘어갈 때, 실패 결과 화면이 뜨고 사라질 때)은 서서히 바꾼다.
			// 시간을 멈춰도(Time.timeScale) 효과는 끝까지 바뀌도록 unscaled 시간을 쓴다
			float step = Time.unscaledDeltaTime / fadeTime;
			volume.weight = Mathf.MoveTowards(volume.weight, deathShown || failureShown ? 1f : 0f, step);
			textAmount = Mathf.MoveTowards(textAmount, deathShown && !failureShown ? 1f : 0f, step);
		}
		ShowText(textAmount);
	}

	// 사망 글자를 amount(0~1)만큼 보인다. 뒤의 버튼을 막지 않도록 클릭은 받지 않는다
	void ShowText(float amount)
	{
		if (deathText == null) return;
		deathText.alpha = amount;
		deathText.blocksRaycasts = false;
		deathText.interactable = false;
	}

	// 지금 내 화면에 사망 효과를 걸어야 하면 true
	bool ShouldShow()
	{
		RefreshLocalPlayer();
		if (health == null) return false;

		bool dead = health.IsDead;
		if (dead && !wasDead)
		{
			// 죽은 순간의 장소를 기억한다. 지금 장소로 판단하면, 레이드가 끝나 죽은 채로 로비에 옮겨질 때 「레이드 밖에서 죽음」으로 바뀌어 효과가 다시 켜진다
			deathTime = Time.time;
			diedInRaid = PlayerPlace.Mine == PlaceScenes.RaidDungeon;
		}
		wasDead = dead;

		if (!dead) return false;
		return !diedInRaid || Time.time - deathTime < health.RespawnDelay;
	}

	// 내 캐릭터가 생기거나 바뀌었으면 그 캐릭터의 PlayerHealth를 찾는다. 매 프레임 GetComponent를 부르지 않으려고 기억해 둔다
	void RefreshLocalPlayer()
	{
		NetworkIdentity current = NetworkClient.localPlayer;
		if (current == localPlayer) return;
		localPlayer = current;
		health = current != null ? current.GetComponent<PlayerHealth>() : null;
		wasDead = false;
	}
}
