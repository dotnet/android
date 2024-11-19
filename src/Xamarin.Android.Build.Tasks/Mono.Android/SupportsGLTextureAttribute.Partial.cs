using System;
using System.Xml.Linq;
using System.Collections.Generic;

using Mono.Cecil;
using Java.Interop.Tools.Cecil;
using Xamarin.Android.Manifest;

namespace Android.App
{
	partial class SupportsGLTextureAttribute
	{
		static ManifestDocumentElement<SupportsGLTextureAttribute> mapping = new ManifestDocumentElement<SupportsGLTextureAttribute> ("supports-gl-texture") {
			{
			  "Name",
			  "name",
			  self          => self.Name,
			  (self, value) => self.Name  = (string) value
			}
		};


		internal XElement ToElement (string packageName, TypeDefinitionCache cache)
		{
			return mapping.ToElement (this, specified, packageName, cache);
		}

		ICollection<string>? specified;

		public static IEnumerable<SupportsGLTextureAttribute> FromCustomAttributeProvider (ICustomAttributeProvider provider, TypeDefinitionCache cache)
		{
			var attrs = provider.GetCustomAttributes ("Android.App.SupportsGLTextureAttribute");
			foreach (var attr in attrs) {
				if (attr.HasConstructorArguments && attr.ConstructorArguments.Count == 1) {
					SupportsGLTextureAttribute self = new SupportsGLTextureAttribute((string)attr.ConstructorArguments[0].Value);
					self.specified = mapping.Load (self, attr, cache);
					self.specified.Add("Name");
					yield return self;					 
				}
			}
		}
	}
}

