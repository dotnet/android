using System;
using System.Collections.Generic;
using System.IO;

namespace ApplicationUtility;

public class AssemblyStore : IAspect
{
	public static string AspectName { get; } = "Assembly Store";

	public IDictionary<string, ApplicationAssembly> Assemblies { get; private set; } = null!;
	public NativeArchitecture Architecture { get; private set; } = NativeArchitecture.Unknown;
	public ulong NumberOfAssemblies => (ulong)(Assemblies?.Count ?? 0);

	public static IAspect LoadAspect (Stream stream, string description)
	{
		throw new NotImplementedException ();
	}

	public static bool ProbeAspect (Stream stream)
	{
		throw new NotImplementedException ();
	}
}
