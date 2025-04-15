using System;
using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Microsoft.Android.AppTools.Assemblies;

public class AssemblyStore
{
	static List<AssemblyStoreItem>? emptyAssemblyList;

	readonly ILogger log;
	readonly AssemblyStoreExplorer explorer;

	public IList<AssemblyStoreItem> Assemblies => explorer.Assemblies ?? GetEmptyAssemblyList ();
	public uint FullFormatVersion { get; private set; } = 0;
	public ulong NumberOfAssemblies => explorer.AssemblyCount;
	public AndroidTargetArch TargetArchitecture => explorer.TargetArch ?? AndroidTargetArch.None;
	public Version? Version { get; private set; }

	internal AssemblyStore (ILogger log, AssemblyStoreExplorer explorer)
	{
		this.log = log;
		this.explorer = explorer;
	}

	static List<AssemblyStoreItem> GetEmptyAssemblyList ()
	{
		if (emptyAssemblyList != null) {
			return emptyAssemblyList;
		}

		emptyAssemblyList = new List<AssemblyStoreItem> ();
		return emptyAssemblyList;
	}
}
