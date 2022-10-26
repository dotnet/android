using System;
using System.IO;
using SQLite;

namespace LinkTestLib
{
	// https://bugzilla.xamarin.com/show_bug.cgi?id=35195
	public static class Bug35195
	{
		public static string AttemptCreateTable ()
		{
			try {
				// Initialize the database name.
				const string sqliteFilename = "TaskDB.db3";
				string libraryPath = Environment.GetFolderPath (Environment.SpecialFolder.UserProfile);
				string path = Path.Combine (libraryPath, sqliteFilename);
				var db = new SQLiteAsyncConnection (path);
				db.CreateTableAsync<TodoTask> ().GetAwaiter ().GetResult ();
				return "[PASS] Create table attempt did not throw";
			} catch (Exception ex) {
				return $"[FAIL] Create table attempt failed!\n{ex}";
			}
		}
	}

	public class TodoTask
	{
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }
		public string Name { get; set; }
		public string Notes { get; set; }
		public bool Done { get; set; }
	}
}
