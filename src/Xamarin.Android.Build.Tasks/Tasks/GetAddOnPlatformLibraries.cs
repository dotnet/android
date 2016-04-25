using System;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	public class GetAddOnPlatformLibraries : Task
	{
		[Required]
		public string AndroidSdkDir { get; set; }
		[Required]
		public string AndroidSdkPlatform { get; set; }
		[Required]
		public string Manifest { get; set; }

		[Output]
		public ITaskItem[] AddOnPlatformLibraries { get; set; }

		public override bool Execute ()
		{
			var doc = XDocument.Load (Manifest);
			var addons = new List<string> ();
			ManifestDocument.AddAddOns (doc.Element ("manifest").Element ("application"), AndroidSdkDir, AndroidSdkPlatform, addons);

			// If the manifest had any AddOn libraries, like maps, we'll need them later
			if (addons.Any ()) {
				AddOnPlatformLibraries = addons.Select (p => new TaskItem (p)).ToArray ();
				Log.LogDebugTaskItems ("  [OUTPUT] AddOnPlatformLibraries:", AddOnPlatformLibraries);
			}
			return true;
		}
	}
}

