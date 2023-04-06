using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class UncaughtExceptionTests : DeviceTest
	{
		class LogcatLine
		{
			public string Text;
			public bool Found = false;
			public int SequenceNumber = -1;
			public int Count = 0;
		};

		[Test]
		public void EnsureUncaughtExceptionWorks ()
		{
			var lib = new XamarinAndroidBindingProject {
				ProjectName = "Scratch.Try",
				AndroidClassParser = "class-parse",
			};

			lib.Imports.Add (
				new Import (() => "Directory.Build.targets") {
					TextContent = () =>
@"<Project>
	<PropertyGroup>
		<JavacSourceVersion>1.8</JavacSourceVersion>
		<JavacTargetVersion>1.8</JavacTargetVersion>
		<Javac>javac</Javac>
		<Jar>jar</Jar>
	</PropertyGroup>
	<ItemGroup>
		<JavaSource Include=""java\**\*.java"" />
		<AndroidJavaSource Include=""@(JavaSource)"" Bind=""False"" />
	</ItemGroup>
	<ItemGroup>
		<EmbeddedJar Include=""$(OutputPath)try.jar"" />
	</ItemGroup>
	<Target Name=""_BuildJar""
		AfterTargets=""ResolveAssemblyReferences""
		Inputs=""@(JavaSource);$(MSBuildThisFile)""
		Outputs=""$(OutputPath)try.jar"">
		<PropertyGroup>
			<_Classes>$(IntermediateOutputPath)classes</_Classes>
		</PropertyGroup>
		<RemoveDir Directories=""$(_Classes)""/>
		<MakeDir Directories=""$(_Classes)"" />
		<Exec Command=""$(Javac) -source $(JavacSourceVersion) -target $(JavacTargetVersion) -d &quot;$(_Classes)&quot; @(JavaSource->'&quot;%(Identity)&quot;', ' ')"" />
		<Exec Command=""$(Jar) cf &quot;$(OutputPath)try.jar&quot; -C &quot;$(_Classes)&quot; ."" />
	</Target>
</Project>
"
			});

			lib.Sources.Add (
				new BuildItem.NoActionResource ("java\\testing\\Run.java") {
					Encoding = new System.Text.UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
					TextContent = () =>
@"package testing;

public final class Run {
	private Run() {
	}

	public static interface CatchThrowableHandler {
		void onCatch(Throwable t);
	}

	public static final void tryCatchFinally (Runnable r, CatchThrowableHandler c, Runnable f) {
		try {
			r.run();
		}
		catch (Throwable t) {
			c.onCatch(t);
		}
		finally {
			f.run();
		}
	}
}
"
			});

			var app = new XamarinAndroidApplicationProject {
				ProjectName = "Scratch.JMJMException",
			};

			app.SetDefaultTargetDevice ();
			app.AddReference (lib);

			app.Sources.Remove (app.GetItem ("MainActivity.cs"));

			string mainActivityTemplate = @"using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Testing;

namespace Scratch.JMJMException
{
	[Register (""${JAVA_PACKAGENAME}.MainActivity""), Activity (Label = ""${PROJECT_NAME}"", MainLauncher = true, Icon = ""@drawable/icon"")]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			Button b = new Button (this) {
				Text = ""Click Me!"",
			};

			Testing.Run.TryCatchFinally (
				new Java.Lang.Runnable (() => {
					Console.WriteLine (""#UET-1# jon: Should be in a Java > Managed [MainActivity.OnCreate] > Java [Run.tryCatchFinally] > Managed [Run] frame. Throwing an exception..."");
					Console.WriteLine (new System.Diagnostics.StackTrace(fNeedFileInfo: true).ToString());
					throw new Exception (""Should be in a Java > Managed [MainActivity.OnCreate] > Java [Run.tryCatchFinally] > Managed [Run] frame. Throwing an exception..."");
				}),
				new MyCatchHandler (),
				new Java.Lang.Runnable (() => {
					Console.WriteLine ($""#UET-3# jon: from Java finally block"");
				})
			);

			SetContentView (b);
		}
	}

	class MyCatchHandler : Java.Lang.Object, Run.ICatchThrowableHandler
	{
		public void OnCatch (Java.Lang.Throwable t)
		{
			Console.WriteLine ($""#UET-2# jon: MyCatchHandler.OnCatch: t={t.ToString()}"");
		}
	}
}
";
			string mainActivity = app.ProcessSourceTemplate (mainActivityTemplate);
			app.Sources.Add (
				new BuildItem.Source ("MainActivity.cs") {
					TextContent = () => mainActivity
				}
			);

			var expectedLogLines = new LogcatLine[] {
				new LogcatLine { Text = "#UET-1#" },
				new LogcatLine { Text = "#UET-2#" },
				new LogcatLine { Text = "#UET-3#" },
			};

			string path = Path.Combine ("temp", TestName);
			using (var libBuilder = CreateDllBuilder (Path.Combine (path, lib.ProjectName)))
			using (var appBuilder = CreateApkBuilder (Path.Combine (path, app.ProjectName))) {
				Assert.True (libBuilder.Build (lib), "Library should have built.");
				Assert.IsTrue (appBuilder.Install (app), "Install should have succeeded.");
				AdbStartActivity ($"{app.PackageName}/{app.JavaPackageName}.MainActivity");

				string logcatPath = Path.Combine (Root, appBuilder.ProjectDirectory, "logcat.log");
				int sequenceCounter = 0;
				MonitorAdbLogcat (
					(string line) => {
						foreach (LogcatLine ll in expectedLogLines) {
							if (line.IndexOf (ll.Text, StringComparison.Ordinal) < 0) {
								continue;
							}
							ll.Found = true;
							ll.Count++;
							ll.SequenceNumber = sequenceCounter++;
							break;
						}
						return false; // we must examine all the lines, and returning `true` aborts the monitoring process
					}, logcatPath, 15);
			}

			AssertValidLine (0, 0);
			AssertValidLine (1, 1);
			AssertValidLine (2, 2);

			void AssertValidLine (int idx, int expectedSequence)
			{
				LogcatLine ll = expectedLogLines [idx];
				Assert.IsTrue (ll.Found, $"Logcat line {idx} was not found");
				Assert.IsTrue (ll.Count == 1, $"Logcat line {idx} should have been found only once but it was found {ll.Count} times");
				Assert.IsTrue (ll.SequenceNumber == expectedSequence, $"Logcat line {idx} sequence number should be {expectedSequence} but it was {ll.SequenceNumber}");
			}
		}
	}
}
