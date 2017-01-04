using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Xamarin.Android.Manifest;

namespace Android.App {

	partial class InstrumentationAttribute {
		
		string _RoundIcon;

		static ManifestDocumentElement<InstrumentationAttribute> mapping = new ManifestDocumentElement<InstrumentationAttribute> ("instrumentation") {
			{
			  "FunctionalTest",
			  "functionalTest",
			  self          => self.FunctionalTest,
			  (self, value) => self.FunctionalTest  = (bool) value
			}, {
			  "HandleProfiling",
			  "handleProfiling",
			  self          => self.HandleProfiling,
			  (self, value) => self.HandleProfiling = (bool) value
			}, {
			  "Icon",
			  "icon",
			  self          => self.Icon,
			  (self, value) => self.Icon  = (string) value
			}, {
			  "Label",
			  "label",
			  self          => self.Label,
			  (self, value) => self.Label = (string) value
			}, {
			  "Name",
			  "name",
			  self          => self.Name,
			  (self, value) => self.Name  = (string) value
			}, {
			  "RoundIcon",
			  "roundIcon",
			  self          => self._RoundIcon,
			  (self, value) => self._RoundIcon  = (string) value
			}, {
			  "TargetPackage",
			  "targetPackage",
			  self          => self.TargetPackage,
			  (self, value) => self.TargetPackage = (string) value
			},
		};

		ICollection<string> specified;

		public static IEnumerable<InstrumentationAttribute> FromCustomAttributeProvider (ICustomAttributeProvider provider)
		{
			foreach (CustomAttribute attr in provider.GetCustomAttributes ("Android.App.InstrumentationAttribute")) {
				InstrumentationAttribute self = new InstrumentationAttribute ();
				self.specified = mapping.Load (self, attr);
				yield return self;
			}
		}

		public void SetTargetPackage (string package)
		{
			TargetPackage = package;
			specified.Add ("TargetPackage");
		}

		public XElement ToElement (string packageName)
		{
			return mapping.ToElement (this, specified, packageName);
		}
	}
}
