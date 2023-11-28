using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

using Microsoft.Build.Utilities;
using Mono.Cecil;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class XAAssemblyResolverNew : IAssemblyResolver
{
	readonly List<MemoryMappedViewStream> viewStreams = new List<MemoryMappedViewStream> ();
	readonly Dictionary<string, AssemblyDefinition> cache;
	bool disposed;
	TaskLoggingHelper log;
	bool loadDebugSymbols;
	ReaderParameters readerParameters;
	AndroidTargetArch targetArch;

	/// <summary>
	/// **MUST** point to directories which contain assemblies for single ABI **only**.
	/// One special case is when linking isn't enabled, in which instance directories
	/// containing ABI-agnostic assemblies can we used as well.
	public ICollection<string> SearchDirectories { get; } = new List<string> ();

	public XAAssemblyResolverNew (AndroidTargetArch targetArch, TaskLoggingHelper log, bool loadDebugSymbols, ReaderParameters? loadReaderParameters = null)
	{
		this.targetArch = targetArch;
		this.log = log;
		this.loadDebugSymbols = loadDebugSymbols;
		this.readerParameters = loadReaderParameters ?? new ReaderParameters();

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

		var file = Path.Combine (directory, $"{name}.dll");
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

class XAAssemblyResolver : IAssemblyResolver
{
	sealed class CacheEntry : IDisposable
	{
		bool disposed;

		Dictionary<AndroidTargetArch, AssemblyDefinition> assemblies;
		TaskLoggingHelper log;
		AndroidTargetArch defaultArch;

		/// <summary>
		/// This field is to be used by the `Resolve` overloads which don't have a way of indicating the desired ABI target for the assembly, but only when the
		/// `AndroidTargetArch.None` entry for the assembly in question is **absent**.  The field is always set to some value: either the very first assembly added
		/// or the one with the `AndroidTargetArch.None` ABI.  The latter always wins.
		/// </summary>
		public AssemblyDefinition Default { get; private set; }
		public Dictionary<AndroidTargetArch, AssemblyDefinition> Assemblies => assemblies;

		public CacheEntry (TaskLoggingHelper log, string filePath, AssemblyDefinition asm, AndroidTargetArch arch)
		{
			if (asm == null) {
				throw new ArgumentNullException (nameof (asm));
			}

			this.log = log;
			Default = asm;
			defaultArch = arch;
			assemblies = new Dictionary<AndroidTargetArch, AssemblyDefinition> {
				{ arch, asm },
			};
		}

		public void Add (AndroidTargetArch arch, AssemblyDefinition asm)
		{
			if (asm == null) {
				throw new ArgumentNullException (nameof (asm));
			}

			if (assemblies.ContainsKey (arch)) {
				log.LogWarning ($"Entry for assembly '{asm}', architecture '{arch}' already exists.  Replacing the old entry.");
			}

			assemblies[arch] = asm;
			if (arch == AndroidTargetArch.None && defaultArch != AndroidTargetArch.None) {
				Default = asm;
				defaultArch = arch;
			}
		}

		void Dispose (bool disposing)
		{
			if (disposed || !disposing) {
				return;
			}

			Default = null;
			foreach (var kvp in assemblies) {
				kvp.Value?.Dispose ();
			}
			assemblies.Clear ();
			disposed = true;
		}

		public void Dispose ()
		{
			Dispose (disposing: true);
			GC.SuppressFinalize (this);
		}
	}

	/// <summary>
	/// Contains a collection of directories where framework assemblies can be found.  This collection **must not**
	/// contain any directories which contain ABI-specific assemblies.  For those, use <see cref="AbiSearchDirectories"/>
	/// </summary>
	public ICollection<string> FrameworkSearchDirectories { get; } = new List<string> ();

	/// <summary>
	/// Contains a collection of directories where Xamarin.Android (via linker, for instance) has placed the ABI
	/// specific assemblies.  Each ABI has its own set of directories to search.
	/// <summary>
	public IDictionary<AndroidTargetArch, ICollection<string>> AbiSearchDirectories { get; } = new Dictionary<AndroidTargetArch, ICollection<string>> ();

	readonly List<MemoryMappedViewStream> viewStreams = new List<MemoryMappedViewStream> ();
	bool disposed;
	TaskLoggingHelper log;
	bool loadDebugSymbols;
	ReaderParameters readerParameters;
	readonly Dictionary<string, CacheEntry> cache;

	public XAAssemblyResolver (TaskLoggingHelper log, bool loadDebugSymbols, ReaderParameters? loadReaderParameters = null)
	{
		this.log = log;
		this.loadDebugSymbols = loadDebugSymbols;
		this.readerParameters = loadReaderParameters ?? new ReaderParameters();

		cache = new Dictionary<string, CacheEntry> (StringComparer.OrdinalIgnoreCase);
	}

	public AssemblyDefinition? Resolve (AssemblyNameReference name)
	{
		return Resolve (name, null);
	}

	public AssemblyDefinition? Resolve (AssemblyNameReference name, ReaderParameters? parameters)
	{
		return Resolve (AndroidTargetArch.None, name, parameters);
	}

	public AssemblyDefinition? Resolve (AndroidTargetArch arch, AssemblyNameReference name, ReaderParameters? parameters = null)
	{
		string shortName = name.Name;
		if (cache.TryGetValue (shortName, out CacheEntry? entry)) {
			return SelectAssembly (arch, name.FullName, entry, loading: false);
		}

		if (arch == AndroidTargetArch.None) {
			return FindAndLoadFromDirectories (arch, FrameworkSearchDirectories, name, parameters);
		}

		if (!AbiSearchDirectories.TryGetValue (arch, out ICollection<string>? directories) || directories == null) {
			throw CreateLoadException (name);
		}

		return FindAndLoadFromDirectories (arch, directories, name, parameters);
	}

	AssemblyDefinition? FindAndLoadFromDirectories (AndroidTargetArch arch, ICollection<string> directories, AssemblyNameReference name, ReaderParameters? parameters)
	{
		string? assemblyFile;
		foreach (string dir in directories) {
			if ((assemblyFile = SearchDirectory (name.Name, dir)) != null) {
				return Load (arch, assemblyFile, parameters);
			}
		}

		return null;
	}

	static FileNotFoundException CreateLoadException (AssemblyNameReference name)
	{
		return new FileNotFoundException ($"Could not load assembly '{name}'.");
	}

	static string? SearchDirectory (string name, string directory)
	{
		if (Path.IsPathRooted (name) && File.Exists (name)) {
			return name;
		}

		var file = Path.Combine (directory, $"{name}.dll");
		if (File.Exists (file)) {
			return file;
		}

		return null;
	}

	public virtual AssemblyDefinition? Load (AndroidTargetArch arch, string filePath, ReaderParameters? readerParameters = null)
	{
		string name = Path.GetFileNameWithoutExtension (filePath);
		AssemblyDefinition? assembly;
		if (cache.TryGetValue (name, out CacheEntry? entry)) {
			assembly = SelectAssembly (arch, name, entry, loading: true);
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

		if (!cache.TryGetValue (name, out entry)) {
			entry = new CacheEntry (log, filePath, assembly, arch);
			cache.Add (name, entry);
		} else {
			entry.Add (arch, assembly);
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
			log.LogWarning ($"Failed to read '{filePath}' with debugging symbols. Retrying to load it without it. Error details are logged below.");
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

	AssemblyDefinition? SelectAssembly (AndroidTargetArch arch, string assemblyName, CacheEntry? entry, bool loading)
	{
		if (entry == null) {
			// Should "never" happen...
			throw new ArgumentNullException (nameof (entry));
		}

		if (arch == AndroidTargetArch.None) {
			// Disabled for now, generates too much noise.
			// if (entry.Assemblies.Count > 1) {
			// 	log.LogWarning ($"Architecture-agnostic entry requested for architecture-specific assembly '{assemblyName}'");
			// }
			return entry.Default;
		}

		if (!entry.Assemblies.TryGetValue (arch, out AssemblyDefinition? asm)) {
			if (loading) {
				return null;
			}

			if (!entry.Assemblies.TryGetValue (AndroidTargetArch.None, out asm)) {
				throw new InvalidOperationException ($"Internal error: assembly '{assemblyName}' for architecture '{arch}' not found in cache entry and architecture-agnostic entry is missing as well");
			}

			if (asm == null) {
				throw new InvalidOperationException ($"Internal error: architecture-agnostic cache entry for assembly '{assemblyName}' is null");
			}

			log.LogWarning ($"Returning architecture-agnostic cache entry for assembly '{assemblyName}'. Requested architecture was: {arch}");
			return asm;
		}

		if (asm == null) {
			throw new InvalidOperationException ($"Internal error: null reference for assembly '{assemblyName}' in assembly cache entry");
		}

		return asm;
	}

	public void Dispose ()
	{
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
