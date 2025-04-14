using System;

using Xamarin.Android.Tools;

namespace Microsoft.Android.AppTools.Assemblies;

public class AssemblyStore
{
	public uint FullFormatVersion { get; private set; } = 0;
	public ulong NumberOfAssemblies { get; private set; } = 0;
	public AndroidTargetArch TargetArchitecture { get; private set; } = AndroidTargetArch.None;
	public Version? Version { get; private set; }
}
