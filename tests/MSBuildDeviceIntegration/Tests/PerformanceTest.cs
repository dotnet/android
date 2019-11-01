using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture, NonParallelizable]
	public class PerformanceTest : DeviceTest
	{
		static readonly Dictionary<string, int> csv_values = new Dictionary<string, int> ();

		[OneTimeSetUp]
		public static void Setup ()
		{
			var csv = Path.Combine (XABuildPaths.TopDirectory, "tests", "msbuild-times-reference", "MSBuildDeviceIntegration.csv");
			using (var reader = File.OpenText (csv)) {
				bool foundHeader = false;
				while (!reader.EndOfStream) {
					var line = reader.ReadLine ();
					if (line.StartsWith ("#") || string.IsNullOrWhiteSpace (line)) {
						continue;
					}
					var split = line.Split (',');
					Assert.AreEqual (2, split.Length, $"{csv} should have two entries per line.");
					if (!foundHeader) {
						// Ignore the first-line header
						foundHeader = true;
						continue;
					}
					string text = split [1];
					if (int.TryParse (text, out int value)) {
						csv_values [split [0]] = value;
					} else {
						Assert.Fail ($"'{text}' is not a valid integer!");
					}
				}
			}
		}

		void DeviceRequired ()
		{
			if (!HasDevices) {
				Assert.Ignore ("Test requires a device attached.");
			}
		}

		void CommercialRequired ()
		{
			if (!CommercialBuildAvailable) {
				Assert.Ignore ("Test requires a commercial build.");
			}
		}

		void Profile (ProjectBuilder builder, Action<ProjectBuilder> action, [CallerMemberName] string caller = null)
		{
			if (!csv_values.TryGetValue (caller, out int expected)) {
				Assert.Fail ($"No timeout value found for a key of {caller}");
			}

			action (builder);
			var actual = builder.LastBuildTime.TotalMilliseconds;
			if (actual > expected) {
				Assert.Fail ($"Exceeded expected time of {expected}ms, actual {actual}ms");
			}
		}

		[Test]
		public void Build_No_Changes ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "Build";
				builder.Build (proj);

				// Profile no changes
				Profile (builder, b => b.Build (proj));

				// Change C# and build
				proj.MainActivity += $"{Environment.NewLine}//comment";
				proj.Touch ("MainActivity.cs");
				builder.Build (proj);

				// Profile no changes
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		public void Build_CSharp_Change ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "Build";
				builder.Build (proj);

				// Profile C# change
				proj.MainActivity += $"{Environment.NewLine}//comment";
				proj.Touch ("MainActivity.cs");
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		public void Build_AndroidResource_Change ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "Build";
				builder.Build (proj);

				// Profile AndroidResource change
				proj.LayoutMain += $"{Environment.NewLine}<!--comment-->";
				proj.Touch ("Resources\\layout\\Main.axml");
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		public void Build_Designer_Change ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "Build";
				builder.Build (proj);

				// Change AndroidResource & run SetupDependenciesForDesigner
				proj.LayoutMain += $"{Environment.NewLine}<!--comment-->";
				proj.Touch ("Resources\\layout\\Main.axml");
				var parameters = new [] { "DesignTimeBuild=True", "AndroidUseManagedDesignTimeResourceGenerator=False" };
				builder.RunTarget (proj, "SetupDependenciesForDesigner", parameters: parameters);

				// Profile AndroidResource change
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		public void Build_JLO_Change ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "Build";
				builder.Build (proj);

				// Profile Java.Lang.Object rename
				proj.MainActivity = proj.MainActivity.Replace ("MainActivity", "MainActivity2");
				proj.Touch ("MainActivity.cs");
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		public void Build_CSProj_Change ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var builder = CreateApkBuilder ()) {
				builder.Target = "Build";
				builder.Build (proj);

				// Profile .csproj change
				proj.Sources.Add (new BuildItem ("None", "Foo.txt") {
					TextContent = () => "Bar",
				});
				Profile (builder, b => b.Build (proj));
			}
		}

		static object [] XAML_Change = new object [] {
			new object [] {
				/* produceReferenceAssembly */ false,
				/* install */                  false,
			},
			new object [] {
				/* produceReferenceAssembly */ true,
				/* install */                  false,
			},
			new object [] {
				/* produceReferenceAssembly */ true,
				/* install */                  true,
			},
		};

		[Test]
		[TestCaseSource (nameof (XAML_Change))]
		public void Build_XAML_Change (bool produceReferenceAssembly, bool install)
		{
			if (install) {
				DeviceRequired ();
				CommercialRequired (); // This test will fail without Fast Deployment
			}

			var path = Path.Combine ("temp", TestName);
			var xaml =
@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<ContentPage xmlns=""http://xamarin.com/schemas/2014/forms""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             x:Class=""MyLibrary.MyPage"">
</ContentPage>";
			var caller = nameof (Build_XAML_Change);
			if (install) {
				caller = caller.Replace ("Build", "Install");
			} else if (produceReferenceAssembly) {
				caller += "_RefAssembly";
			}
			var app = new XamarinFormsAndroidApplicationProject {
				ProjectName = "MyApp",
				Sources = {
					new BuildItem.Source ("Foo.cs") {
						TextContent = () => "public class Foo : Bar { }"
					},
				}
			};
			//NOTE: this will skip a 382ms <VerifyVersionsTask/> from the support library
			app.SetProperty ("XamarinAndroidSupportSkipVerifyVersions", "True");

			int count = 0;
			var lib = new DotNetStandard {
				ProjectName = "MyLibrary",
				Sdk = "Microsoft.NET.Sdk",
				TargetFramework = "netstandard2.0",
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar { public Bar () { System.Console.WriteLine (" + count++ + "); } }"
					},
					new BuildItem ("EmbeddedResource", "MyPage.xaml") {
						TextContent = () => xaml,
					}
				},
				PackageReferences = {
					KnownPackages.XamarinForms_4_0_0_425677
				}
			};
			lib.SetProperty ("ProduceReferenceAssembly", produceReferenceAssembly.ToString ());
			app.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));

			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				libBuilder.Build (lib);
				appBuilder.Target = "Build";
				if (install) {
					appBuilder.Install (app);
				} else {
					appBuilder.Build (app);
				}

				libBuilder.AutomaticNuGetRestore =
					appBuilder.AutomaticNuGetRestore = false;

				// Profile XAML change
				xaml += $"{Environment.NewLine}<!--comment-->";
				lib.Touch ("MyPage.xaml");
				libBuilder.Build (lib, doNotCleanupOnUpdate: true);
				if (install) {
					Profile (appBuilder, b => b.Install (app, doNotCleanupOnUpdate: true), caller);
				} else {
					Profile (appBuilder, b => b.Build (app, doNotCleanupOnUpdate: true), caller);
				}
			}
		}

		[Test]
		public void Install_CSharp_Change ()
		{
			DeviceRequired ();
			CommercialRequired (); // This test will fail without Fast Deployment

			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateApkBuilder ()) {
				builder.Install (proj);

				// Profile C# change
				proj.MainActivity += $"{Environment.NewLine}//comment";
				proj.Touch ("MainActivity.cs");
				Profile (builder, b => b.Install (proj));
			}
		}
	}
}
