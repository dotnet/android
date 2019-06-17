using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_DownloadXDelta : StepWithDownloadProgress
	{
		const string XDeltaTag = "v3.0.11-ci";
		const string XDeltaVersion = "3.0";
		static readonly Dictionary<string, string> platforms = new Dictionary<string, string> () {
			{ "macos", "Darwin" },
			{ "windows", "" },
			{ "linux", "Linux" },
			{ "android", "Android" },
		};

		public Step_DownloadXDelta () : base ($"Downloading xdelta") { }

		protected async override Task<bool> Execute (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			//https://github.com/dellis1972/xdelta/releases/download/$(XDeltaTag)/xdelta-$(XDeltaVersion)-$(_XDeltaDownload.Platform).7z
			string packageCacheDir = context.Properties.GetRequiredValue (KnownProperties.AndroidToolchainCacheDirectory);
			var sevenZip = new SevenZipRunner (context);

			foreach (var platform in platforms) {
				string archiveFileName = $"xdelta-{XDeltaVersion}-{platform.Key}.7z";
				Uri xDeltaUri = new Uri (Configurables.Urls.XDeltaUri, $"{XDeltaTag}/{archiveFileName}");

				string cacheFileName = Path.Combine (packageCacheDir, archiveFileName);

				Utilities.CreateDirectory (Path.GetDirectoryName (cacheFileName));

				if (!await DownloadXDeltaBundle (context, cacheFileName, xDeltaUri))
					return false;

				string extractToPath = Path.Combine (Configurables.Paths.InstallMSBuildDir, platform.Value);

				Utilities.CreateDirectory (extractToPath);

				if (! await sevenZip.Extract (cacheFileName, extractToPath))
					return false;
			}

			return true;
		}

		async Task<bool> DownloadXDeltaBundle (Context context, string localPackagePath, Uri url)
		{
			if (File.Exists (localPackagePath)) {
				if (await Utilities.VerifyArchive (localPackagePath)) {
					Log.StatusLine ("xdelta archive already downloaded and valid");
					return true;
				}
				Utilities.DeleteFileSilent (localPackagePath);
			}

			Log.StatusLine ("Downloading xdelta from ", url.ToString (), tailColor: ConsoleColor.White);

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, int.MaxValue, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {url}", ConsoleColor.White);
			await Download (context, url, localPackagePath, "xdelta", Path.GetFileName (localPackagePath), downloadStatus);

			if (!File.Exists (localPackagePath)) {
				Log.ErrorLine ($"Download of xdelta from {url} failed.");
				return false;
			}

			return true;
		}
	}
}
