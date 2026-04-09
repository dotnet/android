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
}
