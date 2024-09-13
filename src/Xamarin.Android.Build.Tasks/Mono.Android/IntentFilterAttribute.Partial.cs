using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;
using Monodroid;
using MonoDroid.Tuner;

using Java.Interop.Tools.Cecil;

namespace Android.App {

	partial class IntentFilterAttribute {
		bool _AutoVerify;
		string _RoundIcon;

		static readonly Dictionary<string, string> attributeMappings = new Dictionary<string, string> () {
			{ "Icon",             "icon" },
			{ "Label",            "label" },
			{ "Priority",         "priority" },
			{ "DataHost",         "host" },
			{ "DataMimeType",     "mimeType" },
			{ "DataPath",         "path" },
			{ "DataPathPattern",  "pathPattern" },
			{ "DataPathPrefix",   "pathPrefix" },
			{ "DataPort",         "port" },
			{ "DataScheme",       "scheme" },
			{ "AutoVerify",       "autoVerify" },
			{ "DataPathSuffix",   "pathSuffix" },
			{ "DataPathAdvancedPattern", "pathAdvancedPattern" },
		};

		static readonly Dictionary<string, Action<IntentFilterAttribute, object>> setters = new Dictionary<string, Action<IntentFilterAttribute, object>> () {
			{ "Icon",             (self, value) => self.Icon            = (string) value },
			{ "Label",            (self, value) => self.Label           = (string) value },
			{ "Priority",         (self, value) => self.Priority        = (int) value },
			{ "Categories",       (self, value) => self.Categories      = ToStringArray (value) },
			{ "DataHost",         (self, value) => self.DataHost        = (string) value },
			{ "DataMimeType",     (self, value) => self.DataMimeType    = (string) value },
			{ "DataPath",         (self, value) => self.DataPath        = (string) value },
			{ "DataPathPattern",  (self, value) => self.DataPathPattern = (string) value },
			{ "DataPathPrefix",   (self, value) => self.DataPathPrefix  = (string) value },
			{ "DataPort",         (self, value) => self.DataPort        = (string) value },
			{ "DataScheme",       (self, value) => self.DataScheme      = (string) value },
			{ "DataHosts",        (self, value) => self.DataHosts       = ToStringArray (value) },
			{ "DataMimeTypes",    (self, value) => self.DataMimeTypes   = ToStringArray (value) },
			{ "DataPaths",        (self, value) => self.DataPaths       = ToStringArray (value) },
			{ "DataPathPatterns", (self, value) => self.DataPathPatterns= ToStringArray (value) },
			{ "DataPathPrefixes", (self, value) => self.DataPathPrefixes= ToStringArray (value) },
			{ "DataPorts",        (self, value) => self.DataPorts       = ToStringArray (value) },
			{ "DataSchemes",      (self, value) => self.DataSchemes     = ToStringArray (value) },
			{ "AutoVerify",       (self, value) => self._AutoVerify     = (bool) value },
			{ "RoundIcon",        (self, value) => self._RoundIcon      = (string) value },
			{ "DataPathSuffix",   (self, value) => self.DataPathSuffix  = (string) value },
			{ "DataPathSuffixes", (self, value) => self.DataPathSuffixes  = ToStringArray (value) },
			{ "DataPathAdvancedPattern",   (self, value) => self.DataPathAdvancedPattern        = (string) value },
			{ "DataPathAdvancedPatterns",  (self, value) => self.DataPathAdvancedPatterns       = ToStringArray (value) },
		};

		static string[] ToStringArray (object value)
		{
			var values = (CustomAttributeArgument []) value;
			return values.Select (v => (string) v.Value).ToArray ();
		}

		HashSet<string> specified = new HashSet<string> ();

		public static IEnumerable<IntentFilterAttribute> FromTypeDefinition (TypeDefinition type, IMetadataResolver cache)
		{
			IEnumerable<CustomAttribute> attrs = type.GetCustomAttributes ("Android.App.IntentFilterAttribute");
			foreach (CustomAttribute attr in attrs) {
				var self = new IntentFilterAttribute (ToStringArray (attr.ConstructorArguments [0].Value));
				foreach (var e in attr.Properties) {
					self.specified.Add (e.Name);
					setters [e.Name] (self, e.Argument.GetSettableValue (cache));
				}
				yield return self;
			}
		}

		string ReplacePackage (string s, string packageName)
		{
			return s != null ? s.Replace ("@PACKAGE_NAME@", packageName) : null;
		}

