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
			SetProperty (KnownProperties.OutputType, "Library");
		}


		public XamarinAndroidProjectLanguage XamarinAndroidLanguage {
			get { return (XamarinAndroidProjectLanguage) Language; }
		}
	}
}
