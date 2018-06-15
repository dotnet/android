using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Tasks {
	public class CheckForInvalidResourceFileNames : Task {
		[Required]
		public ITaskItem[] Resources { get; set; }

		Regex fileNameCheck = new Regex ("[^a-zA-Z0-9_.]+", RegexOptions.Compiled);
		Regex fileNameWithHyphenCheck = new Regex ("[^a-zA-Z0-9_.-]+", RegexOptions.Compiled);

		public override bool Execute ()
		{
			foreach (var resource in Resources) {
				var fileName = Path.GetFileName (resource.ItemSpec);
				var directory = Path.GetFileName (Path.GetDirectoryName (resource.ItemSpec));
				if (directory.StartsWith ("values", StringComparison.OrdinalIgnoreCase)) {
					var match = fileNameWithHyphenCheck.Match (fileName);
					if (match.Success) {
						Log.LogCodedError ("APT0000", resource.ItemSpec, 0, $"Invalid file name: It must contain only {fileNameWithHyphenCheck}.");
					}
				} else {
					var match = fileNameCheck.Match (fileName);
					if (match.Success) {
						Log.LogCodedError ("APT0000", resource.ItemSpec, 0, $"Invalid file name: It must contain only {fileNameCheck}.");
					}
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
