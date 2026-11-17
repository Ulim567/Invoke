using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 장소 씬 네 개의 땅(Grid 아래 Ground) 콜라이더를 속이 찬 모양(Polygons)으로 바꾸는 에디터 도구. 게임에는 들어가지 않는다.
/// 메뉴 Invoke → 맵 → 땅 콜라이더 속 채우기. 씬 네 개를 차례로 열어 바꾸고 저장한 뒤, 처음 열려 있던 씬으로 돌아온다. 여러 번 눌러도 된다.
/// <para>
/// 이유 (노션 11/16 대시 벽 뚫림): 땅의 Composite Collider 2D가 Outlines(테두리 선만 있는 빈 모양)였다.
/// 대시(0.15초에 4칸, 물리 한 번에 약 0.5칸)처럼 빠르게 벽에 부딪히면 몸이 테두리 선을 넘어 벽 속으로 들어가고,
/// 속이 비어 있어서 밀어내지 못해 벽을 뚫고 나갔다. Polygons(속이 찬 모양)로 바꾸면 벽 속에 조금 들어가도 바깥으로 밀려난다.<br/>
/// 발판(Platform)은 위로 뚫고 오르는 One Way 발판이라 그대로 둔다.
/// </para>
/// </summary>
public static class GroundColliderFixer
{
	static readonly string[] Scenes = { "Lobby", "HuntingGround", "TrainingRoom", "RaidDungeon" };

	[MenuItem("Invoke/맵/땅 콜라이더 속 채우기")]
	static void Fix()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
		string original = EditorSceneManager.GetActiveScene().path;

		foreach (string name in Scenes)
		{
			string path = $"Assets/Scenes/{name}.unity";
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
			var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
			int changed = 0;
			foreach (CompositeCollider2D composite in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsInactive.Include))
			{
				if (composite.gameObject.name != "Ground") continue;
				if (composite.geometryType == CompositeCollider2D.GeometryType.Polygons) continue;
				Undo.RecordObject(composite, "땅 콜라이더 속 채우기");
				composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
				composite.GenerateGeometry();
				changed++;
			}
			if (changed > 0)
			{
				EditorSceneManager.MarkSceneDirty(scene);
				EditorSceneManager.SaveScene(scene);
			}
			Debug.Log($"[GroundColliderFixer] {name}: 땅 콜라이더 {changed}개를 속이 찬 모양으로 바꿈");
		}

		if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
	}
}
