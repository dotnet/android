// Copyright (C) 2018 Microsoft, Inc. All rights reserved.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Monodroid;

namespace Xamarin.Android.Tasks {
	public class ConvertCustomView : Task {

		[Required]
		public string CustomViewMapFile { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

		public string ResourceNameCaseMap { get; set; }

		public ITaskItem [] ResourceDirectories { get; set; }

		public override bool Execute ()
		{
			var resource_name_case_map = MonoAndroidHelper.LoadResourceCaseMap (ResourceNameCaseMap);
			var acw_map = MonoAndroidHelper.LoadAcwMapFile (AcwMapFile);
			var customViewMap = MonoAndroidHelper.LoadCustomViewMapFile (BuildEngine4, CustomViewMapFile);
			var processed = new HashSet<string> ();

			foreach (var kvp in acw_map) {
				var key = kvp.Key;
				var value = kvp.Value;
				if (key == value)
					continue;
				if (customViewMap.TryGetValue (key, out HashSet<string> resourceFiles)) {
					foreach (var file in resourceFiles) {
						if (processed.Contains (file))
							continue;
						if (!File.Exists (file))
							continue;
						var document = XDocument.Load (file, options : LoadOptions.SetLineInfo);
						var e = document.Root;
						bool update = false;
						foreach (var elem in AndroidResource.GetElements (e).Prepend (e)) {
							update |= TryFixCustomView (elem, acw_map, (level, message) => {
								ITaskItem resdir = ResourceDirectories?.FirstOrDefault (x => file.StartsWith (x.ItemSpec)) ?? null;
								switch (level) {
								case TraceLevel.Error:
									Log.FixupResourceFilenameAndLogCodedError ("XA1002", message, file, resdir.ItemSpec, resource_name_case_map);
									break;
								case TraceLevel.Warning:
									Log.FixupResourceFilenameAndLogCodedError ("XA1001", message, file, resdir.ItemSpec, resource_name_case_map);
									break;
								default:
									Log.LogDebugMessage (message);
									break;
								}
							});
						}
						foreach (XAttribute a in AndroidResource.GetAttributes (e)) {
							update |= TryFixCustomClassAttribute (a, acw_map);
							update |= TryFixFragment (a, acw_map);
						}
						if (update) {
							var lastModified = File.GetLastWriteTimeUtc (file);
							if (document.SaveIfChanged (file)) {
								Log.LogDebugMessage ($"Fixed up Custom Views in {file}");
								MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (file, lastModified, Log);
							}
						}
						processed.Add (file);
					}
				}
			}

			return !Log.HasLoggedErrors;
		}

		static readonly XNamespace res_auto = "http://schemas.android.com/apk/res-auto";
		static readonly XNamespace android = "http://schemas.android.com/apk/res/android";

		bool TryFixCustomClassAttribute (XAttribute attr, Dictionary<string, string> acwMap)
		{
			/* Some attributes reference a Java class name.
			 * try to convert those like for TryFixCustomView
			 */
			if (attr.Name != (res_auto + "layout_behavior") &&                            // For custom CoordinatorLayout behavior
			    		(attr.Parent.Name != "transition" || attr.Name.LocalName != "class")) // For custom transitions
				return false;

			if (!acwMap.TryGetValue (attr.Value, out string mappedValue))
				return false;

			attr.Value = mappedValue;
			return true;
		}

		bool TryFixFragment (XAttribute attr, Dictionary<string, string> acwMap)
		{
			// Looks for any: 
			//   <fragment class="My.DotNet.Class" 
			//   <fragment android:name="My.DotNet.Class" ...
			// and tries to change it to the ACW name
			if (attr.Parent.Name != "fragment")
				return false;

			if (attr.Name == "class" || attr.Name == android + "name") {
				if (acwMap.TryGetValue (attr.Value, out string mappedValue)) {
					attr.Value = mappedValue;

					return true;
				} else if (attr.Value?.Contains (',') ?? false) {
					// attr.Value could be an assembly-qualified name that isn't in acw-map.txt;
					// see e5b1c92c, https://github.com/xamarin/xamarin-android/issues/1296#issuecomment-365091948
					var n = attr.Value.Substring (0, attr.Value.IndexOf (','));
					if (acwMap.TryGetValue (n, out mappedValue)) {
						attr.Value = mappedValue;
						return true;
					}
				}
			}

			return false;
		}

		bool TryFixCustomView (XElement elem, Dictionary<string, string> acwMap, Action<TraceLevel, string> logMessage = null)
		{
			// Looks for any <My.DotNet.Class ...
			// and tries to change it to the ACW name
			string name = elem.Name.ToString ();
			if (acwMap.TryGetValue (name, out string mappedValue)) {
				elem.Name = mappedValue;
				return true;
			}
			if (logMessage == null)
				return false;
			var matchingKey = acwMap.FirstOrDefault (x => String.Equals (x.Key, name, StringComparison.OrdinalIgnoreCase));
			if (matchingKey.Key != null) {
				// we have elements with slightly different casing.
				// lets issue a error.
				logMessage (TraceLevel.Error, $"We found a matching key '{matchingKey.Key}' for '{name}'. But the casing was incorrect. Please correct the casing");
			}
			return false;
		}
	}
}