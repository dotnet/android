using System;
using System.Collections.Generic;
using System.IO;

using Xamarin.Android.Tools;

namespace ApplicationUtility;

public class AssemblyStore : IAspect
{
	public static string AspectName { get; } = "Assembly Store";

	public IDictionary<string, ApplicationAssembly> Assemblies { get; private set; } = null!;
	public AndroidTargetArch Architecture { get; private set; } = AndroidTargetArch.None;
	public ulong NumberOfAssemblies => (ulong)(Assemblies?.Count ?? 0);

	public static IAspect LoadAspect (Stream stream, string? description)
	{
		throw new NotImplementedException ();
	}

	public static bool ProbeAspect (Stream stream, string? description)
	{
		throw new NotImplementedException ();
	}
}
