using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class XAAssemblyResolver : IAssemblyResolver
{
	readonly List<MemoryMappedViewStream> viewStreams = new List<MemoryMappedViewStream> ();
	readonly Dictionary<string, AssemblyDefinition> cache;
	bool disposed;
	TaskLoggingHelper log;
	bool loadDebugSymbols;
	ReaderParameters readerParameters;
	readonly AndroidTargetArch targetArch;

	/// <summary>
	/// **MUST** point to directories which contain assemblies for single ABI **only**.
	/// One special case is when linking isn't enabled, in which instance directories
	/// containing ABI-agnostic assemblies can we used as well.
	public ICollection<string> SearchDirectories { get; } = new List<string> ();
	public AndroidTargetArch TargetArch => targetArch;

	public XAAssemblyResolver (AndroidTargetArch targetArch, TaskLoggingHelper log, bool loadDebugSymbols, ReaderParameters? loadReaderParameters = null)
	{
		this.targetArch = targetArch;
		this.log = log;
		this.loadDebugSymbols = loadDebugSymbols;
		this.readerParameters = loadReaderParameters ?? new ReaderParameters ();

		cache = new Dictionary<string, AssemblyDefinition> (StringComparer.OrdinalIgnoreCase);
	}

	public AssemblyDefinition? Resolve (AssemblyNameReference name)
	{
		return Resolve (name, null);
	}

	public AssemblyDefinition? Resolve (AssemblyNameReference name, ReaderParameters? parameters)
	{
		string shortName = name.Name;
		if (cache.TryGetValue (shortName, out AssemblyDefinition? assembly)) {
			return assembly;
		}

		return FindAndLoadFromDirectories (name, parameters);
	}

	AssemblyDefinition? FindAndLoadFromDirectories (AssemblyNameReference name, ReaderParameters? parameters)
	{
		string? assemblyFile;
		foreach (string dir in SearchDirectories) {
			if ((assemblyFile = SearchDirectory (name.Name, dir)) != null) {
				return Load (assemblyFile, parameters);
			}
		}

		return null;
	}

	static string? SearchDirectory (string name, string directory)
	{
		if (Path.IsPathRooted (name) && File.Exists (name)) {
			return name;
		}

		if (!name.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
			name = $"{name}.dll";
		}

		var file = Path.Combine (directory, name);
		if (File.Exists (file)) {
			return file;
		}

		return null;
	}

	public virtual AssemblyDefinition? Load (string filePath, ReaderParameters? readerParameters = null)
	{
		string name = Path.GetFileNameWithoutExtension (filePath);

		if (cache.TryGetValue (name, out AssemblyDefinition? assembly)) {
			if (assembly != null) {
				return assembly;
			}
		}

		try {
			assembly = ReadAssembly (filePath, readerParameters);
		} catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException) {
			// These are ok, we can return null
			return null;
		}

		if (!cache.ContainsKey (name)) {
			cache.Add (name, assembly);
		}

		return assembly;
	}

	AssemblyDefinition ReadAssembly (string filePath, ReaderParameters? readerParametersOverride = null)
	{
		ReaderParameters templateParameters = readerParametersOverride ?? this.readerParameters;
		bool haveDebugSymbols = loadDebugSymbols && File.Exists (Path.ChangeExtension (filePath, ".pdb"));
		var loadReaderParams = new ReaderParameters () {
			ApplyWindowsRuntimeProjections  = templateParameters.ApplyWindowsRuntimeProjections,
			AssemblyResolver                = this,
			MetadataImporterProvider        = templateParameters.MetadataImporterProvider,
			InMemory                        = templateParameters.InMemory,
			MetadataResolver                = templateParameters.MetadataResolver,
			ReadingMode                     = templateParameters.ReadingMode,
			ReadSymbols                     = haveDebugSymbols,
			ReadWrite                       = templateParameters.ReadWrite,
			ReflectionImporterProvider      = templateParameters.ReflectionImporterProvider,
			SymbolReaderProvider            = templateParameters.SymbolReaderProvider,
			SymbolStream                    = templateParameters.SymbolStream,
		};
		try {
			return LoadFromMemoryMappedFile (filePath, loadReaderParams);
		} catch (Exception ex) {
			log.LogWarning ($"Failed to read '{filePath}' with debugging symbols for target architecture '{targetArch}'. Retrying to load it without it. Error details are logged below.");
			log.LogWarning ($"{ex.ToString ()}");
			loadReaderParams.ReadSymbols = false;
			return LoadFromMemoryMappedFile (filePath, loadReaderParams);
		}
	}

	AssemblyDefinition LoadFromMemoryMappedFile (string file, ReaderParameters options)
	{
		// We can't use MemoryMappedFile when ReadWrite is true
		if (options.ReadWrite) {
			return AssemblyDefinition.ReadAssembly (file, options);
		}

		bool origReadSymbols = options.ReadSymbols;
		MemoryMappedViewStream? viewStream = null;
		try {
			// We must disable reading of symbols, even if they were present, because Cecil is unable to find the symbols file when
			// assembly file name is unknown, and this is precisely the case when reading module from a stream.
			// Until this issue is resolved, skipping symbol read saves time because reading exception isn't thrown and we don't
			// retry the load.
			options.ReadSymbols = false;

			// Create stream because CreateFromFile(string, ...) uses FileShare.None which is too strict
			using var fileStream = new FileStream (file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false);
			using var mappedFile = MemoryMappedFile.CreateFromFile (fileStream, null, fileStream.Length, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
			viewStream = mappedFile.CreateViewStream (0, 0, MemoryMappedFileAccess.Read);

			AssemblyDefinition result = ModuleDefinition.ReadModule (viewStream, options).Assembly;
			viewStreams.Add (viewStream);

			// We transferred the ownership of the viewStream to the collection.
			viewStream = null;

			return result;
		} finally {
			options.ReadSymbols = origReadSymbols;
			viewStream?.Dispose ();
		}
	}

	public void Dispose ()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose (disposing: true);
		GC.SuppressFinalize (this);
	}

	protected virtual void Dispose (bool disposing)
	{
		if (disposed || !disposing) {
			return;
		}

		foreach (var kvp in cache) {
			kvp.Value?.Dispose ();
		}
		cache.Clear ();

		foreach (MemoryMappedViewStream viewStream in viewStreams) {
			viewStream.Dispose ();
		}
		viewStreams.Clear ();

		disposed = true;
	}
}
