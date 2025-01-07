using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public abstract class XamarinAndroidProjectLanguage : ProjectLanguage
	{
		public static XamarinAndroidProjectLanguage CSharp = new CSharpLanguage ();
		public static XamarinAndroidProjectLanguage FSharp = new FSharpLanguage ();


		static readonly string default_assembly_info_cs, default_assembly_info_fs;

		static XamarinAndroidProjectLanguage ()
		{
			default_assembly_info_cs = string.Empty;
			using (var sr = new StreamReader (typeof (XamarinAndroidProjectLanguage).Assembly.GetManifestResourceStream ("Xamarin.ProjectTools.Resources.Base.AssemblyInfo.fs")))
				default_assembly_info_fs = sr.ReadToEnd ();
		}

		class FSharpLanguage : XamarinAndroidProjectLanguage
		{
			public override string ProjectTypeGuid {
				get { return "EFBA0AD7-5A72-4C68-AF49-83D382785DCF"; }
			}
			public override string DefaultExtension {
				get { return ".fs"; }
			}
			public override string DefaultDesignerExtension {
				get { return ".fs"; }
			}
			public override string DefaultProjectExtension {
				get { return ".fsproj"; }
			}
			public override string DefaultAssemblyInfo {
				get { return default_assembly_info_fs; }
			}
			public override string ToString ()
			{
				return "FSharp";
			}
		}

		class CSharpLanguage : XamarinAndroidProjectLanguage
		{
			public override string ProjectTypeGuid {
				get { return "EFBA0AD7-5A72-4C68-AF49-83D382785DCF"; }
			}
			public override string DefaultExtension {
				get { return ".cs"; }
			}
			public override string DefaultDesignerExtension {
				get { return ".cs"; }
			}
			public override string DefaultProjectExtension {
				get { return ".csproj"; }
			}
			public override string DefaultAssemblyInfo {
				get { return default_assembly_info_cs; }
			}
			public override string ToString ()
			{
				return "CSharp";
			}
		}
	}

}
