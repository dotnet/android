using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Net;
using System.ComponentModel;
using Xamarin.Android.Tools;

using Java.Interop.Tools.Cecil;

namespace Xamarin.Android.Tasks {

	public class GetAdditionalResourcesFromAssemblies : AsyncTask
	{
		/// <summary>
		/// Environment variable named XAMARIN_CACHEPATH that can be set 
		/// to override the default cache path.
		/// </summary>
		public const string CachePathEnvironmentVar = "%XAMARIN_CACHEPATH%";

		/// <summary>
		/// Base directory under the user's local app data folder that is used as the 
		/// default cache location.
		/// </summary>
		const string CacheBaseDir = "Xamarin";

		/// <summary>
		/// Assemblies whose resources are unzipped to a cache path rather than in-place.
		/// </summary>
		const string AssemblyNamePrefix = "Xamarin.";
		
		[Required]
		public string AndroidSdkDirectory { get; set; }

		[Required]
		public string AndroidNdkDirectory { get; set; }

		[Required]
		public ITaskItem[] Assemblies { get; set; }

		[Required]
		public string CacheFile { get; set;} 

		string CachePath;
		MD5 md5 = MD5.Create ();

		public GetAdditionalResourcesFromAssemblies ()
		{
		}

		readonly Regex regex = new Regex(@"(\$(\w+))\b");

		string SubstituteEnvVariables (string path)
		{
			MatchEvaluator replaceEnvVar = (Match m) => {
				var e = m.Groups [2].Value;
				switch (e) {
				case "ANDROID_SDK_PATH": return AndroidSdkDirectory.TrimEnd (Path.DirectorySeparatorChar);
				case "ANDROID_NDK_PATH": return AndroidNdkDirectory.TrimEnd (Path.DirectorySeparatorChar);
				default:
					var v = Environment.GetEnvironmentVariable (e);
					if (!string.IsNullOrEmpty (v))
						return v;
					return m.Groups [1].Value;
				}
			};

			return regex.Replace (path, replaceEnvVar);
		}

		internal static string ErrorMessage (CustomAttribute attr)
		{
			if (!attr.HasProperties)
				return "";
			CustomAttributeNamedArgument arg = attr.Properties.FirstOrDefault (p => p.Name == "PackageName");
			if (arg.Name != null) {
				string packageName = arg.Argument.Value as string;
				if (packageName != null)
					return string.Format ("Please install package: '{0}' available in SDK installer", packageName);
			}
			arg = attr.Properties.FirstOrDefault (p => p.Name == "InstallInstructions");
			if (arg.Name != null) {
				string instInstructs = arg.Argument.Value as string;
				if (instInstructs != null)
					return "Installation instructions: " + instInstructs;
			}
			return null;
		}

