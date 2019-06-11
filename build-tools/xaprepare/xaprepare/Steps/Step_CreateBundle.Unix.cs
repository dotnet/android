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
	}
}
