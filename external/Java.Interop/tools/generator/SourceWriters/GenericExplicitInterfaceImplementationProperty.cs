using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class GenericExplicitInterfaceImplementationProperty : PropertyWriter
	{
		public GenericExplicitInterfaceImplementationProperty (Property property, GenericSymbol gen, string adapter, Dictionary<string, string> mappings, CodeGenerationOptions opt)
		{
			Name = property.AdjustedName;

			PropertyType = new TypeReferenceWriter (opt.GetTypeReferenceName (property));
			ExplicitInterfaceImplementation = opt.GetOutputName (gen.Gen.FullName);

			Comments.Add ($"// This method is explicitly implemented as a member of an instantiated {gen.FullName}");

			if (property.Getter != null) {
				HasGet = true;

				if (gen.Gen.IsGeneratable)
					GetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.Gen.MetadataXPathReference}/method[@name='{property.Getter.JavaName}'{property.Getter.Parameters.GetMethodXPathPredicate ()}]\"");
				if (property.Getter.GenericArguments != null && property.Getter.GenericArguments.Any ())
					GetterAttributes.Add (new CustomAttr (property.Getter.GenericArguments.ToGeneratedAttributeString ()));

				SourceWriterExtensions.AddSupportedOSPlatform (GetterAttributes, property.Getter, opt);

				GetterAttributes.Add (new RegisterAttr (property.Getter.JavaName, property.Getter.JniSignature, property.Getter.ConnectorName + ":" + property.Getter.GetAdapterName (opt, adapter), additionalProperties: property.Getter.AdditionalAttributeString ()));

				GetBody.Add ($"return {property.Name};");
			}

			if (property.Setter != null) {
				HasSet = true;

				if (gen.Gen.IsGeneratable)
					SetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.Gen.MetadataXPathReference}/method[@name='{property.Setter.JavaName}'{property.Setter.Parameters.GetMethodXPathPredicate ()}]\"");
				if (property.Setter.GenericArguments != null && property.Setter.GenericArguments.Any ())
					SetterAttributes.Add (new CustomAttr (property.Setter.GenericArguments.ToGeneratedAttributeString ()));

				SourceWriterExtensions.AddSupportedOSPlatform (SetterAttributes, property.Setter, opt);

				SetterAttributes.Add (new RegisterAttr (property.Setter.JavaName, property.Setter.JniSignature, property.Setter.ConnectorName + ":" + property.Setter.GetAdapterName (opt, adapter), additionalProperties: property.Setter.AdditionalAttributeString ()));

				// Temporarily rename the parameter to "value"
				var pname = property.Setter.Parameters [0].Name;
				property.Setter.Parameters [0].Name = "value";
				SetBody.Add ($"{property.Name} = {property.Setter.Parameters.GetGenericCall (opt, mappings)};");
				property.Setter.Parameters [0].Name = pname;
			}
		}
	}
}
