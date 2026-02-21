using System;
using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Represents an aspect of a .NET for Android application. An aspect can be an
/// individual assembly, the whole APK/AAB package, a shared library etc.
/// If it exists as a definable, separate entity in the application, that can
/// be identified/detected by looking at its format/location it is most
/// likely an aspect.
/// </summary>
public interface IAspect
{
	/// <summary>
	/// Aspect name, for presentation purposes.
	/// </summary>
	static string AspectName => throw new NotImplementedException ();

	/// <summary>
	/// Probes whether <paramref name="stream"/> contains something this aspect
	/// recognizes and supports. Returns `true` if it can handle the data,
	/// `false` otherwise. The <paramref name="description"/> parameter can be anything that makes
	/// sense for the given aspect (e.g. a file name).
	/// </summary>
	static IAspectState ProbeAspect (Stream stream, string? description = null) => throw new NotImplementedException ();

	/// <summary>
	/// Load the aspect and return instance of a class implementing support for it.
	/// The <paramref name="description"/> parameter can be anything that makes
	/// sense for the given aspect (e.g. a file name).
	/// </summary>
	static IAspect LoadAspect (Stream stream, IAspectState state, string? description = null) => throw new NotImplementedException ();
}
