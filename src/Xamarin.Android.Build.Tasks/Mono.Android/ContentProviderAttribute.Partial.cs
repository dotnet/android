using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Xamarin.Android.Manifest;

namespace Android.Content {

	partial class ContentProviderAttribute {
		
		string _RoundIcon;

		static ManifestDocumentElement<ContentProviderAttribute> mapping = new ManifestDocumentElement<ContentProviderAttribute> ("provider") {
			{
			  "Authorities",
			  "authorities",
			  (self, value) => self.Authorities = ToStringArray (value),
			  self          => string.Join (";", self.Authorities)
			}, {
			  "DirectBootAware",
			  "directBootAware",
			  self          => self.DirectBootAware,
			  (self, value) => self.DirectBootAware = (bool) value
			}, {
			  "Enabled",
			  "enabled",
			  self          => self.Enabled,
			  (self, value) => self.Enabled = (bool) value
			}, {
			  "Exported",
			  "exported",
			  self          => self.Exported,
			  (self, value) => self.Exported  = (bool) value
			}, {
			  "GrantUriPermissions",
			  "grantUriPermissions",
			  self          => self.GrantUriPermissions,
			  (self, value) => self.GrantUriPermissions = (bool) value
			}, {
			  "Icon",
			  "icon",
			  self          => self.Icon,
			  (self, value) => self.Icon  = (string) value
			}, {
			  "InitOrder",
			  "initOrder",
			  self          => self.InitOrder,
			  (self, value) => self.InitOrder = (int) value
			}, {
			  "Label",
			  "label",
			  self          => self.Label,
			  (self, value) => self.Label = (string) value
			}, {
			  "MultiProcess",
			  "multiprocess",
			  self          => self.MultiProcess,
			  (self, value) => self.MultiProcess  = (bool) value
			}, {
			  "Name",
			  "name",
			  self          => self.Name,
			  (self, value) => self.Name  = (string) value
			}, {
			  "Permission",
			  "permission",
			  self          => self.Permission,
			  (self, value) => self.Permission  = (string) value
			}, {
			  "Process",
				"process",
			  self          => self.Process,
			  (self, value) => self.Process = (string) value
			}, {
			  "ReadPermission",
			  "readPermission",
			  self          => self.ReadPermission,
			  (self, value) => self.ReadPermission  = (string) value
			}, {
			  "RoundIcon",
			  "roundIcon",
			  self          => self._RoundIcon,
			  (self, value) => self._RoundIcon  = (string) value
			}, {
			  "Syncable",
			  "syncable",
			  self          => self.Syncable,
			  (self, value) => self.Syncable  = (bool) value
			}, {
			  "WritePermission",
			  "writePermission",
			  self          => self.WritePermission,
			  (self, value) => self.WritePermission = (string) value
			},
		};

		static string[] ToStringArray (object value)
		{
			var values = (CustomAttributeArgument []) value;
			return values.Select (v => (string) v.Value).ToArray ();
		}

		ICollection<string> specified;

		public static ContentProviderAttribute FromTypeDefinition (TypeDefinition type, TypeDefinitionCache cache)
		{
			CustomAttribute attr = type.GetCustomAttributes ("Android.Content.ContentProviderAttribute")
				.SingleOrDefault ();
			if (attr == null)
				return null;
			var self = new ContentProviderAttribute (ToStringArray (attr.ConstructorArguments [0].Value));
			self.specified = mapping.Load (self, attr, cache);
			self.specified.Add ("Authorities");
			return self;
		}

		public XElement ToElement (string packageName, TypeDefinitionCache cache)
		{
			return mapping.ToElement (this, specified, packageName, cache);
		}
	}
}
