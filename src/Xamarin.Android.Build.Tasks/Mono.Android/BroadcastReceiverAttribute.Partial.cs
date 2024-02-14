using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;

using Java.Interop.Tools.Cecil;

using Xamarin.Android.Manifest;

namespace Android.Content {

	partial class BroadcastReceiverAttribute {
		
		string _RoundIcon;

		static ManifestDocumentElement<BroadcastReceiverAttribute> mapping = new ManifestDocumentElement<BroadcastReceiverAttribute> ("receiver") {
			{
			  "Description",
			  "description",
			  self          => self.Description,
			  (self, value) => self.Description = (string) value
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
			  "RoundIcon",
			  "roundIcon",
			  self          => self._RoundIcon,
			  (self, value) => self._RoundIcon  = (string) value
			},
		};


		ICollection<string> specified;

		public static BroadcastReceiverAttribute FromTypeDefinition (TypeDefinition type, TypeDefinitionCache cache)
		{
			CustomAttribute attr = type.GetCustomAttributes ("Android.Content.BroadcastReceiverAttribute")
				.SingleOrDefault ();
			if (attr == null)
				return null;
			var self = new BroadcastReceiverAttribute ();
			self.specified = mapping.Load (self, attr, cache);
			return self;
		}

		public XElement ToElement (string packageName, TypeDefinitionCache cache)
		{
			return mapping.ToElement (this, specified, packageName, cache);
		}
	}
}
