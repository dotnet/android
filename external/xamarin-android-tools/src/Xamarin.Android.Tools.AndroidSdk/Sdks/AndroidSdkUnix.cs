using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using Xamarin.Android.Tools.AndroidSdk.Properties;

namespace Xamarin.Android.Tools
{
	class AndroidSdkUnix : AndroidSdkBase
	{
		// See comments above UnixConfigPath for explanation on why these are needed
		static readonly string? sudo_user;
		static readonly string? user;
		static readonly bool need_chown;

		static AndroidSdkUnix ()
		{
			sudo_user = Environment.GetEnvironmentVariable ("SUDO_USER");
			if (String.IsNullOrEmpty (sudo_user))
				return;

			user = Environment.GetEnvironmentVariable ("USER");
			if (String.IsNullOrEmpty (user) || String.Compare (user, sudo_user, StringComparison.Ordinal) == 0)
				return;
			need_chown = true;
		}

		public AndroidSdkUnix (Action<TraceLevel, string> logger)
			: base (logger)
		{
		}

		public override string NdkHostPlatform32Bit {
			get { return OS.IsMac ? "darwin-x86" : "linux-x86"; }
		}
		public override string NdkHostPlatform64Bit {
			get { return OS.IsMac ? "darwin-x86_64" : "linux-x86_64"; }
		}

		public override string? PreferedAndroidSdkPath {
			get {
				var config_file = GetUnixConfigFile (Logger);
				var androidEl = config_file.Element ("android-sdk");

				if (androidEl != null) {
					var path = (string?)androidEl.Attribute ("path");

					if (ValidateAndroidSdkLocation ("preferred path", path))
						return path;
				}
				return null;
			}
		}

		public override string? PreferedAndroidNdkPath {
			get {
				var config_file = GetUnixConfigFile (Logger);
				var androidEl = config_file.Element ("android-ndk");

				if (androidEl != null) {
					var path = (string?)androidEl.Attribute ("path");

					if (ValidateAndroidNdkLocation ("preferred path", path))
						return path;
				}
				return null;
			}
		}

		public override string? PreferedJavaSdkPath {
			get {
				var config_file = GetUnixConfigFile (Logger);
				var javaEl = config_file.Element ("java-sdk");

				if (javaEl != null) {
					var path = (string?)javaEl.Attribute ("path");

					if (ValidateJavaSdkLocation ("preferred path", path))
						return path;
				}
				return null;
			}
		}

		protected override IEnumerable<string> GetAllAvailableAndroidSdks ()
		{
			var preferedSdkPath = PreferedAndroidSdkPath;
			if (!string.IsNullOrEmpty (preferedSdkPath))
				yield return preferedSdkPath!;

			foreach (string dir in GetSdkFromEnvironmentVariables ()) {
				yield return dir;
			}

			// Look in PATH
			foreach (var adb in ProcessUtils.FindExecutablesInPath (Adb)) {
				var path = Path.GetDirectoryName (adb);
				// Strip off "platform-tools"
				var dir = Path.GetDirectoryName (path);

				if (dir == null)
					continue;

				yield return dir;
			}

			// Check some hardcoded paths for good measure
			var macSdkPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "Library", "Android", "sdk");
			yield return macSdkPath;
		}

		protected override string GetShortFormPath (string path)
		{
			// This is a Windows-ism, don't do anything for Unix
			return path;
		}

		public override void SetPreferredAndroidSdkPath (string? path)
		{
			path = NullIfEmpty (path);

			var doc = GetUnixConfigFile (Logger);

			var androidEl = doc.Element ("android-sdk");

			if (androidEl == null) {
				androidEl = new XElement ("android-sdk");
				doc.Add (androidEl);
			}

			androidEl.SetAttributeValue ("path", path);
			SaveConfig (doc);
		}

		public override void SetPreferredJavaSdkPath (string? path)
		{
			path = NullIfEmpty (path);

			var doc = GetUnixConfigFile (Logger);

			var javaEl = doc.Element ("java-sdk");

			if (javaEl == null) {
				javaEl = new XElement ("java-sdk");
				doc.Add (javaEl);
			}

			javaEl.SetAttributeValue ("path", path);
			SaveConfig (doc);
		}

		public override void SetPreferredAndroidNdkPath (string? path)
		{
			path = NullIfEmpty (path);

			var doc = GetUnixConfigFile (Logger);

			var androidEl = doc.Element ("android-ndk");

			if (androidEl == null) {
				androidEl = new XElement ("android-ndk");
				doc.Add (androidEl);
			}

			androidEl.SetAttributeValue ("path", path);
			SaveConfig (doc);
		}