		void AddAttributeValue (ICollection<string> items, CustomAttribute attr, string errorCode, string errorFmt,
			bool isDirectory, string fullPath)
		{
			if (!attr.HasConstructorArguments || attr.ConstructorArguments.Count != 1) {
				LogWarning ("Attribute {0} doesn't have expected one constructor agrument", attr.AttributeType.FullName);
				return;
			}

			CustomAttributeArgument arg = attr.ConstructorArguments.First ();
			string path = arg.Value as string;
			if (string.IsNullOrEmpty (path)) {
				LogWarning ("Attribute {0} contructor argument is empty or not set to string", attr.AttributeType.FullName);
				return;
			}
			path = SubstituteEnvVariables (path).TrimEnd (Path.DirectorySeparatorChar);
			string baseDir = null;
			CustomAttributeNamedArgument sourceUrl = attr.Properties.FirstOrDefault (p => p.Name == "SourceUrl");
			CustomAttributeNamedArgument embeddedArchive = attr.Properties.FirstOrDefault (p => p.Name == "EmbeddedArchive");
			CustomAttributeNamedArgument version = attr.Properties.FirstOrDefault (p => p.Name == "Version");
			CustomAttributeNamedArgument sha1sum = attr.Properties.FirstOrDefault (p => p.Name == "Sha1sum");
			var isXamarinAssembly = Path.GetFileName (fullPath).StartsWith (AssemblyNamePrefix, StringComparison.OrdinalIgnoreCase);
			var assemblyDir = Path.Combine (CachePath, Path.GetFileNameWithoutExtension (fullPath));
			// upgrade the paths to not strip off the Xamarin. prefix as it might cause assembly 
			// collisions now that we cache everything here.
			var oldAssemblyDir = Path.Combine (CachePath, Path.GetFileNameWithoutExtension (fullPath).Substring (isXamarinAssembly ? AssemblyNamePrefix.Length : 0));
			if (string.Compare (assemblyDir, oldAssemblyDir, StringComparison.OrdinalIgnoreCase) != 0 && Directory.Exists (oldAssemblyDir)) {
				Directory.CreateDirectory (assemblyDir);
				foreach (var oldDir in Directory.GetDirectories (oldAssemblyDir, "*", SearchOption.AllDirectories)) {
					var newDir = oldDir.Replace (oldAssemblyDir, assemblyDir);
					Directory.CreateDirectory (newDir);
				}
				foreach (string oldFile in Directory.GetFiles(oldAssemblyDir, "*.*", 
					SearchOption.AllDirectories)) {
					var newFile = oldFile.Replace (oldAssemblyDir, assemblyDir);
					Directory.CreateDirectory (Path.GetDirectoryName (newFile));
					File.Copy (oldFile, newFile, true);
				}
				Directory.Delete (oldAssemblyDir, recursive: true);
			}
			if (sourceUrl.Name != null) {
				if (new Uri (sourceUrl.Argument.Value as string).IsFile)
					assemblyDir = Path.GetDirectoryName (fullPath);
				baseDir = MakeSureLibraryIsInPlace (assemblyDir, sourceUrl.Argument.Value as string,
					version.Argument.Value as string, embeddedArchive.Argument.Value as string, sha1sum.Argument.Value as string);
			}
			if (!string.IsNullOrEmpty (baseDir) && !Path.IsPathRooted (path))
				path = Path.Combine (baseDir, path);
			if ((isDirectory && Directory.Exists (path)) ||
				(!isDirectory && File.Exists (path))) {
				items.Add (Path.GetFullPath (path).TrimEnd (Path.DirectorySeparatorChar));
				return;
			}
			LogCodedError (errorCode, errorFmt, ErrorMessage (attr), path);
		}

		bool ExtractArchive (string url, string file, string contentDir)
		{
			if (!File.Exists (file)) {
				// this file is supposed to exist! why doesn't it.
				return false;
			}
			if (!Directory.Exists (contentDir)) {
				try {
					Directory.CreateDirectory (contentDir);
					LogMessage ("Extracting {0} to {1}", file, contentDir);
					using (var zip = MonoAndroidHelper.ReadZipFile (file)) {
						int extracted = 0;
						var o = Math.Max(1, (zip.Entries.Count / 10));
						zip.ExtractProgress += (object sender, Ionic.Zip.ExtractProgressEventArgs e) => {
							if ((e.EntriesExtracted % o) != 0 || extracted == e.EntriesExtracted || e.EntriesExtracted == 0)
								return;
							LogMessage ("Extracted {0} of {1} files", e.EntriesExtracted, e.EntriesTotal);
							extracted = e.EntriesExtracted;
						};
						Files.ExtractAll (zip, contentDir);
					}
				}
				catch (Exception e) {
					LogCodedError ("XA5209", "Unzipping failed. Please download {0} and extract it to the {1} directory.", url, contentDir);
					LogCodedError ("XA5209", "Reason: {0}", e.Message);
					Log.LogMessage (MessageImportance.Low, e.ToString ());
					Directory.Delete (contentDir, true);
					return false;
				}
			}
			return true;
		}

		bool IsValidDownload(string file, string sha1)
		{
			if (string.IsNullOrEmpty (sha1))
				return true;
			var hash = Xamarin.Android.Tools.Files.HashFile (file).Replace ("-", String.Empty);
			Log.LogDebugMessage ("File :{0}", file);
			Log.LogDebugMessage ("SHA1 : {0}", hash);
			Log.LogDebugMessage ("Expected SHA1 : {0}", sha1);
			return string.Compare (hash, sha1, StringComparison.InvariantCultureIgnoreCase) == 0;
		}

