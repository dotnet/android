using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;

namespace Microsoft.Android.AppTools.Assemblies;

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

		if (Assemblies == null) {
			return;
		}

		var dict = new Dictionary<string, AssemblyStoreItem> (StringComparer.Ordinal);
		foreach (AssemblyStoreItem item in Assemblies) {
			dict.Add (item.Name, item);
		}
		AssembliesByName = dict.AsReadOnly ();
	}

	protected AssemblyStoreExplorer (FileInfo storeInfo)
		: this (storeInfo.OpenRead (), storeInfo.FullName)
	{}

	public static IList<AssemblyStoreExplorer>? Open (ILogger log, string inputFile, FileFormat format, FileInfo info)
	{
		switch (format) {
			case FileFormat.Unknown:
				log.Debug ($"File '{inputFile}' has an unknown format.");
				return null;

			case FileFormat.Zip:
				log.Debug ($"File '{inputFile}' is a ZIP archive, but not an Android one.");
				return null;

			case FileFormat.AssemblyStore:
			case FileFormat.ELF:
				return new List<AssemblyStoreExplorer> { new AssemblyStoreExplorer (info)};

			case FileFormat.Aab:
				return OpenAab (log, info);

			case FileFormat.AabBase:
				return OpenAabBase (log, info);

			case FileFormat.Apk:
				return OpenApk (log, info);

			default:
				log.Debug ($"File '{inputFile}' has an unsupported format '{format}'");
				return null;
		}
	}

	static IList<AssemblyStoreExplorer>? OpenAab (ILogger log, FileInfo fi)
	{
		return OpenCommon (
			log,
			fi,
			new List<IList<string>> {
				StoreReader_V2.AabPaths,
			}
		);
	}

	static IList<AssemblyStoreExplorer>? OpenAabBase (ILogger log, FileInfo fi)
	{
		return OpenCommon (
			log,
			fi,
			new List<IList<string>> {
				StoreReader_V2.AabBasePaths,
			}
		);
	}

	static IList<AssemblyStoreExplorer>? OpenApk (ILogger log, FileInfo fi)
	{
		return OpenCommon (
			log,
			fi,
			new List<IList<string>> {
				StoreReader_V2.ApkPaths,
			}
		);
	}

	static IList<AssemblyStoreExplorer>? OpenCommon (ILogger log, FileInfo fi, List<IList<string>> pathLists)
	{
		using var zip = ZipArchive.Open (fi.FullName, FileMode.Open);
		IList<AssemblyStoreExplorer>? explorers;
		bool pathsFound;

		foreach (IList<string> paths in pathLists) {
			(explorers, pathsFound) = TryLoad (log, fi, zip, paths);
			if (pathsFound) {
				return explorers;
			}
		}

		log.Debug ("Unable to find any blob entries");
		return null;
	}

	static (IList<AssemblyStoreExplorer>? explorers, bool pathsFound) TryLoad (ILogger log, FileInfo fi, ZipArchive zip, IList<string> paths)
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
			return (null, false);
		}

		return (ret, true);
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
