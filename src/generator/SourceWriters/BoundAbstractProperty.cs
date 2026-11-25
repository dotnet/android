using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class BoundAbstractProperty : PropertyWriter
	{
		readonly MethodCallback getter_callback;
		readonly MethodCallback setter_callback;
		readonly bool suppress_obsolete_warning;

		public BoundAbstractProperty (GenBase gen, Property property, CodeGenerationOptions opt)
		{
			Name = property.AdjustedName;
			ExplicitInterfaceImplementation = property.ExplicitInterface;
			PropertyType = new TypeReferenceWriter (opt.GetTypeReferenceName (property.Getter.RetVal));

			SetVisibility (property.Getter.RetVal.IsGeneric ? "protected" : property.Getter.Visibility);

			IsAbstract = true;
			HasGet = true;

			var isForcedNew = string.Equals ((property.Getter ?? property.Setter).ManagedOverride, "new", StringComparison.OrdinalIgnoreCase);
			var baseProp = isForcedNew ? null : gen.BaseSymbol?.GetPropertyByName (property.Name, true);

			if (baseProp != null) {
				IsOverride = true;
			} else {
				if (isForcedNew || (!ExplicitInterfaceImplementation.HasValue () && gen.RequiresNew (property, opt)))
					IsShadow = true;

				getter_callback = new MethodCallback (gen, property.Getter, opt, property.AdjustedName, false);

				if (property.Setter != null)
					setter_callback = new MethodCallback (gen, property.Setter, opt, property.AdjustedName, false);
			}

			if (gen.IsGeneratable)
				GetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.MetadataXPathReference}/method[@name='{property.Getter.JavaName}'{property.Getter.Parameters.GetMethodXPathPredicate ()}]\"");
			if (property.Getter.IsReturnEnumified)
				GetterAttributes.Add (new GeneratedEnumAttr (true));

			SourceWriterExtensions.AddSupportedOSPlatform (GetterAttributes, property.Getter, opt);
			SourceWriterExtensions.AddAllowNull (Attributes, property, opt);

			GetterAttributes.Add (new RegisterAttr (property.Getter.JavaName, property.Getter.JniSignature, UnmanagedCallbackSupport.GetPropertyConnectorNameFull (gen, property, property.Getter, opt), additionalProperties: property.Getter.AdditionalAttributeString ()));

			SourceWriterExtensions.AddMethodCustomAttributes (GetterAttributes, property.Getter);

			if (property.Setter != null) {
				HasSet = true;

				if (gen.IsGeneratable)
					SetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.MetadataXPathReference}/method[@name='{property.Setter.JavaName}'{property.Setter.Parameters.GetMethodXPathPredicate ()}]\"");

				SourceWriterExtensions.AddSupportedOSPlatform (SetterAttributes, property.Setter, opt);

				SourceWriterExtensions.AddMethodCustomAttributes (SetterAttributes, property.Setter);
				SetterAttributes.Add (new RegisterAttr (property.Setter.JavaName, property.Setter.JniSignature, UnmanagedCallbackSupport.GetPropertyConnectorNameFull (gen, property, property.Setter, opt), additionalProperties: property.Setter.AdditionalAttributeString ()));
			}

			suppress_obsolete_warning = !SourceWriterExtensions.IsTypeObsolete (gen, opt) && SourceWriterExtensions.ReferencesObsoleteType (property, opt);
		}

		public override void Write (CodeWriter writer)
		{
			// Need to write our property callbacks first
			getter_callback?.Write (writer);
			setter_callback?.Write (writer);

			SourceWriterExtensions.WriteWithWarningsDisabled (writer, suppress_obsolete_warning, "0618", () => base.Write (writer));
		}
	}
}
