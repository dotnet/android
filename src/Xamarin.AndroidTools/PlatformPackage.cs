//
// PlatformPackage.cs
//
// Author:
//       Jonathan Pryor <jonp@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xamarin.AndroidTools.PublicationUtilities;

namespace Xamarin.AndroidTools {

	public static class PlatformPackage {

		[Obsolete ("Use GetPlatformPackageVersion(int, ref string)")]
		public static int GetPlatformPackageVersion (int apiLevel)
		{
			string packageName = null;
			return GetPlatformPackageVersion (apiLevel, ref packageName);
		}

		public static int GetPlatformPackageVersion (int apiLevel, ref string packageName)
		{
			// If this throws an ArgumentNullException from Path.Combine(), it's because MonoDroidSdk.RuntimePath is null
			// To fix, either:
			//  1. Provide a `runtimePath` value to `MonoDroidSdk.Refresh()` before calling this method, or
			//  2. Export the `$MONO_ANDROID_PATH` environment variable to an appropriate `runtimePath` value.
			string manifest = Path.Combine (MonoDroidSdk.RuntimePath, "platforms", "android-" + apiLevel, "Mono.Android.Platform.xml");
			if (File.Exists (manifest))
				return MonoDroidSdkBase.GetManifestVersion (manifest);

			string frameworkVersion = MonoDroidSdk.GetFrameworkVersionForApiLevel (apiLevel.ToString ());

			return GetVersionInfo (frameworkVersion);
		}

		internal static Version ToVersion (string frameworkDir)
		{
			string version = Path.GetFileName (frameworkDir);
			if (!version.StartsWith ("v", StringComparison.OrdinalIgnoreCase)) {
				// wat?
				return new Version ();
			}
			version = version.Substring (1);
			Version v;
			if (Version.TryParse (version, out v))
				return v;
			return new Version ();
		}

		static int GetVersionInfo (string frameworkVersion)
		{
			string bclDir           = MonoDroidSdk.FrameworkPath;
			string frameworksDir    = Path.GetDirectoryName (bclDir);

			string platform     = Path.Combine (frameworksDir, frameworkVersion, "Mono.Android.dll");
			var platformTime    = File.GetLastWriteTimeUtc (platform);
			var unixEpoch       = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return (int) (platformTime - unixEpoch).TotalSeconds;
		}

		[Obsolete ("Use GetPlatformPackagePathAsync")]
		public static string GetPlatformPackagePath (int apiLevel, string aaptPath, IProgressNotifier progressReporter, CancellationToken token)
		{
			var task = GetPlatformPackagePathAsync (apiLevel, aaptPath, progressReporter, token);
			task.Wait (token);
			return task.Result;
		}

		public static async Task<string> GetPlatformPackagePathAsync (int apiLevel, string aaptPath, IProgressNotifier progressReporter, CancellationToken token)
		{
			string path = Path.Combine (MonoDroidSdk.RuntimePath, "platforms", "android-" + apiLevel, "Mono.Android.Platform.apk");
			if (File.Exists (path))
				return path;

			string cacheDir = OS.GetXamarinAndroidCacheDir ();
			string manifest = Path.Combine (cacheDir, string.Format ("Mono.Android.Platform.ApiLevel_{0}.xml", apiLevel));
			string apkName  = string.Format ("Mono.Android.Platform.ApiLevel_{0}.apk", apiLevel);

			path = Path.Combine (cacheDir, apkName);

			string frameworkVersion = MonoDroidSdk.GetFrameworkVersionForApiLevel (apiLevel.ToString ());

			int     version = GetVersionInfo (frameworkVersion);

			if (File.Exists (manifest) && File.Exists (path)) {
				int curVersion = MonoDroidSdkBase.GetManifestVersion (manifest);
				if (version == curVersion)
					return path;
			}

			aaptPath  = aaptPath ?? AndroidSdk.GetAaptPath ();

			if (aaptPath == null)
				throw new ArgumentNullException ("aaptPath", "'aaptPath' is null and no pre-built Platform.apk exists!");
			if (!File.Exists (aaptPath))
				throw new ArgumentException ("Could not find `aapt` and no pre-built Platform.apk exists.", "aaptPath");

			ReportBeginStep (progressReporter, "Creating " + path);

			string packageDir   = Path.Combine (Path.GetTempPath (), Path.GetRandomFileName ());
			string resourceDir  = Path.Combine (packageDir, "r");
			string tmanifest    = Path.Combine (packageDir, "AndroidManifest.xml");
			string tpath        = Path.Combine (packageDir, apkName);

			Directory.CreateDirectory (packageDir);
			try {
				CopyAssemblies (frameworkVersion, Path.Combine (resourceDir, "assemblies"), progressReporter, token);
				CreateAndroidManifest (apiLevel, version, frameworkVersion, tmanifest, progressReporter, token);
				string unaligned = Aapt (aaptPath, tmanifest, resourceDir, packageDir, progressReporter, token);
				string unsigned = Path.Combine (packageDir, "unsigned.apk");
				Zipalign (unaligned, unsigned, progressReporter, token);
				await ApkSigner (unsigned, tpath, progressReporter, token);

				Directory.CreateDirectory (cacheDir);
				File.Copy (tpath,     path,     overwrite: true);
				File.Copy (tmanifest, manifest, overwrite: true);
			} finally {
				ReportMessage (progressReporter, "Removing temporary directory: {0}", packageDir);
				Directory.Delete (packageDir, recursive:true);
				ReportEndStep (progressReporter, "Creating " + path);
			}
			return path;
		}

