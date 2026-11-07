using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 화면에 보스 체력 바를 보여 주는 UI. 위치는 Panel의 RectTransform으로 정한다(지금은 임시로 위쪽 가운데). 보여 주기만 하고 값은 바꾸지 않는다.
/// <para>
/// - 내 캐릭터와 같은 씬에 살아 있는 보스(Monster.IsBoss)가 있을 때만 보인다. 여러 마리면 최대 체력이 가장 큰 보스를 보여 준다.<br/>
///   레이드 던전에 들어가면 저절로 나타나고, 보스가 죽거나 씬을 나가면 사라진다. 보스는 Search Interval초마다 한 번 찾는다.<br/>
/// - 빨간 막대(Fill)는 지금 체력. 흰 막대(Trail)는 맞은 뒤 Trail Delay초 기다렸다가 천천히 따라 줄어서 얼마나 깎였는지 보여 준다.<br/>
/// - 페이즈 구분선(Phase Marks)을 보스의 GatewayPhases.PhaseThresholds 비율 위치(70%, 50%, 30%)에 놓는다. 보스에 GatewayPhases가 없으면 Inspector의 Phase Thresholds를 쓴다.<br/>
/// - 이름은 보스 오브젝트 이름에서 "(Clone)"을 뗀 것, 숫자는 "지금 / 최대"로 보여 준다.
/// </para>
/// <para>
/// 사용법: Canvas(Screen Space - Overlay) 아래 바 오브젝트에 붙이고 Inspector에 부품을 넣는다.
/// Fill·Trail Image는 Image Type을 Filled, Fill Method를 Horizontal, Fill Origin을 Left로 둔다.<br/>
/// 네트워크: 보스의 체력은 MonsterNetwork가 서버 값으로 맞춰 주므로 클라이언트 화면에서도 정확하다 (#310).
/// </para>
/// </summary>
public class BossHealthBar : MonoBehaviour
{
	[Header("부품")]
	[SerializeField, Tooltip("보스가 있을 때만 켜는 바 전체 오브젝트. 이 컴포넌트가 붙은 오브젝트가 아니라 그 자식으로 둔다")]
	GameObject panel;

	[SerializeField, Tooltip("지금 체력 막대 (빨강). Image Type Filled, Horizontal")]
	Image fill;

	[SerializeField, Tooltip("맞은 뒤 천천히 따라 줄어드는 막대 (흰색). Fill 뒤에 둔다. Image Type Filled, Horizontal")]
	Image trail;

	[SerializeField, Tooltip("보스 이름 글자")]
	TMP_Text nameText;

	[SerializeField, Tooltip("체력 숫자 글자 (지금 / 최대). 비워 두면 숫자를 보여 주지 않는다")]
	TMP_Text hpText;

	[SerializeField, Tooltip("페이즈 구분선. Phase Thresholds와 같은 순서로 넣는다")]
	RectTransform[] phaseMarks;

	[SerializeField, Tooltip("보스에 GatewayPhases가 없을 때 쓸 페이즈 비율. Gateway는 보스의 값을 읽는다")]
	float[] phaseThresholds = { 0.7f, 0.5f, 0.3f };

	[Header("움직임")]
	[SerializeField, Min(0.05f), Tooltip("보스를 찾는 간격(초)")]
	float searchInterval = 0.5f;

	[SerializeField, Min(0f), Tooltip("맞은 뒤 흰 막대가 줄기 시작하기까지 기다리는 시간(초)")]
	float trailDelay = 0.4f;

	[SerializeField, Min(0.01f), Tooltip("흰 막대가 줄어드는 빠르기 (1초에 전체 길이의 몇 배)")]
	float trailSpeed = 0.6f;

	Monster boss;
	float nextSearchTime;
	int shownHp = -1;
	int shownMaxHp = -1;
	float trailStartTime;

	void Awake()
	{
		if (panel != null) panel.SetActive(false);
		PlacePhaseMarks();
	}

	void Update()
	{
		float now = Time.unscaledTime;
		if (now >= nextSearchTime)
		{
			nextSearchTime = now + searchInterval;
			FindBoss();
		}

		if (boss == null || boss.IsDead)
		{
			if (panel != null && panel.activeSelf) panel.SetActive(false);
			boss = null;
			return;
		}

		Show(now);
	}

