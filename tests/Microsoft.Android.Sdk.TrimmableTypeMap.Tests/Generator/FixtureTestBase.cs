using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public abstract class FixtureTestBase
{
	protected static string CreateTempDir ()
	{
		var dir = Path.Combine (Path.GetTempPath (), "TypeMapTests_" + Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (dir);
		return dir;
	}

	protected static void DeleteTempDir (string dir)
	{
		if (Directory.Exists (dir)) {
			Directory.Delete (dir, recursive: true);
		}
	}

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

	private protected static List<JavaPeerInfo> ScanFixtures () => _cachedFixtures.Value;

	private protected static JavaPeerInfo FindFixtureByJavaName (string javaName)
	{
		var peers = ScanFixtures ();
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	private protected static JavaPeerInfo MakeMcwPeer (string jniName, string managedName, string asmName)
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

	private protected static JavaPeerInfo MakePeerWithActivation (string jniName, string managedName, string asmName)
	{
		var peer = MakeMcwPeer (jniName, managedName, asmName);
		peer.ActivationCtor = new ActivationCtorInfo {
			Style = ActivationCtorStyle.XamarinAndroid,
		};
		return peer;
	}

	private protected static JavaPeerInfo MakeAcwPeer (string jniName, string managedName, string asmName)
		=> MakePeerWithActivation (jniName, managedName, asmName);

	private protected static JavaPeerInfo MakeInterfacePeer (
		string jniName,
		string managedName,
		string asmName,
		string invokerName)
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
}
