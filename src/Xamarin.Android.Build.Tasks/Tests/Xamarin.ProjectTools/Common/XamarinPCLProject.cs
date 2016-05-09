using System;

namespace Xamarin.ProjectTools
{
	public class XamarinPCLProject : XamarinProject
	{
		public XamarinPCLProject ()
		{
			Language = XamarinAndroidProjectLanguage.CSharp;
			ProjectGuid = Guid.NewGuid ().ToString ();
			SetProperty ("ProjectTypeGuids", () => "{" + ProjectTypeGuid + "};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
			SetProperty ("ProjectGuid", () => "{" + ProjectGuid + "}");
			SetProperty ("TargetFrameworkProfile", "Profile78");
			SetProperty ("TargetFrameworkVersion", "v4.5");
			SetProperty ("OutputType", "Library");
		}

		public override string ProjectTypeGuid {
			get {
				return "786C830F-07A1-408B-BD7F-6EE04809D6DB";
			}
		}

		public string TargetFrameworkProfile {
			get { return GetProperty ("TargetFrameworkProfile"); }
			set { SetProperty ("TargetFrameworkProfile", value);  }
		}

		public string TargetFrameworkVersion {
			get { return GetProperty ("TargetFrameworkVersion"); }
			set { SetProperty ("TargetFrameworkVersion", value);  }
		}

		public override Microsoft.Build.Construction.ProjectRootElement Construct ()
		{
			var root = base.Construct ();
			root.AddImport (string.Format ("$(MSBuildExtensionsPath32)\\Microsoft\\Portable\\{0}\\Microsoft.Portable.CSharp.targets", TargetFrameworkVersion ));
			return root;
		}
	}
}

