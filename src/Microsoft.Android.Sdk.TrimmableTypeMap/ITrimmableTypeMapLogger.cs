namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public interface ITrimmableTypeMapLogger
{
	void LogUnresolvedTypeWarning (string name);

	void LogRootingManifestReferencedTypeInfo (string name, string managedTypeName);
}
