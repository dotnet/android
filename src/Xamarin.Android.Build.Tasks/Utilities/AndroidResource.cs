using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Monodroid {
	static class AndroidResource {
		
		public static bool UpdateXmlResource (string res, string filename, Dictionary<string, string> acwMap, IEnumerable<string> additionalDirectories = null, Action<TraceLevel, string> logMessage = null, Action<string, string> registerCustomView = null)
		{
			// use a temporary file so we only update the real file if things actually changed
			string tmpfile = filename + ".bk";
			try {
				XDocument doc = XDocument.Load (filename, LoadOptions.SetLineInfo);
				UpdateXmlResource (res, doc.Root, acwMap, additionalDirectories, logMessage, (e) => {
					registerCustomView?.Invoke (e, filename);
				});
				using (var stream = File.OpenWrite (tmpfile))
					using (var xw = new LinePreservedXmlWriter (new StreamWriter (stream)))
						xw.WriteNode (doc.CreateNavigator (), false);

				return Xamarin.Android.Tasks.MonoAndroidHelper.CopyIfChanged (tmpfile, filename);
			} catch (Exception e) {
				logMessage?.Invoke (TraceLevel.Warning, $"AndroidResgen: Warning while updating Resource XML '{filename}': {e.Message}");
				return false;
			} finally {
				if (File.Exists (tmpfile)) {
					File.Delete (tmpfile);
				}
			}
		}

		static readonly XNamespace android = "http://schemas.android.com/apk/res/android";
		static readonly XNamespace res_auto = "http://schemas.android.com/apk/res-auto";
		static readonly Regex r = new Regex (@"^@\+?(?<package>[^:]+:)?(anim|color|drawable|layout|menu)/(?<file>.*)$", RegexOptions.Compiled);
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

		internal static IEnumerable<T> Prepend<T> (this IEnumerable<T> l, T another) where T : XNode
		{
			yield return another;
			foreach (var e in l)
				yield return e;
		}
		
		static void UpdateXmlResource (string resourcesBasePath, XElement e, Dictionary<string, string> acwMap, IEnumerable<string> additionalDirectories = null, Action<TraceLevel, string> logMessage = null, Action<string> registerCustomView = null)
		{
			foreach (var elem in GetElements (e).Prepend (e)) {
				registerCustomView?.Invoke (elem.Name.ToString ());
			}

			foreach (var path in fixResourcesAliasPaths) {
				foreach(XElement item in e.XPathSelectElements (path).Prepend (e)) {
					TryFixResourceAlias (item, resourcesBasePath, additionalDirectories);
				}
			}

			foreach (XAttribute a in GetAttributes (e)) {
				if (a.IsNamespaceDeclaration)
					continue;

				TryFixFragment (a, acwMap, registerCustomView);
				
				if (TryFixResAuto (a, acwMap))
					continue;

				TryFixCustomClassAttribute (a, acwMap, registerCustomView);

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
			resourceBasePath = resourceBasePath?.Trim ();
			if (string.IsNullOrEmpty (resourceBasePath))
				return true;

			// Avoid resource names that are all whitespace
			value = value?.Trim ();
			if (string.IsNullOrEmpty (value))
				return false; // let's save some time
			if (value.Length < 4 || value [0] != '@') // 4 is the minimum length since we need a string
								  // that is at least of the following
								  // form: @x/y. Checking it here saves some time
								  // below.
				return true;

			int slash = value.IndexOf ('/');
			int colon = value.IndexOf (':');
			if (colon == -1)
				colon = 0;

			// Determine the the potential definition file's path based on the resource type.
			string dirPattern = value.Substring (colon + 1, slash - colon - 1).ToLowerInvariant () + "*";
			string fileNamePattern = value.Substring (slash + 1).ToLowerInvariant () + ".*";
			
			foreach (var dir in Directory.EnumerateDirectories (resourceBasePath, dirPattern)) {
				foreach (var file in Directory.EnumerateFiles (dir, fileNamePattern)) {
					return true;
				}
			}

			// check additional directories if we have them incase the resource is in a library project
			if (additionalDirectories != null) {
				foreach (var additionalDirectory in additionalDirectories) {
					foreach (var dir in Directory.EnumerateDirectories (additionalDirectory, dirPattern)) {
						foreach (var file in Directory.EnumerateFiles (dir, fileNamePattern)) {
							return true;
						}
					}
				}
			}

			// No need to change the reference case.
			return false;
		}
		
		internal static IEnumerable<XAttribute> GetAttributes (XElement e)
		{
			foreach (XAttribute a in e.Attributes ())
				yield return a;
			foreach (XElement c in e.Elements ())
				foreach (XAttribute a in GetAttributes (c))
					yield return a;
		}

		internal static IEnumerable<XElement> GetElements (XElement e)
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

		private static void TryFixFragment (XAttribute attr, Dictionary<string, string> acwMap, Action<string> registerCustomView = null)
		{
			// Looks for any: 
			//   <fragment class="My.DotNet.Class" 
			//   <fragment android:name="My.DotNet.Class" ...
			// and tries to change it to the ACW name
			if (attr.Parent.Name != "fragment")
				return;

			if (attr.Name == "class" || attr.Name == android + "name") {
				var n = attr.Value;
				if (n == null)
					return;
				if (n.Contains (',')) {
					// attr.Value could be an assembly-qualified name that isn't in acw-map.txt;
					// see e5b1c92c, https://github.com/xamarin/xamarin-android/issues/1296#issuecomment-365091948
					n = attr.Value.Substring (0, attr.Value.IndexOf (','));
				}
				registerCustomView?.Invoke (n);
			}
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

		private static void TryFixCustomClassAttribute (XAttribute attr, Dictionary<string, string> acwMap, Action<string> registerCustomView = null)
		{
			/* Some attributes reference a Java class name.
			 * try to convert those like for TryFixCustomView
			 */
			if (attr.Name != (res_auto + "layout_behavior")                              // For custom CoordinatorLayout behavior
			    && (attr.Parent.Name != "transition" || attr.Name.LocalName != "class")) // For custom transitions
				return;

			registerCustomView?.Invoke (attr.Value);
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