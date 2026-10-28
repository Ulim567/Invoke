using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 임시 체력 바. 내 캐릭터의 체력이 바뀌면 Slider 값을 바꾼다. 모양과 위치는 임시이고, 실제 HUD는 이후 따로 만든다.
/// <para>
/// 사용법: 씬의 Canvas 안 Slider에 붙인다. 내 캐릭터가 나타나면 스스로 찾아 연결한다.
/// </para>
/// </summary>
[RequireComponent(typeof(Slider))]
public class TempHealthBar : MonoBehaviour
{
	Slider slider;
	PlayerHealth target;

	void Awake()
	{
		slider = GetComponent<Slider>();
		slider.interactable = false;
		slider.minValue = 0f;
		slider.maxValue = 1f;
	}

	void Update()
	{
		// 내 캐릭터는 Host 버튼을 누른 뒤에 생기므로, 찾을 때까지 매 프레임 확인한다
		if (target != null) return;

		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer == null || !localPlayer.TryGetComponent(out target)) return;

		target.HpChanged += Show;
		Show(target.Hp, target.MaxHp);
	}

	void OnDestroy()
	{
		if (target != null) target.HpChanged -= Show;
	}

	void Show(int hp, int maxHp) => slider.value = maxHp > 0 ? (float)hp / maxHp : 0f;
}
