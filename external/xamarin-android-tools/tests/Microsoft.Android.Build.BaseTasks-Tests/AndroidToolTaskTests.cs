using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Android.Build.BaseTasks.Tests.Utilities;
using Microsoft.Android.Build.Tasks;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Xamarin.Build;

namespace Microsoft.Android.Build.BaseTasks.Tests
{
	[TestFixture]
	public class AndroidToolTaskTests
	{
		public class MyAndroidTask : AndroidTask {
			public override string TaskPrefix {get;} = "MAT";
			public string Key { get; set; }
			public string Value { get; set; }
			public bool ProjectSpecific { get; set; } = false;
			public override bool RunTask ()
			{
				var key = ProjectSpecific ? ProjectSpecificTaskObjectKey (Key) : (Key, (object)string.Empty);
				BuildEngine4.RegisterTaskObjectAssemblyLocal (key, Value, RegisteredTaskObjectLifetime.Build);
				return true;
			}
		}

		public class MyOtherAndroidTask : AndroidTask {
			public override string TaskPrefix {get;} = "MOAT";
			public string Key { get; set; }
			public bool ProjectSpecific { get; set; } = false;

			[Output]
			public string Value { get; set; }
			public override bool RunTask ()
			{
				var key = ProjectSpecific ? ProjectSpecificTaskObjectKey (Key) : (Key, (object)string.Empty);
				Value = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<string> (key, RegisteredTaskObjectLifetime.Build);
				return true;
			}
		}

		[Test]
		[TestCase (true, true, true)]
		[TestCase (false, false, true)]
		[TestCase (true, false, false)]
		[TestCase (false, true, false)]
		public void TestRegisterTaskObjectCanRetrieveCorrectItem (bool projectSpecificA, bool projectSpecificB, bool expectedResult)
		{
			var engine = new MockBuildEngine (TestContext.Out) {
			};
			var task = new MyAndroidTask () {
				BuildEngine = engine,
				Key = "Foo",
				Value = "Foo",
				ProjectSpecific = projectSpecificA,
			};
			task.Execute ();
			var task2 = new MyOtherAndroidTask () {
				BuildEngine = engine,
				Key = "Foo",
				ProjectSpecific = projectSpecificB,
			};
			task2.Execute ();
			Assert.AreEqual (expectedResult, string.Compare (task2.Value, task.Value, ignoreCase: true) == 0);
		}

		[Test]
		[TestCase (true, true, false)]
		[TestCase (false, false, true)]
		[TestCase (true, false, false)]
		[TestCase (false, true, false)]
		public void TestRegisterTaskObjectFailsWhenDirectoryChanges (bool projectSpecificA, bool projectSpecificB, bool expectedResult)
		{
			var engine = new MockBuildEngine (TestContext.Out) {
			};
			MyAndroidTask task;
			var currentDir = Directory.GetCurrentDirectory ();
			Directory.SetCurrentDirectory (Path.Combine (currentDir, ".."));
			try {
				task = new MyAndroidTask () {
					BuildEngine = engine,
					Key = "Foo",
					Value = "Foo",
					ProjectSpecific = projectSpecificA,
				};
			} finally {
				Directory.SetCurrentDirectory (currentDir);
			}
			task.Execute ();
			var task2 = new MyOtherAndroidTask () {
				BuildEngine = engine,
				Key = "Foo",
				ProjectSpecific = projectSpecificB,
			};
			task2.Execute ();
			Assert.AreEqual (expectedResult, string.Compare (task2.Value, task.Value, ignoreCase: true) == 0);
		}
	}
}
