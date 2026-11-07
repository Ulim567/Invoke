using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 메인 카메라가 내 캐릭터를 따라가게 하는 컴포넌트. 플레이어 프리팹에 붙인다.
/// <para>
/// - 내 캐릭터(isLocalPlayer)에서만 카메라를 움직인다. 다른 플레이어가 접속해도 카메라를 빼앗지 않는다.<br/>
/// - 카메라 위치를 Pixel Perfect Camera의 픽셀 격자에 맞춰서(RoundToPixel) 픽셀이 떨리지 않게 한다.<br/>
/// - 카메라 Z는 Camera Z(기본 -10)로 유지한다.<br/>
/// - 씬의 공용 카메라(MainCamera 태그)를 움직이기만 하고, 카메라 프리팹은 고치지 않는다.<br/>
/// - 레이드 던전에서 내 캐릭터가 숨겨지면(관전) 같은 장소에서 살아 있는 다른 플레이어를 따라간다 (#419). 살아 있는 플레이어가 없으면 내가 죽은 자리에 머문다.<br/>
/// - 관전 중에 ← → 를 누르면 따라갈 플레이어를 바꾼다. 순서는 netId 오름차순이고, ←는 이전, →는 다음, 끝에서는 반대쪽 끝으로 돌아간다.
/// </para>
/// </summary>
public class PlayerCameraFollow : NetworkBehaviour
{
	[SerializeField, Tooltip("캐릭터 위치(발바닥)에서 카메라 중심까지의 거리. 캐릭터가 화면 가운데쯤 오게 위로 올린다")]
	private Vector2 offset = new Vector2(0f, 6f);

	[SerializeField, Tooltip("카메라 Z 위치")]
	private float cameraZ = -10f;

	Camera cam;
	PixelPerfectCamera pixelPerfect;
	PlayerHealth health;
	PlayerInputReader input;
	// 관전 중 따라가는 플레이어. null이면 내 캐릭터를 따라간다
	PlayerHealth spectateTarget;
	// 직전 프레임의 좌우 입력 부호(-1, 0, 1). 0에서 ±1로 바뀐 프레임만 「눌림」으로 본다
	int lastMoveSign;
	// ← → 로 넘길 후보. 매번 새로 만들지 않으려고 보관한다
	readonly List<PlayerHealth> candidates = new List<PlayerHealth>(4);

	/// <summary>
	/// 관전 중 카메라가 따라가는 플레이어. 관전 중이 아니거나 따라갈 살아 있는 플레이어가 없으면 null.<br/>
	/// 파티원 목록(RaidPartyView)처럼 「지금 누구를 보고 있는지」를 보여 주는 화면이 읽기만 한다. 값은 이 컴포넌트가 LateUpdate에서 정한다.
	/// </summary>
	public PlayerHealth SpectateTarget => spectateTarget != null ? spectateTarget : null;

	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();
		health = GetComponent<PlayerHealth>();
		input = GetComponent<PlayerInputReader>();
		cam = Camera.main;
		if (cam == null)
		{
			Debug.LogWarning("[PlayerCameraFollow] MainCamera 태그가 붙은 카메라가 없음");
			return;
		}
		pixelPerfect = cam.GetComponent<PixelPerfectCamera>();
		Follow();
	}

	// 캐릭터가 이번 프레임 움직임을 끝낸 뒤 카메라를 옮긴다
	void LateUpdate()
	{
		if (!isLocalPlayer || cam == null) return;
		UpdateSpectateTarget();
		Follow();
	}

	// 관전 중이면 따라갈 플레이어를 정하고, 아니면 비운다. 이벤트를 구독하지 않고 매 프레임 값을 읽어 구독 순서나 해제를 신경 쓰지 않게 한다
	void UpdateSpectateTarget()
	{
		// 관전 중이 아닐 때도 매 프레임 기억해 둔다. 숨겨지는 순간 이미 누르고 있던 키를 「눌림」으로 보지 않기 위함
		// 죽은 동안 PlayerMovement는 MoveX를 쓰지 않으므로(CanAct), 관전에 써도 이동과 겹치지 않는다
		float moveX = input != null ? input.MoveX : 0f;
		int moveSign = moveX > 0f ? 1 : moveX < 0f ? -1 : 0;
		int step = lastMoveSign == 0 ? moveSign : 0;
		lastMoveSign = moveSign;

		// 레이드 던전에서만 관전한다. 클라이언트에서는 scene.name으로 장소를 알 수 없어 PlayerPlace로 읽는다
		bool spectating = health != null && health.IsHidden && PlayerPlace.Mine == PlaceScenes.RaidDungeon;
		if (!spectating)
		{
			spectateTarget = null;
			return;
		}

		// 따라가던 플레이어가 죽거나 장소를 떠나 유효하지 않게 되면 살아 있는 다음 플레이어로 바꾼다
		if (!IsValidSpectateTarget(spectateTarget)) spectateTarget = PickSpectateTarget();
		else if (step != 0) spectateTarget = StepSpectateTarget(step);
	}

	// 지금 대상에서 step(-1 이전, 1 다음)만큼 넘긴 플레이어. 후보가 하나뿐이면 그대로 둔다
	PlayerHealth StepSpectateTarget(int step)
	{
		candidates.Clear();
		var players = PlayerRoster.Players;
		for (int i = 0; i < players.Count; i++)
			if (IsValidSpectateTarget(players[i])) candidates.Add(players[i]);
		if (candidates.Count <= 1) return spectateTarget;

		// 목록 순서는 플레이어가 나타난 순서라 화면마다 다를 수 있으므로, 모든 화면에서 같은 netId 순서로 넘긴다
		candidates.Sort((a, b) => a.netId.CompareTo(b.netId));
		int index = candidates.IndexOf(spectateTarget);
		int next = ((index < 0 ? 0 : index) + step + candidates.Count) % candidates.Count;
		return candidates[next];
	}

	// 내가 아니고, 살아 있고, 같은 장소에 있는 플레이어면 true
	static bool IsValidSpectateTarget(PlayerHealth player)
	{
		return player != null
			&& !PlayerRoster.IsLocal(player)
			&& !player.IsDead
			&& PlayerRoster.IsInMyPlace(player);
	}

	// 내가 죽은 자리에서 가장 가까운 유효한 플레이어를 고른다. 없으면 null (내 캐릭터에 머문다)
	PlayerHealth PickSpectateTarget()
	{
		PlayerHealth nearest = null;
		float nearestDistance = float.MaxValue;
		var players = PlayerRoster.Players;
		for (int i = 0; i < players.Count; i++)
		{
			PlayerHealth player = players[i];
			if (!IsValidSpectateTarget(player)) continue;

			float distance = ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude;
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearest = player;
			}
		}
		return nearest;
	}

	void Follow()
	{
		// 관전 중이면 대상의 위치를, 아니면 내 캐릭터의 위치를 따라간다
		Vector3 position = spectateTarget != null ? spectateTarget.transform.position : transform.position;
		Vector3 target = new Vector3(position.x + offset.x, position.y + offset.y, cameraZ);
		if (pixelPerfect != null) target = pixelPerfect.RoundToPixel(target);
		target.z = cameraZ;
		cam.transform.position = target;
	}
}
