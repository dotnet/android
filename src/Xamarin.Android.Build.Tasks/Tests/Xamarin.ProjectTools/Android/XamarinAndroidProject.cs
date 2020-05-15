using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public abstract class XamarinAndroidProject : DotNetXamarinProject
	{
		protected XamarinAndroidProject (string debugConfigurationName = "Debug", string releaseConfigurationName = "Release")
			: base (debugConfigurationName, releaseConfigurationName)
		{
			Language = XamarinAndroidProjectLanguage.CSharp;

			if (!Builder.UseDotNet) {
				TargetFrameworkVersion = Versions.Android11;
				UseLatestPlatformSdk = true;
				AddReferences ("System.Core", "System.Xml", "Mono.Android");
				ProjectGuid = Guid.NewGuid ().ToString ();
				SetProperty ("ProjectTypeGuids", () => "{" + Language.ProjectTypeGuid + "};{" + ProjectTypeGuid + "}");
				SetProperty ("ProjectGuid", () => "{" + ProjectGuid + "}");
				SetProperty ("MonoAndroidAssetsPrefix", "Assets");
				SetProperty ("MonoAndroidResourcePrefix", "Resources");
				SetProperty (KnownProperties.AndroidUseLatestPlatformSdk, () => UseLatestPlatformSdk ? "True" : "False");
				SetProperty (KnownProperties.TargetFrameworkVersion, () => TargetFrameworkVersion);
			}

			SetProperty (KnownProperties.OutputType, "Library");
		}


		public XamarinAndroidProjectLanguage XamarinAndroidLanguage {
			get { return (XamarinAndroidProjectLanguage) Language; }
		}

		public bool UseLatestPlatformSdk { get; set; }

		public string TargetFrameworkVersion { get; set; }

		/// <summary>
		/// TargetFrameworkVersion=v8.1 -> 81
		/// </summary>
		public string TargetFrameworkAbbreviated => TargetFrameworkVersion?.TrimStart ('v').Replace (".", "");

		/// <summary>
		/// TargetFrameworkVersion=v8.1 -> MonoAndroid81
		/// </summary>
		public string TargetFrameworkMoniker => "MonoAndroid" + TargetFrameworkAbbreviated;
	}
}
