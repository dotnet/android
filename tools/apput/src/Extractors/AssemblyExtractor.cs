using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ApplicationUtility;

[AspectExtractor (containerAspectType: typeof (AssemblyStore),              storedAspectType: typeof (ApplicationAssembly))]
[AspectExtractor (containerAspectType: typeof (AssemblyStoreSharedLibrary), storedAspectType: typeof (ApplicationAssembly))]
[AspectExtractor (containerAspectType: typeof (PackageAAB),                 storedAspectType: typeof (ApplicationAssembly))]
[AspectExtractor (containerAspectType: typeof (PackageAPK),                 storedAspectType: typeof (ApplicationAssembly))]
[AspectExtractor (containerAspectType: typeof (PackageBase),                storedAspectType: typeof (ApplicationAssembly))]
class AssemblyExtractor : BaseExtractorWithOptions<AssemblyExtractorOptions>
{
	public AssemblyExtractor (IAspect containerAspect, AssemblyExtractorOptions options)
		: base (containerAspect, options)
	{}

	public override bool Extract (Stream destinationStream)
	{
		throw new NotSupportedException ($"Extractor handles multiple items, please use the {nameof(GetOutputStreamForPathFn)} overload.");
	}

	public override bool Extract (GetOutputStreamForPathFn getOutputStreamForPath)
	{
		const string AllAssemblies = "all assemblies";

		var patterns = Options.AssemblyPatterns switch {
			null => AllAssemblies,
			_ => Options.AssemblyPatterns.Count == 0 ? AllAssemblies : String.Join (", ", Options.AssemblyPatterns.Select (p => $"'{p}'"))
		};
		Log.Debug ($"Assembly extractor asked to extract the following: {patterns}");

		return ContainerAspect switch {
			AssemblyStore store            => Extract (getOutputStreamForPath, store),
			AssemblyStoreSharedLibrary dso => Extract (getOutputStreamForPath, dso),
			PackageAAB aab                 => Extract (getOutputStreamForPath, aab),
			PackageAPK apk                 => Extract (getOutputStreamForPath, apk),
			PackageBase basepkg            => Extract (getOutputStreamForPath, basepkg),
			_                              => throw new InvalidOperationException ($"Internal error: unexpected container aspect type '{ContainerAspect}'")
		};

	}

	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, AssemblyStore store)
	{
		throw new NotImplementedException ();
	}

	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, AssemblyStoreSharedLibrary dso)
	{
		throw new NotImplementedException ();
	}

	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, ApplicationPackage package)
	{
		if (package.AssemblyStores == null || package.AssemblyStores.Count == 0) {
			return LogNoAssemblies ();
		}

		// TODO: handle individual assemblies here, once we have support for them

		bool haveArchitectures = Options.Architectures != null && Options.Architectures.Count > 0;
		var assemblies = new List<ApplicationAssembly> ();
		foreach (AssemblyStore store in package.AssemblyStores) {
			if (!MatchesRequestedArchitecture (store)) {
				continue;
			}

			assemblies.AddRange (store.Assemblies.Values);
		}

		return Extract (getOutputStreamForPath, assemblies);

		bool LogNoAssemblies ()
		{
			Log.Info ("Package doesn't contain any assemblies.");
			return true;
		}

		bool MatchesRequestedArchitecture (AssemblyStore store)
		{
			if (!haveArchitectures) {
				return true;
			}

			return Options.Architectures!.Contains (Utilities.TargetArchToNative (store.Architecture));
		}
	}

	// `assemblies` is expected to contain only assemblies for the architectures selected by the user.
	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, List<ApplicationAssembly> assemblies)
	{
		if (assemblies.Count == 0) {
			return true;
		}

		if (Options.AssemblyPatterns == null || Options.AssemblyPatterns.Count == 0) {
			return Extract (null, getOutputStreamForPath, assemblies);
		}

		if (!Options.UseRegex) {
			// Glob patterns are combined into a single regex
			return Extract (MakeRegexFromGlobPatterns (), getOutputStreamForPath, assemblies);
		}

		// If the caller chose regexes, we match the patterns one by one. Slower, but regexes may be hard or
		// impossible to combine.
		bool allIsFine = true;
		foreach (string ap in Options.AssemblyPatterns) {
			try {
				// We keep going despite failures, no point in stopping because something failed for a single pattern
				allIsFine &= Extract (MakeRegex (ap), getOutputStreamForPath, assemblies);
			} catch (Exception ex) {
				Log.Warning ($"Attempt to extract assemblies using pattern '{ap}' resulted in exception {ex.GetType ()}: {ex.Message}");
				Log.Debug (ex.ToString ());
				allIsFine = false;
			}
		}

		return allIsFine;
	}

	bool Extract (Regex? nameRegex, GetOutputStreamForPathFn getOutputStreamForPath, List<ApplicationAssembly> assemblies)
	{
		// `null` nameRegex means match all the assemblies
		bool processAll = nameRegex == null;
		var asmName = new StringBuilder ();
		bool allIsFine = true;
		foreach (ApplicationAssembly asm in assemblies) {
			if (!processAll && !nameRegex!.IsMatch (asm.Name)) {
				continue;
			}

			if (asm.IgnoreOnLoad || asm.Size == 0) {
				Log.Info ($"Not extracting assembly '{asm.Name}' as it has no data (ignored? {asm.IgnoreOnLoad}; size {asm.Size})");
				continue;
			}

			asmName.Clear ().Append (asm.Name);
			if (Options.NoDecompress && asm.IsCompressed) {
				asmName.Append (".lz4");
			}

			string relName = Path.Combine (Utilities.ArchNameForPath (asm.Architecture), asmName.ToString ());
			string destPath = Path.Combine (Options.TargetDir, relName);
			Log.Debug ($"Requesting output stream for path '{destPath}'");

			// We don't own the stream, the caller does
			Stream stream = getOutputStreamForPath (destPath);
			if (!asm.WriteToStream (stream, decompress: !Options.NoDecompress)) {
				Log.Error ($"Failed to write assembly {relName} data to file.");
				allIsFine = false;
			}
		}

		return allIsFine;
	}

	Regex MakeRegex (string pattern)
	{
		return new Regex (pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds (10));
	}

	Regex? MakeRegexFromGlobPatterns ()
	{
		var rxSource = new StringBuilder ();
		foreach (string ap in Options.AssemblyPatterns!) {
			if (String.IsNullOrWhiteSpace (ap)) {
				continue;
			}

			string? expression = GlobPatternToRegex (ap);
			if (String.IsNullOrWhiteSpace (expression)) {
				continue;
			}

			if (rxSource.Length > 0) {
				// Using spaces for readability
				rxSource.Append(" | ");
			}
			rxSource.Append (expression);
		}

		string rx = rxSource.ToString ();
		Log.Debug ($"Glob patterns converted to regex: '{rx}'");
		return MakeRegex (rx);
	}

	string? GlobPatternToRegex (string ap)
	{
		// Input may contain escaped characters, Regex.Unescape processes the set of characters we're
		// interested in, so let's use it instead of writing our own code.
		var sb = new StringBuilder (Regex.Unescape (ap));

		// Convert any potential Windows dir separator chars to Unix ones...
		sb.Replace ('\\', '/');

		// ...and find its last instance, if any...
		int lastDirSep = -1;
		for (int i = sb.Length - 1; i >= 0; i--) {
			if (sb[i] == '/') {
				lastDirSep = i;
				break;
			}
		}

		// ...then remove everything up to, and including, the dir separator char...
		if (lastDirSep >= 0) {
			if (lastDirSep == sb.Length - 1) {
				Log.Warning ($"Assembly name pattern '{ap}' doesn't include any assembly name.");
				return null;
			}

			sb.Remove (0, lastDirSep + 1);
		}

		// ...then replace the patterns we don't support with something we can use, if any...
		sb.Replace ("**", "*");

		// ...and finally convert what remains to regular expression patterns
		sb.Replace (".", "\\.").Replace ("*", ".*").Replace ('?', '.');

		if (!ap.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
			// All ApplicationAssembly instances will have names that contain the .dll extension
			sb.Append (".dll");
		}

		return sb.ToString ();
	}
}