		static void CopyAssemblies (string frameworkVersion, string resourceDir, IProgressNotifier progressReporter, CancellationToken token)
		{
			ReportBeginStep (progressReporter, "Copying platform assemblies...");
			Directory.CreateDirectory (resourceDir);
			string bclDir           = MonoDroidSdk.FrameworkPath;
			string frameworksDir    = Path.GetDirectoryName (bclDir);
			string lastDir = null;
			foreach (var frameworkDir in Directory.EnumerateDirectories (frameworksDir).OrderBy (ToVersion)) {
				lastDir = frameworkDir;
				string version = Path.GetFileName (frameworkDir);
				if (version == Path.GetFileName (MonoDroidSdk.FrameworkPath)) {
					// BCL assemblies aren't part of the Platform Package.
					continue;
				}
				foreach (var pattern in new string [] { "*.dll*", "*.pdb" }) {
					foreach (var assembly in Directory.EnumerateFiles (frameworkDir, pattern)) {
						token.ThrowIfCancellationRequested ();
						string file = Path.GetFileName (assembly);
						if (file.StartsWith ("Mono.Android", StringComparison.OrdinalIgnoreCase) &&
								!file.StartsWith ("Mono.Android.Export", StringComparison.OrdinalIgnoreCase))
							continue;
						ReportMessage (progressReporter, "Copying file: {0}", assembly);
						File.Copy (assembly, Path.Combine (resourceDir, Path.GetFileName (assembly)), overwrite:true);
					}
				}
				if (version == frameworkVersion)
					break;
			}
			foreach (var lib in Directory.EnumerateFiles (lastDir, "Mono.Android.*")) {
				if (Path.GetExtension (lib) == ".xml")
					continue;
				token.ThrowIfCancellationRequested ();
				ReportMessage (progressReporter, "Copying file: {0}", lib);
				File.Copy (lib, Path.Combine (resourceDir, Path.GetFileName (lib)), overwrite:true);
			}
			ReportEndStep (progressReporter, "Copying platform assemblies...");
		}

		static void CreateAndroidManifest (int apiLevel, int version, string frameworkVersion, string androidManifest, IProgressNotifier progressReporter, CancellationToken token)
		{
			var nsAndroid = XNamespace.Get ("http://schemas.android.com/apk/res/android");
			var doc = new XDocument (
					new XDeclaration ("1.0", "UTF-8", null),
					new XElement ("manifest",
						new XAttribute (XNamespace.Xmlns + "android", nsAndroid),
						new XAttribute ("package", "Mono.Android.Platform.ApiLevel_" + apiLevel),
						new XAttribute (nsAndroid + "installLocation", "auto"),
						new XAttribute (nsAndroid + "versionCode", version),
						new XAttribute (nsAndroid + "versionName", frameworkVersion),
						new XElement ("uses-sdk",
						    new XAttribute (nsAndroid + "minSdkVersion", 4),
						    new XAttribute (nsAndroid + "targetSdkVersion", apiLevel)),
						new XElement ("application",
							new XAttribute (nsAndroid + "label", string.Format ("Xamarin.Android API-{0} Support", apiLevel)),
							new XAttribute (nsAndroid + "hasCode", "false"))));
			ReportMessage (progressReporter, "Creating: {0}", androidManifest);
			var utf8 = new UTF8Encoding (encoderShouldEmitUTF8Identifier:false);
			using (var o = new StreamWriter (androidManifest, append:false, encoding:utf8)) {
				o.NewLine = "\n";
				doc.Save (o);
			}
			token.ThrowIfCancellationRequested ();
		}

		static string Aapt (string aapt, string androidManifest, string resourceDir, string outDir, IProgressNotifier progressReporter, CancellationToken token)
		{
			// /opt/android/sdk/build-tools/18.0.0/aapt package -f -0 .dll -0 .mdb -M AndroidManifest.xml -I /opt/android/sdk/platforms/android-8/android.jar -F unsigned.apk -k r
			string apk = Path.Combine (outDir, "unaligned.apk");
			var arguments = string.Format ("package -f -0 .dll -0 .mdb -M \"{0}\" -I \"{1}\" -F \"{2}\" -k \"{3}\"",
					androidManifest, Path.Combine (AndroidSdk.GetLatestPlatformDirectory (), "android.jar"), apk, resourceDir);
			var psi = new ProcessStartInfo (aapt, arguments);
			ReportMessage (progressReporter, "Creating: {0}", apk);
			Exec ("Aapt", psi, progressReporter, token);
			return apk;
		}

