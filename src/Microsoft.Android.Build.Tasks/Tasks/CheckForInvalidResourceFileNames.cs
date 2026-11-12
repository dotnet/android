#nullable enable
using System;
using System.IO;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using Microsoft.Android.Build.Tasks;
using Xamarin.Android.Tasks;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks {
	public partial class CheckForInvalidResourceFileNames : AndroidTask {
		public override string TaskPrefix => "CFI";

		[Required]
		public ITaskItem[] Resources { get; set; } = [];

		[GeneratedRegex ("[^a-zA-Z0-9_.]+")]
		static partial Regex FileNameCheck { get; }

		[GeneratedRegex ("[^a-zA-Z0-9_.-]+")]
		static partial Regex FileNameWithHyphenCheck { get; }

		// Source https://docs.oracle.com/javase/tutorial/java/nutsandbolts/_keywords.html
		[GeneratedRegex (@"^\b(abstract|assert|boolean|break|byte|case|catch|char|class|const|continue|default|do|double|else|enum|extends|final|finally|float|for|fp|goto|if|implements|import|instanceof|int|interface|long|native|new|package|private|protected|public|record|return|short|static|strict|super|switch|synchronized|this|throw|throws|transient|try|void|volatile|while)\b$")]
		static partial Regex FileNameJavaReservedWordCheck { get; }

		public override bool RunTask ()
		{
			foreach (var resource in Resources) {
				var resourceFile = resource.GetMetadata ("LogicalName").Replace ('\\', Path.DirectorySeparatorChar);
				if (resourceFile.IsNullOrEmpty ())
					resourceFile = resource.ItemSpec;
				var fileName = Path.GetFileName (resourceFile);
				var directory = Path.GetFileName (Path.GetDirectoryName (resourceFile)) ?? "";
				char firstChar = fileName [0];
				bool isValidFirstChar = ('a' <= firstChar && firstChar <= 'z') || ('A' <= firstChar && firstChar <= 'Z') || firstChar == '_';
				if (!isValidFirstChar) {
					Log.LogCodedError ("APT0004", resource.ItemSpec, 0, Properties.Resources.APT0004);
				}
				if (directory.StartsWith ("values", StringComparison.OrdinalIgnoreCase)) {
					var match = FileNameWithHyphenCheck.Match (fileName);
					if (match.Success) {
						Log.LogCodedError ("APT0002", resource.ItemSpec, 0, Properties.Resources.APT0002, FileNameWithHyphenCheck);
					}
				} else {
					var match = FileNameCheck.Match (fileName);
					if (match.Success) {
						Log.LogCodedError ("APT0003", resource.ItemSpec, 0, Properties.Resources.APT0003, FileNameCheck);
					}
					match = FileNameJavaReservedWordCheck.Match (Path.GetFileNameWithoutExtension (fileName));
					if (match.Success) {
						Log.LogCodedError ("APT0005", resource.ItemSpec, 0, Properties.Resources.APT0005, FileNameCheck);
					}
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
