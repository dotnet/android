using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_CreateBundle : Step
	{
		public Step_CreateBundle ()
			: base ("Creating binary bundle")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			var allRuntimes = new Runtimes ();
			string binRoot = Configurables.Paths.BinDir;
			string bundlePath = Path.Combine (binRoot, Configurables.Paths.XABundleFileName);
			Log.StatusLine ("Generating bundle archive: ", Utilities.GetRelativePath (BuildPaths.XamarinAndroidSourceRoot, bundlePath), tailColor: ConsoleColor.White);
			Utilities.DeleteFileSilent (bundlePath);

			var sevenZip = new SevenZipRunner (context);
			CompressionFormat cf = context.CompressionFormat;
			Func<string, string, List<string>, Task<bool>> compressor;

			if (String.Compare (cf.Name, Configurables.Defaults.ZipCompressionFormatName, StringComparison.OrdinalIgnoreCase) == 0) {
				compressor = sevenZip.Zip;
			} else if (String.Compare (cf.Name, Configurables.Defaults.SevenZipCompressionFormatName, StringComparison.OrdinalIgnoreCase) == 0) {
				compressor = sevenZip.SevenZip;
			} else {
				throw new InvalidOperationException ($"Unsupported compression type: {cf.Description}");
			}

			EnsureAllSDKHeadersAreIncluded (context, allRuntimes);
			List<string> items = allRuntimes.BundleItems.Where (item => item.ShouldInclude == null || item.ShouldInclude (context)).Select (
				item => {
					string relPath = Utilities.GetRelativePath (binRoot, item.SourcePath);
					Log.DebugLine ($"Bundle item: {item.SourcePath} (archive path: {relPath})");
					return relPath;
				}
			).Distinct ().ToList ();
			items.Sort ();

			if (!await compressor (bundlePath, binRoot, items)) {
				Log.ErrorLine ("Bundle archive creation failed, see the log files for details.");
				return false;
			}

			return true;
		}

		void EnsureAllSDKHeadersAreIncluded (Context context, Runtimes allRuntimes)
		{
			string topDirTail = Configurables.Paths.MonoSDKRelativeIncludeSourceDir;
			if (!topDirTail.EndsWith (Path.DirectorySeparatorChar.ToString (), StringComparison.Ordinal))
				topDirTail += Path.DirectorySeparatorChar;

			// Find first enabled runtime - all headers are the same across all runtimes, so we don't care
			// where they come from.
			Runtime runtime = MonoRuntimesHelpers.GetEnabledRuntimes (allRuntimes, enableLogging: false)?.FirstOrDefault ();
			if (runtime == null) {
				Log.WarningLine ("No enabled runtimes (?!)");
				return;
			}

			string runtimeIncludeDirRoot = Path.Combine (Configurables.Paths.MonoSourceFullPath, MonoRuntimesHelpers.GetRootDir (runtime), Configurables.Paths.MonoSDKRelativeIncludeSourceDir);
			IEnumerable<string> sourceIncludes = Directory.EnumerateFiles (runtimeIncludeDirRoot, "*", SearchOption.AllDirectories);
			var destinationIncludes = new List <string> ();

			foreach (RuntimeFile rf in allRuntimes.RuntimeFilesToInstall.Where (rf => rf.Type == RuntimeFileType.SdkHeader)) {
				destinationIncludes.Add (Path.Combine (Configurables.Paths.MonoSourceFullPath, rf.Source (runtime)));
			}

			bool haveDifference = false;
			haveDifference &= ReportDifference (sourceIncludes.Except (destinationIncludes).ToList (), "runtime", "bundle");
			haveDifference &= ReportDifference (destinationIncludes.Except (sourceIncludes).ToList (), "bundle", "runtime");

			if (haveDifference)
				throw new InvalidOperationException ("Differences found between the Mono SDK header files shipped in Mono archive and included in Xamarin.Android bundle");

			bool ReportDifference (List<string> diff, string foundIn, string notFoundIn)
			{
				if (diff.Count == 0)
					return false;

				Log.ErrorLine ($"There are files found in the {foundIn} but not in the {notFoundIn}:");
				foreach (string f in diff) {
					Log.ErrorLine ($"  {context.Characters.Bullet} {Utilities.GetRelativePath (runtimeIncludeDirRoot, f)}");
				}
				Log.ErrorLine ();
				return true;
			}
		}
	}
}