		static void Exec (string step, ProcessStartInfo psi, IProgressNotifier progressReporter, CancellationToken token)
		{
			ReportMessage (progressReporter, "Executing: {0} {1}", psi.FileName, psi.Arguments);

			TextWriter  stdout        = Console.Out;
			TextWriter  stderr        = Console.Error;
			bool        disposeStdout = false;
			if (progressReporter != null) {
				stdout        = new ProgressTextWriter (progressReporter, step);
				stderr        = stdout;
				disposeStdout = true;
			}

			stderr = new TeeTextWriter (stderr);

			try {
				int r = ProcessUtils.StartProcess (psi, stdout, stderr, token).Result;
				ReportMessage (progressReporter, "{0} exited with value: {1}", psi.FileName, r);

				if (r != 0)
					throw new InvalidOperationException (string.Format ("'{0}' exited with code '{1}': {2}", psi.FileName, r, stderr.ToString ()));
			} finally {
				if (disposeStdout)
					stdout.Dispose ();
			}
		}

		static Task ApkSigner (string unsigned, string signed, IProgressNotifier progressReporter, CancellationToken token)
		{
			// See monodroid/tools/msbuild/Tasks/GetAppSettingsDirectory.cs!Execute()
			var keystore = Path.Combine (
					Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData),
					"Xamarin",
					"Mono for Android",
					"debug.keystore");
			ReportMessage (progressReporter, "Creating: {0}", signed);
			var options = new AndroidSigningOptions {
				KeyStore = keystore,
				KeyAlias = "androiddebugkey",
				KeyPass = "android",
				StorePass = "android",
			};
			return PackageSigningTasks.SignPackageWithApkSignerAsync (options, unsigned, signed, token, AndroidSdk.ApkSignerJar,
				result => ReportMessage (progressReporter, result));
		}

		static void Zipalign (string unaligned, string packageFile, IProgressNotifier progressReporter, CancellationToken token)
		{
			// /opt/android/sdk/tools/zipalign 4 unaligned.apk Xamarin.Android.Platform.apk
			var arguments = string.Format ("4 \"{0}\" \"{1}\"",
					unaligned, packageFile);
			var psi = new ProcessStartInfo (AndroidSdk.ZipAlignExe, arguments);
			ReportMessage (progressReporter, "Creating: {0}", packageFile);
			Exec ("zipalign", psi, progressReporter, token);
		}

		internal static void ReportBeginStep (IProgressNotifier progressReporter, string step)
		{
			if (progressReporter != null) {
				progressReporter.BeginStep (step);
				progressReporter.ReportMessage (step);
			}
		}

		internal static void ReportEndStep (IProgressNotifier progressReporter, string step)
		{
			if (progressReporter != null)
				progressReporter.EndStep (step);
		}

		internal static void ReportMessage (IProgressNotifier progressReporter, string format, params object[] args)
		{
			if (progressReporter != null) {
				progressReporter.ReportMessage (string.Format (format, args));
			}
		}
	}

	class ProgressTextWriter : TextWriter {

		public ProgressTextWriter (IProgressNotifier progressRepoter, string step)
		{
			ProgressReporter    = progressRepoter;
			Step                = step;

			ProgressReporter.BeginStep (step);
		}

		public  IProgressNotifier   ProgressReporter    { get; private set; }
		public  string              Step                { get; private set; }

		public override Encoding Encoding {
			get { return Encoding.Default; }
		}

		StringBuilder   message     = new StringBuilder ();

		public override void Write (char value)
		{
			if (value == '\r' || value == '\n') {
				if (message.Length > 0)
					ProgressReporter.ReportMessage (message.ToString ());
				message.Clear ();
				return;
			}
			message.Append (value);
		}

		public override void Write (string value)
		{
			ProgressReporter.ReportMessage (value);
		}

		protected override void Dispose (bool disposing)
		{
			if (Step == null)
				return;
			ProgressReporter.EndStep (Step);
			Step = null;
			base.Dispose (disposing);
		}
	}

	class TeeTextWriter : StringWriter {

		public TeeTextWriter (TextWriter output)
		{
			Output  = output;
		}

		public  TextWriter	Output { get; private set; }

		public override void Write (char value)
		{
			base.Write (value);
			Output.Write (value);
		}

		public override void Write (string value)
		{
			base.Write (value);
			Output.Write (value);
		}

		public override void Write (char[] buffer, int index, int count)
		{
			base.Write (buffer, index, count);
			Output.Write (buffer, index, count);
		}
	}
}
