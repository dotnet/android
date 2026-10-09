using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.Android.Tasks.JniRemapping;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	[Category ("UsesDevice")]
	public class R8RuntimeRemappingTests : DeviceTest
	{
		static bool ContainsField (string [] dexDump, string owner, string name, string signature)
		{
			bool inClass = false;
			bool hasName = false;
			foreach (var line in dexDump) {
				var separator = line.IndexOf (':');
				if (separator < 0) {
					continue;
				}
				var key = line.Substring (0, separator).Trim ();
				var value = line.Substring (separator + 1).Trim ();
				if (key == "Class descriptor") {
					inClass = value == $"'L{owner};'";
					hasName = false;
				} else if (inClass && key == "name") {
					hasName = value == $"'{name}'";
				} else if (hasName && key == "type") {
					if (value == $"'{signature}'") {
						return true;
					}
					hasName = false;
				}
			}
			return false;
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
								public int inheritedValue = 19;
								public static int inheritedStaticValue = 23;
								public int rawValue = 53;
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
			const string rawFieldRules = "-keepclassmembers class example.RuntimePeer { public int rawValue; }";
			string extraRules = rawFieldRules;
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

						[Register ("inheritedValue")]
						public int InheritedValue {
							get => _members.InstanceFields.GetInt32Value ("inheritedValue.I", this);
							set => _members.InstanceFields.SetValue ("inheritedValue.I", this, value);
						}

						[Register ("inheritedStaticValue")]
						public static int InheritedStaticValue {
							get => _members.StaticFields.GetInt32Value ("inheritedStaticValue.I");
							set => _members.StaticFields.SetValue ("inheritedStaticValue.I", value);
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
						peer.Value != 13 || Example.RuntimePeer.StaticValue != 17 || echoed.Value != 7 ||
						boundHidden.HiddenAdd () != 31 || constructedHidden.HiddenValue != 23 ||
						boundHidden.Add (1) != 8 ||
						echoed.GetType () != typeof (Example.RuntimePeer) ||
						hidden.GetType () != HiddenPeerBinding.GetBindingType ())
					throw new InvalidOperationException ("Obfuscated JNI lookup returned an incorrect value or managed type.");
				boundHidden.InheritedValue = 37;
				HiddenPeerBinding.InheritedStaticValue = 41;
				if (boundHidden.InheritedValue != 37 || HiddenPeerBinding.InheritedStaticValue != 41)
					throw new InvalidOperationException ("Inherited field reads or writes failed.");
				var klass = Android.Runtime.JNIEnv.GetObjectClass (peer.Handle);
				try {
					var instanceField = Android.Runtime.JNIEnv.GetFieldID (klass, "rawValue", "I");
					Android.Runtime.JNIEnv.SetField (peer.Handle, instanceField, 71);
					if (Android.Runtime.JNIEnv.GetIntField (peer.Handle, instanceField) != 71)
						throw new InvalidOperationException ("Raw JNI field reads or writes failed.");
				} finally {
					Android.Runtime.JNIEnv.DeleteLocalRef (klass);
				}
				using var peerClass = peer.Class;
				using var reflectedField = peerClass.GetDeclaredField ("rawValue");
				reflectedField.SetInt (peer, 79);
				if (reflectedField.GetInt (peer) != 79)
					throw new InvalidOperationException ("Reflection field reads or writes failed.");
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
				var finalMapping = Path.Combine (intermediate, "r8-jni-final-mapping.txt");
				FileAssert.Exists (finalMapping);
				var mapping = R8Mapping.Load (finalMapping);
				var dexDumps = Directory.GetFiles (intermediate, "classes*.dex", SearchOption.AllDirectories)
					.Select (file => DexUtils.GetDexDump (file, AndroidSdkPath).ToArray ()).ToArray ();
				Assert.IsNotEmpty (dexDumps, "Field assertions must inspect actual DEX output.");
				foreach (var (owner, name) in new [] {
					("example/RuntimePeer", "value"),
					("example/RuntimePeer", "staticValue"),
					("example/RuntimePeer", "inheritedValue"),
					("example/RuntimePeer", "inheritedStaticValue"),
					("example/HiddenPeer", "hiddenValue"),
				}) {
					Assert.IsTrue (mapping.TryGetRenamedClass (owner, out var renamedOwner));
					var renamedField = name;
					if (runtime == AndroidRuntime.NativeAOT) {
						Assert.IsTrue (mapping.TryGetRenamedField (owner, name, out renamedField));
						Assert.AreNotEqual (name, renamedField, $"{owner}.{name} must really be obfuscated.");
						Assert.IsTrue (elements.Any (e => e.Name == "replace-field" &&
							(string) e.Attribute ("source-type") == renamedOwner &&
							(string) e.Attribute ("source-field-name") == name &&
							(string) e.Attribute ("source-field-signature") == "I" &&
							(string) e.Attribute ("target-field-name") == renamedField),
							$"{owner}.{name} must survive retention in the runtime remapping table.");
						Assert.IsFalse (dexDumps.Any (dump => ContainsField (dump, renamedOwner, name, "I")),
							$"DEX must not contain the original name {renamedOwner}.{name}.");
					}
					Assert.IsTrue (dexDumps.Any (dump => ContainsField (dump, renamedOwner, renamedField, "I")),
						$"DEX must contain mapped field {renamedOwner}.{renamedField}.");
					TestContext.WriteLine ($"Field mapping: {owner}.{name}:I -> {renamedOwner}.{renamedField}:I");
				}
				Assert.IsTrue (dexDumps.Any (dump => ContainsField (dump, "example/RuntimePeer", "rawValue", "I")),
					"Explicit keep rules must protect constant-name JNI/reflection access.");
				Assert.IsTrue (dexDumps.Any (dump => ContainsField (dump,
					"net/dot/android/ApplicationRegistration", "Context", "Landroid/content/Context;")),
					"Application.Context uses a constant-name raw JNI field lookup.");
				if (runtime == AndroidRuntime.NativeAOT) {
					Assert.IsTrue (dexDumps.Any (dump => ContainsField (dump,
						"net/dot/jni/nativeaot/NativeAotEnvironmentVars", "systemProperties", "[Ljava/lang/String;")),
						"The native bootstrap must read systemProperties before runtime remapping is available.");
				}
				Assert.AreEqual (runtime == AndroidRuntime.CoreCLR,
					File.ReadAllText (Path.Combine (intermediate, "proguard", "proguard_xamarin.cfg"))
						.Contains ("-keepclassmembernames class * { <fields>; }", StringComparison.Ordinal));
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

				AssertAppRuns ("r8-runtime-remap.log");

				Assert.IsTrue (builder.Build (proj), "A no-op build should succeed.");
				AssertR8Invocations (builder, 0, runtime);
				Assert.IsTrue (builder.Output.IsTargetSkipped ("_CompileToDalvik"));

				if (runtime == AndroidRuntime.NativeAOT) {
					var aaptRules = Path.Combine (intermediate, "aapt_rules.txt");
					FileAssert.Exists (aaptRules);
					var originalAaptRules = File.ReadAllText (aaptRules);
					Assert.IsTrue (builder.Build (proj), "A no-op build should succeed.");
					AssertR8Invocations (builder, 0, runtime);
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
					AssertR8Invocations (builder, 0, runtime);
					FileAssert.Exists (remapObject);
					Assert.AreEqual (ilcTimestamp, File.GetLastWriteTimeUtc (ilcObject), "Recovering the late-linked table must not recompile IL.");
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_AndroidCompileNativeAotR8Remapping"));
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_AndroidLinkNativeAotSharedLibrary"));

					File.Delete (aaptRules);
					Assert.IsTrue (builder.Build (proj), "Missing resource keep rules should be regenerated.");
					AssertR8Invocations (builder, 1, runtime);
					FileAssert.Exists (aaptRules);
					Assert.AreEqual (originalAaptRules, File.ReadAllText (aaptRules));
					Assert.IsFalse (builder.Output.IsTargetSkipped ("_CreateBaseApk"));
				}

				File.Delete (finalMapping);
				Assert.IsTrue (builder.Build (proj), "A missing final mapping must rerun R8, not reuse stale tables.");
				AssertR8Invocations (builder, 1, runtime);
				FileAssert.Exists (finalMapping);

				extraRules = rawFieldRules + Environment.NewLine +
					"-keepclassmembernames class example.RuntimePeer { public int add(int); }";
				proj.Touch ("r8-custom.pro");
				builder.Save (proj, doNotCleanupOnUpdate: true, saveProject: false);
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
				AssertAppRuns ("r8-disabled.log");
			} finally {
				Assert.IsTrue (builder.Uninstall (proj), "Obfuscated app should uninstall.");
			}
		}
	}
}
