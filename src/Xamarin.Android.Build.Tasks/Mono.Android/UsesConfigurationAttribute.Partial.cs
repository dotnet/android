using System;

using System.Collections.Generic;
using Xamarin.Android.Manifest;
using System.Xml.Linq;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;

namespace Android.App {

	partial class UsesConfigurationAttribute	{

		bool _Required;

		static ManifestDocumentElement<UsesConfigurationAttribute> mapping = new ManifestDocumentElement<UsesConfigurationAttribute> ("uses-configuration") {
			{
			  "ReqFiveWayNav",
			  "reqFiveWayNav",
			  self          => self.ReqFiveWayNav,
			  (self, value) => self.ReqFiveWayNav  = (bool) value
			}, {
			  "ReqHardKeyboard",
			  "reqHardKeyboard",
			  self          => self.ReqHardKeyboard,
			  (self, value) => self.ReqHardKeyboard  = (bool) value
			}, {
			  "ReqKeyboardType",
			  "reqKeyboardType",
			  self          => self.ReqKeyboardType,
			  (self, value) => self.ReqKeyboardType = (string) value
			}, {
			  "ReqNavigation",
			  "reqNavigation",
			  self          => self.ReqNavigation,
			  (self, value) => self.ReqNavigation = (string) value
			}, {
			  "ReqTouchScreen",
			  "reqTouchScreen",
			  self          => self.ReqTouchScreen,
			  (self, value) => self.ReqTouchScreen = (string) value
			}
		};

		internal XElement ToElement (string packageName, TypeDefinitionCache cache)
		{
			return mapping.ToElement (this, specified, packageName, cache);
		}

		ICollection<string> specified;

		public static IEnumerable<UsesConfigurationAttribute> FromCustomAttributeProvider (ICustomAttributeProvider provider, TypeDefinitionCache cache)
		{
			var attrs = provider.GetCustomAttributes ("Android.App.UsesConfigurationAttribute");
			foreach (var attr in attrs) {

				UsesConfigurationAttribute self = new UsesConfigurationAttribute ();

				self.specified = mapping.Load (self, attr, cache);

				yield return self;
			}
		}
	}
}

