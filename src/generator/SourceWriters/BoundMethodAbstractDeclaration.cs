using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class BoundMethodAbstractDeclaration : MethodWriter
	{
		readonly Method method;
		readonly CodeGenerationOptions opt;
		readonly MethodCallback method_callback;
		readonly bool suppress_obsolete_warning;

		public BoundMethodAbstractDeclaration (GenBase gen, Method method, CodeGenerationOptions opt, GenBase impl)
		{
			this.method = method;
			this.opt = opt;

			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));
			this.AddMethodParameters (method.Parameters, opt);

			if (method.RetVal.IsGeneric && gen != null) {
				Name = method.Name;
				ExplicitInterfaceImplementation = opt.GetOutputName (gen.FullName);

				SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);

				Body.Add ("throw new NotImplementedException ();");

				suppress_obsolete_warning = !SourceWriterExtensions.IsTypeObsolete (impl ?? gen, opt) &&
					(SourceWriterExtensions.IsTypeObsolete (gen, opt) || SourceWriterExtensions.ReferencesObsoleteType (method, opt));
				return;
			}

			Name = method.AdjustedName;
			ExplicitInterfaceImplementation = method.ExplicitInterface;

			IsAbstract = true;
			SetVisibility (method.Visibility);

			NewFirst = true;

			if (string.Equals (method.ManagedOverride, "override", StringComparison.OrdinalIgnoreCase))
				IsOverride = true;
			else if (string.Equals (method.ManagedOverride, "new", StringComparison.OrdinalIgnoreCase))
				IsShadow = true;

			if (!IsOverride && !ExplicitInterfaceImplementation.HasValue () && impl.RequiresNew (method.AdjustedName, method, opt))
				IsShadow = true;

			method_callback = new MethodCallback (impl, method, opt, null, method.IsReturnCharSequence);

			method.JavadocInfo?.AddJavadocs (Comments);

			if (method.DeclaringType.IsGeneratable)
				Comments.Add ($"// Metadata.xml XPath method reference: path=\"{method.GetMetadataXPathReference (method.DeclaringType)}\"");

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);
			SourceWriterExtensions.AddRestrictToWarning (Attributes, method.AnnotatedVisibility, false, opt);

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Attributes.Add (new RegisterAttr (method.JavaName, method.JniSignature, UnmanagedCallbackSupport.GetConnectorName (impl, method, opt), additionalProperties: method.AdditionalAttributeString ()));

			SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);

			var hasObsoleteAttr = Attributes.OfType<ObsoleteAttr> ().Any ();
			suppress_obsolete_warning = !SourceWriterExtensions.IsTypeObsolete (impl ?? gen, opt) && !hasObsoleteAttr && SourceWriterExtensions.ReferencesObsoleteType (method, opt);
		}

		public override void Write (CodeWriter writer)
		{
			// Need to write our property callback first
			method_callback?.Write (writer);

			SourceWriterExtensions.WriteWithWarningsDisabled (writer, suppress_obsolete_warning, "0618", () => base.Write (writer));
		}
	}
}
