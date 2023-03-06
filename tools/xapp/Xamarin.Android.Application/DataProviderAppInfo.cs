using System;

using Xamarin.Tools.Zip;

namespace Xamarin.Android.Application;

class DataProviderAppInfo : DataProvider
{
	public string ArchiveType        { get; }
	public bool HasRuntimeConfigBlob { get; private set; }
	public bool IsDebug              { get; private set; }
	public bool IsProfileable        { get; private set; }
	public bool IsSigned             { get; private set; }
	public bool IsTesting            { get; private set; }
	public string PackageName        { get; private set; } = String.Empty;
	public bool UsesAssemblyStores   { get; private set; }

	public DataProviderAppInfo (ZipArchive archive, string inputPath, string archiveType)
		: base (inputPath)
	{
		ArchiveType = archiveType;
	}
}
