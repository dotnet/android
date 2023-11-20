using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.AssemblyStore;

class AssemblyStoreExplorer
{
	readonly AssemblyStoreReader reader;

	public string StorePath                                         { get; }
	public AndroidTargetArch? TargetArch                            { get; }
	public uint AssemblyCount                                       { get; }
	public uint IndexEntryCount                                     { get; }
	public IList<AssemblyStoreItem>? Assemblies                     { get; }
	public IDictionary<string, AssemblyStoreItem>? AssembliesByName { get; }
	public bool Is64Bit                                             { get; }

	protected AssemblyStoreExplorer (Stream storeStream, string path)
	{
		StorePath = path;
		var storeReader = AssemblyStoreReader.Create (storeStream, path);
		if (storeReader == null) {
			storeStream.Dispose ();
			throw new NotSupportedException ($"Format of assembly store '{path}' is unsupported");
		}

		reader = storeReader;
		TargetArch = reader.TargetArch;
		AssemblyCount = reader.AssemblyCount;
		IndexEntryCount = reader.IndexEntryCount;
		Assemblies = reader.Assemblies;
		Is64Bit = reader.Is64Bit;

		var dict = new Dictionary<string, AssemblyStoreItem> (StringComparer.Ordinal);
		foreach (AssemblyStoreItem item in Assemblies) {
			dict.Add (item.Name, item);
		}
		AssembliesByName = dict.AsReadOnly ();
	}

	protected AssemblyStoreExplorer (FileInfo storeInfo)
		: this (storeInfo.OpenRead (), storeInfo.FullName)
	{}

	public static (IList<AssemblyStoreExplorer>? explorers, string? errorMessage) Open (string inputFile)
	{
		(FileFormat format, FileInfo? info) = Utils.DetectFileFormat (inputFile);
		if (info == null) {
			return (null, $"File '{inputFile}' does not exist.");
		}

		switch (format) {
			case FileFormat.Unknown:
				return (null, $"File '{inputFile}' has an unknown format.");

			case FileFormat.Zip:
				return (null, $"File '{inputFile}' is a ZIP archive, but not an Android one.");

			case FileFormat.AssemblyStore:
				return (new List<AssemblyStoreExplorer> { new AssemblyStoreExplorer (info)}, null);

			case FileFormat.Aab:
				return OpenAab (info);

			case FileFormat.AabBase:
				return OpenAabBase (info);

			case FileFormat.Apk:
				return OpenApk (info);

			default:
				return (null, $"File '{inputFile}' has an unsupported format '{format}'");
		}
	}

	static (IList<AssemblyStoreExplorer>? explorers, string? errorMessage) OpenAab (FileInfo fi)
	{
		return OpenCommon (
			fi,
			new List<IList<string>> {
				StoreReader_V2.AabPaths,
				StoreReader_V1.AabPaths,
			}
		);
	}

	static (IList<AssemblyStoreExplorer>? explorers, string? errorMessage) OpenAabBase (FileInfo fi)
	{
		return OpenCommon (
			fi,
			new List<IList<string>> {
				StoreReader_V2.AabBasePaths,
				StoreReader_V1.AabBasePaths,
			}
		);
	}

	static (IList<AssemblyStoreExplorer>? explorers, string? errorMessage) OpenApk (FileInfo fi)
	{
		return OpenCommon (
			fi,
			new List<IList<string>> {
				StoreReader_V2.ApkPaths,
				StoreReader_V1.ApkPaths,
			}
		);
	}

	static (IList<AssemblyStoreExplorer>? explorers, string? errorMessage) OpenCommon (FileInfo fi, List<IList<string>> pathLists)
	{
		using var zip = ZipArchive.Open (fi.FullName, FileMode.Open);
		IList<AssemblyStoreExplorer>? explorers;
		string? errorMessage;
		bool pathsFound;

		foreach (IList<string> paths in pathLists) {
			(explorers, errorMessage, pathsFound) = TryLoad (fi, zip, paths);
			if (pathsFound) {
				return (explorers, errorMessage);
			}
		}

		return (null, "Unable to find any blob entries");
	}

	static (IList<AssemblyStoreExplorer>? explorers, string? errorMessage, bool pathsFound) TryLoad (FileInfo fi, ZipArchive zip, IList<string> paths)
	{
		var ret = new List<AssemblyStoreExplorer> ();

		foreach (string path in paths) {
			if (!zip.ContainsEntry (path)) {
				continue;
			}

			ZipEntry entry = zip.ReadEntry (path);
			var stream = new MemoryStream ();
			entry.Extract (stream);
			ret.Add (new AssemblyStoreExplorer (stream, $"{fi.FullName}!{path}"));
		}

		if (ret.Count == 0) {
			return (null, null, false);
		}

		return (ret, null, true);
	}

	public Stream? ReadImageData (AssemblyStoreItem item, bool uncompressIfNeeded = false)
	{
		return reader.ReadEntryImageData (item, uncompressIfNeeded);
	}

	string EnsureCorrectAssemblyName (string assemblyName)
	{
		assemblyName = Path.GetFileName (assemblyName);
		if (reader.NeedsExtensionInName) {
			if (!assemblyName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
				return $"{assemblyName}.dll";
			}
		} else {
			if (assemblyName.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
				return Path.GetFileNameWithoutExtension (assemblyName);
			}
		}

		return assemblyName;
	}

	public IList<AssemblyStoreItem>? Find (string assemblyName, AndroidTargetArch? targetArch = null)
	{
		if (Assemblies == null) {
			return null;
		}

		assemblyName = EnsureCorrectAssemblyName (assemblyName);
		var items = new List<AssemblyStoreItem> ();
		foreach (AssemblyStoreItem item in Assemblies) {
			if (String.CompareOrdinal (assemblyName, item.Name) != 0) {
				continue;
			}

			if (targetArch != null && item.TargetArch != targetArch) {
				continue;
			}

			items.Add (item);
		}

		if (items.Count == 0) {
			return null;
		}

		return items;
	}

	public bool Contains (string assemblyName, AndroidTargetArch? targetArch = null)
	{
		IList<AssemblyStoreItem>? items = Find (assemblyName, targetArch);
		if (items == null || items.Count == 0) {
			return false;
		}

		return true;
	}
}