		string MakeSureLibraryIsInPlace (string destinationBase, string url, string version, string embeddedArchive, string sha1)
		{
			if (string.IsNullOrEmpty (url))
				return null;

			Log.LogDebugMessage ("Making sure we have {0} downloaded and extracted {1} from it...", url, embeddedArchive);

			string destinationDir = version == null ? destinationBase : Path.Combine (destinationBase, version);
			bool createDestinationDirectory = !Directory.Exists (destinationDir);
			if (createDestinationDirectory)
				Directory.CreateDirectory (destinationDir);

			var hash = string.Concat (md5.ComputeHash (Encoding.UTF8.GetBytes (url)).Select (b => b.ToString ("X02")));
			var uri = new Uri (url);

			string zipDir =  !uri.IsFile ? Path.Combine (CachePath, "zips") : destinationDir;
			bool createZipDirectory = !Directory.Exists (zipDir);
			if (createZipDirectory)
				Directory.CreateDirectory (zipDir);

			string file = Path.Combine (zipDir, !uri.IsFile ? hash + ".zip" : Path.GetFileName (uri.AbsolutePath));
			if (!File.Exists (file) || !IsValidDownload (file, sha1)) {
				int progress = -1;
				DownloadProgressChangedEventHandler downloadHandler = (o, e) => {
					if (e.ProgressPercentage % 10 != 0 || progress == e.ProgressPercentage)
						return;
					progress = e.ProgressPercentage;
					LogMessage ("\t({0}/{1}b), total {2:F1}%", e.BytesReceived,
						e.TotalBytesToReceive, e.ProgressPercentage);
				};
				using (var client = new System.Net.WebClient ()) {
					client.DownloadProgressChanged += downloadHandler;
					LogMessage ("  Downloading {0} into {1}", url, zipDir);
					try {
						client.DownloadFileTaskAsync (url, file).Wait (Token);
						LogMessage ("  Downloading Complete");
					} catch (Exception e) {
						LogCodedError ("XA5208", "Download failed. Please download {0} and put it to the {1} directory.", url, destinationDir);
						LogCodedError ("XA5208", "Reason: {0}", e.GetBaseException ().Message);
						Log.LogMessage (MessageImportance.Low, e.ToString ());
						if (File.Exists (file))
							File.Delete (file);
					}
					client.DownloadProgressChanged -= downloadHandler;
				}
			}
			else
				LogDebugMessage ("    reusing existing archive: {0}", file);
			string contentDir = Path.Combine (destinationDir, "content");

			int attempt = 0;
			while (attempt < 3 && !Log.HasLoggedErrors) {
				var success = ExtractArchive (url, file, contentDir);
				if (!success && Log.HasLoggedErrors) {
					break;
				}

				if (!string.IsNullOrEmpty (embeddedArchive)) {
					string embeddedDir = Path.Combine (destinationDir, "embedded");
					success = ExtractArchive (string.Format ("{0}:{1}", url, embeddedArchive), Path.Combine (contentDir, embeddedArchive), embeddedDir);
					if (success) {
						contentDir = embeddedDir;
						break;
					}
					if (Log.HasLoggedErrors)
						break;
					if (!success) {
						Log.LogWarning ("Expected File {0} does not exist. Trying to extract again.", Path.Combine (contentDir, embeddedArchive));
						if (Directory.Exists (contentDir))
							Directory.Delete (contentDir, recursive: true);
					}
				} else
					break;
				attempt++;
			}

			if (string.IsNullOrEmpty (contentDir) || !Directory.Exists (contentDir)) {
				if (createZipDirectory)
					Directory.Delete (zipDir);
				if (createDestinationDirectory)
					Directory.Delete (destinationDir);
			}

			return contentDir;
		}

