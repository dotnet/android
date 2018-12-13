using System;
using System.IO;
using System.Linq;
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

		[Required, Output]
		public ITaskItem[]  DestinationFiles    { get; set; }

		public string       HashHeader          { get; set; }

		CancellationTokenSource cancellationTokenSource;

		public void Cancel ()
		{
			cancellationTokenSource?.Cancel ();
		}

		public override bool Execute ()
		{
			if (SourceUris.Length != DestinationFiles.Length) {
				Log.LogError ("SourceUris.Length must equal DestinationFiles.Length.");
				return false;
			}

			var source  = cancellationTokenSource = new CancellationTokenSource ();
			var tasks   = new Task<ITaskItem> [SourceUris.Length];
			using (var client = new HttpClient ()) {
				client.Timeout = TimeSpan.FromHours (3);
				for (int i = 0; i < SourceUris.Length; ++i) {
					tasks [i] = DownloadFile (client, source, SourceUris [i], DestinationFiles [i]);
				}
				TTask.WaitAll (tasks, source.Token);
			}

			DestinationFiles = tasks.Select (t => t.Result).ToArray ();

			return !Log.HasLoggedErrors;
		}

		async Task<ITaskItem> DownloadFile (HttpClient client, CancellationTokenSource source, string uri, ITaskItem destinationFile)
		{
			if (!string.IsNullOrEmpty (HashHeader)) {
				var hashSuffix = await CheckHashHeader (client, source, uri);
				if (!string.IsNullOrEmpty (hashSuffix)) {
					var directory = Path.GetDirectoryName (destinationFile.ItemSpec);
					var fileName = Path.GetFileNameWithoutExtension (destinationFile.ItemSpec);
					var extension = Path.GetExtension (destinationFile.ItemSpec);
					destinationFile.ItemSpec = Path.Combine (directory, fileName + "-" + hashSuffix + extension);
					Log.LogMessage (MessageImportance.Normal, $"Hash found using '{HashHeader}', destination file changing to '{destinationFile}'.");
				}
			}
			if (File.Exists (destinationFile.ItemSpec)) {
				Log.LogMessage (MessageImportance.Normal, $"Skipping uri '{uri}' as destination file already exists '{destinationFile}'.");
				return destinationFile;
			}
			var dp       = Path.GetDirectoryName (destinationFile.ItemSpec);
			var dn       = Path.GetFileName (destinationFile.ItemSpec);
			var tempPath = Path.Combine (dp, "." + dn + ".download");
			Directory.CreateDirectory(dp);

			Log.LogMessage (MessageImportance.Normal, $"Downloading `{uri}` to `{tempPath}`.");
			try {
				using (var r = await client.GetAsync (uri, HttpCompletionOption.ResponseHeadersRead, source.Token)) {
					r.EnsureSuccessStatusCode ();
					using (var s = await r.Content.ReadAsStreamAsync ())
					using (var o = File.OpenWrite (tempPath)) {
						await s.CopyToAsync (o, 4096, source.Token);
					}
				}
				Log.LogMessage (MessageImportance.Low, $"mv '{tempPath}' '{destinationFile}'.");
				File.Move (tempPath, destinationFile.ItemSpec);
			}
			catch (Exception e) {
				Log.LogError ("Unable to download URL `{0}` to `{1}`: {2}", uri, destinationFile, e.Message);
				Log.LogErrorFromException (e);
			}
			return destinationFile;
		}

		async Task<string> CheckHashHeader (HttpClient client, CancellationTokenSource source, string uri)
		{
			var request = new HttpRequestMessage (HttpMethod.Head, uri);
			using (var response = await client.SendAsync (request, source.Token)) {
				response.EnsureSuccessStatusCode ();
				if (response.Headers.TryGetValues (HashHeader, out var values)) {
					foreach (var value in values) {
						Log.LogMessage (MessageImportance.Low, $"{HashHeader}: {value}");

						//Current format: `x-goog-hash: crc32c=8HATIw==`
						if (!string.IsNullOrWhiteSpace (value)) {
							return value.Trim ();
						}
					}
				}
			}

			return null;
		}
	}
}
