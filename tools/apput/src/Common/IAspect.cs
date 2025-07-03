using System.IO;

namespace ApplicationUtility;

/// <summary>
/// Represets an aspect of a .NET for Android application. An aspect can be an
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
	abstract static string AspectName { get; }

	/// <summary>
	/// Probes whether <paramref name="stream"/> contains something this aspect
	/// recognizes and supports. Returns `true` if it can handle the data,
	/// `false` otherwise.
	/// </summary>
	abstract static bool ProbeAspect (Stream stream);

	/// <summary>
	/// Load the aspect and return instance of a class implementing support for it.
	/// The <paramref name="description"/> parameter can be anything that makes
	/// sense for the given aspect (e.g. a file name).
	/// </summary>
	abstract static IAspect LoadAspect (Stream stream, string description);
}
