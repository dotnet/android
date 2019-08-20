using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	class GeneratedMonoAndroidProjitemsFile : GeneratedFile
	{
		const string FileTop = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- This is a GENERATED FILE -->
<!-- See build-tools/xaprepare/xaprepare/Application/GeneratedMonoAndroidProjitemsFile.cs -->
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
";

		const string OutputFileName = "Mono.Android.Apis.projitems";

		const string FileBottom = @"</Project>";

		public GeneratedMonoAndroidProjitemsFile ()
			: base (Path.Combine (Configurables.Paths.BuildBinDir, OutputFileName))
		{}

		public override void Generate (Context context)
		{
			using (var fs = File.Open (OutputPath, FileMode.Create)) {
				using (var sw = new StreamWriter (fs)) {
					GenerateFile (sw);
				}
			}
		}

		void GenerateFile (StreamWriter sw)
		{
			sw.Write (FileTop);

			sw.WriteLine ("  <ItemGroup>");
			BuildAndroidPlatforms.AllPlatforms.ForEach (androidPlatform => WriteGroupApiInfo (sw, androidPlatform));
			sw.WriteLine ("  </ItemGroup>");

			sw.Write (FileBottom);
		}

		void WriteGroupApiInfo (StreamWriter sw, AndroidPlatform androidPlatform)
		{
			if (string.IsNullOrWhiteSpace (androidPlatform.ApiName)) {
				return;
			}

			sw.WriteLine ($"    <AndroidApiInfo Include=\"{androidPlatform.Include}\">");
			sw.WriteLine ($"      <Name>{androidPlatform.ApiName}</Name>");
			sw.WriteLine ($"      <Level>{androidPlatform.ApiLevel}</Level>");
			sw.WriteLine ($"      <Id>{androidPlatform.PlatformID}</Id>");
			sw.WriteLine ($"      <Stable>{androidPlatform.Stable}</Stable>");
			sw.WriteLine ($"    </AndroidApiInfo>");
		}
	}
}