using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Net;

namespace Xamarin.Android.Prepare
{
	partial class Step_Get_Android_BuildTools : StepWithDownloadProgress
	{
		List<(string package, string prefix)> packages = new List<(string package, string prefix)>();

		public Step_Get_Android_BuildTools ()
			: base ("Downloading build-tools archive")
		{
			string XABuildToolsVersion     = Context.Instance.Properties [KnownProperties.XABuildToolsVersion] ?? String.Empty;
			string XABuildToolsPackagePrefixMacOS = Context.Instance.Properties [KnownProperties.XABuildToolsPackagePrefixMacOS] ?? string.Empty;
			string XABuildToolsPackagePrefixWindows = Context.Instance.Properties [KnownProperties.XABuildToolsPackagePrefixWindows] ?? string.Empty;
			string XABuildToolsPackagePrefixLinux = Context.Instance.Properties [KnownProperties.XABuildToolsPackagePrefixLinux] ?? string.Empty;

			packages.Add ((package: $"build-tools_r{XABuildToolsVersion}-macosx.zip", prefix: XABuildToolsPackagePrefixMacOS));
			packages.Add ((package: $"build-tools_r{XABuildToolsVersion}-windows.zip", prefix: XABuildToolsPackagePrefixWindows));
			packages.Add ((package: $"build-tools_r{XABuildToolsVersion}-linux.zip", prefix: XABuildToolsPackagePrefixLinux));
		}

		protected override async Task<bool> Execute (Context context)
		{
			bool success = true;
			foreach (var package in packages) {
				success &= await DownloadBuildToolsPackage (context, package.prefix + package.package, package.package);
				if (!success) {
					Log.InfoLine ($"build-tools package '{package.package}' not present");
					return false;
				}
			}

			context.BuildToolsArchiveDownloaded = success;
			return true;
		}

		async Task<bool> DownloadBuildToolsPackage (Context context, string packageName, string localPackageName)
		{
			string localArchivePath = Path.Combine (Configurables.Paths.AndroidBuildToolsCacheDir, localPackageName);
			Uri url = new Uri (AndroidToolchain.AndroidUri, packageName);

			if (Utilities.FileExists (localArchivePath)) {
				Log.StatusLine ($"build-tools already downloaded ({localArchivePath})");
				return true;
			}

			Log.StatusLine ($"Downloading {packageName} from ", url.ToString (), tailColor: ConsoleColor.White);
 			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (url);
			if (!success) {
				if (status == HttpStatusCode.NotFound)
					Log.ErrorLine ("build-tools URL not found");
				else
					Log.ErrorLine ("Failed to obtain build-tools size. HTTP status code: {status} ({(int)status})");
				return false;
			}
			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {url}", ConsoleColor.White);
			await Download (context, url, localArchivePath, "build-tools", Path.GetFileName (localArchivePath), downloadStatus);

			if (!File.Exists (localArchivePath)) {
				Log.ErrorLine ($"Download of build-tools from {url} failed");
				return false;
			}

			return true;
		}
	}
}
