using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Covers the experimental <c>[UnmanagedCallersOnly]</c> binding callback format: a binding
/// assembly marked with <c>[assembly: JavaPeerCallbackFormat (2)]</c> emits <c>n_*</c> callbacks
/// that are themselves <c>[UnmanagedCallersOnly]</c> and have no <c>Get*Handler ()</c> connector.
/// Such callbacks must be bound by <c>RegisterNatives</c> directly, because a generated forwarding
/// wrapper would have to managed-call them, which the runtime forbids.
/// </summary>
public class UnmanagedCallersOnlyCallbackTests : FixtureTestBase
{
	static string UcoFixtureAssemblyPath {
		get {
			var dir = Path.GetDirectoryName (typeof (UnmanagedCallersOnlyCallbackTests).Assembly.Location)
				?? throw new InvalidOperationException ("Cannot determine test assembly directory");
			var path = Path.Combine (dir, "TestUcoFixtures.dll");
			Assert.True (File.Exists (path), $"TestUcoFixtures.dll not found at {path}.");
			return path;
		}
	}

	static readonly Lazy<List<JavaPeerInfo>> _ucoPeers = new (() => {
		using var scanner = new JavaPeerScanner ();
		using var ucoReader = new PEReader (File.OpenRead (UcoFixtureAssemblyPath));
		using var fixtureReader = new PEReader (File.OpenRead (TestFixtureAssemblyPath));
		return scanner.Scan (new [] {
			MakeInput (ucoReader),
			MakeInput (fixtureReader),
		});
	});

	static AssemblyInput MakeInput (PEReader peReader)
	{
		var reader = peReader.GetMetadataReader ();
		return new AssemblyInput (reader.GetString (reader.GetAssemblyDefinition ().Name), "", peReader);
	}

	static List<JavaPeerInfo> UcoPeers => _ucoPeers.Value;

	static JavaPeerInfo FindPeer (string managedName) =>
		UcoPeers.FirstOrDefault (p => p.ManagedTypeName == managedName)
			?? throw new InvalidOperationException ($"Peer '{managedName}' was not scanned.");

	static MarshalMethodInfo FindMarshalMethod (JavaPeerInfo peer, string nativeCallbackName) =>
		peer.MarshalMethods.FirstOrDefault (m => m.NativeCallbackName == nativeCallbackName)
			?? throw new InvalidOperationException (
				$"'{nativeCallbackName}' not found on {peer.ManagedTypeName}; found: " +
				string.Join (", ", peer.MarshalMethods.Select (m => m.NativeCallbackName)));

	const string UcoWidget = "Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures.MyWidget";
	const string LegacyWidget = "Microsoft.Android.Sdk.TrimmableTypeMap.Tests.TestUcoFixtures.MyLegacyWidget";

	[Fact]
	public void Scanner_DetectsUnmanagedCallersOnlyCallbacks ()
	{
		var peer = FindPeer (UcoWidget);
		Assert.True (FindMarshalMethod (peer, "n_OnLayout_ZIIII").IsUnmanagedCallersOnlyCallback);
		Assert.True (FindMarshalMethod (peer, "n_GetCount").IsUnmanagedCallersOnlyCallback);
	}

	[Fact]
	public void Scanner_LeavesLegacyConnectorCallbacksAlone ()
	{
		var peer = FindPeer (LegacyWidget);
		Assert.False (FindMarshalMethod (peer, "n_GetFlags").IsUnmanagedCallersOnlyCallback);
	}

	[Fact]
	public void Scanner_CapturesCallbackSignatureForMarkedAssemblies ()
	{
		// The marker forces unconditional n_* resolution, so even an unambiguous signature
		// (which the scanner would normally skip) has its CLR types captured.
		var method = FindMarshalMethod (FindPeer (UcoWidget), "n_OnLayout_ZIIII");
		Assert.NotNull (method.NativeCallbackParameterTypeNames);
		var parameterTypes = method.NativeCallbackParameterTypeNames;
		Assert.NotNull (parameterTypes);
		Assert.Equal (new [] { "System.SByte", "System.Int32", "System.Int32", "System.Int32", "System.Int32" }, parameterTypes);
		Assert.Equal ("System.Void", method.NativeCallbackReturnTypeName);
	}

	[Fact]
	public void Scanner_UnmarkedAssemblies_AreNotTreatedAsUnmanagedCallersOnly ()
	{
		// The regular (unmarked) fixture assembly must be completely unaffected.
		foreach (var peer in ScanFixtures ()) {
			foreach (var method in peer.MarshalMethods) {
				Assert.False (method.IsUnmanagedCallersOnlyCallback,
					$"{peer.ManagedTypeName}.{method.NativeCallbackName} should not be [UnmanagedCallersOnly].");
			}
		}
	}

