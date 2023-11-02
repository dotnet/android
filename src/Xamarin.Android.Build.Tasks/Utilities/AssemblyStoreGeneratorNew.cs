using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks;

class AssemblyStoreAssemblyInfoNew
{
	public AndroidTargetArch Arch  { get; }
	public string InArchivePath    { get; }
	public string SourceFilePath   { get; }

	public string? SymbolsFilePath { get; set; }
	public string? ConfigFilePath  { get; set; }

	public AssemblyStoreAssemblyInfoNew (string sourceFilePath, string inArchiveAssemblyPath, ITaskItem assembly)
	{
		Arch = MonoAndroidHelper.GetTargetArch (assembly);
		if (Arch == AndroidTargetArch.None) {
			throw new InvalidOperationException ($"Internal error: assembly item '{assembly}' lacks ABI information metadata");
		}

		SourceFilePath = sourceFilePath;
		InArchivePath = inArchiveAssemblyPath;
	}
}

class AssemblyStoreGeneratorNew
{
	readonly TaskLoggingHelper log;
	readonly Dictionary<AndroidTargetArch, List<AssemblyStoreAssemblyInfoNew>> assemblies;

	public AssemblyStoreGeneratorNew (TaskLoggingHelper log)
	{
		this.log = log;
		assemblies = new Dictionary<AndroidTargetArch, List<AssemblyStoreAssemblyInfoNew>> ();
	}

	public void Add (AssemblyStoreAssemblyInfoNew asmInfo)
	{
		if (!assemblies.TryGetValue (asmInfo.Arch, out List<AssemblyStoreAssemblyInfoNew> infos)) {
			infos = new List<AssemblyStoreAssemblyInfoNew> ();
			assemblies.Add (asmInfo.Arch, infos);
		}

		infos.Add (asmInfo);
	}
}
