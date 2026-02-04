using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Appends marshal method symbols to the NativeAOT exports file.
/// The exports file format is a linker version script:
/// V1.0 {
///     global:
///         JNI_OnLoad;
///         symbol2;
///     local: *;
/// };
/// </summary>
public class AppendMarshalMethodExports : AndroidTask
{
	public override string TaskPrefix => "AMME";

	[Required]
	public string MarshalMethodsExportsFile { get; set; } = "";

	[Required]
	public string ExportsFile { get; set; } = "";

	public override bool RunTask ()
	{
		if (!File.Exists (MarshalMethodsExportsFile)) {
			Log.LogMessage (MessageImportance.High, $"[AMME] Marshal methods exports file not found: {MarshalMethodsExportsFile}");
			return true;
		}

		if (!File.Exists (ExportsFile)) {
			Log.LogMessage (MessageImportance.High, $"[AMME] Exports file not found: {ExportsFile}");
			return true;
		}

		// Read the symbols from our exports file (one per line)
		var symbols = File.ReadAllLines (MarshalMethodsExportsFile);
		if (symbols.Length == 0) {
			Log.LogMessage (MessageImportance.High, $"[AMME] No symbols to append");
			return true;
		}

		// Read the existing exports file
		var content = File.ReadAllText (ExportsFile);

		// Find the position to insert (before "local: *;")
		int insertPos = content.IndexOf ("local: *;", StringComparison.Ordinal);
		if (insertPos < 0) {
			Log.LogWarning ($"[AMME] Could not find 'local: *;' in exports file, appending to end");
			// Try to find end of global section
			insertPos = content.IndexOf ("};", StringComparison.Ordinal);
			if (insertPos < 0) {
				Log.LogError ($"[AMME] Could not parse exports file format");
				return false;
			}
		}

		// Build the new content
		var sb = new StringBuilder ();
		sb.Append (content.Substring (0, insertPos));
		foreach (var symbol in symbols) {
			if (!string.IsNullOrWhiteSpace (symbol)) {
				sb.AppendLine ($"        {symbol.Trim ()};");
			}
		}
		sb.Append ("    "); // Indent for "local: *;"
		sb.Append (content.Substring (insertPos));

		File.WriteAllText (ExportsFile, sb.ToString ());
		Log.LogMessage (MessageImportance.High, $"[AMME] Appended {symbols.Length} symbols to exports file");

		return true;
	}
}
