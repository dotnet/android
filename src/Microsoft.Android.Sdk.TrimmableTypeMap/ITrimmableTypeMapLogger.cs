namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public interface ITrimmableTypeMapLogger
{
	void LogNoJavaPeerTypesFound ();
	void LogJavaPeerScanInfo (int assemblyCount, int peerCount);
	void LogGeneratingJcwFilesInfo (int jcwPeerCount, int totalPeerCount);
	void LogDeferredRegistrationTypesInfo (int typeCount);
	void LogGeneratedTypeMapAssemblyInfo (string assemblyName, int typeCount);
	void LogGeneratedRootTypeMapInfo (int assemblyReferenceCount);
	void LogGeneratedTypeMapAssembliesInfo (int assemblyCount);
	void LogGeneratedJcwFilesInfo (int sourceCount);
	void LogRootingManifestReferencedTypeInfo (string javaTypeName, string managedTypeName);
	void LogManifestReferencedTypeNotFoundWarning (string javaTypeName);

	/// <summary>
	/// Reports that a registered Java constructor on the given peer has no matching
	/// user-visible managed constructor and will fall back to the legacy
	/// <c>(IntPtr, JniHandleOwnership)</c> activation-ctor path. Useful for diagnosing
	/// silently-skipped <c>[Export]</c> ctor logic — see
	/// <see cref="JavaConstructorInfo.CtorFallbackReason"/>.
	/// </summary>
	void LogUserCtorFallbackInfo (string managedTypeName, string jniSignature, CtorFallbackReason reason);
}
