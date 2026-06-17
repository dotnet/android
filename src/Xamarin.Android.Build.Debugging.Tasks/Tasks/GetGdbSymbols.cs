using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xamarin.Tools.Zip;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.AndroidTools;
using Microsoft.Android.Build.Tasks;


namespace Xamarin.Android.Tasks
{
	public class GetGdbSymbols : AndroidTask
	{
		public override string TaskPrefix => "GGDB";

		public string AdbTarget { get; set; }

		[Required]
		public string GdbSymbolsPath { get; set;}

		[Required]
		public string Package { get; set; }

		[Required]
		public string OutputPath { get; set; }

		[Required]
		public string PrimaryCpuAbi {get; set; }

		public GetGdbSymbols ()
		{
			AdbTarget = null;
		}

		public override bool RunTask ()
		{
			var device = AndroidHelper.ParseTarget (AdbTarget, Log, logErrors: true, engine4: BuildEngine4);
			if (device == null) {
				return false;
			}
			device.EnsureProperties (CancellationToken.None).Wait ();
			var arch = PrimaryCpuAbi;
			Log.LogDebugMessage ("Device Abi {0}", arch);

			var apk = string.Format ("{0}{1}-Signed.apk", OutputPath, Package);
			ExtractFilesFromPath (apk, string.Format ("lib/{0}/", arch));

			return !Log.HasLoggedErrors;
		}

		void ExtractFilesFromPath(string apk, string path)
		{
			using (var zip = ZipArchive.Open (apk, FileMode.Open)) {
				foreach (var e in zip.Where (x => x.FullName.StartsWith (path, StringComparison.OrdinalIgnoreCase))) {
					Log.LogDebugMessage ("Extracting {0} from {1}", e.FullName, apk);
					using (var fs = new FileStream (Path.Combine (GdbSymbolsPath, Path.GetFileName (e.FullName)), FileMode.OpenOrCreate)) {
						e.Extract (fs);
					}
				}
			}
		}
	}
}