		void SaveConfig (XElement doc)
		{
			string cfg = UnixConfigPath;
			List <string>? created = null;

			if (!File.Exists (cfg)) {
				string? dir = Path.GetDirectoryName (cfg);
				if (dir != null && !Directory.Exists (dir)) {
					Directory.CreateDirectory (dir);
					AddToList (dir);
				}
				AddToList (cfg);
			}
			doc.Save (cfg);
			FixOwnership (created);

			void AddToList (string path)
			{
				if (created == null)
					created = new List <string> ();
				created.Add (path);
			}
		}

		static  readonly    string  GetUnixConfigDirOverrideName            = $"UnixConfigPath directory override! {typeof (AndroidSdkInfo).AssemblyQualifiedName}";

		// There's a small problem with the code below. Namely, if it runs under `sudo` the folder location
		// returned by Environment.GetFolderPath will depend on how sudo was invoked:
		//
		//   1. `sudo command` will not reset the environment and while the user running the command will be
		//      `root` (or any other user specified in the command), the `$HOME` environment variable will point
		//      to the original user's home. The effect will be that any files/directories created in this
		//      session will be owned by `root` (or any other user as above) and not the original user. Thus, on
		//      return, the original user will not have write (or read/write) access to the directory/file
		//      created. This causes https://devdiv.visualstudio.com/DevDiv/_workitems/edit/597752
		//
		//   2. `sudo -i command` starts an "interactive" session which resets the environment (by reading shell
		//      startup scripts among other steps) and the above problem doesn't occur.
		//
		// The behavior of 1. is, arguably, a bug in Mono fixing of which may bring unknown side effects,
		// however. Therefore we'll do our best below to work around the issue. `sudo` puts the original user's
		// login name in the `SUDO_USER` environment variable and we can use its presence to both detect 1.
		// above and work around the issue. We will do it in the simplest possible manner, by invoking chown(1)
		// to set the proper ownership.
		// Note that it will NOT fix situations when a mechanism other than `sudo`, but with similar effects, is
		// used! The generic fix would require a number of more complicated checks as well as a number of
		// p/invokes (with quite a bit of data marshaling) and it is likely that it would be mostly wasted
		// effort, as the sudo situation appears to be the most common (while happening few and far between in
		// general)
		//
		private static string UnixConfigPath {
			get {
				var p   = AppDomain.CurrentDomain.GetData (GetUnixConfigDirOverrideName)?.ToString ();
				if (string.IsNullOrEmpty (p)) {
					p   = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".config", "xbuild");
				}
				return Path.Combine (p, "monodroid-config.xml");
			}
		}

		internal static XElement GetUnixConfigFile (Action<TraceLevel, string> logger)
		{
			var file = UnixConfigPath;
			XDocument? doc = null;
			if (File.Exists (file)) {
				try {
					doc = XDocument.Load (file);
				} catch (Exception ex) {
					logger (TraceLevel.Error, string.Format (Resources.InvalidMonodroidConfigFile_path_message, file, ex.Message));
					logger (TraceLevel.Verbose, ex.ToString ());

					// move out of the way and create a new one
					doc = new XDocument (new XElement ("monodroid"));
					var newFileName = file + ".old";
					if (File.Exists (newFileName)) {
						File.Delete (newFileName);
					}

					File.Move (file, newFileName);
				}
			}

			if (doc == null || doc.Root == null) {
				doc = new XDocument (new XElement ("monodroid"));
			}
			return doc.Root!;
		}

		void FixOwnership (List<string>? paths)
		{
			if (!need_chown || paths == null || paths.Count == 0)
				return;

			var stdout = new StringWriter ();
			var stderr = new StringWriter ();
			var args = new List <string> {
				QuoteString (sudo_user!)
			};

			foreach (string p in paths)
				args.Add (QuoteString (p));

			var psi = new ProcessStartInfo (OS.IsMac ? "/usr/sbin/chown" : "/bin/chown") {
				CreateNoWindow = true,
				Arguments = String.Join (" ", args),
			};
			Logger (TraceLevel.Verbose, $"Changing filesystem object ownership: {psi.FileName} {psi.Arguments}");
			Task<int> chown_task = ProcessUtils.StartProcess (psi, stdout, stderr, System.Threading.CancellationToken.None);

			if (chown_task.Result != 0) {
				Logger (TraceLevel.Warning, $"Failed to change ownership of filesystem object(s)");
				Logger (TraceLevel.Verbose, $"standard output: {stdout}");
				Logger (TraceLevel.Verbose, $"standard error: {stderr}");
			}

			string QuoteString (string p)
			{
				return $"\"{p}\"";
			}
		}

	}
}
