using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class GetConvertedJavaLibraries : Task
	{
		[Required]
		public string Extension { get; set; }
		public string OutputJackDirectory { get; set; }
		public string [] JarsToConvert { get; set; }
		[Output]
		public string [] ConvertedFilesToBeGenerated { get; set; }

		public override bool Execute ()
		{
			Log.LogDebugMessage ("GetConvertedJavaLibraries Task");
			Log.LogDebugMessage ("  Extension: {0}", Extension);
			Log.LogDebugMessage ("  OutputJackDirectory: {0}", OutputJackDirectory);
			Log.LogDebugTaskItems ("  JarsToConvert:", JarsToConvert);

			var md5 = MD5.Create ();
			ConvertedFilesToBeGenerated =
				(JarsToConvert ?? new string [0]).Select (
					j => File.Exists (Path.Combine (Path.GetDirectoryName (j), "xamarin_cache.jack"))
						? Path.Combine (Path.GetDirectoryName (j), "xamarin_cache.jack")
						: Path.Combine (OutputJackDirectory,
							BitConverter.ToString (md5.ComputeHash (Encoding.UTF8.GetBytes (j))) + Path.ChangeExtension (Path.GetFileName (j), Extension)))
					.ToArray ();
			Log.LogDebugTaskItems ("  ConvertedFilesToBeGenerated:", ConvertedFilesToBeGenerated);
			return true;
		}
	}
}