		public XElement ToElement (string packageName)
		{
			var r = new XElement ("intent-filter",
					ToAttribute ("Icon",       Icon),
					ToAttribute ("Label",      ReplacePackage (Label, packageName)),
					ToAttribute ("Priority",   Priority),
					ToAttribute ("AutoVerify", _AutoVerify),
					Actions.Select (a => new XElement ("action", new XAttribute (android + "name", ReplacePackage (a, packageName)))),
					(Categories ?? Array.Empty<string> ()).Select (c => new XElement ("category", new XAttribute (android + "name", ReplacePackage (c, packageName)))),
					GetData (packageName));
			AndroidResource.UpdateXmlResource (r);
			return r;
		}

		static readonly XNamespace android = "http://schemas.android.com/apk/res/android";

		XAttribute ToAttribute (string name, bool value)
		{
			if (!specified.Contains (name))
				return null;
			return new XAttribute (android + attributeMappings [name], value ? "true" : "false");
		}

		XAttribute ToAttribute (string name, int value)
		{
			if (!specified.Contains (name))
				return null;
			return new XAttribute (android + attributeMappings [name], value);
		}

		XAttribute ToAttribute (string name, string value)
		{
			if (value == null)
				return null;
			return new XAttribute (android + attributeMappings [name], value);
		}

		IEnumerable<XElement> GetData (string packageName)
		{
			Func<string,XAttribute> toHost        = v => ToAttribute ("DataHost",        ReplacePackage (v, packageName));
			Func<string,XAttribute> toMimeType    = v => ToAttribute ("DataMimeType",    ReplacePackage (v, packageName));
			Func<string,XAttribute> toPath        = v => ToAttribute ("DataPath",        ReplacePackage (v, packageName));
			Func<string,XAttribute> toPathPattern = v => ToAttribute ("DataPathPattern", ReplacePackage (v, packageName));
			Func<string,XAttribute> toPathPrefix  = v => ToAttribute ("DataPathPrefix",  ReplacePackage (v, packageName));
			Func<string,XAttribute> toPort        = v => ToAttribute ("DataPort",        ReplacePackage (v, packageName));
			Func<string,XAttribute> toScheme      = v => ToAttribute ("DataScheme",      ReplacePackage (v, packageName));
			Func<string,XAttribute> toPathSuffix      = v => ToAttribute ("DataPathSuffix",			  ReplacePackage (v, packageName));
			Func<string,XAttribute> toPathAdvancedPattern = v => ToAttribute ("DataPathAdvancedPattern",      ReplacePackage (v, packageName));
			Func<Func<string,XAttribute>, string, XElement> toData = (f, s) => string.IsNullOrEmpty (s) ? null : new XElement ("data", f (s));
			var empty = Array.Empty<string> ();
			var dataList = Enumerable.Empty<XElement> ()
				.Concat ((DataHosts ?? empty).Select (p => toData (toHost, p)))
				.Concat ((DataMimeTypes ?? empty).Select (p => toData (toMimeType, p)))
				.Concat ((DataPaths ?? empty).Select (p => toData (toPath, p)))
				.Concat ((DataPathPatterns ?? empty).Select (p => toData (toPathPattern, p)))
				.Concat ((DataPathPrefixes ?? empty).Select (p => toData (toPathPrefix, p)))
				.Concat ((DataPorts ?? empty).Select (p => toData (toPort, p)))
				.Concat ((DataSchemes ?? empty).Select (p => toData (toScheme, p)))
				.Concat ((DataPathSuffixes ?? empty).Select (p => toData (toPathSuffix, p)))
				.Concat ((DataPathAdvancedPatterns ?? empty).Select (p => toData (toPathAdvancedPattern, p)));
			if (string.IsNullOrEmpty (DataHost) && string.IsNullOrEmpty (DataMimeType) &&
					string.IsNullOrEmpty (DataPath) && string.IsNullOrEmpty (DataPathPattern) && string.IsNullOrEmpty (DataPathPrefix) &&
					string.IsNullOrEmpty (DataPort) && string.IsNullOrEmpty (DataScheme) && string.IsNullOrEmpty (DataPathSuffix) &&
					string.IsNullOrEmpty (DataPathAdvancedPattern) && !dataList.Any ())
				return null;
			return new XElement [] {
					toData (toHost, DataHost),
					toData (toMimeType, DataMimeType),
					toData (toPath, DataPath),
					toData (toPathPattern, DataPathPattern),
					toData (toPathPrefix, DataPathPrefix),
					toData (toPort, DataPort),
					toData (toScheme, DataScheme),
					toData (toPathSuffix, DataPathSuffix),
					toData (toPathAdvancedPattern, DataPathAdvancedPattern)}
				.Concat (dataList).Where (x => x != null);
		}
	}
}
