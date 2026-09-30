using Microsoft.Android.Sdk.TrimmableTypeMap;

namespace Xamarin.Android.Build.Tests;

sealed class TrimmableTypeMapTestLogger : ITrimmableTypeMapLogger
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
