using System;
using System.IO;

namespace ApplicationUtility;

public class ApplicationAssembly : IAspect
{
	public static string AspectName { get; } = "Application assembly";

	public bool IsCompressed    { get; private set; }
	public string Name          { get; private set; } = "";
	public ulong CompressedSize { get; private set; }
	public ulong Size           { get; private set; }
	public bool IgnoreOnLoad    { get; private set; }

	public static IAspect LoadAspect (Stream stream, string? description)
	{
		throw new NotImplementedException ();
	}

	public static bool ProbeAspect (Stream stream, string? description)
	{
		throw new NotImplementedException ();
	}
}
