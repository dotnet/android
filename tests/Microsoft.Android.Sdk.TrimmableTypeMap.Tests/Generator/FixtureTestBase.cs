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

	internal static List<JavaPeerInfo> ScanFixtures () => _cachedFixtures.Value;

	internal static JavaPeerInfo FindFixtureByJavaName (string javaName)
	{
		var peers = ScanFixtures ();
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	internal static string CreateTempDir ()
	{
		var dir = Path.Combine (Path.GetTempPath (), "TrimmableTypeMap_" + Path.GetRandomFileName ());
		Directory.CreateDirectory (dir);
		return dir;
	}

	internal static void DeleteTempDir (string dir)
	{
		if (Directory.Exists (dir))
			try { Directory.Delete (dir, true); } catch { }
	}

	internal static JavaPeerInfo MakeMcwPeer (string jniName, string managedName, string asmName)
	{
		var ns = managedName.Contains ('.') ? managedName.Substring (0, managedName.LastIndexOf ('.')) : "";
		var typePart = managedName.Contains ('.') ? managedName.Substring (managedName.LastIndexOf ('.') + 1) : managedName;
		var shortName = typePart.Contains ('+') ? typePart.Substring (typePart.LastIndexOf ('+') + 1) : typePart;
		return new JavaPeerInfo {
			JavaName = jniName,
			CompatJniName = jniName,
			ManagedTypeName = managedName,
			ManagedTypeNamespace = ns,
			ManagedTypeShortName = shortName,
			AssemblyName = asmName,
		};
	}

	internal static JavaPeerInfo MakePeerWithActivation (string jniName, string managedName, string asmName)
	{
		return MakeMcwPeer (jniName, managedName, asmName) with {
			ActivationCtor = new ActivationCtorInfo {
				DeclaringTypeName = managedName,
				DeclaringAssemblyName = asmName,
				Style = ActivationCtorStyle.XamarinAndroid,
			},
		};
	}

	internal static JavaPeerInfo MakeAcwPeer (string jniName, string managedName, string asmName)
	{
		return MakePeerWithActivation (jniName, managedName, asmName) with {
			MarshalMethods = new List<MarshalMethodInfo> {
				new MarshalMethodInfo {
					JniName = "<init>",
					NativeCallbackName = "n_ctor",
					JniSignature = "()V",
					ManagedMethodName = ".ctor",
					JniReturnType = "V",
					IsConstructor = true,
				},
			},
		};
	}

	/// <summary>
	/// Creates a <see cref="JavaPeerInfo"/> representing a Java interface peer with an associated invoker.
	/// </summary>
	/// <example>
	/// MakeInterfacePeer("android/view/View$OnClickListener", "Android.Views.View+IOnClickListener",
	///     "Mono.Android", "Android.Views.View+IOnClickListenerInvoker")
	/// </example>
	internal static JavaPeerInfo MakeInterfacePeer (string jniName, string managedName, string asmName, string invokerName)
	{
		var ns = managedName.Contains ('.') ? managedName.Substring (0, managedName.LastIndexOf ('.')) : "";
		var shortName = managedName.Contains ('.') ? managedName.Substring (managedName.LastIndexOf ('.') + 1) : managedName;
		return new JavaPeerInfo {
			JavaName = jniName,
			CompatJniName = jniName,
			ManagedTypeName = managedName,
			ManagedTypeNamespace = ns,
			ManagedTypeShortName = shortName,
			AssemblyName = asmName,
			IsInterface = true,
			InvokerTypeName = invokerName,
		};
	}
}
