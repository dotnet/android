using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public abstract class FixtureTestBase
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (FixtureTestBase).Assembly.Location)
				?? throw new InvalidOperationException ("Cannot determine test assembly directory");
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
			CompatJniName = jniName,
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
	{
		return MakePeerWithActivation (jniName, managedName, asmName) with {
			DoNotGenerateAcw = false,
			JavaConstructors = new List<JavaConstructorInfo> {
				new JavaConstructorInfo { ConstructorIndex = 0, JniSignature = "()V" },
			},
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

	private protected static JavaPeerInfo MakeInterfacePeer (
		string jniName = "android/view/View$OnClickListener",
		string managedName = "Android.Views.View+IOnClickListener",
		string asmName = "Mono.Android",
		string invokerName = "Android.Views.View+IOnClickListenerInvoker")
	{
		var (ns, shortName) = ParseManagedTypeName (managedName);
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

	private protected static MarshalMethodInfo MakeMarshalMethod (string jniName, string callbackName, string jniSig, bool isConstructor = false)
	{
		return new MarshalMethodInfo {
			JniName = jniName,
			NativeCallbackName = callbackName,
			JniSignature = jniSig,
			ManagedMethodName = isConstructor ? ".ctor" : callbackName.StartsWith ("n_") ? callbackName.Substring (2) : callbackName,
			JniReturnType = jniSig.Contains (')') ? jniSig.Substring (jniSig.IndexOf (')') + 1) : "V",
			IsConstructor = isConstructor,
		};
	}

	private protected static List<string> GetTypeRefNames (MetadataReader reader) =>
		reader.TypeReferences
			.Select (h => reader.GetTypeReference (h))
			.Select (t => reader.GetString (t.Name))
			.ToList ();

	private protected static List<string> GetMemberRefNames (MetadataReader reader) =>
		Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (i => reader.GetMemberReference (MetadataTokens.MemberReferenceHandle (i)))
			.Select (m => reader.GetString (m.Name))
			.ToList ();

	private protected static string CreateTempDir ()
	{
		var dir = Path.Combine (Path.GetTempPath (), $"typemap-test-{Guid.NewGuid ():N}");
		Directory.CreateDirectory (dir);
		return dir;
	}

	private protected static void DeleteTempDir (string dir)
	{
		if (Directory.Exists (dir))
			try { Directory.Delete (dir, true); } catch { }
	}
}
