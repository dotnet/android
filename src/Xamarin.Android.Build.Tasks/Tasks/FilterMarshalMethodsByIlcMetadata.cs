using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Filters marshal method object files and exports based on which proxy types
/// survive ILC (NativeAOT) trimming. This significantly reduces binary size by
/// not linking marshal methods for types that were trimmed away.
/// 
/// The task parses ILC's metadata.csv to find surviving proxy types, then:
/// 1. Filters the list of .o files to only include survivors
/// 2. Filters the exports file to only include symbols defined in surviving .o files
/// 3. Outputs the Java class names for proguard rule generation
/// </summary>
public class FilterMarshalMethodsByIlcMetadata : AndroidTask
{
	public override string TaskPrefix => "FMMIM";

	/// <summary>
	/// Path to ILC's metadata.csv file which contains surviving types.
	/// </summary>
	[Required]
	public string IlcMetadataFile { get; set; } = "";

	/// <summary>
	/// All marshal method object files (unfiltered).
	/// </summary>
	[Required]
	public ITaskItem[] MarshalMethodObjectFiles { get; set; } = Array.Empty<ITaskItem> ();

	/// <summary>
	/// The marshal methods exports file to filter.
	/// </summary>
	[Required]
	public string MarshalMethodsExportsFile { get; set; } = "";

	/// <summary>
	/// The TypeMaps assembly that contains the type mappings.
	/// Used to extract Java class names for proguard rules.
	/// </summary>
	public string? TypeMapsAssemblyPath { get; set; }

	/// <summary>
	/// Output: Filtered marshal method object files to link.
	/// </summary>
	[Output]
	public ITaskItem[] FilteredObjectFiles { get; set; } = Array.Empty<ITaskItem> ();

	/// <summary>
	/// Output: Filtered exports file path.
	/// </summary>
	[Output]
	public string FilteredExportsFile { get; set; } = "";

	/// <summary>
	/// Output: Java class names that survived trimming (for proguard).
	/// </summary>
	[Output]
	public ITaskItem[] SurvivingJavaClasses { get; set; } = Array.Empty<ITaskItem> ();

	public override bool RunTask ()
	{
		if (!File.Exists (IlcMetadataFile)) {
			Log.LogMessage (MessageImportance.High, $"[{TaskPrefix}] ILC metadata file not found: {IlcMetadataFile}");
			Log.LogMessage (MessageImportance.High, $"[{TaskPrefix}] Keeping all {MarshalMethodObjectFiles.Length} object files (no filtering)");
			FilteredObjectFiles = MarshalMethodObjectFiles;
			FilteredExportsFile = MarshalMethodsExportsFile;
			return true;
		}

		// Parse surviving proxy types from ILC metadata
		var survivingTypes = ParseSurvivingProxyTypes (IlcMetadataFile);
		Log.LogMessage (MessageImportance.High, $"[{TaskPrefix}] Found {survivingTypes.Count} surviving proxy types in ILC metadata");

		// Filter object files
		var filteredFiles = new List<ITaskItem> ();
		var keptCount = 0;
		var skippedCount = 0;

		foreach (var item in MarshalMethodObjectFiles) {
			var fileName = Path.GetFileNameWithoutExtension (item.ItemSpec);
			
			// Skip infrastructure files (always keep them)
			if (!fileName.StartsWith ("marshal_methods_", StringComparison.Ordinal)) {
				filteredFiles.Add (item);
				keptCount++;
				continue;
			}

			// Always keep marshal_methods_init (contains typemap_get_function_pointer)
			if (fileName == "marshal_methods_init") {
				filteredFiles.Add (item);
				keptCount++;
				continue;
			}

			// Extract type name from file name: marshal_methods_Android_App_Activity -> Android_App_Activity
			var typeName = fileName.Substring ("marshal_methods_".Length);
			
			// Check if this type survived trimming
			if (survivingTypes.Contains (typeName)) {
				filteredFiles.Add (item);
				keptCount++;
			} else {
				skippedCount++;
				Log.LogMessage (MessageImportance.Low, $"[{TaskPrefix}] Skipping trimmed type: {typeName}");
			}
		}

		FilteredObjectFiles = filteredFiles.ToArray ();
		Log.LogMessage (MessageImportance.High, $"[{TaskPrefix}] Filtered object files: {keptCount} kept, {skippedCount} skipped");

		// Collect all symbols from the filtered object files
		var definedSymbols = CollectDefinedSymbols (filteredFiles);
		Log.LogMessage (MessageImportance.High, $"[{TaskPrefix}] Collected {definedSymbols.Count} symbols from filtered object files");

		// Filter exports file based on defined symbols
		FilterExportsFile (definedSymbols);

		return true;
	}

