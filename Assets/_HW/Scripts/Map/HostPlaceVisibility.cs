using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 호스트 화면에서 내 캐릭터가 없는 장소 씬을 그리지 않는다. 메인 카메라(Base 씬)에 붙인다. 연출만 한다.
/// <para>
/// - 호스트는 서버라서 장소 씬(로비, 사냥터, 연습장, 레이드 던전)을 모두 띄워 둔다. 장소 씬은 같은 좌표에 겹쳐 있어서,
///   그대로 두면 사냥터 화면에 로비의 벽·소품이 겹쳐 보인다. 클라이언트는 자기 장소 씬만 불러오므로 상관없다.<br/>
/// - 내 캐릭터가 들어 있는 씬이 아닌 장소 씬의 Renderer(그림, 타일, 몬스터, 다른 플레이어 등)를 끈다. 오브젝트는 그대로 살아 있다 (서버 동작은 그대로).<br/>
/// - 원래 꺼져 있던 Renderer는 건드리지 않는다. 이 컴포넌트가 끈 것만 기억했다가, 그 씬에 다시 들어가면 켠다.<br/>
/// - 새로 생긴 것(몬스터 등)도 잡도록 Check Interval초마다 다시 본다.
/// </para>
/// </summary>
public class HostPlaceVisibility : MonoBehaviour
{
	[SerializeField, Min(0.05f), Tooltip("숨긴 씬에 새로 생긴 그림을 찾는 간격(초)")]
	float checkInterval = 0.5f;

	static readonly HashSet<string> PlaceNames = new HashSet<string>
	{
		PlaceScenes.Lobby, PlaceScenes.HuntingGround, PlaceScenes.TrainingRoom, PlaceScenes.RaidDungeon,
	};

	// 이 컴포넌트가 끈 Renderer
	readonly HashSet<Renderer> hidden = new HashSet<Renderer>();
	readonly List<Renderer> buffer = new List<Renderer>();
	Scene shownScene;
	float nextCheck;

	void LateUpdate()
	{
		bool host = NetworkServer.active && NetworkClient.active;
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (!host || me == null)
		{
			ShowAll();
			return;
		}

		Scene mine = me.scene;
		if (mine == shownScene && Time.unscaledTime < nextCheck) return;
		shownScene = mine;
		nextCheck = Time.unscaledTime + checkInterval;

		// 내 씬으로 돌아온 것들은 다시 켠다
		hidden.RemoveWhere(r => r == null);
		buffer.Clear();
		foreach (Renderer r in hidden)
			if (r.gameObject.scene == mine) buffer.Add(r);
		foreach (Renderer r in buffer)
		{
			r.enabled = true;
			hidden.Remove(r);
		}

		// 다른 장소 씬은 끈다
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene scene = SceneManager.GetSceneAt(i);
			if (!scene.isLoaded || scene == mine || !PlaceNames.Contains(scene.name)) continue;
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
				{
					if (!r.enabled) continue;
					r.enabled = false;
					hidden.Add(r);
				}
			}
		}
	}

	void OnDisable() => ShowAll();

	void ShowAll()
	{
		if (hidden.Count == 0) return;
		foreach (Renderer r in hidden)
			if (r != null) r.enabled = true;
		hidden.Clear();
		shownScene = default;
	}
}
