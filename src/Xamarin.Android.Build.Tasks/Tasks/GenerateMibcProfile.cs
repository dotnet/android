#nullable enable

using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Generates a MIBC profile listing the methods of the "main app assembly" so that
/// <c>crossgen2</c> in partial mode still ReadyToRun compiles it.
///
/// This runs after ILLink has trimmed the application, but before crossgen2 runs, so the profile
/// only ever contains methods that survived trimming.
/// </summary>
public class GenerateMibcProfile : AndroidTask
{
	public override string TaskPrefix => "GMP";

	/// <summary>
	/// The "main app assembly", after trimming.
	/// </summary>
	[Required]
	public string MainAssembly { get; set; } = "";

	/// <summary>Path of the <c>.mibc</c> file to write.</summary>
	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		if (!File.Exists (MainAssembly)) {
			Log.LogDebugMessage ($"Skipping MIBC profile generation, '{MainAssembly}' does not exist.");
			return !Log.HasLoggedErrors;
		}

		int methods = MibcProfileWriter.Write ([MainAssembly], OutputFile, message => Log.LogDebugMessage ("{0}", message));
		Log.LogDebugMessage ($"Wrote {methods} method(s) to '{OutputFile}'.");

		return !Log.HasLoggedErrors;
	}
}