	/// <summary>
	/// Parses the ILC metadata.csv file to extract proxy type names that survived trimming.
	/// The format is: Handle, Kind, Name, Children
	/// We're looking for TypeDefinition entries with names like "_Microsoft.Android.TypeMaps.Android_App_Activity_Proxy"
	/// </summary>
	HashSet<string> ParseSurvivingProxyTypes (string metadataFile)
	{
		var survivingTypes = new HashSet<string> (StringComparer.Ordinal);
		
		// Pattern to match: TypeDefinition, "_Microsoft.Android.TypeMaps.{TypeName}_Proxy"
		var proxyPattern = new Regex (@"TypeDefinition,\s*""_Microsoft\.Android\.TypeMaps\.([^""]+)_Proxy""", RegexOptions.Compiled);

		foreach (var line in File.ReadLines (metadataFile)) {
			var match = proxyPattern.Match (line);
			if (match.Success) {
				var typeName = match.Groups[1].Value;
				survivingTypes.Add (typeName);
			}
		}

		return survivingTypes;
	}

	/// <summary>
	/// Collects all globally defined symbols from the object files.
	/// Uses 'nm' tool to extract symbol names.
	/// </summary>
	HashSet<string> CollectDefinedSymbols (List<ITaskItem> objectFiles)
	{
		var symbols = new HashSet<string> (StringComparer.Ordinal);

		foreach (var item in objectFiles) {
			var objPath = item.ItemSpec;
			if (!File.Exists (objPath))
				continue;

			try {
				// Use nm to get defined symbols (T = text/code section)
				var psi = new ProcessStartInfo {
					FileName = "nm",
					Arguments = $"-g \"{objPath}\"",
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using var process = Process.Start (psi);
				if (process == null)
					continue;

				var output = process.StandardOutput.ReadToEnd ();
				process.WaitForExit ();

				// Parse nm output: each line is like "0000000000000000 T symbolname"
				foreach (var line in output.Split ('\n')) {
					var parts = line.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length >= 3 && parts[1] == "T") {
						symbols.Add (parts[2]);
					}
				}
			} catch (Exception ex) {
				Log.LogMessage (MessageImportance.Low, $"[{TaskPrefix}] Failed to read symbols from {objPath}: {ex.Message}");
			}
		}

		return symbols;
	}

	/// <summary>
	/// Filters the exports file to only include symbols that are defined in the object files.
	/// </summary>
	void FilterExportsFile (HashSet<string> definedSymbols)
	{
		if (!File.Exists (MarshalMethodsExportsFile)) {
			Log.LogMessage (MessageImportance.High, $"[{TaskPrefix}] Marshal methods exports file not found: {MarshalMethodsExportsFile}");
			FilteredExportsFile = MarshalMethodsExportsFile;
			return;
		}

		var outputPath = Path.Combine (
			Path.GetDirectoryName (MarshalMethodsExportsFile)!,
			"marshal_methods_exports_filtered.txt"
		);

		var keptSymbols = 0;
		var skippedSymbols = 0;

		using (var writer = new StreamWriter (outputPath)) {
			foreach (var line in File.ReadLines (MarshalMethodsExportsFile)) {
				var symbol = line.Trim ();
				if (string.IsNullOrEmpty (symbol))
					continue;

				// Keep symbols that are defined in our object files
				if (definedSymbols.Contains (symbol)) {
					writer.WriteLine (symbol);
					keptSymbols++;
				} else {
					skippedSymbols++;
					Log.LogMessage (MessageImportance.Low, $"[{TaskPrefix}] Skipping symbol not in object files: {symbol}");
				}
			}
		}

		FilteredExportsFile = outputPath;
		Log.LogMessage (MessageImportance.High, $"[{TaskPrefix}] Filtered exports: {keptSymbols} kept, {skippedSymbols} skipped");
	}
}
