using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class R8RuntimeRemappingTests : DeviceTest
	{
		[Test]
		public void CoreClrLargeBootstrapStartsInDebug ()
		{
			if (IgnoreUnsupportedConfiguration (AndroidRuntime.CoreCLR, release: false)) {
				return;
			}
			var proj = new XamarinAndroidApplicationProject {
				EnableDefaultItems = true,
			};
			proj.SetRuntime (AndroidRuntime.CoreCLR);
			proj.SetRuntimeIdentifiers ([DeviceAbi]);
			proj.SetDefaultTargetDevice ();
			proj.OtherBuildItems.Add (new BuildItem ("AndroidEnvironment", "bootstrap-env.txt") {
				TextContent = () => "CORECLR_BOOTSTRAP_ENV=from-binary-blob",
			});
			for (int i = 0; i < 5000; i++) {
				proj.OtherBuildItems.Add (new BuildItem ("RuntimeHostConfigurationOption", $"Test.Bootstrap.{i:D4}") {
					Metadata = { { "Value", new string ('v', 16) + i } },
				});
			}
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", """
				if (System.AppContext.GetData ("Test.Bootstrap.0000") is not string first ||
						first != "vvvvvvvvvvvvvvvv0" ||
						System.AppContext.GetData ("Test.Bootstrap.4999") is not string last ||
						last != "vvvvvvvvvvvvvvvv4999" ||
						System.Environment.GetEnvironmentVariable ("CORECLR_BOOTSTRAP_ENV") != "from-binary-blob" ||
						System.String.IsNullOrEmpty (System.AppContext.BaseDirectory))
					throw new System.InvalidOperationException ("CoreCLR bootstrap properties were not applied.");
				System.Console.WriteLine ("CORECLR_LARGE_BOOTSTRAP_SUCCESS");
				""");
			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj), "The application with 5000 bootstrap properties should install.");
			var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
			byte [] bootstrap = File.ReadAllBytes (Directory.GetFiles (intermediate, "coreclr-bootstrap.bin",
				SearchOption.AllDirectories).Single ());
			Assert.Greater (bootstrap.Length, 64 * 1024);
			Assert.GreaterOrEqual (BitConverter.ToUInt32 (bootstrap, 20), 5003u,
				"The application should retain all 5000 custom properties and three runtime-owned properties.");
			ClearAdbLogcat ();
			RunProjectAndAssert (proj, builder, doNotCleanupOnUpdate: true);
			Assert.IsTrue (MonitorAdbLogcat (
				line => line.Contains ("CORECLR_LARGE_BOOTSTRAP_SUCCESS", StringComparison.Ordinal),
				Path.Combine (Root, builder.ProjectDirectory, "coreclr-large-bootstrap.log"), timeout: 30));
		}

		void AssertR8Invocations (ProjectBuilder builder, int expected, AndroidRuntime runtime, bool obfuscationEnabled = true)
		{
			var binlog = Path.Combine (Root, builder.ProjectDirectory, $"{Path.GetFileNameWithoutExtension (builder.BuildLogFile)}.binlog");
			var build = BinaryLog.ReadBuild (binlog);
			var tasks = build.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Task> ().ToList ();
			var r8 = tasks.Where (t => t.Name == "R8").ToList ();
			Assert.AreEqual (expected, r8.Count, $"Unexpected R8 invocation count in {binlog}.");
			if (expected != 1 || !obfuscationEnabled) {
				return;
			}
			string trimTargetName = runtime == AndroidRuntime.NativeAOT ? "IlcCompile" : "_RunILLink";
			var trimmingTargets = build.FindChildrenRecursive<Target> (t => t.Name == trimTargetName).ToList ();
			Assert.IsNotEmpty (trimmingTargets, $"Expected the {trimTargetName} target before R8.");
			foreach (var trimming in trimmingTargets) {
				Assert.LessOrEqual (trimming.EndTime, r8 [0].StartTime, "R8 must run after managed trimming/ILC.");
			}
			var nativeLinkTasks = tasks.Where (t =>
				runtime == AndroidRuntime.NativeAOT
					? t.Name == "LinkNativeAotSharedLibrary"
					: t.Name == "LinkNativeRuntime" || t.Name == "LinkApplicationSharedLibraries").ToList ();
			Assert.IsNotEmpty (nativeLinkTasks, "Expected the final native link after R8.");
			foreach (var link in nativeLinkTasks) {
				Assert.GreaterOrEqual (link.StartTime, r8 [0].EndTime, "Native linking must consume the final R8 mapping.");
			}
		}

		[TestCase (AndroidRuntime.CoreCLR, false)]
		[TestCase (AndroidRuntime.NativeAOT, true)]
		public void EmptyExplicitRemappingRuns (AndroidRuntime runtime, bool release)
		{
			if (IgnoreUnsupportedConfiguration (runtime, release))
				return;
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = release,
				OtherBuildItems = {
					new BuildItem ("_AndroidRemapMembers", "empty-remap.xml") { TextContent = () => "<replacements />" },
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
			proj.SetDefaultTargetDevice ();
			proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}",
				"""Console.WriteLine ("EMPTY_REMAP_SUCCESS");""");
			using var builder = CreateApkBuilder ();
			Assert.IsTrue (builder.Install (proj));
			try {
				var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
				Assert.IsEmpty (Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories));
				ClearAdbLogcat ();
				RunProjectAndAssert (proj, builder, doNotCleanupOnUpdate: true);
				Assert.IsTrue (MonitorAdbLogcat (line => line.Contains ("EMPTY_REMAP_SUCCESS", StringComparison.Ordinal),
					Path.Combine (Root, builder.ProjectDirectory, "empty-remap.log"), timeout: 30));
			} finally {
				Assert.IsTrue (builder.Uninstall (proj));
			}
		}

		[TestCase (AndroidRuntime.CoreCLR, false)]
		[TestCase (AndroidRuntime.CoreCLR, true)]
		[TestCase (AndroidRuntime.NativeAOT, false)]
		[TestCase (AndroidRuntime.NativeAOT, true)]
		public void ObfuscatedMembersRun (AndroidRuntime runtime, bool compress)
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

							class HiddenPeer extends RuntimePeer {
								public int hiddenValue = 23;
								public HiddenPeer () {}
								public int hiddenAdd () { return hiddenValue + 2; }
							}
							""",
					},
				},
			};
			proj.SetRuntime (runtime);
			proj.SetRuntimeIdentifiers (new [] { DeviceAbi });
			proj.SetDefaultTargetDevice ();
			proj.SetProperty ("AndroidLinkTool", "r8");
			proj.SetProperty ("AllowUnsafeBlocks", "true");
			proj.SetProperty ("TrimMode", "full");
			proj.SetProperty ("AndroidR8ObfuscationMode", "runtime-remapping");
			if (compress)
				proj.SetProperty ("_AndroidR8CompressBinaryBlobs", "true");
			proj.SetProperty ("AndroidCreateProguardMappingFile", "false");
			proj.SetProperty ("ProguardConfigFiles", "r8-custom.pro");
			string extraRules = "";
			proj.OtherBuildItems.Add (new BuildItem ("None", "r8-custom.pro") {
				TextContent = () => extraRules,
			});
			proj.Sources.Add (new BuildItem.Source ("HiddenPeerBinding.cs") {
				TextContent = () => """
					using System;
					using System.Diagnostics.CodeAnalysis;
					using Android.Runtime;
					using Java.Interop;

					[Register ("example/HiddenPeer", DoNotGenerateAcw = true)]
					public class HiddenPeerBinding : Example.RuntimePeer
					{
						static readonly JniPeerMembers _members = new XAPeerMembers ("example/HiddenPeer", typeof (HiddenPeerBinding));
						public override JniPeerMembers JniPeerMembers => _members;
						protected override IntPtr ThresholdClass => _members.JniPeerType.PeerReference.Handle;
						protected override Type ThresholdType => _members.ManagedPeerType;

						public HiddenPeerBinding () {}
						public HiddenPeerBinding (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer) {}

						[Register ("hiddenValue")]
						public int HiddenValue {
							get => _members.InstanceFields.GetInt32Value ("hiddenValue.I", this);
							set => _members.InstanceFields.SetValue ("hiddenValue.I", this, value);
						}

						[Register ("hiddenAdd", "()I", "")]
						public unsafe int HiddenAdd () => _members.InstanceMethods.InvokeVirtualInt32Method ("hiddenAdd.()I", this, null);

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
				using var constructedHidden = new HiddenPeerBinding ();
				var boundHidden = (HiddenPeerBinding) hidden;
				boundHidden.HiddenValue = 29;
				if (peer.Add (2) != 15 || peer.Add ("abc") != 16 ||
						Example.RuntimePeer.StaticValue != 17 || echoed.Value != 7 ||
						boundHidden.HiddenAdd () != 31 || constructedHidden.HiddenValue != 23 ||
						boundHidden.Add (1) != 8 ||
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
			AssertR8Invocations (builder, 1, runtime);
			try {
				var intermediate = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath);
				var remapFiles = Directory.GetFiles (intermediate, "r8-jni-remap.xml", SearchOption.AllDirectories);
				Assert.IsNotEmpty (remapFiles, "A compact runtime remapping file should be generated.");
				var elements = remapFiles.SelectMany (file => XDocument.Load (file).Root.Elements ()).ToList ();
				Assert.IsTrue (elements.Any (e => e.Name == "replace-method" &&
					(string) e.Attribute ("source-method-name") == "add" &&
					(string) e.Attribute ("source-method-signature") == "(I)I" &&
					(string) e.Attribute ("target-method-name") != "add"), "The exercised methods must really be obfuscated.");
				Assert.IsFalse (elements.Any (e => e.Name == "replace-field" &&
					((string) e.Attribute ("source-field-name") == "value" ||
						(string) e.Attribute ("source-field-name") == "staticValue" ||
						(string) e.Attribute ("source-field-name") == "hiddenValue")),
					"Fields with stable names and signatures do not need member remapping entries.");
				StringAssert.Contains ("-keepclassmembernames class * { <fields>; }",
					File.ReadAllText (Path.Combine (intermediate, "proguard", "proguard_xamarin.cfg")));
				Assert.IsTrue (elements.Any (e => e.Name == "replace-type" &&
					(string) e.Attribute ("from") == "example/HiddenPeer" &&
					(string) e.Attribute ("to") != "example/HiddenPeer"), "Java-to-managed activation must exercise a genuinely renamed class.");
				var hiddenType = (string) elements.First (e => e.Name == "replace-type" &&
					(string) e.Attribute ("from") == "example/HiddenPeer").Attribute ("to");
				Assert.IsTrue (elements.Any (e => e.Name == "replace-method" &&
					(string) e.Attribute ("source-type") == hiddenType &&
					(string) e.Attribute ("source-method-name") == "hiddenAdd" &&
					(string) e.Attribute ("target-method-name") != "hiddenAdd"), "Method lookups must use the renamed owner.");
				Assert.IsFalse (elements.Any (e => (string) e.Attribute ("source-method-name") == "unusedMethod"),
					"An unused method on a retained type must not occupy the runtime table.");
				var binaryBlob = Directory.GetFiles (intermediate, "libbinary_blobs.so", SearchOption.AllDirectories).Single ();
				byte [] data = File.ReadAllBytes (binaryBlob);
				int envelope = data.AsSpan ().IndexOf (new byte [] { 0x42, 0x4c, 0x42, 0x42 });
				Assert.GreaterOrEqual (envelope, 0, "The ELF must contain the remapping payload.");
				Assert.AreEqual (compress ? 1 : 0, BitConverter.ToUInt16 (data, envelope + 6), "Unexpected compression mode.");

				AssertAppRuns ("r8-runtime-remap.log");

				var aaptRules = Path.Combine (intermediate, "aapt_rules.txt");
				string? originalAaptRules = null;
				if (runtime == AndroidRuntime.NativeAOT) {
					FileAssert.Exists (aaptRules);
					originalAaptRules = File.ReadAllText (aaptRules);
				}
				Assert.IsTrue (builder.Build (proj), "A no-op build should succeed.");
				AssertR8Invocations (builder, 0, runtime);
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_CompileToDalvik"));

				if (runtime == AndroidRuntime.NativeAOT) {
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidGenerateNativeAotR8Remapping"));
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidGenerateNativeAotR8BinaryBlobs"));
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidLinkNativeAotSharedLibrary"));
					FileAssert.Exists (aaptRules, "IncrementalClean must retain AAPT keep rules.");
					Assert.AreEqual (originalAaptRules, File.ReadAllText (aaptRules));

					var ilcObject = Directory.GetFiles (intermediate, $"{proj.ProjectName}.o", SearchOption.AllDirectories).Single ();
					var ilcTimestamp = File.GetLastWriteTimeUtc (ilcObject);
					File.Delete (binaryBlob);
					Assert.IsTrue (builder.Build (proj), "A missing binary-blob library should be regenerated.");
					AssertR8Invocations (builder, 0, runtime);
					FileAssert.Exists (binaryBlob);
					Assert.AreEqual (ilcTimestamp, File.GetLastWriteTimeUtc (ilcObject), "Recovering the data library must not recompile IL.");
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_AndroidGenerateNativeAotR8BinaryBlobs"));
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidLinkNativeAotSharedLibrary"),
						"Remapping-data changes must not relink the application.");

					File.Delete (aaptRules);
					Assert.IsTrue (builder.Build (proj), "Missing resource keep rules should be regenerated.");
					AssertR8Invocations (builder, 1, runtime);
					FileAssert.Exists (aaptRules);
					Assert.AreEqual (originalAaptRules, File.ReadAllText (aaptRules));
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreateBaseApk"));
				}

				var finalMapping = Path.Combine (intermediate, "r8-jni-final-mapping.txt");
				FileAssert.Exists (finalMapping);
				File.Delete (finalMapping);
				Assert.IsTrue (builder.Build (proj), "A missing final mapping must rerun R8, not reuse stale tables.");
				AssertR8Invocations (builder, 1, runtime);
				FileAssert.Exists (finalMapping);

				extraRules = "-keepclassmembernames class example.RuntimePeer { public int add(int); }";
				proj.Touch ("r8-custom.pro");
				Assert.IsTrue (builder.Install (proj), "Changed R8 rules must update the late-linked tables.");
				AssertR8Invocations (builder, 1, runtime);
				var changedElements = Directory.GetFiles (intermediate, "r8-jni-remap.xml", SearchOption.AllDirectories)
					.SelectMany (file => XDocument.Load (file).Root.Elements ()).ToList ();
				Assert.IsFalse (changedElements.Any (e => e.Name == "replace-method" &&
					(string) e.Attribute ("source-method-name") == "add" &&
					(string) e.Attribute ("source-method-signature") == "(I)I"),
					"The changed rule should remove the int overload's remapping entry.");
				Assert.IsTrue (changedElements.Any (e => e.Name == "replace-method" &&
					(string) e.Attribute ("source-method-name") == "add" &&
					(string) e.Attribute ("source-method-signature") == "(Ljava/lang/String;)I" &&
					(string) e.Attribute ("target-method-name") != "add"),
					"The other exercised overload should remain remapped.");
				AssertAppRuns ("r8-changed-rules.log");

				proj.SetProperty ("AndroidR8ObfuscationMode", "disabled");
				Assert.IsTrue (builder.Install (proj), "Disabling obfuscation should rebuild and install the baseline.");
				AssertR8Invocations (builder, 1, runtime, obfuscationEnabled: false);
				StringAssert.Contains ("-dontobfuscate", File.ReadAllText (Path.Combine (intermediate, "proguard", "proguard_xamarin.cfg")));
				using (var apk = ZipFile.OpenRead (Path.Combine (Root, builder.ProjectDirectory,
					proj.OutputPath, $"{proj.PackageName}-Signed.apk"))) {
					Assert.IsNull (apk.GetEntry ($"lib/{DeviceAbi}/libbinary_blobs.so"),
						"Disabling obfuscation without explicit remapping must remove the stale packaged library.");
				}
				AssertAppRuns ("r8-disabled.log");
			} finally {
				Assert.IsTrue (builder.Uninstall (proj), "Obfuscated app should uninstall.");
			}
		}
	}
}
