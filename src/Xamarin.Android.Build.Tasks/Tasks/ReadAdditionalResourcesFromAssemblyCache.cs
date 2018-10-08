using System;
using System.IO;
using System.Linq;
using System.Xml.XPath;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class ReadAdditionalResourcesFromAssemblyCache : Task {

		[Required]
		public string CacheFile { get; set;} 

		[Output]
		public string[] AdditionalAndroidResourcePaths { get; set; }

		[Output]
		public string[] AdditionalJavaLibraryReferences { get; set; }

		[Output]
		public string[] AdditionalNativeLibraryReferences { get; set; }

		[Output]
		public  bool IsResourceCacheValid { get; set; }

		public ReadAdditionalResourcesFromAssemblyCache ()
		{
			AdditionalAndroidResourcePaths = new string [0];
			AdditionalJavaLibraryReferences = new string [0];
			AdditionalNativeLibraryReferences = new string [0];
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("Task ReadAdditionalResourcesFromAssemblyCache");
			Log.LogDebugMessage ("  CacheFile: {0}", CacheFile);
			if (!File.Exists (CacheFile)) {
				Log.LogDebugMessage ("{0} does not exist. No Additional Resources found", CacheFile);
				return !Log.HasLoggedErrors;
			}
			var doc = XDocument.Load (CacheFile);
			AdditionalAndroidResourcePaths = doc.GetPaths ("AdditionalAndroidResourcePaths",
				"AdditionalAndroidResourcePath");
			AdditionalJavaLibraryReferences = doc.GetPaths ("AdditionalJavaLibraryReferences",
				"AdditionalJavaLibraryReference");
			AdditionalNativeLibraryReferences = doc.GetPaths ("AdditionalNativeLibraryReferences",
				"AdditionalNativeLibraryReference");

			Log.LogDebugTaskItems ("  AdditionalAndroidResourcePaths: ", AdditionalAndroidResourcePaths);
			Log.LogDebugTaskItems ("  AdditionalJavaLibraryReferences: ", AdditionalJavaLibraryReferences);
			Log.LogDebugTaskItems ("  AdditionalNativeLibraryReferences: ", AdditionalNativeLibraryReferences);

			IsResourceCacheValid = AdditionalAndroidResourcePaths.All (x => Directory.Exists (x)) &&
				AdditionalJavaLibraryReferences.All (x => File.Exists (x)) &&
				AdditionalNativeLibraryReferences.All (x => File.Exists (x));

			Log.LogDebugMessage ("  IsValid: {0}", IsResourceCacheValid);

			return !Log.HasLoggedErrors;
		}
	}
}

