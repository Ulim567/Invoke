using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 화면마다 버튼에 키 표시(KeyBadge)를 붙이고, 입력칸이 여러 개인 화면에 Tab 이동(TabFieldNavigator)을 붙이는 에디터 도구 (#430). 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 키 표시 붙이기. 여러 번 눌러도 된다. 레이드 방·레벨업 화면 만들기, 로비 입구 창 시안대로를 누르면 끝난 뒤 이것도 저절로 다시 돈다.
/// </summary>
public static class KeyBadgeInstaller
{
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";

	// 프리팹, 캔버스 배율(640 기준), (버튼 경로, 키)
	static readonly (string prefab, float scale, (string path, string key)[] badges, string[] tabRoots)[] Screens =
	{
		("Assets/_HW/Lobby/LobbyUI.prefab", 3f, new[]
		{
			("EntrancePanel/Window/EnterButton", "Enter"),
			("EntrancePanel/Window/CloseButton", "ESC"),
		}, new string[0]),
		("Assets/_HW/UI/Raid/RaidLobbyCanvas.prefab", 1f, new[]
		{
			("PanelRoot/RoomListPanel/Buttons/CreateButton", "Enter"),
			("PanelRoot/CreatePopup/Window/Buttons/ConfirmButton", "Enter"),
			("PanelRoot/CreatePopup/Window/Buttons/CancelButton", "ESC"),
			("PanelRoot/PasswordPopup/Window/Buttons/ConfirmButton", "Enter"),
			("PanelRoot/PasswordPopup/Window/Buttons/CancelButton", "ESC"),
			("PanelRoot/PartyPanel/Buttons/StartButton", "Enter"),
			("PanelRoot/PartyPanel/Buttons/ReadyButton", "Enter"),
			("PanelRoot/PartyPanel/Buttons/LeaveButton", "ESC"),
		}, new[] { "PanelRoot/CreatePopup", "PanelRoot/PasswordPopup" }),
		("Assets/_HW/UI/LevelUp/LevelUpCanvas.prefab", 1f, new (string, string)[0], new string[0]), // X에 붙였던 표시를 지우려고 남겨 둠
		("Assets/_HW/UI/Account/AccountCanvas.prefab", 1f, new (string, string)[0],
			new[] { "Screen/LoginPage", "Screen/SignUpPage" }),
	};

	[MenuItem("Invoke/UI/키 표시 붙이기")]
	public static void Install()
	{
		var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		int badges = 0, tabs = 0;
		foreach (var screen in Screens)
		{
			if (AssetDatabase.LoadAssetAtPath<GameObject>(screen.prefab) == null)
			{
				Debug.LogWarning($"[KeyBadgeInstaller] {screen.prefab}이 없음");
				continue;
			}
			GameObject root = PrefabUtility.LoadPrefabContents(screen.prefab);
			try
			{
				foreach (var (path, key) in screen.badges)
				{
					Transform button = Find(root.transform, path);
					if (button == null) { Debug.LogWarning($"[KeyBadgeInstaller] {screen.prefab}에서 {path}를 찾지 못함"); continue; }
					KeyBadge.Attach((RectTransform)button, key, font, null, screen.scale); // 테두리 없이 흐린 글자만
					badges++;
				}
				foreach (string path in screen.tabRoots)
				{
					Transform page = Find(root.transform, path);
					if (page == null) { Debug.LogWarning($"[KeyBadgeInstaller] {screen.prefab}에서 {path}를 찾지 못함"); continue; }
					if (page.GetComponent<TabFieldNavigator>() == null) page.gameObject.AddComponent<TabFieldNavigator>();
					tabs++;
				}
				// X 버튼에는 붙이지 않는다: 버튼 옆 제목줄 글자(버그 조각 등)와 겹치고, X는 누구나 닫기로 안다. 예전에 붙인 것은 지운다
				foreach (KeyBadge old in root.GetComponentsInChildren<KeyBadge>(true))
				{
					Transform button = old.transform.parent;
					bool wanted = false;
					foreach (var (path, _) in screen.badges)
						if (Find(root.transform, path) == button) wanted = true;
					if (!wanted) Object.DestroyImmediate(old.gameObject);
				}
				// #434 때 입구 창 왼쪽 아래에 적은 「Enter 들어가기 Esc 닫기」는 버튼의 키 표시로 바뀌었으므로 지운다
				Transform oldHint = Find(root.transform, "EntrancePanel/Window/KeyHint");
				if (oldHint != null) Object.DestroyImmediate(oldHint.gameObject);

				PrefabUtility.SaveAsPrefabAsset(root, screen.prefab);
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(root);
			}
		}
		Debug.Log($"[KeyBadgeInstaller] 키 표시 {badges}개, Tab 이동 {tabs}곳을 붙임");
	}

	/// <summary>다른 UI 빌더가 프리팹을 다시 만든 뒤 부른다. 같은 프리팹을 연 채로 부르지 않게 한 프레임 미룬다</summary>
	public static void InstallLater() => EditorApplication.delayCall += Install;

	// "a/b/c": 루트 아래 이름 경로
	static Transform Find(Transform root, string path)
	{
		Transform t = root;
		foreach (string name in path.Split('/'))
		{
			t = t.Find(name);
			if (t == null) return null;
		}
		return t;
	}
}
