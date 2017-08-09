using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public abstract class XamarinAndroidProject : XamarinProject
	{
		protected XamarinAndroidProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			Language = XamarinAndroidProjectLanguage.CSharp;
			TargetFrameworkVersion = Versions.Marshmallow;
			UseLatestPlatformSdk = true;
			AddReferences ("System.Core", "System.Xml", "Mono.Android");
			ProjectGuid = Guid.NewGuid ().ToString ();

			SetProperty ("ProjectTypeGuids", () => "{" + Language.ProjectTypeGuid + "};{" + ProjectTypeGuid + "}");
			SetProperty ("ProjectGuid", () => "{" + ProjectGuid + "}");

			SetProperty ("OutputType", "Library");
			SetProperty ("MonoAndroidAssetsPrefix", "Assets");
			SetProperty ("MonoAndroidResourcePrefix", "Resources");

			SetProperty (KnownProperties.AndroidUseLatestPlatformSdk, () => UseLatestPlatformSdk ? "True" : "False");
			SetProperty (KnownProperties.TargetFrameworkVersion, () => TargetFrameworkVersion);

			SetProperty (ReleaseProperties, KnownProperties.AndroidUseSharedRuntime, "False");
		}


		public XamarinAndroidProjectLanguage XamarinAndroidLanguage {
			get { return (XamarinAndroidProjectLanguage) Language; }
		}

		public bool UseLatestPlatformSdk { get; set; }

		public string TargetFrameworkVersion { get; set; }
	}
}
