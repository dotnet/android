using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Xamarin.Android.Tools
{
	class AndroidSdkUnix : AndroidSdkBase
	{
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

		public override string PreferedAndroidSdkPath { 
			get {
				var config_file = GetUnixConfigFile ();
				var androidEl = config_file.Root.Element ("android-sdk");

				if (androidEl != null) {
					var path = (string)androidEl.Attribute ("path");

					if (ValidateAndroidSdkLocation (path))
						return path;
				}
				return null;
			}
		}

		public override string PreferedAndroidNdkPath { 
			get {
				var config_file = GetUnixConfigFile ();
				var androidEl = config_file.Root.Element ("android-ndk");

				if (androidEl != null) {
					var path = (string)androidEl.Attribute ("path");

					if (ValidateAndroidNdkLocation (path))
						return path;
				}
				return null;
			}
		}

		public override string PreferedJavaSdkPath { 
			get {
				var config_file = GetUnixConfigFile ();
				var javaEl = config_file.Root.Element ("java-sdk");

				if (javaEl != null) {
					var path = (string)javaEl.Attribute ("path");

					if (ValidateJavaSdkLocation (path))
						return path;
				}
				return null;
			}
		}

		protected override IEnumerable<string> GetAllAvailableAndroidSdks ()
		{
			var preferedSdkPath = PreferedAndroidSdkPath;
			if (!string.IsNullOrEmpty (preferedSdkPath))
				yield return preferedSdkPath;

			// Look in PATH
			foreach (var path in FindExecutableInPath (Adb)) {
				// Strip off "platform-tools"
				var dir = Path.GetDirectoryName (path);

				if (ValidateAndroidSdkLocation (dir))
					yield return dir;
			}

			// Check some hardcoded paths for good measure
			var macSdkPath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "Library", "Android", "sdk");
			if (ValidateAndroidSdkLocation (macSdkPath))
				yield return macSdkPath;
		}

		protected override string GetJavaSdkPath ()
		{
			var preferedJavaSdkPath = PreferedJavaSdkPath;
			if (!string.IsNullOrEmpty (preferedJavaSdkPath))
				return preferedJavaSdkPath;

			// Look in PATH
			foreach (var path in FindExecutableInPath (JarSigner)) {
				// Strip off "bin"
				var dir = Path.GetDirectoryName (path);

				if (ValidateJavaSdkLocation (dir))
					return dir;
			}

			return null;
		}

		public override bool ValidateJavaSdkLocation (string loc) 
		{
			var result = base.ValidateJavaSdkLocation (loc);

			if (result) {
				// handle apple's java stub
				const string javaHomeExe = "/usr/libexec/java_home";

				if (File.Exists (javaHomeExe)) {
					// returns true if there is a java installed
					var javaHomeTask = ProcessUtils.ExecuteToolAsync<bool> (javaHomeExe,
						(output) => {
							if (output.Contains ("(null)")) {
								return false;
							}

							return true;
						}, System.Threading.CancellationToken.None
					);

					if (!javaHomeTask.Result) {
						return false;
					}
				}
			}

			return result;
		}

		protected override IEnumerable<string> GetAllAvailableAndroidNdks ()
		{
			var preferedNdkPath = PreferedAndroidNdkPath;
			if (!string.IsNullOrEmpty (preferedNdkPath))
				yield return preferedNdkPath;

			// Look in PATH
			foreach (var path in FindExecutableInPath (NdkStack)) {
				if (ValidateAndroidNdkLocation (path))
					yield return path;
			}
		}

		protected override string GetShortFormPath (string path)
		{
			// This is a Windows-ism, don't do anything for Unix
			return path;
		}

		public override void SetPreferredAndroidSdkPath (string path)
		{
			path = NullIfEmpty (path);

			var doc = GetUnixConfigFile ();
			var androidEl = doc.Root.Element ("android-sdk");

			if (androidEl == null) {
				androidEl = new XElement ("android-sdk");
				doc.Root.Add (androidEl);
			}

			androidEl.SetAttributeValue ("path", path);
			doc.Save (UnixConfigPath);
		}

		public override void SetPreferredJavaSdkPath (string path)
		{
			path = NullIfEmpty (path);

			var doc = GetUnixConfigFile ();
			var javaEl = doc.Root.Element ("java-sdk");

			if (javaEl == null) {
				javaEl = new XElement ("java-sdk");
				doc.Root.Add (javaEl);
			}

			javaEl.SetAttributeValue ("path", path);
			doc.Save (UnixConfigPath);
		}

		public override void SetPreferredAndroidNdkPath (string path)
		{
			path = NullIfEmpty (path);

			var doc = GetUnixConfigFile ();
			var androidEl = doc.Root.Element ("android-ndk");

			if (androidEl == null) {
				androidEl = new XElement ("android-ndk");
				doc.Root.Add (androidEl);
			}

			androidEl.SetAttributeValue ("path", path);
			doc.Save (UnixConfigPath);
		}

		private static string UnixConfigPath {
			get {
				var p = Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);
				return Path.Combine (Path.Combine (p, "xbuild"), "monodroid-config.xml");
			}
		}

		private XDocument GetUnixConfigFile ()
		{
			var file = UnixConfigPath;
			XDocument doc = null;
			if (!File.Exists (file)) {
				string dir = Path.GetDirectoryName (file);
				if (!Directory.Exists (dir))
					Directory.CreateDirectory (dir);
			} else {
				try {
					doc = XDocument.Load (file);
				} catch (Exception ex) {
					Logger (TraceLevel.Error, "Could not load monodroid configuration file");
					Logger (TraceLevel.Verbose, ex.ToString ());

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
			return doc;
		}

	}
}
