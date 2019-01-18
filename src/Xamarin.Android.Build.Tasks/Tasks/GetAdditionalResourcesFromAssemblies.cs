using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{

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
		public bool DesignTimeBuild { get; set; }

		[Required]
		public string CacheFile { get; set; }

		string CachePath;
		MD5 md5 = MD5.Create ();

		public GetAdditionalResourcesFromAssemblies ()
		{
		}

		readonly Regex regex = new Regex(@"(\$(\w+))\b");

		readonly string[] extraPaths = new string[] {
			Path.Combine ("extras", "android"),
			Path.Combine ("extras", "google"),
		};

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

		internal static string ErrorMessage (CustomAttributeValue<object> attributeValue)
		{
			if (attributeValue.NamedArguments.Length == 0)
				return "";
			var arg = attributeValue.NamedArguments.FirstOrDefault (p => p.Name == "PackageName");
			if (arg.Name != null) {
				string packageName = arg.Value as string;
				if (packageName != null)
					return string.Format ("Please install package: '{0}' available in SDK installer", packageName);
			}
			arg = attributeValue.NamedArguments.FirstOrDefault (p => p.Name == "InstallInstructions");
			if (arg.Name != null) {
				string instInstructs = arg.Value as string;
				if (instInstructs != null)
					return "Installation instructions: " + instInstructs;
			}
			return null;
		}

		void AddAttributeValue (ICollection<string> items, CustomAttributeValue<object> attributeValue, string errorCode, string errorFmt,
			bool isDirectory, string fullPath, string attributeFullName)
		{
			if (attributeValue.NamedArguments.Length == 0 || attributeValue.FixedArguments.Length != 1) {
				LogCodedWarning (errorCode, "Attribute {0} doesn't have expected one constructor agrument", attributeFullName);
				return;
			}

			var arg = attributeValue.FixedArguments.First ();
			string path = arg.Value as string;
			if (string.IsNullOrEmpty (path)) {
				LogCodedWarning (errorCode, "Attribute {0} contructor argument is empty or not set to string", attributeFullName);
				return;
			}
			path = SubstituteEnvVariables (path).TrimEnd (Path.DirectorySeparatorChar);
			string baseDir = null;
			var sourceUrl = attributeValue.NamedArguments.FirstOrDefault (p => p.Name == "SourceUrl");
			var embeddedArchive = attributeValue.NamedArguments.FirstOrDefault (p => p.Name == "EmbeddedArchive");
			var version = attributeValue.NamedArguments.FirstOrDefault (p => p.Name == "Version");
			var sha1sum = attributeValue.NamedArguments.FirstOrDefault (p => p.Name == "Sha1sum");
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
				if (new Uri (sourceUrl.Value as string).IsFile)
					assemblyDir = Path.GetDirectoryName (fullPath);
				baseDir = MakeSureLibraryIsInPlace (assemblyDir, sourceUrl.Value as string,
					version.Value as string, embeddedArchive.Value as string, sha1sum.Value as string);
			}
			if (!string.IsNullOrEmpty (baseDir) && !Path.IsPathRooted (path))
				path = Path.Combine (baseDir, path);
			if ((isDirectory && Directory.Exists (path)) ||
				(!isDirectory && File.Exists (path))) {
				items.Add (Path.GetFullPath (path).TrimEnd (Path.DirectorySeparatorChar));
				return;
			}
			if (!DesignTimeBuild)
				LogCodedError (errorCode, errorFmt, ErrorMessage (attributeValue), path);
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
						var o = Math.Max(1, (zip.EntryCount / 10));
						Files.ExtractAll (zip, contentDir, (progress, total) => {
							if ((progress % o) != 0 || extracted == progress || progress == 0)
								return;
							LogMessage ("Extracted {0} of {1} files", progress, total);
							extracted = progress;
						});
					}
				}
				catch (Exception e) {
					LogCodedError ("XA5209", "Unzipping failed. Please download {0} and extract it to the {1} directory.", url, contentDir);
					LogCodedError ("XA5209", "Reason: {0}", e.Message);
					LogMessage (e.ToString (), MessageImportance.Low);
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

			var hashFile = file + ".sha1";
			if (File.Exists (hashFile) && string.Compare (File.ReadAllText (hashFile), sha1, StringComparison.InvariantCultureIgnoreCase) == 0)
				return true;
			
			var hash = Xamarin.Android.Tools.Files.HashFile (file).Replace ("-", String.Empty);
			LogDebugMessage ("File :{0}", file);
			LogDebugMessage ("SHA1 : {0}", hash);
			LogDebugMessage ("Expected SHA1 : {0}", sha1);

			var isValid = string.Compare (hash, sha1, StringComparison.InvariantCultureIgnoreCase) == 0;
			if (isValid)
				File.WriteAllText (hashFile, hash);

			return isValid;
		}

		void DoDownload (long totalBytes, long offset, Stream responseStream, Stream outputStream, Action<long, long, int> progressCallback = null)
		{
			long readSoFar = offset;
			byte [] buffer = new byte [8192];
			int bufferSize = buffer.Length;
			int nread = 0;
			float percent, tb = (float)totalBytes, lastPercent = 0;
			outputStream.Seek (offset, SeekOrigin.Begin);

			while ((nread = responseStream.Read (buffer, 0, buffer.Length)) > 0) {
				readSoFar += nread;
				outputStream.Write (buffer, 0, nread);
				percent = (float)readSoFar / tb * 100;
				if (percent - lastPercent > 1) {
					progressCallback?.Invoke (readSoFar, totalBytes, (int)percent);
					lastPercent = percent;
				}
				outputStream.Flush ();
			}
		}

		void Download (string file, Uri uri, Action<long, long, int> progressCallback = null)
		{
			var request = WebRequest.CreateHttp (uri);
			int offset = 0;
			if (File.Exists (file) && !MonoAndroidHelper.IsValidZip (file)) {
				var fi = new FileInfo (file);
				request.AddRange (fi.Length);
				offset = (int)fi.Length;
				LogMessage ("Partial download detected. Resuming from previous download progress.");
			}
			if (!File.Exists (file) || offset > 0) {
				HttpWebResponse response = null;
				try {
					response = (HttpWebResponse)request.GetResponse ();
				}
				catch (WebException ex) {
					var exceptionResponse = ex.Response as HttpWebResponse;
					if (exceptionResponse?.StatusCode != HttpStatusCode.RequestedRangeNotSatisfiable)
						throw;
					// Download the entire file again.
					request.Abort ();
					request = WebRequest.CreateHttp (uri);
					File.Delete (file);
					offset = 0;
					request.AddRange (0);
					response = (HttpWebResponse)request.GetResponse ();
					LogMessage ("Could not resume previous download. Starting again.");
				}
				if (response != null) {
					long totalBytes = response.ContentLength + offset;
					using (var responseStream = response.GetResponseStream ()) {
						using (var outputStream = new FileStream (file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)) {
							DoDownload (totalBytes, offset, responseStream, outputStream, progressCallback);
						}
					}
				}
			}
		}

		string MakeSureLibraryIsInPlace (string destinationBase, string url, string version, string embeddedArchive, string sha1)
		{
			if (string.IsNullOrEmpty (url))
				return null;

			LogDebugMessage ("Making sure we have {0} downloaded and extracted {1} from it...", url, embeddedArchive);

			string destinationDir = version == null ? destinationBase : Path.Combine (destinationBase, version);
			bool createDestinationDirectory = !Directory.Exists (destinationDir);
			if (createDestinationDirectory)
				Directory.CreateDirectory (destinationDir);

			var hash = string.Concat (md5.ComputeHash (Encoding.UTF8.GetBytes (url)).Select (b => b.ToString ("X02")));
			var uri = new Uri (url);

			var extraPath = extraPaths.FirstOrDefault (x => File.Exists (Path.Combine (AndroidSdkDirectory, x, embeddedArchive ?? String.Empty)));

			string zipDir =  !uri.IsFile ? Path.Combine (CachePath, "zips") : destinationDir;
			bool createZipDirectory = !Directory.Exists (zipDir);
			if (createZipDirectory)
				Directory.CreateDirectory (zipDir);

			string file = Path.Combine (zipDir, !uri.IsFile ? hash + ".zip" : Path.GetFileName (uri.AbsolutePath));
			if (string.IsNullOrEmpty (extraPath) && (!File.Exists (file) || !IsValidDownload (file, sha1) || !MonoAndroidHelper.IsValidZip (file))) {
				if (DesignTimeBuild) {
					LogDebugMessage ($"DesignTimeBuild={DesignTimeBuild}. Skipping download of {url}");
					return null;
				}

				int progress = -1;
				var downloadHandler = new Action<long, long, int>((r,t,p) => {
					if (p % 10 != 0 || progress == p)
						return;
					progress = p;
					LogMessage ("\t({0}/{1}b), total {2:F1}%", r,
						t, p);
				});
				LogMessage ("  Downloading {0} into {1}", url, zipDir);
				try {
					Download (file, uri, downloadHandler);
					if (MonoAndroidHelper.IsValidZip (file))
						LogMessage ("  Downloading Complete");
					else 
						LogCodedError ("XA5208", "Download succeeded but the zip file was not valid. Please do a clean build and try again.");
				} catch (Exception e) {
					LogCodedError ("XA5208", "Download failed. Please build again.");
					LogCodedError ("XA5208", "Reason: {0}", e.GetBaseException ().Message);
					LogMessage (e.ToString (), MessageImportance.Low);
				}
			}
			else {
				if (string.IsNullOrEmpty (extraPath))
					LogDebugMessage ("    reusing existing archive: {0}", file);
				else
					LogDebugMessage ("    found `{0}` in `{1}`", embeddedArchive, Path.Combine (AndroidSdkDirectory, extraPath));
			}

			string contentDir = string.IsNullOrEmpty (extraPath) ? Path.Combine (destinationDir, "content") : Path.Combine (AndroidSdkDirectory, extraPath);

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
						LogWarning ("Expected File {0} does not exist. Trying to extract again.", Path.Combine (contentDir, embeddedArchive));
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
			Yield ();
			try {
				DoExecute ();
			} finally {
				Reacquire ();
			}

			return !Log.HasLoggedErrors;
		}

		void DoExecute ()
		{
			if (Environment.GetEnvironmentVariable ("XA_DL_IGNORE_CERT_ERRROS") == "yesyesyes") {
				ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
				LogDebugMessage ("    Disabling download certificate validation callback.");
			}
			var androidResources   = new HashSet<string> ();
			var javaLibraries      = new HashSet<string> ();
			var nativeLibraries    = new HashSet<string> ();
			var assemblies         = new HashSet<string> ();

			if (Assemblies == null)
				return;

			var cacheFileFullPath = CacheFile;
			if (!Path.IsPathRooted (cacheFileFullPath))
				cacheFileFullPath = Path.Combine (WorkingDirectory, cacheFileFullPath);

			System.Threading.Tasks.Task.Run (() => {
				// The cache location can be overriden by the (to be documented) XAMARIN_CACHEPATH
				CachePath = Environment.ExpandEnvironmentVariables (CachePathEnvironmentVar);
				CachePath = CachePath != CachePathEnvironmentVar
				? CachePath
				: Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), CacheBaseDir);

				foreach (var assemblyItem in Assemblies) {
					string fullPath = Path.GetFullPath (assemblyItem.ItemSpec);
					if (DesignTimeBuild && !File.Exists (fullPath)) {
						LogWarning ("Failed to load '{0}'. Check the file exists or the project has been built.", fullPath);
						continue;
					}
					if (assemblies.Contains (fullPath)) {
						LogDebugMessage ("  Skip assembly: {0}, it was already processed", fullPath);
						continue;
					}
					// don't try to even load mscorlib it will fail.
					if (string.Compare (Path.GetFileNameWithoutExtension (fullPath), "mscorlib", StringComparison.OrdinalIgnoreCase) == 0)
						continue;
					assemblies.Add (fullPath);
					using (var pe = new PEReader (File.OpenRead (fullPath))) {
						var reader = pe.GetMetadataReader ();
						var assembly = reader.GetAssemblyDefinition ();
						// Append source file name (without the Xamarin. prefix or extension) to the base folder
						// This would help avoid potential collisions.
						foreach (var handle in assembly.GetCustomAttributes ()) {
							var attribute = reader.GetCustomAttribute (handle);
							var fullName = reader.GetCustomAttributeFullName (attribute);
							switch (fullName) {
								case "Android.IncludeAndroidResourcesFromAttribute":
									AddAttributeValue (androidResources, attribute.GetCustomAttributeArguments (), "XA5206", "{0}. Android resource directory {1} doesn't exist.", true, fullPath, fullName);
									break;
								case "Java.Interop.JavaLibraryReferenceAttribute":
									AddAttributeValue (javaLibraries, attribute.GetCustomAttributeArguments (), "XA5207", "{0}. Java library file {1} doesn't exist.", false, fullPath, fullName);
									break;
								case "Android.NativeLibraryReferenceAttribute":
									AddAttributeValue (nativeLibraries, attribute.GetCustomAttributeArguments (), "XA5210", "{0}. Native library file {1} doesn't exist.", false, fullPath, fullName);
									break;
							}
						}
					}
				}
			}, Token).ContinueWith (Complete);

			var result = base.Execute ();

			if (!result || Log.HasLoggedErrors) {
				if (File.Exists (cacheFileFullPath))
					File.Delete (cacheFileFullPath);
				return;
			}

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
			document.SaveIfChanged (cacheFileFullPath);

			LogDebugTaskItems ("  AdditionalAndroidResourcePaths: ", AdditionalAndroidResourcePaths);
			LogDebugTaskItems ("  AdditionalJavaLibraryReferences: ", AdditionalJavaLibraryReferences);
			LogDebugTaskItems ("  AdditionalNativeLibraryReferences: ", AdditionalNativeLibraryReferences);
		}
	}
}