	[Fact]
	public void ModelBuilder_DirectCallbacks_AreNotEmittedAsWrappers ()
	{
		var model = ModelBuilder.Build (UcoPeers, "TestUcoTypeMap.dll", "TestUcoTypeMap");
		var proxy = FindProxy (model, UcoWidget);

		Assert.DoesNotContain (proxy.UcoMethods, u => u.CallbackMethodName == "n_OnLayout_ZIIII");
		Assert.DoesNotContain (proxy.UcoMethods, u => u.CallbackMethodName == "n_GetCount");

		var registration = proxy.NativeRegistrations.Single (r => r.JniMethodName == "n_OnLayout_ZIIII");
		Assert.NotNull (registration.DirectCallback);
		var direct = registration.DirectCallback;
		Assert.NotNull (direct);
		Assert.True (direct.IsDirectUnmanagedCallersOnlyCallback);
		Assert.Equal ("n_OnLayout_ZIIII", direct.CallbackMethodName);
	}

	[Fact]
	public void ModelBuilder_LegacyCallbacks_StillGetWrappers ()
	{
		var model = ModelBuilder.Build (UcoPeers, "TestUcoTypeMap.dll", "TestUcoTypeMap");
		var proxy = FindProxy (model, LegacyWidget);

		Assert.Contains (proxy.UcoMethods, u => u.CallbackMethodName == "n_GetFlags");
		var registration = proxy.NativeRegistrations.Single (r => r.JniMethodName == "n_GetFlags");
		Assert.Null (registration.DirectCallback);
	}

	[Fact]
	public void Emitter_DirectCallbacks_AreLdftnThroughAMemberRef ()
	{
		using var stream = new MemoryStream ();
		new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0)).Generate (UcoPeers, stream, "TestUcoTypeMap");
		stream.Position = 0;

		using var peReader = new PEReader (stream);
		var reader = peReader.GetMetadataReader ();

		// Wrapper names are derived from the JNI method name: n_{jniName}_uco_{i}.
		var wrapperNames = reader.MethodDefinitions
			.Select (h => reader.GetString (reader.GetMethodDefinition (h).Name))
			.Where (n => n.Contains ("_uco", StringComparison.Ordinal))
			.ToList ();

		// No forwarding wrapper was emitted for the [UnmanagedCallersOnly] callbacks…
		Assert.DoesNotContain (wrapperNames, n => n.StartsWith ("n_onLayout_uco", StringComparison.Ordinal));
		Assert.DoesNotContain (wrapperNames, n => n.StartsWith ("n_getCount_uco", StringComparison.Ordinal));

		// …but the legacy connector callback still has one.
		Assert.Contains (wrapperNames, n => n.StartsWith ("n_getFlags_uco", StringComparison.Ordinal));

		// A MemberRef to the external callback exists, so RegisterNatives can ldftn it.
		var memberRefNames = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (MetadataTokens.MemberReferenceHandle)
			.Select (h => reader.GetString (reader.GetMemberReference (h).Name))
			.ToList ();
		Assert.Contains ("n_OnLayout_ZIIII", memberRefNames);
		Assert.Contains ("n_GetCount", memberRefNames);
	}

	[Fact]
	public void Emitter_ProducesAVerifiablyLoadableAssembly ()
	{
		using var stream = new MemoryStream ();
		new TypeMapAssemblyGenerator (new Version (11, 0, 0, 0)).Generate (UcoPeers, stream, "TestUcoTypeMap");
		stream.Position = 0;

		using var peReader = new PEReader (stream);
		var reader = peReader.GetMetadataReader ();
		Assert.True (reader.IsAssembly);

		// IgnoresAccessChecksTo is required: n_* callbacks are private statics in another assembly.
		// The attribute type is synthesized into the generated assembly itself.
		var typeNames = reader.TypeDefinitions
			.Select (h => reader.GetString (reader.GetTypeDefinition (h).Name))
			.ToList ();
		Assert.Contains ("IgnoresAccessChecksToAttribute", typeNames);

		// Every direct registration ldftn's a MemberRef whose declaring type lives in the
		// binding assembly, not in the generated typemap assembly.
		var callbackRefs = Enumerable.Range (1, reader.GetTableRowCount (TableIndex.MemberRef))
			.Select (MetadataTokens.MemberReferenceHandle)
			.Select (h => reader.GetMemberReference (h))
			.Where (mr => reader.GetString (mr.Name) == "n_OnLayout_ZIIII")
			.ToList ();
		var callbackRef = Assert.Single (callbackRefs);
		Assert.Equal (HandleKind.TypeReference, callbackRef.Parent.Kind);
		Assert.Equal ("UcoWidget", reader.GetString (reader.GetTypeReference ((TypeReferenceHandle) callbackRef.Parent).Name));
	}

	static JavaPeerProxyData FindProxy (TypeMapAssemblyData model, string managedTypeName)
	{
		var shortName = managedTypeName.Substring (managedTypeName.LastIndexOf ('.') + 1);
		return model.ProxyTypes.FirstOrDefault (p => p.TargetType?.ManagedTypeName == managedTypeName)
			?? model.ProxyTypes.FirstOrDefault (p => p.TypeName.Contains (shortName, StringComparison.Ordinal))
			?? throw new InvalidOperationException (
				$"Proxy for '{managedTypeName}' not found; found: " +
				string.Join (", ", model.ProxyTypes.Select (p => p.TypeName)));
	}
}
