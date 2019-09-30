using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GetConvertedJavaLibraries : AndroidTask
	{
		public override string TaskPrefix => "GCJ";

		[Required]
		public string Extension { get; set; }
		public string OutputJackDirectory { get; set; }
		public string [] JarsToConvert { get; set; }
		[Output]
		public string [] ConvertedFilesToBeGenerated { get; set; }

		public override bool RunTask ()
		{
			ConvertedFilesToBeGenerated =
				(JarsToConvert ?? new string [0]).Select (
					j => Path.Combine (OutputJackDirectory,
					                   Files.HashString (j) + Path.ChangeExtension (Path.GetFileName (j), Extension)))
				             .ToArray ();
			Log.LogDebugTaskItems ("  ConvertedFilesToBeGenerated:", ConvertedFilesToBeGenerated);
			return true;
		}
	}
}

