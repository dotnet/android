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
	void LogLibraryManifestMergeWarning (string message);
	void LogInvalidManifestPlaceholderWarning (string placeholders);
	void LogUnresolvableJavaPeerSkippedWarning (
		string managedTypeName,
		string assemblyName,
		string unresolvedTypeName,
		string unresolvedAssemblyName,
		string unresolvedAssemblyPath);
	void LogJniAddNativeMethodRegistrationAttributeError (string managedTypeName);
	void LogInvalidJavaNameError (string javaName, string invalidIdentifier);
	void LogDuplicateJavaTypeError (string javaName);
	void LogDuplicateJavaTypeDetailsError (string javaName, string managedTypeName);
	void LogExportFieldWithParametersError ();
	void LogExportOnGenericTypeError ();
	void LogExportFieldOnGenericTypeError ();
	void LogExportFieldReturnsVoidError ();
	void LogUnsupportedExportSignatureError (string memberName, string managedTypeName);
	void LogAmbiguousConstructorSignatureError (string managedTypeName, string jniSignature);
	void LogUnsupportedConstructorParameterTypeError (string managedTypeName, string parameterType);
	void LogMissingBaseConstructorError (string managedTypeName, string jniSignature);
	void LogInvalidSuperArgumentsStringError (string managedTypeName, string superArgumentsString);
	void LogCustomJavaObjectError (string managedTypeName);
	void LogCustomJavaObjectWarning (string managedTypeName);
}
