using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ELFSharp.ELF;
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
		void AssertR8Invocations (ProjectBuilder builder, int expected, bool obfuscationEnabled = true)
		{
			var binlog = Path.Combine (Root, builder.ProjectDirectory, $"{Path.GetFileNameWithoutExtension (builder.BuildLogFile)}.binlog");
			var build = BinaryLog.ReadBuild (binlog);
			var tasks = build.FindChildrenRecursive<Microsoft.Build.Logging.StructuredLogger.Task> ().ToList ();
			var r8 = tasks.Where (t => t.Name == "R8").ToList ();
			Assert.AreEqual (expected, r8.Count, $"Unexpected R8 invocation count in {binlog}.");
			if (expected != 1 || !obfuscationEnabled) {
				return;
			}
			foreach (var trimming in build.FindChildrenRecursive<Target> (t => t.Name == "_RunILLink" || t.Name == "IlcCompile")) {
				Assert.LessOrEqual (trimming.EndTime, r8 [0].StartTime, "R8 must run after managed trimming/ILC.");
			}
			var assets = tasks.Where (t => t.Name == "GenerateJniRemappingAsset").ToList ();
			Assert.IsNotEmpty (assets, "R8 mappings must reach the binary asset producer.");
			foreach (var asset in assets) {
				Assert.GreaterOrEqual (asset.StartTime, r8 [0].EndTime, "Binary generation must consume the final R8 mapping.");
			}
		}

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
				for (int i = 0; i < 8; i++) {
					GC.Collect ();
					GC.WaitForPendingFinalizers ();
					using var afterCollection = Example.RuntimePeer.Create ();
					if (peer.Add (2) != 15 || boundHidden.HiddenAdd () != 31 || afterCollection.Value != 7)
						throw new InvalidOperationException ("Cached remapping pointers did not survive collection.");
				}
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
			AssertR8Invocations (builder, 1);
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
				var hiddenType = (string) elements.First (e => e.Name == "replace-type" &&
					(string) e.Attribute ("from") == "example/HiddenPeer").Attribute ("to");
				Assert.IsTrue (elements.Any (e => e.Name == "replace-method" &&
					(string) e.Attribute ("source-type") == hiddenType &&
					(string) e.Attribute ("source-method-name") == "hiddenAdd" &&
					(string) e.Attribute ("target-method-name") != "hiddenAdd"), "Method lookups must use the renamed owner.");
				Assert.IsTrue (elements.Any (e => e.Name == "replace-field" &&
					(string) e.Attribute ("source-type") == hiddenType &&
					(string) e.Attribute ("source-field-name") == "hiddenValue" &&
					(string) e.Attribute ("target-field-name") != "hiddenValue"), "Field lookups must use the renamed owner.");
				Assert.IsFalse (elements.Any (e => (string) e.Attribute ("source-method-name") == "unusedMethod"),
					"An unused method on a retained type must not occupy the runtime table.");
				Assert.IsEmpty (Directory.GetFiles (intermediate, "jni_remap.*.ll", SearchOption.AllDirectories));
				Assert.IsEmpty (Directory.GetFiles (intermediate, "jni_remap.*.o", SearchOption.AllDirectories));
				var asset = Directory.GetFiles (intermediate, "jni-remap.bin", SearchOption.AllDirectories).Single ();
				var originalAsset = File.ReadAllBytes (asset);
				var apkPath = Path.Combine (Root, builder.ProjectDirectory, proj.OutputPath, $"{proj.PackageName}-Signed.apk");
				byte [] ReadPackagedPayload ()
				{
					using var apk = System.IO.Compression.ZipFile.OpenRead (apkPath);
					var entry = apk.GetEntry ($"lib/{proj.GetRuntimeIdentifiersAsAbis ().Single ()}/libandroid_runtime_blobs.so")
						?? throw new AssertionException ("The data-only ELF must be packaged for the running ABI.");
					using var data = new MemoryStream ();
					using var input = entry.Open ();
					input.CopyTo (data);
					data.Position = 0;
					using var elf = ELFReader.Load (data, shouldOwnStream: false);
					return elf.Sections.Single (section => section.Name == "payload").GetContents ();
				}
				CollectionAssert.AreEqual (originalAsset, ReadPackagedPayload ());
				var dataElf = Directory.GetFiles (intermediate, "libandroid_runtime_blobs.so", SearchOption.AllDirectories).Single ();
				var dataTimestamp = File.GetLastWriteTimeUtc (dataElf);

				AssertAppRuns ("r8-runtime-remap.log");

				Assert.IsTrue (builder.Build (proj), "A no-op build should succeed.");
				AssertR8Invocations (builder, 0);
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_CompileToDalvik"));
				Assert.AreEqual (dataTimestamp, File.GetLastWriteTimeUtc (dataElf), "A no-op must not regenerate the data ELF.");

				if (runtime == AndroidRuntime.NativeAOT) {
					var aaptRules = Path.Combine (intermediate, "aapt_rules.txt");
					FileAssert.Exists (aaptRules);
					var originalAaptRules = File.ReadAllText (aaptRules);
					Assert.IsTrue (builder.Build (proj), "A no-op build should succeed.");
					AssertR8Invocations (builder, 0);
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidGenerateNativeAotR8Remapping"));
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidGenerateNativeAotR8RemappingAsset"));
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidLinkNativeAotSharedLibrary"));
					FileAssert.Exists (aaptRules, "IncrementalClean must retain AAPT keep rules.");
					Assert.AreEqual (originalAaptRules, File.ReadAllText (aaptRules));

					var ilcObject = Directory.GetFiles (intermediate, $"{proj.ProjectName}.o", SearchOption.AllDirectories).Single ();
					var ilcTimestamp = File.GetLastWriteTimeUtc (ilcObject);
					var nativeTimestamps = Directory.GetFiles (Path.Combine (Root, builder.ProjectDirectory), "*.so", SearchOption.AllDirectories)
						.Where (path => Path.GetFileName (path) != "libandroid_runtime_blobs.so")
						.ToDictionary (path => path, File.GetLastWriteTimeUtc);
					File.Delete (asset);
					Assert.IsTrue (builder.Build (proj), "A missing binary remapping asset should be regenerated.");
					AssertR8Invocations (builder, 0);
					FileAssert.Exists (asset);
					CollectionAssert.AreEqual (originalAsset, File.ReadAllBytes (asset));
					CollectionAssert.AreEqual (originalAsset, ReadPackagedPayload (), "Recovered bytes must reach the packaged data ELF.");
					Assert.AreEqual (ilcTimestamp, File.GetLastWriteTimeUtc (ilcObject), "Recovering the asset must not recompile IL.");
					foreach (var nativeFile in nativeTimestamps) {
						Assert.AreEqual (nativeFile.Value, File.GetLastWriteTimeUtc (nativeFile.Key), "Recovering the asset must not relink native code.");
					}
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_AndroidGenerateNativeAotR8RemappingAsset"));
					Assert.IsTrue (builder.Output.IsTargetSkipped ("_AndroidLinkNativeAotSharedLibrary"));

					File.Delete (aaptRules);
					Assert.IsTrue (builder.Build (proj), "Missing resource keep rules should be regenerated.");
					AssertR8Invocations (builder, 1);
					FileAssert.Exists (aaptRules);
					Assert.AreEqual (originalAaptRules, File.ReadAllText (aaptRules));
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreateBaseApk"));
				}

				var finalMapping = Path.Combine (intermediate, "r8-jni-final-mapping.txt");
				FileAssert.Exists (finalMapping);
				File.Delete (finalMapping);
				Assert.IsTrue (builder.Build (proj), "A missing final mapping must rerun R8, not reuse stale tables.");
				AssertR8Invocations (builder, 1);
				FileAssert.Exists (finalMapping);

				extraRules = "-keepclassmembernames class example.RuntimePeer { public int value; }";
				var nativeFiles = Directory.GetFiles (intermediate, "*.o", SearchOption.AllDirectories)
					.Concat (Directory.GetFiles (intermediate, "*.so", SearchOption.AllDirectories)
						.Where (path => Path.GetFileName (path) != "assembly-store.so" && Path.GetFileName (path) != "libandroid_runtime_blobs.so"))
					.ToDictionary (path => path, File.GetLastWriteTimeUtc);
				proj.Touch ("r8-custom.pro");
				Assert.IsTrue (builder.Install (proj), "Changed R8 rules must update the binary remapping asset.");
				AssertR8Invocations (builder, 1);
				Assert.IsFalse (originalAsset.SequenceEqual (File.ReadAllBytes (asset)), "Changed mappings must change the bytes used at runtime.");
				CollectionAssert.AreEqual (File.ReadAllBytes (asset), ReadPackagedPayload (), "Changed mappings must reach the packaged data ELF.");
				foreach (var nativeFile in nativeFiles) {
					Assert.AreEqual (nativeFile.Value, File.GetLastWriteTimeUtc (nativeFile.Key), "A mapping change must not rebuild native code.");
				}
				AssertAppRuns ("r8-changed-rules.log");

				proj.SetProperty ("AndroidR8ObfuscationMode", "disabled");
				Assert.IsTrue (builder.Install (proj), "Disabling obfuscation should rebuild and install the baseline.");
				AssertR8Invocations (builder, 1, obfuscationEnabled: false);
				StringAssert.Contains ("-dontobfuscate", File.ReadAllText (Path.Combine (intermediate, "proguard", "proguard_xamarin.cfg")));
				var baselineAsset = Path.Combine (intermediate, "android", "jni-remap", "jni-remap.bin");
				Assert.AreEqual (64, new FileInfo (baselineAsset).Length, "Disabling R8 remapping must replace stale mappings with a valid empty asset.");
				CollectionAssert.AreEqual (File.ReadAllBytes (baselineAsset), ReadPackagedPayload (), "Disabling remapping must replace stale data in the packaged ELF.");
				AssertAppRuns ("r8-disabled.log");
			} finally {
				Assert.IsTrue (builder.Uninstall (proj), "Obfuscated app should uninstall.");
			}
		}
	}
}