	// 내 캐릭터와 같은 씬의 살아 있는 보스 중 최대 체력이 가장 큰 보스를 고른다
	void FindBoss()
	{
		Scene myScene = MyScene();
		Monster best = null;

		// 0.5초마다 한 번만 찾으므로 부담이 작다. 정렬하지 않아 더 빠르다
		Monster[] monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
		for (int i = 0; i < monsters.Length; i++)
		{
			Monster m = monsters[i];
			if (!m.IsBoss || m.IsDead) continue;
			if (myScene.IsValid() && m.gameObject.scene != myScene) continue;
			if (best == null || m.MaxHp > best.MaxHp) best = m;
		}

		if (best == boss) return;
		boss = best;
		shownHp = -1; // 새 보스면 막대를 처음부터 맞춘다
		if (boss != null)
		{
			SetName(boss.name);
			// 구분선은 보스의 페이즈 비율에 맞춘다 (Gateway 70%, 50%, 30%)
			if (boss.TryGetComponent(out GatewayPhases phases) && phases.PhaseThresholds.Count > 0)
			{
				phaseThresholds = new float[phases.PhaseThresholds.Count];
				for (int i = 0; i < phaseThresholds.Length; i++) phaseThresholds[i] = phases.PhaseThresholds[i];
			}
			PlacePhaseMarks();
		}
	}

	// 내 캐릭터가 있는 씬. 네트워크를 켜지 않은 테스트처럼 내 캐릭터가 없으면 아무 씬이나 본다(IsValid false)
	static Scene MyScene()
	{
		NetworkIdentity me = NetworkClient.localPlayer;
		return me != null ? me.gameObject.scene : default;
	}

	void Show(float now)
	{
		if (panel != null && !panel.activeSelf) panel.SetActive(true);

		int hp = boss.Hp;
		int maxHp = boss.MaxHp;
		float ratio = maxHp > 0 ? (float)hp / maxHp : 0f;

		if (hp != shownHp || maxHp != shownMaxHp)
		{
			// 체력이 줄면 잠깐 기다렸다가 흰 막대를 줄인다. 처음이거나 체력이 늘면 바로 맞춘다
			bool damaged = shownHp >= 0 && hp < shownHp;
			if (damaged) trailStartTime = now + trailDelay;
			else if (trail != null) trail.fillAmount = ratio;

			shownHp = hp;
			shownMaxHp = maxHp;
			if (fill != null) fill.fillAmount = ratio;
			if (hpText != null) hpText.text = $"{hp} / {maxHp}";
		}

		if (trail != null && trail.fillAmount > ratio && now >= trailStartTime)
			trail.fillAmount = Mathf.MoveTowards(trail.fillAmount, ratio, trailSpeed * Time.unscaledDeltaTime);
	}

	void SetName(string objectName)
	{
		if (nameText == null) return;
		const string clone = "(Clone)";
		nameText.text = objectName.EndsWith(clone) ? objectName.Substring(0, objectName.Length - clone.Length).TrimEnd() : objectName;
	}

	// 페이즈 구분선을 막대 위 비율 위치에 놓는다. 구분선의 부모는 막대(Fill과 같은 크기)로 둔다
	void PlacePhaseMarks()
	{
		if (phaseMarks == null) return;
		for (int i = 0; i < phaseMarks.Length; i++)
		{
			RectTransform mark = phaseMarks[i];
			if (mark == null) continue;
			bool used = i < phaseThresholds.Length;
			mark.gameObject.SetActive(used);
			if (!used) continue;

			float x = Mathf.Clamp01(phaseThresholds[i]);
			// 앵커를 세로로 벌리면 Height가 "부모보다 얼마나 큰가"로 바뀌므로, 한 점에 모아 Inspector의 Height를 그대로 쓴다
			mark.anchorMin = new Vector2(x, 0.5f);
			mark.anchorMax = new Vector2(x, 0.5f);
			mark.anchoredPosition = Vector2.zero;
		}
	}
}
