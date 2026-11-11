#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using NUnit.Framework;
using Microsoft.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class GetAndroidDefineConstantsTests : BaseTest
	{
		[TestCase ("36", null, 36, "")]
		[TestCase ("36.1", null, 36, "__ANDROID_36_1__")]
		[TestCase ("38.2", "11.0-rc+test", 38, "__ANDROID_36_1__;__ANDROID_37_1__;__ANDROID_37_2__;__ANDROID_37_3__;__ANDROID_37_4__;__ANDROID_38_1__;__ANDROID_38_2__")]
		public void Constants (string apiLevel, string? productVersion, int major, string minorConstants)
		{
			var task = new GetAndroidDefineConstants {
				AndroidApiLevel = apiLevel,
				ProductVersion = productVersion,
			};
			Assert.IsTrue (task.Execute ());
			var constants = task.AndroidDefineConstants ?? throw new InvalidOperationException ("No constants returned.");
			var expected = new List<string> ();
			if (productVersion != null)
				expected.Add ("__XAMARIN_ANDROID_11_0_rc_test__");
			expected.AddRange (["__MOBILE__", "__ANDROID__"]);
			expected.AddRange (Enumerable.Range (1, major).Select (i => $"__ANDROID_{i}__"));
			expected.AddRange (minorConstants.Split (';', StringSplitOptions.RemoveEmptyEntries));
			CollectionAssert.AreEqual (expected, constants.Select (i => i.ItemSpec));
		}

		[TestCase ("")]
		[TestCase ("invalid")]
		public void InvalidApiLevel (string apiLevel)
		{
			var errors = new List<BuildErrorEventArgs> ();
			var task = new GetAndroidDefineConstants {
				AndroidApiLevel = apiLevel,
				BuildEngine = new MockBuildEngine (TestContext.Out, errors: errors),
			};
			Assert.IsFalse (task.Execute ());
			Assert.IsNull (task.AndroidDefineConstants);
			Assert.IsEmpty (errors);
		}

		[TestCase ("GetAndroidDefineConstants")]
		[TestCase ("Microsoft.Android.Tasks.GetAndroidDefineConstants")]
		public void RegisteredTaskNamesExecute (string taskName)
		{
			string directory = Path.Combine (Root, "temp", TestName);
			Directory.CreateDirectory (directory);
			string commonTargets = Path.Combine (NativeAotBootstrapTestTools.FindRepositoryRoot (),
				"src", "Xamarin.Android.Build.Tasks", "Xamarin.Android.Common.targets");
			var registrations = XDocument.Load (commonTargets).Descendants ()
				.Where (e => e.Name.LocalName == "UsingTask" && (string?) e.Attribute ("TaskName") == "Microsoft.Android.Tasks.GetAndroidDefineConstants")
				.Select (e => new XElement (e) { Name = "UsingTask" })
				.ToArray ();
			Assert.AreEqual (2, registrations.Length);

			string project = Path.Combine (directory, "constants.proj");
			string output = Path.Combine (directory, "constants.txt");
			new XDocument (new XElement ("Project",
				new XElement ("PropertyGroup",
					new XElement ("_MicrosoftAndroidBuildTasksAssembly", typeof (GetAndroidDefineConstants).Assembly.Location)),
				registrations,
				new XElement ("Target", new XAttribute ("Name", "Build"),
					new XElement (taskName,
						new XAttribute ("AndroidApiLevel", "36.1"),
						new XAttribute ("ProductVersion", "11.0-rc+test"),
						new XElement ("Output", new XAttribute ("TaskParameter", "AndroidDefineConstants"), new XAttribute ("ItemName", "_Constants"))),
					new XElement ("WriteLinesToFile",
						new XAttribute ("File", output),
						new XAttribute ("Lines", "@(_Constants)"),
						new XAttribute ("Overwrite", "true")))))
				.Save (project);

			string dotnet = Environment.GetEnvironmentVariable ("DOTNET_HOST_PATH") ?? "dotnet";
			var result = NativeAotBootstrapTestTools.Run (dotnet, "msbuild", project, "-t:Build", "-v:minimal", "-nr:false");
			Assert.AreEqual (0, result.ExitCode, result.Output + result.Error);
			var lines = File.ReadAllLines (output);
			Assert.AreEqual ("__XAMARIN_ANDROID_11_0_rc_test__", lines [0]);
			Assert.AreEqual ("__ANDROID_36_1__", lines [^1]);
		}
	}
}
