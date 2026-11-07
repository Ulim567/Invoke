using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 카메라가 맵 밖(땅이 없는 곳)을 보여 주지 않게 막는다. 메인 카메라(Base 씬)에 붙인다. 연출만 한다.
/// <para>
/// - 내 캐릭터가 있는 장소 씬의 땅 타일맵(Grid 아래 이름 Ground)이 차지하는 범위를 맵 범위로 본다. 장소 씬마다 값을 적지 않아도 된다.<br/>
/// - 가로: 화면 왼쪽·오른쪽 끝이 맵 끝을 넘지 않게 카메라를 멈춘다. 양 끝 벽(Edge Inset, 2칸)도 안 보이게 그만큼 안쪽에서 멈춘다. 맵이 화면보다 좁으면 맵 가운데에 둔다.<br/>
/// - 세로: 화면 아래에 바닥이 Floor Shown칸(HUD 띠 포함 5.25칸)보다 많이 보이지 않게 카메라가 더 내려가지 않는다 위는 하늘이라 막지 않지만, 천장이 화면보다 높은 실내(연습장)는 천장 위가 안 보이게 위도 막는다.<br/>
/// - 카메라를 캐릭터에 맞추는 일은 박진우의 PlayerCameraFollow가 하고, 이것은 그 뒤에(실행 순서 900) 위치를 범위 안으로 당기기만 한다.
///   배경 Parallax(1000)는 이것보다 뒤에 돌아서 당긴 위치를 기준으로 움직인다.<br/>
/// - 장소는 PlayerPublicInfo.Place(이우림)로 본다. 호스트는 장소 씬이 여러 개 열려 있으므로 내 캐릭터가 실제로 들어 있는 씬을 먼저 본다.<br/>
/// - 픽셀이 떨리지 않게 1/16칸(1픽셀) 단위로 맞춘다.
/// </para>
/// </summary>
[DefaultExecutionOrder(900)]
[RequireComponent(typeof(Camera))]
public class CameraBounds : MonoBehaviour
{
	[SerializeField, Tooltip("맵 범위로 쓸 타일맵 이름 (장소 씬의 Grid 아래)")]
	string groundTilemapName = "Ground";

	[SerializeField, Min(0f), Tooltip("양 끝 벽 두께(칸). 맵 양 끝의 검은 벽이 화면에 안 보이게 이만큼 안쪽에서 멈춘다 (MapPainter의 벽은 2칸)")]
	float edgeInset = 2f;

	[SerializeField, Tooltip("모든 장소의 바닥 윗면 Y (map-background.md: 로비·사냥터·연습장·레이드 모두 -4)")]
	float floorTop = -4f;

	[SerializeField, Min(0f), Tooltip("화면 아래에 보이는 바닥 깊이(칸). HUD 아래 띠(약 3.75칸)에 가리는 부분을 포함한다. 카메라가 이보다 아래로 내려가지 않는다")]
	float floorShown = 5.25f;

	[SerializeField, Min(1), Tooltip("1칸의 픽셀 수. 카메라 위치를 이 단위로 맞춘다")]
	int pixelsPerUnit = 16;

	Camera cam;
	readonly Dictionary<Scene, Bounds?> boundsByScene = new Dictionary<Scene, Bounds?>();

	void Awake() => cam = GetComponent<Camera>();

	void LateUpdate()
	{
		if (cam == null || !cam.orthographic) return;
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (me == null) return;

		Scene scene = PlaceScene(me);
		if (!scene.IsValid() || !scene.isLoaded) return;
		if (!boundsByScene.TryGetValue(scene, out Bounds? found))
		{
			found = FindGroundBounds(scene);
			boundsByScene[scene] = found;
		}
		if (found == null) return;
		Bounds map = found.Value;

		float halfHeight = cam.orthographicSize;
		float halfWidth = halfHeight * cam.aspect;
		Vector3 position = transform.position;

		float left = map.min.x + edgeInset;
		float right = map.max.x - edgeInset;
		if (right - left <= halfWidth * 2f) position.x = (left + right) * 0.5f;
		else position.x = Mathf.Clamp(position.x, left + halfWidth, right - halfWidth);
		// 세로: 땅 아래가 보이지 않게, 그리고 바닥이 너무 많이 보이지 않게 (바닥 윗면에서 Floor Shown칸까지만)
		float minY = Mathf.Max(map.min.y + halfHeight, floorTop - floorShown + halfHeight);
		// 천장이 있는 실내(연습장): 땅 타일맵 위 끝이 화면보다 높으면 천장 위(하늘)가 보이지 않게 위도 막는다.
		// 바깥 장소는 벽 위가 하늘이라 이 조건에 안 걸린다 (벽 꼭대기가 낮아 위를 막으면 바닥 쪽과 부딪힘)
		float maxY = map.max.y - halfHeight;
		position.y = maxY >= minY ? Mathf.Clamp(position.y, minY, maxY) : Mathf.Max(position.y, minY);

		position.x = Mathf.Round(position.x * pixelsPerUnit) / pixelsPerUnit;
		position.y = Mathf.Round(position.y * pixelsPerUnit) / pixelsPerUnit;
		transform.position = position;
	}

	// 내 캐릭터가 있는 장소 씬. 호스트·서버에서는 캐릭터가 실제로 그 씬에 들어 있고, 클라이언트에서는 Base 씬에 있으므로 장소 이름으로 찾는다
	static Scene PlaceScene(GameObject me)
	{
		string place = PlayerPlace.Of(me);
		if (string.IsNullOrEmpty(place)) return default;
		if (me.scene.name == place) return me.scene;
		return SceneManager.GetSceneByName(place);
	}

	Bounds? FindGroundBounds(Scene scene)
	{
		foreach (GameObject root in scene.GetRootGameObjects())
		{
			foreach (Tilemap tilemap in root.GetComponentsInChildren<Tilemap>(true))
			{
				if (tilemap.name != groundTilemapName) continue;
				tilemap.CompressBounds();
				BoundsInt cells = tilemap.cellBounds;
				if (cells.size.x <= 0 || cells.size.y <= 0) return null;
				Vector3 min = tilemap.CellToWorld(cells.min);
				Vector3 max = tilemap.CellToWorld(cells.max);
				var bounds = new Bounds();
				bounds.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
				return bounds;
			}
		}
		return null;
	}

	// 장소 씬이 내려가면 그 씬의 범위를 잊는다 (연습장은 들어갈 때마다 새로 열림)
	void OnEnable() => SceneManager.sceneUnloaded += Forget;
	void OnDisable() => SceneManager.sceneUnloaded -= Forget;
	void Forget(Scene scene) => boundsByScene.Remove(scene);
}
