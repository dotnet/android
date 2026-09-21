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
	public class BoundAbstractProperty : PropertyWriter
	{
		readonly MethodCallback getter_callback;
		readonly MethodCallback setter_callback;

		public BoundAbstractProperty (GenBase gen, Property property, CodeGenerationOptions opt)
		{
			Name = property.AdjustedName;
			ExplicitInterfaceImplementation = property.ExplicitInterface;
			PropertyType = new TypeReferenceWriter (opt.GetTypeReferenceName (property.Getter.RetVal));

			SetVisibility (property.Getter.RetVal.IsGeneric ? "protected" : property.Getter.Visibility);

			IsAbstract = true;
			HasGet = true;

			var baseProp = gen.BaseSymbol != null ? gen.BaseSymbol.GetPropertyByName (property.Name, true) : null;

			if (baseProp != null) {
				IsOverride = true;
			} else {
				IsShadow = gen.RequiresNew (property, opt);

				if (opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1)
					getter_callback = new MethodCallback (gen, property.Getter, opt, property.AdjustedName, false);

				if (property.Setter != null && opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1)
					setter_callback = new MethodCallback (gen, property.Setter, opt, property.AdjustedName, false);
			}

			// Add [Obsolete] or [ObsoletedOSPlatform]. Without this an abstract declaration of a
			// deprecated Java member would lose its deprecation, while the overrides generated
			// for it (invokers, implementors) keep theirs.
			if (property.IsWholePropertyDeprecated) {
				SourceWriterExtensions.AddObsolete (Attributes, property.Getter.Deprecated.Trim (), opt, deprecatedSince: property.Getter.DeprecatedSince);
			} else {
				if (property.Getter?.Deprecated != null)
					SourceWriterExtensions.AddObsolete (GetterAttributes, property.Getter.Deprecated.Trim (), opt, deprecatedSince: property.Getter?.DeprecatedSince);

				if (property.Setter?.Deprecated != null)
					SourceWriterExtensions.AddObsolete (SetterAttributes, property.Setter.Deprecated.Trim (), opt, deprecatedSince: property.Setter?.DeprecatedSince);
			}

			JavaProjectionWarnings.AddObsoleteSuppressions (this, property, opt, gen);
			JavaProjectionWarnings.AddNullabilitySuppressions (this, gen, property, opt);

			if (gen.IsGeneratable)
				GetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.MetadataXPathReference}/method[@name='{property.Getter.JavaName}'{property.Getter.Parameters.GetMethodXPathPredicate ()}]\"");
			if (property.Getter.IsReturnEnumified)
				GetterAttributes.Add (new GeneratedEnumAttr (true));

			SourceWriterExtensions.AddSupportedOSPlatform (GetterAttributes, property.Getter, opt);

			GetterAttributes.Add (new RegisterAttr (property.Getter.JavaName, property.Getter.JniSignature, property.Getter.GetConnectorNameFull (opt), additionalProperties: property.Getter.AdditionalAttributeString ()) {
				MemberType	    = opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1 ? null : (MemberTypes?) MemberTypes.Method,
			});

			SourceWriterExtensions.AddMethodCustomAttributes (GetterAttributes, property.Getter);

			if (property.Setter != null) {
				HasSet = true;

				if (gen.IsGeneratable)
					SetterComments.Add ($"// Metadata.xml XPath method reference: path=\"{gen.MetadataXPathReference}/method[@name='{property.Setter.JavaName}'{property.Setter.Parameters.GetMethodXPathPredicate ()}]\"");

				SourceWriterExtensions.AddSupportedOSPlatform (SetterAttributes, property.Setter, opt);

				SourceWriterExtensions.AddMethodCustomAttributes (SetterAttributes, property.Setter);
				SetterAttributes.Add (new RegisterAttr (property.Setter.JavaName, property.Setter.JniSignature, property.Setter.GetConnectorNameFull (opt), additionalProperties: property.Setter.AdditionalAttributeString ()) {
					MemberType	    = opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1 ? null : (MemberTypes?) MemberTypes.Method,
				});
			}
		}

		public override void Write (CodeWriter writer)
		{
			// Need to write our property callbacks first
			getter_callback?.Write (writer);
			setter_callback?.Write (writer);

			base.Write (writer);
		}
	}
}
