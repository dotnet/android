using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class GetConvertedJavaLibraries : AndroidTask
	{
		public override string TaskPrefix => "GCJ";

		[Required]
		public string Extension { get; set; } = string.Empty;
		public string? OutputJackDirectory { get; set; }
		public string []? JarsToConvert { get; set; }
		[Output]
		public string []? ConvertedFilesToBeGenerated { get; set; }

		public override bool RunTask ()
		{
			ConvertedFilesToBeGenerated =
				(JarsToConvert ?? Array.Empty<string> ()).Select (
					j => Path.Combine (OutputJackDirectory,
					                   Files.HashString (j) + Path.ChangeExtension (Path.GetFileName (j), Extension)))
				             .ToArray ();
			Log.LogDebugTaskItems ("  ConvertedFilesToBeGenerated:", ConvertedFilesToBeGenerated);
			return true;
		}
	}
}

