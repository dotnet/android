using System;
using System.IO;

namespace ApplicationUtility;

public abstract class ApplicationPackage : IAspect
{
	public static string AspectName { get; } = "Application package";

	public abstract string PackageFormat { get; }

	public static IAspect LoadAspect (Stream stream, string description)
	{
		throw new NotImplementedException ();
	}

	public static bool ProbeAspect (Stream stream)
	{
		throw new NotImplementedException ();
	}
}
