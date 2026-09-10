using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class R8ObfuscationTests : DeviceTest
	{
		const string SuccessMarker = "# R8_SELECTIVE_OBFUSCATION_RESULT 21";
		const string JavaClassName = "example/ObfuscationProbe";
		const string JavaMappingClassName = "example.ObfuscationProbe";

		[TestCase (AndroidRuntime.MonoVM)]
		[TestCase (AndroidRuntime.CoreCLR)]
		public void PrivateJavaMembersAreObfuscated (AndroidRuntime runtime)
		{
			var packageName = $"com.xamarin.r8obfuscation.{runtime.ToString ().ToLowerInvariant ()}";
			var proj = new XamarinAndroidApplicationProject (packageName: packageName) {
				IsRelease = true,
				OtherBuildItems = {
					new AndroidItem.AndroidJavaSource ("ObfuscationProbe.java") {
						Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
						TextContent = () => """
							package example;

							public class ObfuscationProbe {
								public static int publicEntry (int value) {
									return protectedEntry (value) + packagePrivateEntry (value) + privateEntry (value);
								}

								protected static int protectedEntry (int value) {
									return value + 1;
								}

								static int packagePrivateEntry (int value) {
									return value + 2;
								}

								private static int privateEntry (int value) {
									return value + 3;
								}
							}
							""",
						Metadata = {
							{ "Bind", "False" },
						},
					},
					new BuildItem (AndroidBuildActions.ProguardConfiguration, "proguard.cfg") {
						TextContent = () => """
							-keep,allowobfuscation class example.ObfuscationProbe {
							   <methods>;
							   <fields>;
							}
							""",
					},
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("AndroidR8ObfuscationMode", "private-members");
			proj.SetProperty ("AndroidCreateProguardMappingFile", "true");
			proj.SetDefaultTargetDevice ();
			proj.MainActivity = proj.DefaultMainActivity.Replace (
				"//${AFTER_ONCREATE}",
				"""
				using var probeClass = Java.Lang.Class.ForName (
					"example.ObfuscationProbe",
					initialize: true,
					Android.App.Application.Context.ClassLoader);
				IntPtr publicEntry = Android.Runtime.JNIEnv.GetStaticMethodID (probeClass.Handle, "publicEntry", "(I)I");
				int result = Android.Runtime.JNIEnv.CallStaticIntMethod (probeClass.Handle, publicEntry, new Android.Runtime.JValue (5));
				Console.WriteLine ($"# R8_SELECTIVE_OBFUSCATION_RESULT {result}");
				if (result != 21) {
					throw new InvalidOperationException ($"Unexpected R8 obfuscation probe result: {result}");
				}
				""");

			using var builder = CreateApkBuilder ();
			bool installed = false;
			try {
				installed = builder.Install (proj);
				Assert.IsTrue (installed, "Project should have installed.");

				var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
				var mappingFiles = Directory.GetFiles (
					Path.Combine (projectDirectory, proj.OutputPath),
					"mapping.txt",
					SearchOption.AllDirectories);
				Assert.AreEqual (1, mappingFiles.Length, "The R8 build should produce one mapping file.");
				string mapping = File.ReadAllText (mappingFiles [0]);

				StringAssert.Contains ($"{JavaMappingClassName} -> {JavaMappingClassName}:", mapping,
					"Java class names should remain unchanged.");
				string mappedPackagePrivateMethod = GetRenamedMethod (mapping, "packagePrivateEntry");
				string mappedPrivateMethod = GetRenamedMethod (mapping, "privateEntry");

				var dexFiles = Directory.GetFiles (
					Path.Combine (projectDirectory, proj.IntermediateOutputPath),
					"classes*.dex",
					SearchOption.AllDirectories);
				Assert.IsNotEmpty (dexFiles, "R8 should produce at least one DEX file.");
				AssertDexContainsMethod (dexFiles, "publicEntry");
				AssertDexContainsMethod (dexFiles, "protectedEntry");
				AssertDexContainsMethod (dexFiles, mappedPackagePrivateMethod);
				AssertDexContainsMethod (dexFiles, mappedPrivateMethod);
				AssertDexDoesNotContainMethod (dexFiles, "packagePrivateEntry");
				AssertDexDoesNotContainMethod (dexFiles, "privateEntry");

				ClearAdbLogcat ();
				RunProjectAndAssert (proj, builder, doNotCleanupOnUpdate: true);
				Assert.IsTrue (WaitForActivityToStart (proj.PackageName, "MainActivity",
					Path.Combine (projectDirectory, "logcat.log"), 30), "Activity should have started.");
				Assert.IsTrue (MonitorAdbLogcat (line => line.Contains (SuccessMarker),
					Path.Combine (projectDirectory, "startup-logcat.log"), 45), $"Output did not contain {SuccessMarker}.");
			} finally {
				if (installed) {
					try {
						builder.ThrowOnBuildFailure = false;
						if (!builder.Uninstall (proj)) {
							TestContext.Error.WriteLine ($"Failed to uninstall '{proj.PackageName}' during test cleanup.");
						}
					} catch (Exception ex) {
						TestContext.Error.WriteLine ($"Failed to uninstall '{proj.PackageName}' during test cleanup: {ex}");
					}
				}
			}
		}

		static string GetRenamedMethod (string mapping, string methodName)
		{
			var match = Regex.Match (
				mapping,
				$@"(?m)^\s*(?:\d+:\d+:)?int {Regex.Escape (methodName)}\(int\)(?::\d+:\d+)?\s+->\s+(?<name>\S+)\s*$");
			Assert.IsTrue (match.Success, $"R8 mapping should contain {methodName}(int).");
			string mappedMethodName = match.Groups ["name"].Value;
			Assert.AreNotEqual (methodName, mappedMethodName, $"{methodName}(int) should be renamed.");
			return mappedMethodName;
		}

		void AssertDexContainsMethod (string [] dexFiles, string methodName)
		{
			Assert.IsTrue (dexFiles.Any (dex => DexUtils.ContainsClassWithMethod (
				$"L{JavaClassName};", methodName, "(I)I", dex, AndroidSdkPath)),
				$"DEX should contain {JavaClassName}.{methodName}(int).");
		}

		void AssertDexDoesNotContainMethod (string [] dexFiles, string methodName)
		{
			Assert.IsFalse (dexFiles.Any (dex => DexUtils.ContainsClassWithMethod (
				$"L{JavaClassName};", methodName, "(I)I", dex, AndroidSdkPath)),
				$"DEX should not contain {JavaClassName}.{methodName}(int).");
		}
	}
}
