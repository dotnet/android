#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public sealed class EnsureAndroidSdkLicense : Task
	{
		[Required]
		public string LicenseDirectory { get; set; } = "";

		[Required]
		public ITaskItem [] LicenseHashes { get; set; } = [];

		public int LockTimeoutSeconds { get; set; } = 60;

		public bool ValidateOnly { get; set; }

		public override bool Execute ()
		{
			Directory.CreateDirectory (LicenseDirectory);

			var licenseFile = Path.Combine (LicenseDirectory, "android-sdk-license");
			var lockFile = Path.Combine (LicenseDirectory, ".android-sdk-license.lock");
			using (AcquireLock (lockFile)) {
				var licenses = new List<string> ();
				if (File.Exists (licenseFile)) {
					foreach (var line in File.ReadAllLines (licenseFile)) {
						var license = line.Trim ();
						if (license.Length == 0)
							continue;
						if (!IsSha1 (license)) {
							Log.LogError ($"Android SDK license file '{licenseFile}' contains invalid fingerprint '{license}'.");
							return false;
						}
						if (!licenses.Contains (license, StringComparer.OrdinalIgnoreCase))
							licenses.Add (license);
					}
				}

				foreach (var item in LicenseHashes) {
					var license = item.ItemSpec.Trim ();
					if (!IsSha1 (license)) {
						Log.LogError ($"Android SDK license fingerprint '{license}' is not a 40-character SHA-1 hash.");
						return false;
					}
					if (ValidateOnly && !licenses.Contains (license, StringComparer.OrdinalIgnoreCase)) {
						Log.LogError ($"Android SDK license file '{licenseFile}' does not contain expected fingerprint '{license}'.");
						return false;
					}
					if (!ValidateOnly && !licenses.Contains (license, StringComparer.OrdinalIgnoreCase))
						licenses.Add (license);
				}

				if (licenses.Count == 0) {
					Log.LogError ("At least one Android SDK license fingerprint is required.");
					return false;
				}

				if (!ValidateOnly)
					WriteAtomically (licenseFile, licenses);
			}

			return !Log.HasLoggedErrors;
		}

		FileStream AcquireLock (string lockFile)
		{
			var timer = Stopwatch.StartNew ();
			while (true) {
				try {
					return new FileStream (lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				} catch (IOException) when (timer.Elapsed < TimeSpan.FromSeconds (LockTimeoutSeconds)) {
					Thread.Sleep (100);
				}
			}
		}

		static bool IsSha1 (string value)
		{
			return value.Length == 40 && value.All (character =>
				(character >= '0' && character <= '9') ||
				(character >= 'a' && character <= 'f') ||
				(character >= 'A' && character <= 'F'));
		}

		static void WriteAtomically (string destination, IEnumerable<string> lines)
		{
			var temporaryFile = Path.Combine (Path.GetDirectoryName (destination) ?? "", $".{Path.GetFileName (destination)}.{Guid.NewGuid ():N}.tmp");
			try {
				File.WriteAllText (temporaryFile, string.Concat (lines.Select (line => line + "\n")), new UTF8Encoding (encoderShouldEmitUTF8Identifier: false));
				File.Move (temporaryFile, destination, overwrite: true);
			} finally {
				File.Delete (temporaryFile);
			}
		}
	}
}
