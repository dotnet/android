using System;
using System.IO;

using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class AssemblyStoreAssemblyInfo
{
	public AndroidTargetArch Arch    { get; }
	public string InArchivePath      { get; }
	public FileInfo SourceFile   { get; }

	public FileInfo? SymbolsFile { get; set; }
	public FileInfo? ConfigFile  { get; set; }

	public AssemblyStoreAssemblyInfo (string sourceFilePath, string inArchiveAssemblyPath, ITaskItem assembly)
	{
		Arch = MonoAndroidHelper.GetTargetArch (assembly);
		if (Arch == AndroidTargetArch.None) {
			throw new InvalidOperationException ($"Internal error: assembly item '{assembly}' lacks ABI information metadata");
		}

		SourceFile = new FileInfo (sourceFilePath);
		InArchivePath = inArchiveAssemblyPath;
	}
}
