using System.IO;

namespace Xamarin.Android.Prepare
{
	class GeneratedJavaInteropConfigPropsFile : GeneratedFile
	{
		const string Content = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- This is a GENERATED FILE -->
<!-- See https://github.com/xamarin/xamarin-android/tree/master/build-tools/xaprepare/xaprepare/Application/GeneratedJavaInteropConfigPropsFile.cs -->
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <JavaInteropDefineConstants>XA_JI_EXCLUDE</JavaInteropDefineConstants>
  </PropertyGroup>
</Project>
";

		public GeneratedJavaInteropConfigPropsFile (Context context)
			: base (Path.Combine (Configurables.Paths.ExternalJavaInteropDir, "bin", $"Build{context.Configuration}", "XAConfig.props"))
		{}

		public override void Generate (Context context)
		{
			using (var fs = File.Open (OutputPath, FileMode.Create)) {
				using (var sw = new StreamWriter (fs)) {
					sw.Write (Content);
				}
			}
		}
	}
}
