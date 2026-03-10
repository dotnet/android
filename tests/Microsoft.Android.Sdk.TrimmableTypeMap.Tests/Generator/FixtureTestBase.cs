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

	private protected static List<JavaPeerInfo> ScanFixtures () => _cachedFixtures.Value;

	private protected static JavaPeerInfo FindFixtureByJavaName (string javaName)
	{
		var peers = ScanFixtures ();
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	private protected static JavaPeerInfo FindFixtureByManagedName (string managedName)
	{
		var peers = ScanFixtures ();
		var peer = peers.FirstOrDefault (p => p.ManagedTypeName == managedName);
		Assert.NotNull (peer);
		return peer;
	}

	static (string ns, string shortName) ParseManagedTypeName (string managedName)
	{
		var ns = managedName.Contains ('.') ? managedName.Substring (0, managedName.LastIndexOf ('.')) : "";
		var typePart = managedName.Contains ('.') ? managedName.Substring (managedName.LastIndexOf ('.') + 1) : managedName;
		var shortName = typePart.Contains ('+') ? typePart.Substring (typePart.LastIndexOf ('+') + 1) : typePart;
		return (ns, shortName);
	}

	private protected static JavaPeerInfo MakeMcwPeer (string jniName, string managedName, string asmName)
	{
		var (ns, shortName) = ParseManagedTypeName (managedName);
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
		return MakeMcwPeer (jniName, managedName, asmName) with {
			ActivationCtor = new ActivationCtorInfo {
				DeclaringTypeName = managedName,
				DeclaringAssemblyName = asmName,
				Style = ActivationCtorStyle.XamarinAndroid,
			},
		};
	}

	private protected static JavaPeerInfo MakeAcwPeer (string jniName, string managedName, string asmName)
		=> MakePeerWithActivation (jniName, managedName, asmName);

	private protected static JavaPeerInfo MakeInterfacePeer (
		string jniName,
		string managedName,
		string asmName,
		string invokerName)
	{
		var (ns, shortName) = ParseManagedTypeName (managedName);
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
