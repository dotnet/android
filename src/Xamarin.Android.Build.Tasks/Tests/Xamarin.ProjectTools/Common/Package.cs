using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	// used to generate packages.config
	// e.g. <package id="Xamarin.Android.Support.v13" version="20.0.0.4" targetFramework="MonoAndroid545" />
	public class Package
	{
		public Package ()
		{
			AutoAddReferences = true;
			References = new List<BuildItem.Reference> ();
		}

		public Package (Package other, bool audoAddReferences)
		{
			Id = other.Id;
			Version = other.Version;
			TargetFramework = other.TargetFramework;
			AutoAddReferences = audoAddReferences;
			References = new List<BuildItem.Reference> (other.References);
		}

		public bool AutoAddReferences { get; set; }
		public string Id { get; set; }
		public string Version { get; set; }
		public string TargetFramework { get; set; }
		public IList<BuildItem.Reference> References { get; private set; }
	}

}
