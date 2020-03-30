using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("Node-2")]
	public class ResolveMonoAndroidSdksTests : BaseTest
	{
		static readonly string [] parameters = new [] { "_ResolveMonoAndroidSdksDependsOn=" };
		static readonly string [] messages = new [] {
			"MonoAndroid Tools",
			"Java SDK",
			"Android SDK",
			"Android NDK",
			"Android Platform API level",
		};

		static Dictionary<string, string> ValuesFromLog (ProjectBuilder b)
		{
			var values = new Dictionary<string, string> ();
			foreach (var line in b.LastBuildOutput) {
				foreach (var key in messages) {
					int index = line.IndexOf (key + ":", StringComparison.OrdinalIgnoreCase);
					if (index != -1) {
						index += key.Length + 1;
						var value = line.Substring (index, line.Length - index).Trim ();
						// The log line might also include a message such as: (TaskId:7)
						index = value.IndexOf ("(", StringComparison.OrdinalIgnoreCase);
						if (index != -1) {
							value = value.Substring (0, index).Trim ();
						}
						values [key] = value;
					}
				}
			}
			return values;
		}

		[Test]
		public void NormalInputs ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("MonoAndroidToolsDirectory", "xat");
			proj.SetProperty ("_JavaSdkDirectory", "jdk");
			proj.SetProperty ("_AndroidSdkDirectory", "sdk");
			proj.SetProperty ("_AndroidNdkDirectory", "ndk");
			proj.SetProperty ("_AndroidApiLevel", "29");

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Target = "_ResolveMonoAndroidSdks";
				Assert.IsTrue (b.Build (proj, parameters: parameters), "Build should have succeeded.");

				var values = ValuesFromLog (b);
				Assert.AreEqual ($"xat{Path.DirectorySeparatorChar}", values ["MonoAndroid Tools"]);
				Assert.AreEqual ($"jdk{Path.DirectorySeparatorChar}", values ["Java SDK"]);
				Assert.AreEqual ($"sdk{Path.DirectorySeparatorChar}", values ["Android SDK"]);
				Assert.AreEqual ($"ndk{Path.DirectorySeparatorChar}", values ["Android NDK"]);
				Assert.AreEqual ("29",    values ["Android Platform API level"]);
			}
		}

		[Test]
		public void MissingAndroidNDK ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("MonoAndroidToolsDirectory", "xat");
			proj.SetProperty ("_JavaSdkDirectory", "jdk");
			proj.SetProperty ("_AndroidSdkDirectory", "sdk");
			proj.SetProperty ("_AndroidNdkDirectory", "");
			proj.SetProperty ("_AndroidApiLevel", "29");

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Target = "_ResolveMonoAndroidSdks";
				Assert.IsTrue (b.Build (proj, parameters: parameters), "Build should have succeeded.");

				var values = ValuesFromLog (b);
				Assert.AreEqual ($"xat{Path.DirectorySeparatorChar}", values ["MonoAndroid Tools"]);
				Assert.AreEqual ($"jdk{Path.DirectorySeparatorChar}", values ["Java SDK"]);
				Assert.AreEqual ($"sdk{Path.DirectorySeparatorChar}", values ["Android SDK"]);
				Assert.AreEqual ("",      values ["Android NDK"]);
				Assert.AreEqual ("29",    values ["Android Platform API level"]);
			}
		}

		[Test]
		public void HasTrailingSlash ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.SetProperty ("MonoAndroidToolsDirectory", $"xat{Path.DirectorySeparatorChar}");
			proj.SetProperty ("_JavaSdkDirectory", $"jdk{Path.DirectorySeparatorChar}");
			proj.SetProperty ("_AndroidSdkDirectory", $"sdk{Path.DirectorySeparatorChar}");
			proj.SetProperty ("_AndroidNdkDirectory", $"ndk{Path.DirectorySeparatorChar}");
			proj.SetProperty ("_AndroidApiLevel", "29");

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				b.Target = "_ResolveMonoAndroidSdks";
				Assert.IsTrue (b.Build (proj, parameters: parameters), "Build should have succeeded.");

				var values = ValuesFromLog (b);
				Assert.AreEqual ($"xat{Path.DirectorySeparatorChar}", values ["MonoAndroid Tools"]);
				Assert.AreEqual ($"jdk{Path.DirectorySeparatorChar}", values ["Java SDK"]);
				Assert.AreEqual ($"sdk{Path.DirectorySeparatorChar}", values ["Android SDK"]);
				Assert.AreEqual ($"ndk{Path.DirectorySeparatorChar}", values ["Android NDK"]);
				Assert.AreEqual ("29",    values ["Android Platform API level"]);
			}
		}
	}
}
