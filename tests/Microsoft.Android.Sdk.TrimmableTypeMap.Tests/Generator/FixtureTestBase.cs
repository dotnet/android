using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public abstract class FixtureTestBase
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (FixtureTestBase).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	static readonly Lazy<List<JavaPeerInfo>> _cachedFixtures = new (() => {
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan (new [] { TestFixtureAssemblyPath });
	});

	protected static List<JavaPeerInfo> ScanFixtures () => _cachedFixtures.Value;

	protected static JavaPeerInfo FindFixtureByJavaName (string javaName)
	{
		var peers = ScanFixtures ();
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	protected static string CreateTempDir ()
	{
		var dir = Path.Combine (Path.GetTempPath (), $"typemap-test-{Guid.NewGuid ():N}");
		Directory.CreateDirectory (dir);
		return dir;
	}

	protected static void DeleteTempDir (string dir)
	{
		if (Directory.Exists (dir))
			try { Directory.Delete (dir, true); } catch { }
	}

	protected static JavaPeerInfo MakeMcwPeer (string jniName, string managedName, string asmName)
	{
		var ns = managedName.Contains ('.') ? managedName.Substring (0, managedName.LastIndexOf ('.')) : "";
		var typePart = managedName.Contains ('.') ? managedName.Substring (managedName.LastIndexOf ('.') + 1) : managedName;
		var shortName = typePart.Contains ('+') ? typePart.Substring (typePart.LastIndexOf ('+') + 1) : typePart;
		return new JavaPeerInfo {
			JavaName = jniName,
			ManagedTypeName = managedName,
			ManagedTypeNamespace = ns,
			ManagedTypeShortName = shortName,
			AssemblyName = asmName,
		};
	}

	protected static JavaPeerInfo MakePeerWithActivation (string jniName, string managedName, string asmName)
	{
		var peer = MakeMcwPeer (jniName, managedName, asmName);
		peer.ActivationCtor = new ActivationCtorInfo {
			Style = ActivationCtorStyle.XamarinAndroid,
		};
		return peer;
	}

	protected static JavaPeerInfo MakeAcwPeer (string jniName, string managedName, string asmName)
	{
		var peer = MakePeerWithActivation (jniName, managedName, asmName);
		peer.DoNotGenerateAcw = false;
		peer.JavaConstructors = new List<JavaConstructorInfo> {
			new JavaConstructorInfo { ConstructorIndex = 0, JniSignature = "()V" },
		};
		peer.MarshalMethods = new List<MarshalMethodInfo> {
			new MarshalMethodInfo {
				JniName = "<init>",
				NativeCallbackName = "n_ctor",
				JniSignature = "()V",
				IsConstructor = true,
			},
		};
		return peer;
	}

	protected static JavaPeerInfo MakeInterfacePeer (
		string jniName = "android/view/View$OnClickListener",
		string managedName = "Android.Views.View+IOnClickListener",
		string asmName = "Mono.Android",
		string invokerName = "Android.Views.View+IOnClickListenerInvoker")
	{
		var ns = managedName.Contains ('.') ? managedName.Substring (0, managedName.LastIndexOf ('.')) : "";
		var shortName = managedName.Contains ('.') ? managedName.Substring (managedName.LastIndexOf ('.') + 1) : managedName;
		return new JavaPeerInfo {
			JavaName = jniName,
			ManagedTypeName = managedName,
			ManagedTypeNamespace = ns,
			ManagedTypeShortName = shortName,
			AssemblyName = asmName,
			IsInterface = true,
			InvokerTypeName = invokerName,
		};
	}

	protected static MarshalMethodInfo MakeMarshalMethod (string jniName, string callbackName, string jniSig, bool isConstructor = false)
	{
		return new MarshalMethodInfo {
			JniName = jniName,
			NativeCallbackName = callbackName,
			JniSignature = jniSig,
			IsConstructor = isConstructor,
		};
	}
}
