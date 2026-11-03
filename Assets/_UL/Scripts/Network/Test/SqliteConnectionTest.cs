using System;
using System.IO;
using SQLite;
using UnityEngine;

/// <summary>
/// SQLite 패키지(com.gilzoide.sqlite-net)가 이 컴퓨터에서 동작하는지 확인하는 테스트 컴포넌트.
/// DB 파일을 만들고, 한 줄을 저장한 뒤 다시 읽어서 결과를 로그로 남긴다.
/// <para>
/// 사용법<br/>
/// - 아무 씬의 아무 오브젝트에 붙이고 Play한다. 네트워크를 켜지 않아도 된다.<br/>
/// - Console에 "[SqliteConnectionTest] 성공" 로그가 나오면 동작한다. Play할 때마다 저장한 줄 수가 1씩 늘어난다.<br/>
/// - 실제 계정 DB와 섞이지 않도록 테스트 전용 파일(SqliteTest.db)을 쓴다.
/// </para>
/// </summary>
public class SqliteConnectionTest : MonoBehaviour
{
	const string FileName = "SqliteTest.db";

	// 테스트로 저장하는 한 줄. sqlite-net이 이 클래스 모양대로 테이블을 만든다
	class TestRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public string Note { get; set; }
		public DateTime SavedAt { get; set; }
	}

	void Start()
	{
		string path = Path.Combine(Application.persistentDataPath, FileName);

		try
		{
			using (var db = new SQLiteConnection(path))
			{
				db.CreateTable<TestRow>();
				db.Insert(new TestRow { Note = $"{Application.platform}에서 저장", SavedAt = DateTime.Now });

				int count = db.Table<TestRow>().Count();
				TestRow last = db.Table<TestRow>().OrderByDescending(row => row.Id).First();
				Debug.Log($"[SqliteConnectionTest] 성공. 저장한 줄 {count}개, 마지막 줄: {last.Id} / {last.Note} / {last.SavedAt}\n파일: {path}");
			}
		}
		catch (DllNotFoundException e)
		{
			// SQLite 실행 파일(native 라이브러리)이 이 플랫폼용으로 빌드에 들어가지 않았을 때 난다
			Debug.LogError($"[SqliteConnectionTest] 실패: 이 플랫폼용 SQLite 라이브러리를 찾지 못했습니다. ({Application.platform})\n{e}");
		}
		catch (Exception e)
		{
			Debug.LogError($"[SqliteConnectionTest] 실패: {e.GetType().Name}\n{e}\n파일: {path}");
		}
	}
}