		public override bool Execute ()
		{
			LogDebugMessage ("GetAdditionalResourcesFromAssemblies Task");
			LogDebugMessage ("  AndroidSdkDirectory: {0}", AndroidSdkDirectory);
			LogDebugMessage ("  AndroidNdkDirectory: {0}", AndroidNdkDirectory);
			LogDebugTaskItems ("  Assemblies: ", Assemblies);

			if (Environment.GetEnvironmentVariable ("XA_DL_IGNORE_CERT_ERRROS") == "yesyesyes") {
				ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
				LogDebugMessage ("    Disabling download certificate validation callback.");
			}
			var androidResources   = new HashSet<string> ();
			var javaLibraries      = new HashSet<string> ();
			var nativeLibraries    = new HashSet<string> ();
			var assemblies         = new HashSet<string> ();

			if (Assemblies == null)
				return true;

			System.Threading.Tasks.Task.Run (() => {
				// The cache location can be overriden by the (to be documented) XAMARIN_CACHEPATH
				CachePath = Environment.ExpandEnvironmentVariables (CachePathEnvironmentVar);
				CachePath = CachePath != CachePathEnvironmentVar
				? CachePath
				: Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), CacheBaseDir);

				var resolver = new DirectoryAssemblyResolver (Log.LogWarning, loadDebugSymbols: false);
				foreach (var assItem in Assemblies) {
					string fullPath = Path.GetFullPath (assItem.ItemSpec);
					if (assemblies.Contains (fullPath)) {
						LogDebugMessage ("  Skip assembly: {0}, it was already processed", fullPath);
						continue;
					}
					assemblies.Add (fullPath);
					resolver.Load (fullPath);
					// Append source file name (without the Xamarin. prefix or extension) to the base folder
					// This would help avoid potential collisions.
					foreach (var ca in resolver.GetAssembly (assItem.ItemSpec).CustomAttributes) {
						switch (ca.AttributeType.FullName) {
						case "Android.IncludeAndroidResourcesFromAttribute":
							AddAttributeValue (androidResources, ca, "XA5206", "{0}. Android resource directory {1} doesn't exist.", true, fullPath);
							break;
						case "Java.Interop.JavaLibraryReferenceAttribute":
							AddAttributeValue (javaLibraries, ca, "XA5207", "{0}. Java library file {1} doesn't exist.", false, fullPath);
							break;
						case "Android.NativeLibraryReferenceAttribute":
							AddAttributeValue (nativeLibraries, ca, "XA5210", "{0}. Native library file {1} doesn't exist.", false, fullPath);
							break;
						}
					}
				}
			}).ContinueWith ((t) => {
				if (t.Exception != null)
					Log.LogErrorFromException (t.Exception.GetBaseException ());
				Complete ();
			});

			var result = base.Execute ();

			var AdditionalAndroidResourcePaths = androidResources.ToArray ();
			var AdditionalJavaLibraryReferences = javaLibraries.ToArray ();
			var AdditionalNativeLibraryReferences = nativeLibraries
				.Where (x => MonoAndroidHelper.GetNativeLibraryAbi (x) != null)
				.ToArray ();

			var document = new XDocument (
				new XDeclaration ("1.0", "UTF-8", null),
				new XElement ("Paths",
					new XElement ("AdditionalAndroidResourcePaths",
							AdditionalAndroidResourcePaths.Select(e => new XElement ("AdditionalAndroidResourcePath", e))),
					new XElement ("AdditionalJavaLibraryReferences",
							AdditionalJavaLibraryReferences.Select(e => new XElement ("AdditionalJavaLibraryReference", e))),
					new XElement ("AdditionalNativeLibraryReferences", 
							AdditionalNativeLibraryReferences.Select(e => new XElement ("AdditionalNativeLibraryReference", e)))
					));
			document.Save (CacheFile);

			LogDebugTaskItems ("  AdditionalAndroidResourcePaths: ", AdditionalAndroidResourcePaths);
			LogDebugTaskItems ("  AdditionalJavaLibraryReferences: ", AdditionalJavaLibraryReferences);
			LogDebugTaskItems ("  AdditionalNativeLibraryReferences: ", AdditionalNativeLibraryReferences);

			return result && !Log.HasLoggedErrors;
		}
	}
}
