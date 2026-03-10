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
		bool haveArchitectures = Options.Architectures != null && Options.Architectures.Count > 0;
		var assemblies = new List<ApplicationAssembly> ();
		var pdbs = new List<AssemblyPdb> ();

		if (package.AssemblyStores != null) {
			foreach (AssemblyStore store in package.AssemblyStores) {
				if (!StoreForRequestedArchitecture (store)) {
					continue;
				}

				assemblies.AddRange (store.Assemblies.Values);
				if (Options.ExtractPDB) {
					pdbs.AddRange (store.PDBs.Values);
				}
			}
		}

		if (package.StandaloneAssemblies != null) {
			Log.Debug ($"Package has {package.StandaloneAssemblies.Count} standalone assemblies.");
			foreach (ApplicationAssembly asm in package.StandaloneAssemblies) {
				if (!AssemblyForRequestedArchitecture (asm)) {
					Log.Debug ($"Assembly {asm.Name} ignored, architecture {asm.Architecture} is not requested.");
					continue;
				}

				assemblies.Add (asm);
			}
		}

		if (Options.ExtractPDB && package.StandalonePdbs != null) {
			Log.Debug ($"Package has {package.StandalonePdbs.Count} standalone PDBs.");
			foreach (AssemblyPdb pdb in package.StandalonePdbs) {
				if (!PdbForRequestedArchitecture (pdb)) {
					Log.Debug ($"PDB {pdb.Name} ignored, architecture {pdb.Architecture} is not requested.");
					continue;
				}

				pdbs.Add (pdb);
			}
		}

		return Extract (getOutputStreamForPath, assemblies, pdbs);

		bool StoreForRequestedArchitecture (AssemblyStore store) => MatchesRequestedArchitecture (Utilities.TargetArchToNative (store.Architecture));
		bool AssemblyForRequestedArchitecture (ApplicationAssembly asm) => MatchesRequestedArchitecture (asm.Architecture);
		bool PdbForRequestedArchitecture (AssemblyPdb pdb) => MatchesRequestedArchitecture (pdb.Architecture);

		bool MatchesRequestedArchitecture (NativeArchitecture arch)
		{
			if (!haveArchitectures) {
				return true;
			}

			return Options.Architectures!.Contains (arch);
		}
	}

	// `assemblies` and `pdbs` are expected to contain only entries for the architectures selected by the user.
	bool Extract (GetOutputStreamForPathFn getOutputStreamForPath, List<ApplicationAssembly> assemblies, List<AssemblyPdb> pdbs)
	{
		if (assemblies.Count == 0) {
			return true;
		}

		if (Options.AssemblyPatterns == null || Options.AssemblyPatterns.Count == 0) {
			return Extract (null, getOutputStreamForPath, assemblies);
		}

		bool allIsFine = true;
		if (!Options.UseRegex) {
			// Glob patterns are combined into a single regex
			allIsFine &= Extract (MakeRegexFromGlobPatterns (".dll"), getOutputStreamForPath, assemblies);
			if (Options.ExtractPDB) {
				allIsFine &= Extract (MakeRegexFromGlobPatterns (".pdb"), getOutputStreamForPath, pdbs);
			}

			return allIsFine;
		}

		// If the caller chose regexes, we match the patterns one by one. Slower, but regexes may be hard or
		// impossible to combine.
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

	bool Extract (Regex? nameRegex, GetOutputStreamForPathFn getOutputStreamForPath, List<AssemblyPdb> pdbs)
	{
		throw new NotImplementedException ();
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

	Regex? MakeRegexFromGlobPatterns (string fileExtension)
	{
		var rxSource = new StringBuilder ();
		foreach (string ap in Options.AssemblyPatterns!) {
			if (String.IsNullOrWhiteSpace (ap)) {
				continue;
			}

			string? expression = GlobPatternToRegex (ap, fileExtension);
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

	string? GlobPatternToRegex (string ap, string fileExtension)
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

		// TODO: we will always deal with either .pdb or .dll extensions here. Make sure the glob
		//       regex works if the glob pattern is e.g. Mono*.dll and we're requesting '.pdb' to
		//       be extracted. Also make sure we don't end up with .dll.pdb or .pdb.dll patterns.
		if (!ap.EndsWith (fileExtension, StringComparison.OrdinalIgnoreCase)) {
			// All ApplicationAssembly instances will have names that contain the .dll extension
			// All AssemblyPdb instances will have names that contain the .pdb extension
			sb.Append (fileExtension);
		}

		return sb.ToString ();
	}
}
