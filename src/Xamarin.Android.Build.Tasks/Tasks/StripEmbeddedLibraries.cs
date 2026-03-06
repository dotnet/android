#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks;

/// <summary>
/// An MSBuild task that strips embedded Android resources (.jar, __AndroidNativeLibraries__.zip,
/// __AndroidLibraryProjects__.zip, __AndroidEnvironment__) from trimmed assemblies.
///
/// This runs in the inner build after ILLink but before ReadyToRun/crossgen2 compilation,
/// so that R2R images are generated from the already-stripped assemblies.
/// </summary>
public class StripEmbeddedLibraries : AndroidTask
{
	public override string TaskPrefix => "SEL";

	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	public bool Deterministic { get; set; }

	public override bool RunTask ()
	{
		var resolver = new DefaultAssemblyResolver ();
		var searchDirectories = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		foreach (var assembly in Assemblies) {
			var dir = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec));
			if (searchDirectories.Add (dir)) {
				resolver.AddSearchDirectory (dir);
			}
		}

		try {
			foreach (var assembly in Assemblies) {
				if (MonoAndroidHelper.IsFrameworkAssembly (assembly)) {
					continue;
				}

				StripAssembly (assembly.ItemSpec, resolver);
			}
		} finally {
			resolver.Dispose ();
		}

		return !Log.HasLoggedErrors;
	}

	void StripAssembly (string assemblyPath, IAssemblyResolver resolver)
	{
		string pdbPath = Path.ChangeExtension (assemblyPath, ".pdb");
		bool havePdb = File.Exists (pdbPath);

		var readerParams = new ReaderParameters {
			ReadSymbols = havePdb,
			ReadWrite = false,
			AssemblyResolver = resolver,
		};

		bool assembly_modified = false;

		using (var assembly = AssemblyDefinition.ReadAssembly (assemblyPath, readerParams)) {
			foreach (var module in assembly.Modules) {
				foreach (var resource in module.Resources.ToArray ()) {
					if (ShouldStripResource (resource)) {
						Log.LogDebugMessage ($"  Stripped {resource.Name} from {assembly.Name.Name}.dll");
						module.Resources.Remove (resource);
						assembly_modified = true;
					}
				}
			}

			if (!assembly_modified) {
				return;
			}

			// Write modified assembly using the write-to-temp-then-copy pattern
			// from MarshalMethodsAssemblyRewriter to avoid file locking issues.
			string directory = Path.Combine (Path.GetDirectoryName (assemblyPath), "stripped");
			Directory.CreateDirectory (directory);
			string tempOutput = Path.Combine (directory, Path.GetFileName (assemblyPath));

			var writerParams = new WriterParameters {
				WriteSymbols = havePdb,
				DeterministicMvid = Deterministic,
			};

			Log.LogDebugMessage ($"  Writing stripped assembly: {assemblyPath}");
			assembly.Write (tempOutput, writerParams);

			CopyFile (tempOutput, assemblyPath);
			RemoveFile (tempOutput);

			if (havePdb) {
				string tempPdb = Path.ChangeExtension (tempOutput, ".pdb");
				if (File.Exists (tempPdb)) {
					CopyFile (tempPdb, pdbPath);
				}
				RemoveFile (tempPdb);
			}

			// Clean up temp directory if empty
			try {
				if (Directory.Exists (directory) && !Directory.EnumerateFileSystemEntries (directory).Any ()) {
					Directory.Delete (directory);
				}
			} catch (Exception) {
				// Ignore cleanup failures
			}
		}
	}

	/// <summary>
	/// Determines whether a resource should be stripped from the assembly.
	/// Matches the same criteria as the old ILLink StripEmbeddedLibraries step.
	/// </summary>
	internal static bool ShouldStripResource (Resource resource)
	{
		if (!(resource is EmbeddedResource))
			return false;
		// Embedded jars
		if (resource.Name.EndsWith (".jar", StringComparison.InvariantCultureIgnoreCase))
			return true;
		// Embedded AndroidNativeLibrary archive
		if (resource.Name == "__AndroidNativeLibraries__.zip")
			return true;
		// Embedded AndroidResourceLibrary archive
		if (resource.Name == "__AndroidLibraryProjects__.zip")
			return true;
		// Embedded AndroidEnvironment items
		if (resource.Name.StartsWith ("__AndroidEnvironment__", StringComparison.Ordinal))
			return true;
		return false;
	}

	void CopyFile (string source, string target)
	{
		Log.LogDebugMessage ($"  Copying stripped assembly: {source} -> {target}");

		string targetBackup = $"{target}.bak";
		if (File.Exists (target)) {
			// Try to avoid sharing violations by first renaming the target
			File.Move (target, targetBackup);
		}

		File.Copy (source, target, true);

		if (File.Exists (targetBackup)) {
			try {
				File.Delete (targetBackup);
			} catch (Exception ex) {
				Log.LogDebugMessage ($"  While trying to delete '{targetBackup}', exception was thrown: {ex}");
				Log.LogDebugMessage ($"  Failed to delete backup file '{targetBackup}', ignoring.");
			}
		}
	}

	void RemoveFile (string? path)
	{
		if (string.IsNullOrEmpty (path) || !File.Exists (path)) {
			return;
		}

		try {
			Log.LogDebugMessage ($"  Deleting: {path}");
			File.Delete (path);
		} catch (Exception ex) {
			Log.LogWarning ($"Unable to delete temporary file '{path}'");
			Log.LogDebugMessage ($"  {ex}");
		}
	}
}
