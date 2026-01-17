using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks;

class MonoAndroidRuntimeMarshalMethodsFixUp
{
	const string RuntimeTypeName = "Android.Runtime.AndroidEnvironmentInternal";

	public static bool Run (TaskLoggingHelper log, List<ITaskItem> items)
	{
		bool everythingWorked = true;
		foreach (ITaskItem item in items) {
			if (!ApplyFixUp (log, item)) {
				everythingWorked = false;
			}
		}

		return everythingWorked;
	}

	static bool ApplyFixUp (TaskLoggingHelper log, ITaskItem monoAndroidRuntime)
	{
		string newDirPath = Path.Combine (Path.GetDirectoryName (monoAndroidRuntime.ItemSpec), "new");
		string newFilePath = Path.Combine (newDirPath, Path.GetFileName (monoAndroidRuntime.ItemSpec));
		Directory.CreateDirectory (newDirPath);

		string origPdbPath = Path.ChangeExtension (monoAndroidRuntime.ItemSpec, ".pdb");
		bool havePdb = File.Exists (origPdbPath);

		log.LogDebugMessage ($"Fixing up {monoAndroidRuntime.ItemSpec}");
		var readerParams = new ReaderParameters () {
			InMemory = true,
			ReadSymbols = havePdb,
		};
		AssemblyDefinition asmdef = AssemblyDefinition.ReadAssembly (monoAndroidRuntime.ItemSpec, readerParams);
		TypeDefinition? androidRuntimeInternal = null;
		foreach (ModuleDefinition module in asmdef.Modules) {
			androidRuntimeInternal = FindAndroidRuntimeInternal (module);
			if (androidRuntimeInternal != null) {
				break;
			}
		}

		if (androidRuntimeInternal == null) {
			log.LogDebugMessage ($"'{RuntimeTypeName}' not found in {monoAndroidRuntime.ItemSpec}");
			return true; // Not an error, per se...
		}
		log.LogDebugMessage ($"Found '{RuntimeTypeName}', making it public");
		androidRuntimeInternal.IsPublic = true;

		var writerParams = new WriterParameters {
			WriteSymbols = havePdb,
		};
		asmdef.Write (newFilePath, writerParams);

		CopyFile (log, newFilePath, monoAndroidRuntime.ItemSpec);
		RemoveFile (log, newFilePath);

		if (!havePdb) {
			return true;
		}

		string pdbPath = Path.ChangeExtension (newFilePath, ".pdb");
		havePdb = File.Exists (pdbPath);
		if (!havePdb) {
			return true;
		}

		CopyFile (log, pdbPath, origPdbPath);
		RemoveFile (log, pdbPath);

		return true;
	}

	static void CopyFile (TaskLoggingHelper log, string source, string target)
	{
		log.LogDebugMessage ($"Copying rewritten assembly: {source} -> {target}");
		MonoAndroidHelper.CopyFileAvoidSharingViolations (log, source, target);
	}

	static void RemoveFile (TaskLoggingHelper log, string? path)
	{
		log.LogDebugMessage ($"Deleting: {path}");
		MonoAndroidHelper.TryRemoveFile (log, path);
	}

	static TypeDefinition? FindAndroidRuntimeInternal (ModuleDefinition module)
	{
		foreach (TypeDefinition t in module.Types) {
			if (MonoAndroidHelper.StringEquals (RuntimeTypeName, t.FullName)) {
				return t;
			}
		}

		return null;
	}
}
