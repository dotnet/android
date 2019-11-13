using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_DownloadNuGet : StepWithDownloadProgress
	{
		public Step_DownloadNuGet ()
			: base ("Download NuGet")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			if (context == null)
				throw new ArgumentNullException (nameof (context));

			Uri nugetUrl = Configurables.Urls.NugetUri;
			string localNugetPath = Configurables.Paths.LocalNugetPath;

			if (Utilities.FileExists (localNugetPath)) {
				Log.StatusLine ($"NuGet already downloaded ({localNugetPath})");
				return true;
			}

			Utilities.CreateDirectory (Path.GetDirectoryName (localNugetPath));
			Log.StatusLine ("Downloading NuGet");

			(bool success, ulong size, HttpStatusCode status) = await Utilities.GetDownloadSizeWithStatus (nugetUrl);
			if (!success) {
				if (status == HttpStatusCode.NotFound)
					Log.ErrorLine ("NuGet URL not found");
				else
					Log.ErrorLine ("Failed to obtain NuGet size. HTTP status code: {status} ({(int)status})");
				return false;
			}

			DownloadStatus downloadStatus = Utilities.SetupDownloadStatus (context, size, context.InteractiveSession);
			Log.StatusLine ($"  {context.Characters.Link} {nugetUrl}", ConsoleColor.White);
			await Download (context, nugetUrl, localNugetPath, "NuGet", Path.GetFileName (localNugetPath), downloadStatus);

			if (!File.Exists (localNugetPath)) {
				Log.ErrorLine ($"Download of NuGet from {nugetUrl} failed");
				return false;
			}

			return true;
		}
	}
}
