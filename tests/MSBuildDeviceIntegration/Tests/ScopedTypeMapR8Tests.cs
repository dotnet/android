using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
[Category ("UsesDevice")]
public class ScopedTypeMapR8Tests : DeviceTest
{
	[TestCase ("llvm-ir")]
	[TestCase ("trimmable")]
	public void PreservesJniMembersAndShrinksJavaOnlyDependencies (string implementation)
	{
		var proj = new XamarinAndroidApplicationProject (packageName: PackageUtils.MakePackageName (AndroidRuntime.CoreCLR, "scopedr8_" + implementation.Replace ("-", ""))) {
			IsRelease = true,
		};
		proj.SetRuntime (AndroidRuntime.CoreCLR);
		proj.SetRuntimeIdentifiers ([DeviceAbi]);
		proj.SetProperty ("AndroidTypeMapImplementation", implementation);
		proj.SetProperty ("AndroidLinkTool", "r8");
		proj.SetProperty ("AndroidR8ObfuscationMode", "disabled");
		proj.SetProperty ("_AndroidEnableTypemapR8Trimming", "true");
		proj.SetProperty ("TrimMode", "full");
		proj.SetProperty ("AndroidSdkDirectory", AndroidSdkResolver.GetAndroidSdkPath ());
		proj.SetProperty ("JavaSdkDirectory", AndroidSdkResolver.GetJavaSdkPath ());
		proj.SetDefaultTargetDevice ();
		proj.AndroidJavaSources.Add (JavaSource ("ScopedBase.java", """
			package example;
			public class ScopedBase {
			    public int inheritedField = 11;
			    private int privateField = 13;
			    public static int staticField = 17;
			    public int inheritedMethod() { return 19; }
			    protected int protectedMethod() { return 23; }
			}
			"""));
		proj.AndroidJavaSources.Add (JavaSource ("ScopedContract.java", """
			package example;
			public interface ScopedContract {
			    default int defaultMethod() { return 29; }
			}
			"""));
		proj.AndroidJavaSources.Add (JavaSource ("ScopedPeer.java", """
			package example;
			public class ScopedPeer extends ScopedBase implements ScopedContract {
			    public ScopedPeer() {}
			    public int keptMethod() { return Helper.used(); }
			    public int jniOnly() { return 3; }
			}
			class Helper {
			    static int used() { return 7; }
			    static int unused() { return UnusedDependency.value(); }
			}
			class UnusedDependency {
			    static int value() { return 42; }
			}
			"""));
		var marker = "R8_SCOPED_MEMBERS_PASS " + Guid.NewGuid ().ToString ("N");
		proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_ONCREATE}", """
			using var peer = new Example.ScopedPeer ();
			Require (peer.KeptMethod (), 7);
			Require (((Example.IScopedContract) peer).DefaultMethod (), 29);
			var klass = Android.Runtime.JNIEnv.GetObjectClass (peer.Handle);
			try {
				Require (Invoke ("jniOnly"), 3);
				Require (Invoke ("inheritedMethod"), 19);
				Require (Invoke ("protectedMethod"), 23);
				Require (Invoke ("defaultMethod"), 29);
				Require (ReadField ("inheritedField"), 11);
				Require (ReadField ("privateField"), 13);
				var field = Android.Runtime.JNIEnv.GetStaticFieldID (klass, "staticField", "I");
				Require (Android.Runtime.JNIEnv.GetStaticIntField (klass, field), 17);
				Console.WriteLine ("${MARKER}");
			} finally {
				Android.Runtime.JNIEnv.DeleteLocalRef (klass);
			}

			int Invoke (string name) {
				var method = Android.Runtime.JNIEnv.GetMethodID (klass, name, "()I");
				return Android.Runtime.JNIEnv.CallIntMethod (peer.Handle, method);
			}
			int ReadField (string name) {
				var field = Android.Runtime.JNIEnv.GetFieldID (klass, name, "I");
				return Android.Runtime.JNIEnv.GetIntField (peer.Handle, field);
			}
			static void Require (int actual, int expected) {
				if (actual != expected) {
					throw new InvalidOperationException ($"JNI result {actual}, expected {expected}.");
				}
			}
			""".Replace ("${MARKER}", marker, StringComparison.Ordinal));

		using var builder = CreateApkBuilder ();
		bool installed = false;
		try {
			installed = builder.Install (proj);
			Assert.IsTrue (installed, "The scoped-retention app should install.");
			var projectDirectory = Path.Combine (Root, builder.ProjectDirectory);
			var dexFiles = Directory.GetFiles (Path.Combine (projectDirectory, proj.IntermediateOutputPath), "classes*.dex", SearchOption.AllDirectories);
			Assert.IsNotEmpty (dexFiles);
			Assert.IsTrue (dexFiles.Any (dex => DexUtils.ContainsClassWithMethod ("Lexample/Helper;", "used", "()I", dex, AndroidSdkPath)));
			Assert.IsFalse (dexFiles.Any (dex => DexUtils.ContainsClassWithMethod ("Lexample/Helper;", "unused", "()I", dex, AndroidSdkPath)),
				"Preserving JNI-facing peers must not keep unused members of Java-only dependencies.");
			Assert.IsFalse (dexFiles.Any (dex => DexUtils.ContainsClass ("Lexample/UnusedDependency;", dex, AndroidSdkPath)));
			Assert.IsTrue (MonitorAdbLogcat (line => line.Contains (marker, StringComparison.Ordinal),
				Path.Combine (projectDirectory, "scoped-members-logcat.log"), ActivityStartTimeoutInSeconds,
				onMonitoringStarted: () => StartActivityAndAssert (proj)),
				"The app must successfully invoke preserved JNI methods and fields, including inherited members.");
		} finally {
			if (installed) {
				RunAdbCommand ($"uninstall {proj.PackageName}");
			}
		}
	}

	static AndroidItem.AndroidJavaSource JavaSource (string name, string source) => new (name) {
		Encoding = Encoding.ASCII,
		TextContent = () => source,
		Metadata = {
			{ "Bind", "True" },
		},
	};
}
