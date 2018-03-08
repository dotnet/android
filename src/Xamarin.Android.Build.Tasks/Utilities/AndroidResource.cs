using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Monodroid {
	static class AndroidResource {
		
		public static void UpdateXmlResource (string filename, Dictionary<string, string> acwMap, IEnumerable<string> additionalDirectories = null)
		{
			// use a temporary file so we only update the real file if things actually changed
			string tmpfile = filename + ".bk";
			try {
				XDocument doc = XDocument.Load (filename, LoadOptions.SetLineInfo);

				// The assumption here is that the file we're fixing up is in a directory below the
				// obj/${Configuration}/res/ directory and so appending ../gives us the actual path to
				// 'res/'
				UpdateXmlResource (Path.Combine (Path.GetDirectoryName (filename), ".."), doc.Root, acwMap, additionalDirectories);
				using (var stream = File.OpenWrite (tmpfile))
					using (var xw = new LinePreservedXmlWriter (new StreamWriter (stream)))
						xw.WriteNode (doc.CreateNavigator (), false);
				Xamarin.Android.Tasks.MonoAndroidHelper.CopyIfChanged (tmpfile, filename);
				File.Delete (tmpfile);
			}
			catch (Exception e) {
				if (File.Exists (tmpfile)) {
					File.Delete (tmpfile);
				}
				Console.Error.WriteLine ("AndroidResgen: Warning while updating Resource XML '{0}': {1}", filename, e.Message);
				return;
			}
		}

		static readonly XNamespace android = "http://schemas.android.com/apk/res/android";
		static readonly XNamespace res_auto = "http://schemas.android.com/apk/res-auto";
		static readonly Regex r = new Regex (@"^@\+?(?<package>[^:]+:)?(anim|color|drawable|layout|menu)/(?<file>.*)$");
		static readonly string[] fixResourcesAliasPaths = {
			"/resources/item",
			"/resources/integer-array/item",
			"/resources/array/item",
			"/resources/style/item",
		};

		public static void UpdateXmlResource (XElement e)
		{
			UpdateXmlResource (e, new Dictionary<string,string> ());
		}

		public static void UpdateXmlResource (XElement e, Dictionary<string, string> acwMap)
		{
			UpdateXmlResource (null, e, acwMap);
		}

		static IEnumerable<T> Prepend<T> (this IEnumerable<T> l, T another) where T : XNode
		{
			yield return another;
			foreach (var e in l)
				yield return e;
		}
		
		static void UpdateXmlResource (string resourcesBasePath, XElement e, Dictionary<string, string> acwMap, IEnumerable<string> additionalDirectories = null)
		{
			foreach (var elem in GetElements (e).Prepend (e)) {
				TryFixCustomView (elem, acwMap);
			}

			foreach (var path in fixResourcesAliasPaths) {
				foreach(XElement item in e.XPathSelectElements (path).Prepend (e)) {
					TryFixResourceAlias (item, resourcesBasePath, additionalDirectories);
				}
			}

			foreach (XAttribute a in GetAttributes (e)) {
				if (a.IsNamespaceDeclaration)
					continue;

				if (TryFixFragment (a, acwMap))
					continue;
				
				if (TryFixResAuto (a, acwMap))
					continue;

				if (TryFixCustomClassAttribute (a, acwMap))
					continue;

				if (a.Name.Namespace != android &&
						!(a.Name.LocalName == "layout" && a.Name.Namespace == XNamespace.None &&
						  a.Parent.Name.LocalName == "include" && a.Parent.Name.Namespace == XNamespace.None))
					continue;

				Match m = r.Match (a.Value);
				if (!m.Success)
					continue;
				if (m.Groups ["package"].Success)
					continue;
				a.Value = TryLowercaseValue (a.Value, resourcesBasePath, additionalDirectories);
			}
		}

		static bool ResourceNeedsToBeLowerCased (string value, string resourceBasePath, IEnumerable<string> additionalDirectories)
		{
			// Might be a bit of an overkill, but the data comes (indirectly) from the user since it's the
			// path to the msbuild's intermediate output directory and that location can be changed by the
			// user. It's better to be safe than sorry.
			resourceBasePath = (resourceBasePath ?? String.Empty).Trim ();
			if (String.IsNullOrEmpty (resourceBasePath))
				return true;

			// Avoid resource names that are all whitespace
			value = (value ?? String.Empty).Trim ();
			if (String.IsNullOrEmpty (value))
				return false; // let's save some time
			if (value.Length < 4 || value [0] != '@') // 4 is the minimum length since we need a string
								  // that is at least of the following
								  // form: @x/y. Checking it here saves some time
								  // below.
				return true;

			string filePath = null;
			int slash = value.IndexOf ('/');
			int colon = value.IndexOf (':');
			if (colon == -1)
				colon = 0;

			// Determine the the potential definition file's path based on the resource type.
			string dirPrefix = value.Substring (colon + 1, slash - colon - 1).ToLowerInvariant ();
			string fileNamePattern = value.Substring (slash + 1).ToLowerInvariant () + ".*";
			
			if (Directory.EnumerateDirectories (resourceBasePath, dirPrefix + "*").Any (dir => Directory.EnumerateFiles (dir, fileNamePattern).Any ()))
				return true;

			// check additional directories if we have them incase the resource is in a library project
			if (additionalDirectories != null)
				foreach (var additionalDirectory in additionalDirectories)
					if (Directory.EnumerateDirectories (additionalDirectory, dirPrefix + "*").Any (dir => Directory.EnumerateFiles (dir, fileNamePattern).Any ()))
						return true;

			// No need to change the reference case.
			return false;
		}
		
		static IEnumerable<XAttribute> GetAttributes (XElement e)
		{
			foreach (XAttribute a in e.Attributes ())
				yield return a;
			foreach (XElement c in e.Elements ())
				foreach (XAttribute a in GetAttributes (c))
					yield return a;
		}

		static IEnumerable<XElement> GetElements (XElement e)
		{
			foreach (var a in e.Elements ()) {
				yield return a;

				foreach (var b in GetElements (a))
					yield return b;
			}
		}

		private static void TryFixResourceAlias (XElement elem, string resourceBasePath, IEnumerable<string> additionalDirectories)
		{
			// Looks for any resources aliases:
			//   <item type="layout" name="">@layout/Page1</item>
			//   <item type="layout" name="">@drawable/Page1</item>
			// and corrects the alias to be lower case.
			if (elem.Name == "item" && !string.IsNullOrEmpty(elem.Value) ) {
				string value = elem.Value.Trim();
				Match m = r.Match (value);
				if (m.Success) {
					elem.Value = TryLowercaseValue (elem.Value, resourceBasePath, additionalDirectories);
				}
			}
		}

		private static bool TryFixFragment (XAttribute attr, Dictionary<string, string> acwMap)
		{
			// Looks for any: 
			//   <fragment class="My.DotNet.Class" 
			//   <fragment android:name="My.DotNet.Class" ...
			// and tries to change it to the ACW name
			if (attr.Parent.Name != "fragment")
				return false;

			if (attr.Name == "class" || attr.Name == android + "name") {
				if (acwMap.ContainsKey (attr.Value)) {
					attr.Value = acwMap[attr.Value];

					return true;
				}
				else if (attr.Value?.Contains (',') ?? false) {
					// attr.Value could be an assembly-qualified name that isn't in acw-map.txt;
					// see e5b1c92c, https://github.com/xamarin/xamarin-android/issues/1296#issuecomment-365091948
					var n = attr.Value.Substring (0, attr.Value.IndexOf (','));
					if (acwMap.ContainsKey (n)) {
						attr.Value = acwMap [n];
						return true;
					}
				}
			}

			return false;
		}

		private static bool TryFixResAuto (XAttribute attr, Dictionary<string, string> acwMap)
		{
			if (attr.Name.Namespace != res_auto)
				return false;
			switch (attr.Name.LocalName) {
			case "rectLayout":
			case "roundLayout":
			case "actionLayout":
				attr.Value = attr.Value.ToLowerInvariant ();
				return true;
			}
			return false;
		}

		private static bool TryFixCustomView (XElement elem, Dictionary<string, string> acwMap)
		{
			// Looks for any <My.DotNet.Class ...
			// and tries to change it to the ACW name
			if (acwMap.ContainsKey (elem.Name.ToString ())) {
				elem.Name = acwMap[elem.Name.ToString ()];
				return true;
			}

			return false;
		}

		private static bool TryFixCustomClassAttribute (XAttribute attr, Dictionary<string, string> acwMap)
		{
			/* Some attributes reference a Java class name.
			 * try to convert those like for TryFixCustomView
			 */
			if (attr.Name != (res_auto + "layout_behavior")                              // For custom CoordinatorLayout behavior
			    && (attr.Parent.Name != "transition" || attr.Name.LocalName != "class")) // For custom transitions
				return false;

			string mappedValue;
			if (!acwMap.TryGetValue (attr.Value, out mappedValue))
				return false;

			attr.Value = mappedValue;
			return true;
		}

		private static string TryLowercaseValue (string value, string resourceBasePath, IEnumerable<string> additionalDirectories)
		{
			int s = value.LastIndexOf ('/');
			if (s >= 0) {
				if (ResourceNeedsToBeLowerCased (value, resourceBasePath, additionalDirectories))
					return value.Substring (0, s) + "/" + value.Substring (s+1).ToLowerInvariant ();
			}
			return value;
		}
	}
}