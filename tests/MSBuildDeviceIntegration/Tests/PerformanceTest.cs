using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class PerformanceTest : DeviceTest
	{
		const int Retry = 2;
		static readonly Dictionary<string, int> csv_values = new Dictionary<string, int> ();

		[OneTimeSetUp]
		public static void Setup ()
		{
			var csv = Path.Combine (XABuildPaths.TopDirectory, "tests", "msbuild-times-reference", "MSBuildDeviceIntegration.csv");
			using (var reader = File.OpenText (csv)) {
				bool foundHeader = false;
				while (!reader.EndOfStream) {
					var line = reader.ReadLine ();
					if (line.StartsWith ("#", StringComparison.Ordinal) || string.IsNullOrWhiteSpace (line)) {
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

		void Profile (ProjectBuilder builder, Action<ProjectBuilder> action, [CallerMemberName] string caller = null)
		{
			if (!csv_values.TryGetValue (caller, out int expected)) {
				Assert.Fail ($"No timeout value found for a key of {caller}");
			}

			action (builder);
			var actual = GetDurationFromBinLog (builder);
			TestContext.Out.WriteLine($"expected: {expected}ms, actual: {actual}ms");
			if (actual > expected) {
				Assert.Fail ($"Exceeded expected time of {expected}ms, actual {actual}ms");
			}
		}

		void ProfileTask (ProjectBuilder builder, string task, int iterations, Action<ProjectBuilder> action, [CallerMemberName] string caller = null)
		{
			if (!csv_values.TryGetValue (caller, out int expected)) {
				Assert.Fail ($"No timeout value found for a key of {caller}");
			}
			double total = 0;
			for (int i=0; i < iterations; i++) {
				action (builder);
				var duration = GetTaskDurationFromBinLog (builder, task);
				TestContext.Out.WriteLine($"run {i} took: {duration}ms");
				total += duration;
			}
			total /= iterations;
			TestContext.Out.WriteLine($"expected: {expected}ms, actual: {total}ms");
			if (total > expected) {
				Assert.Fail ($"Exceeded expected time of {expected}ms, actual {total}ms");
			}
		}

		double GetTaskDurationFromBinLog (ProjectBuilder builder, string task)
		{
			var binlog = Path.Combine (Root, builder.ProjectDirectory, $"{Path.GetFileNameWithoutExtension (builder.BuildLogFile)}.binlog");
			FileAssert.Exists (binlog);

			var build = BinaryLog.ReadBuild (binlog);
			var duration = build
				.FindChildrenRecursive<Task> (t => t.Name == task)
				.Aggregate (TimeSpan.Zero, (duration, target) => duration + target.Duration);

			if (duration == TimeSpan.Zero)
				throw new InvalidDataException ($"No task build duration found in {binlog}");

			return duration.TotalMilliseconds;
		}

		double GetDurationFromBinLog (ProjectBuilder builder)
		{
			var binlog = Path.Combine (Root, builder.ProjectDirectory, $"{Path.GetFileNameWithoutExtension (builder.BuildLogFile)}.binlog");
			FileAssert.Exists (binlog);

			var build = BinaryLog.ReadBuild (binlog);
			var duration = build
				.FindChildrenRecursive<Project> ()
				.Aggregate (TimeSpan.Zero, (duration, project) => duration + project.Duration);

			if (duration == TimeSpan.Zero)
				throw new InvalidDataException ($"No project build duration found in {binlog}");

			return duration.TotalMilliseconds;
		}

		ProjectBuilder CreateBuilderWithoutLogFile (string directory = null, bool isApp = true)
		{
			var builder = isApp ? CreateApkBuilder (directory) : CreateDllBuilder (directory);
			builder.BuildLogFile = null;
			builder.Verbosity = LoggerVerbosity.Quiet;
			return builder;
		}

		XamarinAndroidApplicationProject CreateApplicationProject ()
		{
			var proj = new XamarinAndroidApplicationProject () {
			};
			proj.SetAndroidSupportedAbis (DeviceAbi); // Use a single ABI
			proj.SetProperty ("_FastDeploymentDiagnosticLogging", "False");
			return proj;
		}

		[Test]
		[Retry (Retry)]
		public void Build_From_Clean_DontIncludeRestore ()
		{
			var proj = CreateApplicationProject ();
			using (var builder = CreateBuilderWithoutLogFile ()) {
				builder.AutomaticNuGetRestore = false;
				builder.Target = "Build";
				builder.Restore (proj);
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		[Retry (Retry)]
		public void Build_No_Changes ()
		{
			var proj = CreateApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateBuilderWithoutLogFile ()) {
				builder.Target = "Build";
				builder.Build (proj);
				builder.AutomaticNuGetRestore = false;

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
		[Retry (Retry)]
		public void Build_CSharp_Change ()
		{
			var proj = CreateApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateBuilderWithoutLogFile ()) {
				builder.Target = "Build";
				builder.Build (proj);
				builder.AutomaticNuGetRestore = false;

				// Profile C# change
				proj.MainActivity += $"{Environment.NewLine}//comment";
				proj.Touch ("MainActivity.cs");
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		[Retry (Retry)]
		public void Build_AndroidResource_Change ()
		{
			AssertCommercialBuild (); // If <BuildApk/> runs, this test will fail without Fast Deployment

			var proj = CreateApplicationProject ();
			using (var builder = CreateBuilderWithoutLogFile ()) {
				builder.Target = "Build";
				builder.Build (proj);
				builder.AutomaticNuGetRestore = false;

				// Profile AndroidResource change
				proj.LayoutMain += $"{Environment.NewLine}<!--comment-->";
				proj.Touch ("Resources\\layout\\Main.axml");
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		[Retry (Retry)]
		public void Build_AndroidAsset_Change ()
		{
			AssertCommercialBuild (); // If <BuildApk/> runs, this test will fail without Fast Deployment

			var bytes = new byte [1024*1024*10];
			var rnd = new Random ();
			rnd.NextBytes (bytes);
			var lib = new XamarinAndroidLibraryProject () {
				ProjectName = "Library1",
			};
			lib.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("Assets\\foo.bar") {
				BinaryContent = () => bytes,
			});
			var proj = CreateApplicationProject ();
			proj.ProjectName = "App1";
			proj.References.Add (new BuildItem.ProjectReference ("..\\Library1\\Library1.csproj"));
			rnd.NextBytes (bytes);
			proj.OtherBuildItems.Add (new AndroidItem.AndroidAsset ("Assets\\foo.bar") {
				BinaryContent = () => bytes,
			});
			using (var libBuilder = CreateBuilderWithoutLogFile (Path.Combine ("temp", TestName, lib.ProjectName), isApp: false))
			using (var builder = CreateBuilderWithoutLogFile (Path.Combine ("temp", TestName, proj.ProjectName))) {
				builder.Target = "Build";
				libBuilder.Build (lib);
				builder.Build (proj);
				libBuilder.AutomaticNuGetRestore =
					builder.AutomaticNuGetRestore = false;

				rnd.NextBytes (bytes);
				lib.Touch ("Assets\\foo.bar");
				libBuilder.Build (lib);
				builder.Target = "SignAndroidPackage";
				// Profile AndroidAsset change
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		[Retry (Retry)]
		public void Build_JLO_Change ()
		{
			var className = "Foo";
			var proj = CreateApplicationProject ();
			proj.Sources.Add (new BuildItem.Source ("Foo.cs") {
				TextContent = () => $"class {className} : Java.Lang.Object {{}}"
			});
			using (var builder = CreateBuilderWithoutLogFile ()) {
				builder.Target = "Build";
				builder.Build (proj);
				builder.AutomaticNuGetRestore = false;

				// Profile Java.Lang.Object rename
				className = "Foo2";
				proj.Touch ("Foo.cs");
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		[Retry (Retry)]
		public void Build_AndroidManifest_Change ()
		{
			AssertCommercialBuild (); // If <BuildApk/> runs, this test will fail without Fast Deployment

			var proj = CreateApplicationProject ();
			using (var builder = CreateBuilderWithoutLogFile ()) {
				builder.Target = "Build";
				builder.Build (proj);
				builder.AutomaticNuGetRestore = false;

				// Profile AndroidManifest.xml change
				proj.AndroidManifest += $"{Environment.NewLine}<!--comment-->";
				proj.Touch ("Properties\\AndroidManifest.xml");
				Profile (builder, b => b.Build (proj));
			}
		}

		[Test]
		[Category ("UsesDevice")]
		[Retry (Retry)]
		public void Build_XAML_Change ([Values (true, false)] bool install)
		{
			if (install) {
				AssertCommercialBuild (); // This test will fail without Fast Deployment
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
			}
			var app = CreateApplicationProject ();
			app.ProjectName = "MyApp";
			app.Sources.Add (new BuildItem.Source ("Foo.cs") {
				TextContent = () => "public class Foo : Bar { }"
			});
			app.PackageReferences.Add (KnownPackages.XamarinForms);
			app.PackageReferences.Add (KnownPackages.AndroidXBrowser); // Guava.ListenableFuture: https://github.com/xamarin/AndroidX/issues/535
			//NOTE: this will skip a 382ms <VerifyVersionsTask/> from the support library
			app.SetProperty ("XamarinAndroidSupportSkipVerifyVersions", "True");

			int count = 0;
			var lib = new DotNetStandard {
				ProjectName = "MyLibrary",
				Sdk = "Microsoft.NET.Sdk",
				TargetFramework = "net8.0", // Vanilla project
				Sources = {
					new BuildItem.Source ("Bar.cs") {
						TextContent = () => "public class Bar { public Bar () { System.Console.WriteLine (" + count++ + "); } }"
					},
					new BuildItem ("EmbeddedResource", "MyPage.xaml") {
						TextContent = () => xaml,
					}
				},
				PackageReferences = {
					KnownPackages.XamarinForms,
				}
			};
			app.References.Add (new BuildItem.ProjectReference ($"..\\{lib.ProjectName}\\{lib.ProjectName}.csproj", lib.ProjectName, lib.ProjectGuid));

			using (var libBuilder = CreateBuilderWithoutLogFile (Path.Combine (path, lib.ProjectName), isApp: false))
			using (var appBuilder = CreateBuilderWithoutLogFile (Path.Combine (path, app.ProjectName))) {
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
		[Category ("UsesDevice")]
		[Retry (Retry)]
		public void Install_CSharp_Change ()
		{
			AssertCommercialBuild (); // This test will fail without Fast Deployment

			var proj = CreateApplicationProject ();
			proj.PackageName = "com.xamarin.install_csharp_change";
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateBuilderWithoutLogFile ()) {
				builder.Install (proj);
				builder.AutomaticNuGetRestore = false;

				// Profile C# change
				proj.MainActivity += $"{Environment.NewLine}//comment";
				proj.Touch ("MainActivity.cs");
				Profile (builder, b => b.Install (proj));
			}
		}

		[Test]
		[Category ("UsesDevice")]
		[Retry (Retry)]
		public void Install_CSharp_FromClean ()
		{
			AssertCommercialBuild (); // This test will fail without Fast Deployment

			var proj = CreateApplicationProject ();
			proj.PackageName = "com.xamarin.install_csharp_change";
			proj.MainActivity = proj.DefaultMainActivity;
			using (var builder = CreateBuilderWithoutLogFile ()) {
				builder.BuildLogFile = "install.log";
				builder.Verbosity = LoggerVerbosity.Quiet;
				builder.Install (proj);
				builder.AutomaticNuGetRestore = false;
				ProfileTask (builder, "FastDeploy", 20, b => {
					b.Uninstall (proj);
					b.Install (proj);
				});
			}
		}
	}
}
