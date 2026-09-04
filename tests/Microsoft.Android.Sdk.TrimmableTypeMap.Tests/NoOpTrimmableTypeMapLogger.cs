namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

class NoOpTrimmableTypeMapLogger : ITrimmableTypeMapLogger
{
	public virtual void LogNoJavaPeerTypesFound () { }
	public virtual void LogJavaPeerScanInfo (int assemblyCount, int peerCount) { }
	public virtual void LogGeneratingJcwFilesInfo (int jcwPeerCount, int totalPeerCount) { }
	public virtual void LogDeferredRegistrationTypesInfo (int typeCount) { }
	public virtual void LogGeneratedTypeMapAssemblyInfo (string assemblyName, int typeCount) { }
	public virtual void LogGeneratedRootTypeMapInfo (int assemblyReferenceCount) { }
	public virtual void LogGeneratedTypeMapAssembliesInfo (int assemblyCount) { }
	public virtual void LogGeneratedJcwFilesInfo (int sourceCount) { }
	public virtual void LogRootingManifestReferencedTypeInfo (string javaTypeName, string managedTypeName) { }
	public virtual void LogManifestReferencedTypeNotFoundWarning (string javaTypeName) { }
	public virtual void LogLibraryManifestMergeWarning (string message) { }
	public virtual void LogInvalidManifestPlaceholderWarning (string placeholders) { }
	public virtual void LogUnresolvableJavaPeerSkippedWarning (
		string managedTypeName,
		string assemblyName,
		string unresolvedTypeName,
		string unresolvedAssemblyName,
		string unresolvedAssemblyPath) { }
	public virtual void LogJniAddNativeMethodRegistrationAttributeError (string managedTypeName) { }
	public virtual void LogInvalidJavaNameError (string javaName, string invalidIdentifier) { }
	public virtual void LogDuplicateJavaTypeError (string javaName) { }
	public virtual void LogDuplicateJavaTypeDetailsError (string javaName, string managedTypeName) { }
	public virtual void LogExportFieldWithParametersError () { }
	public virtual void LogExportFieldOnGenericTypeError () { }
	public virtual void LogExportFieldReturnsVoidError () { }
	public virtual void LogCustomJavaObjectError (string managedTypeName) { }
	public virtual void LogCustomJavaObjectWarning (string managedTypeName) { }
}
