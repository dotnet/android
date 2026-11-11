using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Android.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Build.Tests
{
	[TestClass]
	[DoNotParallelize]
	public class GeneratePackageManagerJavaTests
	{
		static object [] [] CheckPackageManagerAssemblyOrderChecks () => new object [] [] {
			new object[] {
				/* resolvedUserAssemblies */ new string [] {
					"linked/Xamarin.AndroidX.SavedState.dll",
					"linked/HelloAndroid.dll",
				},
				/* resolvedAssemblies */     new string [] {
					"linked/Xamarin.AndroidX.SavedState.dll",
					"linked/HelloAndroid.dll",
					"linked/System.Console.dll",
					"linked/System.Linq.dll",
				}
			},
			new object[] {
				/* resolvedUserAssemblies */ new string [] {
					"linked/HelloAndroid.dll",
					"linked/Xamarin.AndroidX.SavedState.dll",
				},
				/* resolvedAssemblies */     new string [] {
					"linked/Xamarin.AndroidX.SavedState.dll",
					"linked/System.Console.dll",
					"linked/System.Linq.dll",
					"linked/HelloAndroid.dll",
				}
			},
		};

		[TestMethod]
		[DynamicData (nameof (CheckPackageManagerAssemblyOrderChecks))]
		public void CheckPackageManagerAssemblyOrder (string [] resolvedUserAssemblies, string [] resolvedAssemblies)
		{
			// avoid a PathTooLongException because using the TestName will include ALL the arguments.
			var testHash = Files.HashString (string.Join ("", resolvedUserAssemblies) + string.Join ("", resolvedAssemblies));
			var path = Path.Combine (AppContext.BaseDirectory, "temp", $"CheckPackageManagerAssemblyOrder{testHash}");
			Directory.CreateDirectory (path);
			try {
				var packageManagerTask = new GeneratePackageManagerJava {
					BuildEngine = new MockBuildEngine (Console.Out),
					MainAssembly = "linked/HelloAndroid.dll",
					OutputDirectory = Path.Combine (path, "src", "mono"),
				};

				Assert.IsTrue (packageManagerTask.Execute (), "GeneratePackageManagerJava task should have executed.");

				Assert.AreEqual (
					File.ReadAllText (Path.Combine (AppContext.BaseDirectory, "Expected", "CheckPackageManagerAssemblyOrder.java")).Replace ("\r\n", "\n"),
					File.ReadAllText (Path.Combine (path, "src", "mono", "MonoPackageManager_Resources.java")).Replace ("\r\n", "\n"));
			} finally {
				Directory.Delete (path, recursive: true);
			}
		}
	}
}
