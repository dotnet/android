using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

namespace generator.SourceWriters
{
	public class BoundInterfacePropertyDeclaration : PropertyWriter
	{
		public BoundInterfacePropertyDeclaration (GenBase gen, Property property, string adapter, CodeGenerationOptions opt)
		{
			Name = property.AdjustedName;

			PropertyType = new TypeReferenceWriter (opt.GetTypeReferenceName (property));
			IsAutoProperty = true;

			// Allow user to force adding the 'abstract' keyword for "reabstraction"
			if ((property.Getter ?? property.Setter).ManagedOverride?.ToLowerInvariant () == "reabstract")
				IsAbstract = true;

			if (property.Getter != null) {
				HasGet = true;

				if (gen.IsGeneratable)
					GetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.MetadataXPathReference}/method[@name='{property.Getter.JavaName}'{property.Getter.Parameters.GetMethodXPathPredicate ()}]\"");
				if (property.Getter.GenericArguments?.Any () == true)
					GetterAttributes.Add (new CustomAttr (property.Getter.GenericArguments.ToGeneratedAttributeString ()));

				SourceWriterExtensions.AddSupportedOSPlatform (GetterAttributes, property.Getter, opt);

				GetterAttributes.Add (new RegisterAttr (property.Getter.JavaName, property.Getter.JniSignature, property.Getter.ConnectorName + ":" + property.Getter.GetAdapterName (opt, adapter), additionalProperties: property.Getter.AdditionalAttributeString ()) {
					MemberType	    = opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1 ? null : (MemberTypes?) MemberTypes.Method,
				});
			}

			if (property.Setter != null) {
				HasSet = true;

				if (gen.IsGeneratable)
					SetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.MetadataXPathReference}/method[@name='{property.Setter.JavaName}'{property.Setter.Parameters.GetMethodXPathPredicate ()}]\"");
				if (property.Setter.GenericArguments?.Any () == true)
					SetterAttributes.Add (new CustomAttr (property.Setter.GenericArguments.ToGeneratedAttributeString ()));

				SourceWriterExtensions.AddSupportedOSPlatform (SetterAttributes, property.Setter, opt);

				SetterAttributes.Add (new RegisterAttr (property.Setter.JavaName, property.Setter.JniSignature, property.Setter.ConnectorName + ":" + property.Setter.GetAdapterName (opt, adapter), additionalProperties: property.Setter.AdditionalAttributeString ()) {
					MemberType	    = opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1 ? null : (MemberTypes?) MemberTypes.Method,
				});
			}
		}
	}
}
