using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using TTask = System.Threading.Tasks.Task;
using MTask = Microsoft.Build.Utilities.Task;

namespace Xamarin.Android.BuildTools.PrepTasks {

	public class DownloadUri : MTask, ICancelableTask
	{
		public DownloadUri ()
		{
		}

		[Required]
		public string[]     SourceUris          { get; set; }

		[Required]
		public ITaskItem[]  DestinationFiles    { get; set; }

		CancellationTokenSource cancellationTokenSource;

		public void Cancel ()
		{
			cancellationTokenSource?.Cancel ();
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, "DownloadUri:");
			Log.LogMessage (MessageImportance.Low, "  SourceUris:");
			foreach (var uri in SourceUris) {
				Log.LogMessage (MessageImportance.Low, "    {0}", uri);
			}
			Log.LogMessage (MessageImportance.Low, "  DestinationFiles:");
			foreach (var dest in DestinationFiles) {
				Log.LogMessage (MessageImportance.Low, "    {0}", dest.ItemSpec);
			}

			if (SourceUris.Length != DestinationFiles.Length) {
				Log.LogError ("SourceUris.Length must equal DestinationFiles.Length.");
				return false;
			}

			var source  = cancellationTokenSource = new CancellationTokenSource ();
			var tasks   = new TTask [SourceUris.Length];
			using (var client = new HttpClient ()) {
				client.Timeout = TimeSpan.FromHours (3);
				for (int i = 0; i < SourceUris.Length; ++i) {
					tasks [i] = DownloadFile (client, source, SourceUris [i], DestinationFiles [i].ItemSpec);
				}
				TTask.WaitAll (tasks, source.Token);
			}

			return !Log.HasLoggedErrors;
		}

		async TTask DownloadFile (HttpClient client, CancellationTokenSource source, string uri, string destinationFile)
		{
			if (File.Exists (destinationFile)) {
				Log.LogMessage (MessageImportance.Normal, $"Skipping uri '{uri}' as destination file already exists '{destinationFile}'.");
				return;
			}
			var dp       = Path.GetDirectoryName (destinationFile);
			var dn       = Path.GetFileName (destinationFile);
			var tempPath = Path.Combine (dp, "." + dn + ".download");
			Directory.CreateDirectory(dp);

			Log.LogMessage (MessageImportance.Normal, $"Downloading `{uri}` to `{tempPath}`.");
			try {
				using (var r = await client.GetAsync (uri, source.Token)) {
					r.EnsureSuccessStatusCode ();
					using (var s = await r.Content.ReadAsStreamAsync ())
					using (var o = File.OpenWrite (tempPath)) {
						await s.CopyToAsync (o, 4096, source.Token);
					}
				}
				Log.LogMessage (MessageImportance.Low, $"mv '{tempPath}' '{destinationFile}'.");
				File.Move (tempPath, destinationFile);
			}
			catch (Exception e) {
				Log.LogError ("Unable to download URL `{0}` to `{1}`: {2}", uri, destinationFile, e.Message);
				Log.LogErrorFromException (e);
			}
		}
	}
}
