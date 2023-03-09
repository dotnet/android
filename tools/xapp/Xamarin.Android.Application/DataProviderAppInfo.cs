using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using Xamarin.Android.Application.Utilities;
using Xamarin.Android.AssemblyStore;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Application;

class DataProviderAppInfo : DataProvider
{
	const string SignatureDirPath = "META-INF";

	InputReaderZip reader;

	public string ArchiveType               => reader.ArchiveType;
	public bool HasRuntimeConfigBlob        { get; private set; }
	public bool HasAssemblyStoresManifest   { get; private set; }
	public bool IsClassicXA                 { get; private set; }
	public bool IsDebug                     { get; private set; }
	public bool IsProfileable               { get; private set; }
	public bool IsSigned                    { get; private set; }
	public bool IsTesting                   { get; private set; }
	public uint NumberOfAbiAssemblyStores   { get; private set; } = 0;
	public string PackageName               { get; private set; } = String.Empty;
	public string[] SupportedAbis           { get; private set; }
	public uint TotalNumberOfAssemblyStores { get; private set; } = 0;
	public bool UsesAOT                     { get; private set; }
	public bool UsesAssemblyStores          { get; private set; }
	public bool UsesMAUI                    { get; private set; }
	public bool UsesXamarinForms            { get; private set; }

	public DataProviderAppInfo (InputReaderZip reader, ILogger log)
		: base (reader.ArchivePath, log)
	{
		this.reader = reader;

		HasRuntimeConfigBlob = HasFile (reader.AssembliesDirPath, "rc.bin");
		// TODO: full IsClassicXA detection
		if (!HasRuntimeConfigBlob) {
		}

		XmlDocument? manifest = LoadManifest (log);
		if (manifest == null) {
                        log.WarningLine ("Unable to parse Android manifest from the apk");
		} else {
			// From the manifest
			// TODO: IsDebug
			// TODO: IsProfileable
			// TODO: IsTesting
			// TODO: PackageName
		}

		IsSigned = HasFile (SignatureDirPath, "ANDROIDD.RSA") || HasFile (SignatureDirPath, "BNDLTOOL.RSA");
		UsesAssemblyStores = HasFile (reader.AssembliesDirPath, "assemblies.blob");
		if (UsesAssemblyStores) {
			TotalNumberOfAssemblyStores = 1;
			HasAssemblyStoresManifest = HasFile (reader.AssembliesDirPath, "assemblies.manifest");
		}

		string libDirLead = $"{reader.NativeLibsDirPath}/";
		string assembliesDirLead = $"{reader.AssembliesDirPath}/";

		var abis = new HashSet<string> (StringComparer.Ordinal);

		foreach (ZipEntry entry in reader.Archive) {
			if (UsesAssemblyStores) {
				if (entry.FullName.StartsWith (assembliesDirLead, StringComparison.Ordinal) && IsAssemblyAbiBlob (entry.FullName)) {
					TotalNumberOfAssemblyStores++;
					NumberOfAbiAssemblyStores++;
				}
			}

			if (!entry.FullName.StartsWith (libDirLead, StringComparison.Ordinal)) {
				continue;
			}

			string? dir = Path.GetDirectoryName (entry.FullName);
			string? file = Path.GetFileName (entry.FullName);

			if (String.IsNullOrEmpty (dir) || String.IsNullOrEmpty (file)) {
				continue;
			}

			string? abi = Path.GetFileName (dir);
			if (!abis.Contains (abi!)) {
				abis.Add (abi!);
			}

			if (!UsesAOT && file.StartsWith ("libaot-", StringComparison.Ordinal)) {
				UsesAOT = true;
			}
		}

		SupportedAbis = new string[abis.Count];
		abis.CopyTo (SupportedAbis);
	}

	XmlDocument? LoadManifest (ILogger log)
	{
		string manifestPath = MakeZipPath (reader.ManifestDirPath, InputReaderZip.AndroidManifestName);
		log.DebugLine ($"Trying to load and parse Android manifest from the archive: {manifestPath}");

		ZipEntry manifestEntry = reader.Archive.ReadEntry (manifestPath);
		using var manifestData = new MemoryStream ();
		manifestEntry.Extract (manifestData);
		manifestData.Seek (0, SeekOrigin.Begin);

		try {
			var axml = new AXMLParser (manifestData, log);
			return axml.Parse ();
		} catch (Exception ex) {
			log.DebugLine ("Failed to parse Android manifest.");
			log.DebugLine (ex.ToString ());
			return null;
		}
	}

	bool IsAssemblyAbiBlob (string entryName) => !String.IsNullOrEmpty (AssemblyStoreReader.GetBlobArchitecture (entryName));

	bool HasFile (string path, string fileName, bool caseSensitive = true) => reader.Archive.ContainsEntry (MakeZipPath (path, fileName), caseSensitive);

	static string MakeZipPath (string dirPath, string fileName)
	{
		if (String.IsNullOrEmpty (dirPath))
			return fileName;
		return $"{dirPath}/{fileName}";
	}
}
