using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TrimmableTypeMapIncrementalTests
{
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
		var generator = new TrimmableTypeMapGenerator (new SilentLogger ());

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

	sealed class SilentLogger : ITrimmableTypeMapLogger
	{
		public void LogNoJavaPeerTypesFound () { }
		public void LogJavaPeerScanInfo (int assemblyCount, int peerCount) { }
		public void LogGeneratingJcwFilesInfo (int jcwPeerCount, int totalPeerCount) { }
		public void LogDeferredRegistrationTypesInfo (int typeCount) { }
		public void LogGeneratedTypeMapAssemblyInfo (string assemblyName, int typeCount) { }
		public void LogGeneratedRootTypeMapInfo (int assemblyReferenceCount) { }
		public void LogGeneratedTypeMapAssembliesInfo (int assemblyCount) { }
		public void LogGeneratedJcwFilesInfo (int sourceCount) { }
		public void LogRootingManifestReferencedTypeInfo (string javaTypeName, string managedTypeName) { }
		public void LogManifestReferencedTypeNotFoundWarning (string javaTypeName) { }
		public void LogInvalidManifestPlaceholderWarning (string placeholders) { }
		public void LogUnresolvableJavaPeerSkippedWarning (
			string managedTypeName,
			string assemblyName,
			string unresolvedTypeName,
			string unresolvedAssemblyName,
			string unresolvedAssemblyPath) { }
		public void LogJniAddNativeMethodRegistrationAttributeError (string managedTypeName) { }
		public void LogInvalidJavaNameError (string javaName, string invalidIdentifier) { }
		public void LogDuplicateJavaTypeError (string javaName) { }
		public void LogDuplicateJavaTypeDetailsError (string javaName, string managedTypeName) { }
		public void LogExportFieldWithParametersError () { }
		public void LogExportOnGenericTypeError () { }
		public void LogExportFieldOnGenericTypeError () { }
		public void LogExportFieldReturnsVoidError () { }
		public void LogUnsupportedExportSignatureError (string memberName, string managedTypeName) { }
		public void LogAmbiguousConstructorSignatureError (string managedTypeName, string jniSignature) { }
		public void LogUnsupportedConstructorParameterTypeError (string managedTypeName, string parameterType) { }
		public void LogMissingBaseConstructorError (string managedTypeName, string jniSignature) { }
		public void LogInvalidSuperArgumentsStringError (string managedTypeName, string superArgumentsString) { }
		public void LogRidSpecificCallbackMetadataMismatchError (string assemblyName, string firstPath, string secondPath) { }
		public void LogCustomJavaObjectError (string managedTypeName) { }
		public void LogCustomJavaObjectWarning (string managedTypeName) { }
	}
}
