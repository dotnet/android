#nullable enable
using System;
using System.IO;

using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class AssemblyStoreAssemblyInfo
{
	public AndroidTargetArch Arch        { get; }
	public FileInfo SourceFile           { get; }
	public string AssemblyName           { get; }
	public byte[] AssemblyNameBytes      { get; }
	public string AssemblyNameNoExt      { get; }
	public byte[] AssemblyNameNoExtBytes { get; }
	public FileInfo? SymbolsFile         { get; set; }
	public FileInfo? ConfigFile          { get; set; }
	public bool Ignored                  { get; }

	public AssemblyStoreAssemblyInfo (string sourceFilePath, ITaskItem assembly, bool assemblyIsIgnored = false)
	{
		Arch = MonoAndroidHelper.GetTargetArch (assembly);
		if (Arch == AndroidTargetArch.None) {
			throw new InvalidOperationException ($"Internal error: assembly item '{assembly}' lacks ABI information metadata");
		}

		SourceFile = new FileInfo (sourceFilePath);
		Ignored = assemblyIsIgnored;

		string? name = Path.GetFileName (SourceFile.Name);
		if (name == null) {
			throw new InvalidOperationException ("Internal error: info without assembly name");
		}

		if (name.EndsWith (".lz4", StringComparison.OrdinalIgnoreCase)) {
			name = Path.GetFileNameWithoutExtension (name);
		}

		string nameNoExt = Path.GetFileNameWithoutExtension (name);
		string? culture = assembly.GetMetadata ("Culture");
		if (!culture.IsNullOrEmpty ()) {
			name = $"{culture}/{name}";
			nameNoExt = $"{culture}/{nameNoExt}";
		}

		(AssemblyName, AssemblyNameBytes) = SetName (name);
		(AssemblyNameNoExt, AssemblyNameNoExtBytes) = SetName (nameNoExt);

		(string name, byte[] bytes) SetName (string assemblyName)
		{
			return (assemblyName, MonoAndroidHelper.Utf8StringToBytes (assemblyName));
		}
	}
}
