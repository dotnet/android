using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Xamarin.Tools.Zip;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class CreateNativeLibraryArchive : Task
	{
		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public ITaskItem[] EmbeddedNativeLibraries { get; set; }
		
		public CreateNativeLibraryArchive ()
		{
		}

		public override bool Execute ()
		{
			Log.LogDebugMessage ("EmbeddedNativeLibraries Task");
			Log.LogDebugMessage ("  OutputDirectory: {0}", OutputDirectory);
			Log.LogDebugTaskItems ("  EmbeddedNativeLibraries:", EmbeddedNativeLibraries);

			var outDirInfo = new DirectoryInfo (OutputDirectory);
			
			// Copy files into _NativeLibraryImportsDirectoryName (native_library_imports) dir.
			if (!outDirInfo.Exists)
				outDirInfo.Create ();
			foreach (var lib in EmbeddedNativeLibraries) {
				// seealso bug #3477 to find out why we use this method.
				var abi = MonoAndroidHelper.GetNativeLibraryAbi (lib);
				if (abi == null) {
					Log.LogWarning (
							subcategory:      string.Empty,
							warningCode:      "XA4300",
							helpKeyword:      string.Empty,
							file:             lib.ItemSpec,
							lineNumber:       0,
							columnNumber:     0,
							endLineNumber:    0,
							endColumnNumber:  0,
							message:          "Native library '{0}' will not be bundled because it has an unsupported ABI.",
							messageArgs:      new []{
								lib.ItemSpec,
							}
					);
					continue;
				}
				if (!outDirInfo.GetDirectories (abi).Any ())
					outDirInfo.CreateSubdirectory (abi);
				MonoAndroidHelper.CopyIfChanged (lib.ItemSpec, Path.Combine (OutputDirectory, abi, Path.GetFileName (lib.ItemSpec)));
			}

			// Archive native libraries in a zip.
			using (var stream = new MemoryStream ()) {
				using (var zip = ZipArchive.Create (stream)) {
					zip.AddDirectory (OutputDirectory, outDirInfo.Name);
				}
				stream.Position = 0;
				string outpath = Path.Combine (outDirInfo.Parent.FullName, "__AndroidNativeLibraries__.zip");
				if (Files.ArchiveZip (outpath, f => {
					using (var fs = new FileStream (f, FileMode.CreateNew)) {
						stream.CopyTo (fs);
					}
				}))
					Log.LogDebugMessage ("Saving contents to " + outpath);
			}
			
			return true;
		}
	}
}

