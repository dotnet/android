using System;

using System.Collections.Generic;
using Xamarin.Android.Manifest;
using System.Xml.Linq;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;

namespace Android.App {

	partial class UsesFeatureAttribute	{

		bool _Required;
		int _Version;

		static ManifestDocumentElement<UsesFeatureAttribute> mapping = new ManifestDocumentElement<UsesFeatureAttribute> ("uses-feature") {
			{
			  "Name",
			  "name",
			  self          => self.Name,
			  (self, value) => self.Name  = (string) value
			}, {
			  "Required",
			  "required",
			  self          => self._Required,
			  (self, value) => self._Required  = (bool) value
			}, {
			  "GLESVersion",
			  "glEsVersion",
			  self          => self.GLESVesionAsString(),
			  (self, value) => self.GLESVersion  = (int) value
			}, {
			  "Version",
			  "version",
			  self          => self._Version,
			  (self, value) => self._Version = (int) value
			}
		};

		internal string GLESVesionAsString ()
		{
			return String.Format("0x{0}", GLESVersion.ToString("X8"));
		}

		internal XElement ToElement (string packageName, TypeDefinitionCache cache)
		{
			return mapping.ToElement (this, specified, packageName, cache);
		}

		ICollection<string> specified;

		public static IEnumerable<UsesFeatureAttribute> FromCustomAttributeProvider (ICustomAttributeProvider provider, TypeDefinitionCache cache)
		{
			var attrs = provider.GetCustomAttributes ("Android.App.UsesFeatureAttribute");
			foreach (var attr in attrs) {

				UsesFeatureAttribute self = new UsesFeatureAttribute ();

				if (attr.HasProperties) {	
					// handle the case where the user sets additional properties
					self.specified = mapping.Load (self, attr, cache);
					if (self.specified.Contains("GLESVersion") && self.GLESVersion==0) {
						throw new InvalidOperationException("Invalid value '0' for UsesFeatureAttribute.GLESVersion.");
					}
				}
				if (attr.HasConstructorArguments) {
					// in this case the user used one of the Consructors to pass arguments and possibly properties
					// so we only create the specified list if its not been created already
					if (self.specified == null)
						self.specified = new List<string>();
					foreach(var arg in attr.ConstructorArguments) {
						if (arg.Value.GetType() == typeof(string)) {
							self.specified.Add("Name");
							self.Name = (string)arg.Value;
						}
					}
				}
				yield return self;
			}
		}
	}
}

