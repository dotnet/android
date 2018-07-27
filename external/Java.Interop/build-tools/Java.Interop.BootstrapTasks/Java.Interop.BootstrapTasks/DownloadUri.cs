using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using TTask = System.Threading.Tasks.Task;
using MTask = Microsoft.Build.Utilities.Task;

namespace Java.Interop.BootstrapTasks {

	public class DownloadUri : MTask
	{
		public DownloadUri ()
		{
		}

		[Required]
		public string[]     SourceUris          { get; set; }

		[Required]
		public ITaskItem[]  DestinationFiles    { get; set; }

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

			var tasks   = new TTask [SourceUris.Length];
			using (var client = new HttpClient ()) {
				client.Timeout = TimeSpan.FromHours (3);
				for (int i = 0; i < SourceUris.Length; ++i) {
					tasks [i] = DownloadFile (client, SourceUris [i], DestinationFiles [i].ItemSpec);
				}
				TTask.WaitAll (tasks);
			}

			return !Log.HasLoggedErrors;
		}

		async TTask DownloadFile (HttpClient client, string uri, string destinationFile)
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
				using (var s = await client.GetStreamAsync (uri))
				using (var o = File.OpenWrite (tempPath)) {
					await s.CopyToAsync (o);
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
