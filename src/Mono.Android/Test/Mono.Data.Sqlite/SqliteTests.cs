using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Android.App;
using Android.Content;
using Android.Runtime;

using Mono.Data.Sqlite;

using NUnit.Framework;

namespace Mono.Data.Sqlite.Tests
{
	[TestFixture]
	public class SqliteTests
	{
		class ItemsDb
		{
			public static readonly Dictionary <string, string> dbItems = new Dictionary <string, string> (StringComparer.Ordinal) {
				{"sample", "text"},
				{"more", "items"},
				{"another", "item"},
			};

			public static readonly ItemsDb Instance;

			static readonly string fileName = "items.db3";
			string dbPath;
			bool dbInitialized;

			static ItemsDb ()
			{
				if ((int)Android.OS.Build.VERSION.SdkInt < 8) {
					return;
				}

				Instance = new ItemsDb ();
			}

			ItemsDb ()
			{
				dbPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), fileName);
			}

			SqliteConnection GetConnection ()
			{
				var conn = new SqliteConnection ("Data Source=" + dbPath);
				return InitDb (conn);
			}

			SqliteConnection InitDb (SqliteConnection conn)
			{
				if (dbInitialized)
					return conn;

				if (File.Exists (dbPath))
					File.Delete (dbPath);

				SqliteConnection.CreateFile (dbPath);
				dbInitialized = true;

				var commands = new List <string> {
					"CREATE TABLE ITEMS (Key ntext, Value ntext);",
				};

				foreach (var kvp in dbItems) {
					commands.Add ($"INSERT INTO [Items] ([Key], [Value]) VALUES ('{kvp.Key}', '{kvp.Value}')");
				}

				foreach (string cmd in commands) {
					WithCommand (c => {
						c.CommandText = cmd;
						c.ExecuteNonQuery ();
					});
				}

				return conn;
			}

			public void WithConnection (Action<SqliteConnection> action)
			{
				var connection = GetConnection ();
				try {
					connection.Open ();
					action (connection);
				} finally {
					connection.Close ();
				}
			}

			public void WithCommand (Action<SqliteCommand> command)
			{
				WithConnection (conn => {
					using (var cmd = conn.CreateCommand ())
						command (cmd);
				});
			}
		}

		[Test]
		public void BasicFunctionality ()
		{
			if (ItemsDb.Instance == null) {
                                Assert.Ignore ("SQLite is not supported on this platform.");
                                return;
                        }

			var dbContents = new Dictionary<string, string> (StringComparer.Ordinal);
                        ItemsDb.Instance.WithCommand (c => {
                                        c.CommandText = "SELECT [Key], [Value] FROM [Items]";
                                        var r = c.ExecuteReader ();
                                        while (r.Read()) {
						dbContents.Add (r ["Key"].ToString (), r ["Value"].ToString ());
                                        }
                        });

			AssertDictionariesAreEqual (ItemsDb.dbItems, dbContents);
                        do {
                                ItemsDb.Instance.WithCommand (c => {
                                                c.CommandText = "CREATE TABLE TESTTABLE (DATA blob not null)";
                                                c.ExecuteNonQuery ();
                                });
                                ItemsDb.Instance.WithCommand (c => {
                                                c.CommandText = "SELECT * FROM TESTTABLE";
                                                using (var r = c.ExecuteReader ()) {
                                                        string typeName = r.GetDataTypeName (0);
                                                        Assert.IsFalse (typeName != "blob", $"Bug in DbDataReader.GetDataTypeName: should be 'blob', got: {typeName}");
                                                }
                                });
                                ItemsDb.Instance.WithCommand (c => {
                                                c.CommandText = "DROP TABLE TESTTABLE";
                                                c.ExecuteNonQuery ();
                                });
                        } while (false);
		}

		static void AssertDictionariesAreEqual (Dictionary <string, string> expected, Dictionary <string, string> dict)
		{
			Assert.AreEqual (expected.Count, dict.Count, $"Number of entries read from the database ({dict.Count}) is different than expected ({expected.Count})");

			foreach (var kvp in expected) {
				string value;

				Assert.IsTrue (dict.TryGetValue (kvp.Key, out value), $"Database does not contain expected entry with key '{kvp.Key}'");
				Assert.AreEqual (kvp.Value, value, $"Database has a different value for key '{kvp.Key}': '{value}' instead of '{kvp.Value}'");
			}
		}
	}
}
