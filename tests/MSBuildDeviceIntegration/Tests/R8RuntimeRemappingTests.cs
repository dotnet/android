using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class R8RuntimeRemappingTests : DeviceTest
	{
		[TestCase (AndroidRuntime.CoreCLR)]
		[TestCase (AndroidRuntime.NativeAOT)]
		public void ObfuscatedMembersRun (AndroidRuntime runtime)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release: true)) {
				return;
			}

			var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (runtime, "r8remapping")) {
				IsRelease = true,
				EnableDefaultItems = true,
				OtherBuildItems = {
					new AndroidItem.AndroidJavaSource ("RuntimePeer.java") {
						Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false),
						Metadata = {
							{ "Bind", "True" },
						},
						TextContent = () => """
							package example;

							public class RuntimePeer {
								public int value = 7;
								public static int staticValue = 11;
								public RuntimePeer () {}
								public RuntimePeer echo (RuntimePeer other) { return other; }
								public static RuntimePeer create () { return new RuntimePeer (); }
								public static Object createHidden () { return new HiddenPeer (); }
								public int add (int amount) { return value + amount; }
								public int add (String text) { return value + text.length (); }
								public int unusedMethod () { return -1; }
							}

							class HiddenPeer extends RuntimePeer {}
							""",
					},
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
			proj.SetDefaultTargetDevice ();
			proj.SetProperty ("AndroidTypeMapImplementation", "trimmable");
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("AndroidEnableR8Obfuscation", "true");
			if (runtime == AndroidRuntime.NativeAOT) {
				proj.SetProperty ("AndroidR8ObfuscationMode", "runtime-remapping");
			}
			proj.Sources.Add (new BuildItem.Source ("HiddenPeerBinding.cs") {
				TextContent = () => """
					using System;
					using System.Diagnostics.CodeAnalysis;
					using Android.Runtime;

					[Register ("example/HiddenPeer", DoNotGenerateAcw = true)]
					public class HiddenPeerBinding : Example.RuntimePeer
					{
						public HiddenPeerBinding (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

						[DynamicDependency (DynamicallyAccessedMemberTypes.PublicConstructors, typeof (HiddenPeerBinding))]
						public static Type GetBindingType () => typeof (HiddenPeerBinding);
					}
					""",
			});
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", """
				using var peer = new Example.RuntimePeer ();
				peer.Value = 13;
				Example.RuntimePeer.StaticValue = 17;
				using var created = Example.RuntimePeer.Create ();
				var echoed = peer.Echo (created);
				using var hidden = Example.RuntimePeer.CreateHidden ();
				if (peer.Add (2) != 15 || peer.Add ("abc") != 16 ||
						Example.RuntimePeer.StaticValue != 17 || echoed.Value != 7 ||
						echoed.GetType () != typeof (Example.RuntimePeer) ||
						hidden.GetType () != HiddenPeerBinding.GetBindingType ())
					throw new InvalidOperationException ("Obfuscated JNI lookup returned an incorrect value or managed type.");
				Console.WriteLine ("R8_RUNTIME_REMAP_SUCCESS");
				""");

			using var builder = CreateApkBuilder ();
			void AssertAppRuns (string logFile)
			{
				ClearAdbLogcat ();
				RunProjectAndAssert (proj, builder, doNotCleanupOnUpdate: true);
				Assert.IsTrue (MonitorAdbLogcat (
					line => line.Contains ("R8_RUNTIME_REMAP_SUCCESS", StringComparison.Ordinal),
					Path.Combine (Root, builder.ProjectDirectory, logFile),
					timeout: 30), "Constructors, overloads, fields, and peer return values should work.");
			}
			Assert.IsTrue (builder.Install (proj), "Obfuscated app should build and install.");
			try {
				var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
				var remapFiles = Directory.GetFiles (intermediate, "r8-jni-remap.xml", SearchOption.AllDirectories);
				Assert.IsNotEmpty (remapFiles, "A compact runtime remapping file should be generated.");
				var elements = remapFiles.SelectMany (file => XDocument.Load (file).Root.Elements ()).ToList ();
				Assert.IsTrue (elements.Any (e => e.Name == "replace-method" &&
					(string) e.Attribute ("source-method-name") == "add" &&
					(string) e.Attribute ("target-method-name") != "add"), "The exercised methods must really be obfuscated.");
				Assert.IsTrue (elements.Any (e => e.Name == "replace-field" &&
					(string) e.Attribute ("source-field-name") == "value" &&
					(string) e.Attribute ("target-field-name") != "value"), "The exercised fields must really be obfuscated.");
				Assert.IsTrue (elements.Any (e => e.Name == "replace-type" &&
					(string) e.Attribute ("from") == "example/HiddenPeer" &&
					(string) e.Attribute ("to") != "example/HiddenPeer"), "Java-to-managed activation must exercise a genuinely renamed class.");
				Assert.IsFalse (elements.Any (e => (string) e.Attribute ("source-method-name") == "unusedMethod"),
					"An unused method on a retained type must not occupy the runtime table.");

				AssertAppRuns ("r8-runtime-remap.log");

				if (runtime == AndroidRuntime.NativeAOT) {
					var aaptRules = Path.Combine (intermediate, "aapt_rules.txt");
					FileAssert.Exists (aaptRules);
					var originalAaptRules = File.ReadAllText (aaptRules);
					Assert.IsTrue (builder.Build (proj), "A no-op build should succeed.");
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidGenerateNativeAotR8Remapping"));
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidCompileNativeAotR8Remapping"));
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidLinkNativeAotSharedLibrary"));
					FileAssert.Exists (aaptRules, "IncrementalClean must retain AAPT keep rules.");
					Assert.AreEqual (originalAaptRules, File.ReadAllText (aaptRules));

					var ilcObject = Directory.GetFiles (intermediate, $"{proj.ProjectName}.o", SearchOption.AllDirectories).Single ();
					var ilcTimestamp = File.GetLastWriteTimeUtc (ilcObject);
					var remapObject = Directory.GetFiles (intermediate, $"jni_remap.{DeviceAbi}.o", SearchOption.AllDirectories).Single ();
					File.Delete (remapObject);
					Assert.IsTrue (builder.Build (proj), "A missing remapping object should be regenerated.");
					FileAssert.Exists (remapObject);
					Assert.AreEqual (ilcTimestamp, File.GetLastWriteTimeUtc (ilcObject), "Recovering the late-linked table must not recompile IL.");
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_AndroidCompileNativeAotR8Remapping"));
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_AndroidLinkNativeAotSharedLibrary"));

					File.Delete (aaptRules);
					Assert.IsTrue (builder.Build (proj), "Missing resource keep rules should be regenerated.");
					FileAssert.Exists (aaptRules);
					Assert.AreEqual (originalAaptRules, File.ReadAllText (aaptRules));
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreateBaseApk"));
				}

				proj.SetProperty ("AndroidEnableR8Obfuscation", "false");
				Assert.IsTrue (builder.Install (proj), "Disabling obfuscation should rebuild and install the baseline.");
				StringAssert.Contains ("-dontobfuscate", File.ReadAllText (Path.Combine (intermediate, "proguard", "proguard_xamarin.cfg")));
				AssertAppRuns ("r8-disabled.log");
			} finally {
				Assert.IsTrue (builder.Uninstall (proj), "Obfuscated app should uninstall.");
			}
		}
	}
}
