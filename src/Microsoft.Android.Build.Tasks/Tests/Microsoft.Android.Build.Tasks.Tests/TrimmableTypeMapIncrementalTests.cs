using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TrimmableTypeMapIncrementalTests
{
	[TestCase (false)]
	[TestCase (true)]
	public void ChangingFrameworkSealednessRegeneratesTypeMap (bool isSealed)
	{
		var peer = CreatePeer ("Mono.Android", "Java.Lang.String", "java/lang/String") with {
			IsSealed = isSealed,
		};
		var changedPeer = peer with { IsSealed = !isSealed };

		CollectionAssert.AreEqual (
			new [] { "_Mono.Android.TypeMap" },
			GetChangedAssemblyNames ([peer], [changedPeer]));

		var version = new Version (12, 0);
		var generator = new TypeMapAssemblyGenerator (version);
		var initial = generator.CreateModel ([peer], "_Mono.Android.TypeMap");
		var changed = generator.CreateModel ([changedPeer], "_Mono.Android.TypeMap");
		CollectionAssert.AreNotEqual (
			MetadataHelper.ComputeFingerprints (initial, version, useSharedTypemapUniverse: true, includeIncremental: false).Content,
			MetadataHelper.ComputeFingerprints (changed, version, useSharedTypemapUniverse: true, includeIncremental: false).Content,
			"Changing the emitted base class must also change the deterministic MVID.");
	}

	[Test]
	public void ChangedCrossAssemblyAliasRegeneratesOwner ()
	{
		var owner = CreatePeer ("Owner", "Owner.JavaObject", "java/lang/Object");
		var alias = CreatePeer ("Alias", "Alias.JavaObject", "java/lang/Object") with {
			IsFromJniTypeSignature = true,
		};
		var unrelated = CreatePeer ("Alias", "Alias.Widget", "alias/Widget");
		var initialPeers = new List<JavaPeerInfo> { owner, alias, unrelated };
		var changedPeers = new List<JavaPeerInfo> {
			owner,
			alias with {
				ManagedTypeName = "Alias.ChangedJavaObject",
				ManagedTypeShortName = "ChangedJavaObject",
			},
			unrelated,
		};

		CollectionAssert.AreEqual (
			new [] { "_Owner.TypeMap" },
			GetChangedAssemblyNames (initialPeers, changedPeers));
	}

	[Test]
	public void AddingAssemblyRegeneratesItsTypeMapAndRoot ()
	{
		var initialPeers = new List<JavaPeerInfo> {
			CreatePeer ("MyApp", "MyApp.MainActivity", "my/app/MainActivity"),
		};
		var changedPeers = new List<JavaPeerInfo> (initialPeers) {
			CreatePeer ("MyLibrary", "MyLibrary.Widget", "my/library/Widget"),
		};

		CollectionAssert.AreEqual (
			new [] { "_MyLibrary.TypeMap", "_Microsoft.Android.TypeMaps" },
			GetChangedAssemblyNames (initialPeers, changedPeers));
	}

	static string [] GetChangedAssemblyNames (List<JavaPeerInfo> initialPeers, List<JavaPeerInfo> changedPeers)
	{
		var fingerprints = new Dictionary<string, byte []> (StringComparer.Ordinal);
		var generator = new TrimmableTypeMapGenerator (new TrimmableTypeMapTestLogger ());

		var initial = generator.GenerateTypeMapAssemblies (
			initialPeers,
			new Version (11, 0),
			useSharedTypemapUniverse: true,
			(name, fingerprint) => {
				fingerprints.Add (name, fingerprint);
				return true;
			});
		DisposeGeneratedAssemblies (initial);

		var changed = generator.GenerateTypeMapAssemblies (
			changedPeers,
			new Version (11, 0),
			useSharedTypemapUniverse: true,
			(name, fingerprint) => !fingerprints.TryGetValue (name, out var prior) || !prior.SequenceEqual (fingerprint));
		try {
			return changed.Select (assembly => assembly.Name).ToArray ();
		} finally {
			DisposeGeneratedAssemblies (changed);
		}
	}

	static void DisposeGeneratedAssemblies (IEnumerable<GeneratedAssembly> assemblies)
	{
		foreach (var assembly in assemblies) {
			assembly.Content.Dispose ();
		}
	}

	static JavaPeerInfo CreatePeer (string assemblyName, string managedTypeName, string javaName)
	{
		int separator = managedTypeName.LastIndexOf ('.');
		return new JavaPeerInfo {
			JavaName = javaName,
			CompatJniName = javaName,
			ManagedTypeName = managedTypeName,
			ManagedTypeNamespace = separator < 0 ? "" : managedTypeName.Substring (0, separator),
			ManagedTypeShortName = separator < 0 ? managedTypeName : managedTypeName.Substring (separator + 1),
			AssemblyName = assemblyName,
			DoNotGenerateAcw = true,
		};
	}

}
