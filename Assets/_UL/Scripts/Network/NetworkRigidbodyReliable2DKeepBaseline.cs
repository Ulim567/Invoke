using Mirror;
using UnityEngine;

/// <summary>
/// 텔레포트해도 위치 동기화 기준값(차이 압축의 기준)을 지우지 않는 NetworkRigidbodyReliable2D.
/// 플레이어 프리팹의 NetworkRigidbodyReliable2D 대신 붙인다. 설정값(Sync Direction 등)은 그대로 쓴다.
/// <para>
/// 왜 필요한가<br/>
/// - NetworkTransformReliable은 위치를 "지난 값과의 차이"로 보낸다. 지난 값이 보내는 쪽과 받는 쪽에서 같아야 위치가 맞는다.<br/>
/// - Mirror 96.0.1의 OnTeleport는 ResetState()를 불러 이 지난 값을 0으로 지운다. 서버는 바로 지우지만 클라이언트는 텔레포트 알림을 늦게 받는다.
///   그 사이 클라이언트가 옛 기준으로 보낸 위치를 서버가 0 기준으로 풀어서, 다른 화면에서 캐릭터가 그 차이만큼 어긋나 보인다.<br/>
/// - Mirror 자신도 OnTeleport 주석에서 텔레포트 때 기준값을 지우면 안 된다고 적고 있다. 이 컴포넌트가 그 동작을 되살린다.
/// </para>
/// <para>
/// OnEnable, OnDisable, 클라이언트 권한 변경 때의 ResetState()는 그대로 둔다. 이때는 보내는 쪽과 받는 쪽이 함께 새로 시작하므로 기준값을 지워도 맞는다.
/// </para>
/// </summary>
public class NetworkRigidbodyReliable2DKeepBaseline : NetworkRigidbodyReliable2D
{
	protected override void OnTeleport(Vector3 destination)
	{
		// base.OnTeleport가 지우는 값을 미리 저장했다가 되돌린다.
		// last는 새로 보기 시작한 플레이어에게 처음 위치를 보낼 때 쓰므로(initialState) 함께 보존한다.
		Vector3Long serializedPosition = lastSerializedPosition;
		Vector3Long deserializedPosition = lastDeserializedPosition;
		Vector3Long serializedScale = lastSerializedScale;
		Vector3Long deserializedScale = lastDeserializedScale;
		TransformSnapshot lastSnapshot = last;

		base.OnTeleport(destination);

		lastSerializedPosition = serializedPosition;
		lastDeserializedPosition = deserializedPosition;
		lastSerializedScale = serializedScale;
		lastDeserializedScale = deserializedScale;
		last = lastSnapshot;
	}

	protected override void OnTeleport(Vector3 destination, Quaternion rotation)
	{
		Vector3Long serializedPosition = lastSerializedPosition;
		Vector3Long deserializedPosition = lastDeserializedPosition;
		Vector3Long serializedScale = lastSerializedScale;
		Vector3Long deserializedScale = lastDeserializedScale;
		TransformSnapshot lastSnapshot = last;

		base.OnTeleport(destination, rotation);

		lastSerializedPosition = serializedPosition;
		lastDeserializedPosition = deserializedPosition;
		lastSerializedScale = serializedScale;
		lastDeserializedScale = deserializedScale;
		last = lastSnapshot;
	}
}
