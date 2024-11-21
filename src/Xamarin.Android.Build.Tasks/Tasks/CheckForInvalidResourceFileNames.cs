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
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks {
	public class CheckForInvalidResourceFileNames : AndroidTask {
		public override string TaskPrefix => "CFI";

		[Required]
		public ITaskItem[] Resources { get; set; }

		Regex fileNameCheck = new Regex ("[^a-zA-Z0-9_.]+", RegexOptions.Compiled);
		Regex fileNameWithHyphenCheck = new Regex ("[^a-zA-Z0-9_.-]+", RegexOptions.Compiled);

		Regex fileNameJavaReservedWordCheck = new Regex("^\\b(abstract|continue|for|new|switch|assert|default|goto|package|synchronized|boolean|do|if|private|this|break|double|implements|protected|throw|byte|else|import|public|throws|case|enum|instanceof|return|transient|catch|extends|int|short|try|char|final|interface|static|void|class|finally|long|strict|fp|volatile|const|float|native|super|while)\\b$", RegexOptions.Compiled);

		public override bool RunTask ()
		{
			foreach (var resource in Resources) {
				var resourceFile = resource.GetMetadata ("LogicalName").Replace ('\\', Path.DirectorySeparatorChar);
				if (string.IsNullOrEmpty (resourceFile))
					resourceFile = resource.ItemSpec;
				var fileName = Path.GetFileName (resourceFile);
				var directory = Path.GetFileName (Path.GetDirectoryName (resourceFile));
				char firstChar = fileName [0];
				bool isValidFirstChar = ('a' <= firstChar && firstChar <= 'z') || ('A' <= firstChar && firstChar <= 'Z') || firstChar == '_';
				if (!isValidFirstChar) {
					Log.LogCodedError ("APT0004", resource.ItemSpec, 0, Properties.Resources.APT0004);
				}
				if (directory.StartsWith ("values", StringComparison.OrdinalIgnoreCase)) {
					var match = fileNameWithHyphenCheck.Match (fileName);
					if (match.Success) {
						Log.LogCodedError ("APT0002", resource.ItemSpec, 0, Properties.Resources.APT0002, fileNameWithHyphenCheck);
					}
				} else {
					var match = fileNameCheck.Match (fileName);
					if (match.Success) {
						Log.LogCodedError ("APT0003", resource.ItemSpec, 0, Properties.Resources.APT0003, fileNameCheck);
					}
					match = fileNameJavaReservedWordCheck.Match (Path.GetFileNameWithoutExtension (fileName));
					if (match.Success) {
						Log.LogCodedError ("APT0005", resource.ItemSpec, 0, Properties.Resources.APT0005, fileNameCheck);
					}
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
