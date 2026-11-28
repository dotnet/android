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
	public class BoundMethod : MethodWriter
	{
		readonly MethodCallback callback;
		List<string> disabled_warnings;

		public Method JavaMethod { get; }

		public BoundMethod (GenBase type, Method method, CodeGenerationOptions opt, bool generateCallbacks, bool forceOverride = false, bool? isShadow = null, bool isInvoker = false, bool? isObsoleteContext = null)
		{
			JavaMethod = method;

			if (generateCallbacks && method.IsVirtual)
				callback = new MethodCallback (type, method, opt, null, method.IsReturnCharSequence);

			Name = method.AdjustedName;

			IsStatic = method.IsStatic;
			IsSealed = method.IsOverride && method.IsFinal;

			SetVisibility (type is InterfaceGen && !IsStatic ? string.Empty : method.Visibility);

			// TODO: Clean up this logic
			var is_explicit = opt.SupportDefaultInterfaceMethods && type is InterfaceGen && method.OverriddenInterfaceMethod != null;
			var virt_ov = is_explicit ? string.Empty : forceOverride ? " override" : method.IsOverride ? (opt.SupportDefaultInterfaceMethods && method.OverriddenInterfaceMethod != null ? " virtual" : " override") : method.IsVirtual ? " virtual" : string.Empty;

			IsVirtual = virt_ov.Trim () == "virtual";
			IsOverride = virt_ov.Trim () == "override";

			// When using DIM, don't generate "virtual sealed" methods, remove both modifiers instead
			if (!forceOverride && opt.SupportDefaultInterfaceMethods && method.OverriddenInterfaceMethod != null && IsVirtual && IsSealed) {
				IsVirtual = false;
				IsSealed = false;
			}

			if (is_explicit)
				ExplicitInterfaceImplementation = GetDeclaringTypeOfExplicitInterfaceMethod (method.OverriddenInterfaceMethod);

			// Allow user to override our explicit interface logic
			if (method.ExplicitInterface.HasValue ())
				ExplicitInterfaceImplementation = method.ExplicitInterface;

			// Allow user to override our virtual/override logic
			if (!forceOverride && string.Equals (method.ManagedOverride, "virtual", StringComparison.OrdinalIgnoreCase)) {
				IsVirtual = true;
				IsOverride = false;
			} else if (!forceOverride && string.Equals (method.ManagedOverride, "override", StringComparison.OrdinalIgnoreCase)) {
				IsVirtual = false;
				IsOverride = true;
			} else if (!forceOverride && string.Equals (method.ManagedOverride, "none", StringComparison.OrdinalIgnoreCase)) {
				IsVirtual = false;
				IsOverride = false;
			} else if (!forceOverride && string.Equals (method.ManagedOverride, "new", StringComparison.OrdinalIgnoreCase)) {
				IsVirtual = false;
				IsOverride = false;
				IsSealed = false;
				IsShadow = true;
			}

			if (!IsOverride && !ExplicitInterfaceImplementation.HasValue () && (isShadow ?? type.RequiresNew (method.AdjustedName, method, opt)))
				IsShadow = true;

			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));

			method.JavadocInfo?.AddJavadocs (Comments);

			if (method.DeclaringType.IsGeneratable)
				Comments.Add ($"// Metadata.xml XPath method reference: path=\"{method.GetMetadataXPathReference (method.DeclaringType)}\"");

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);
			SourceWriterExtensions.AddRestrictToWarning (Attributes, method.AnnotatedVisibility, false, opt);

			if (method.IsReturnEnumified)
				Attributes.Add (new GeneratedEnumAttr (true));

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Attributes.Add (new RegisterAttr (method.JavaName, method.JniSignature, method.IsVirtual ? UnmanagedCallbackSupport.GetConnectorNameFull (type, method, opt) : string.Empty, additionalProperties: method.AdditionalAttributeString ()));

			SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);
			this.AddMethodParameters (method.Parameters, opt);

			SourceWriterExtensions.AddMethodBody (Body, method, opt);

			var hasObsoleteAttr = Attributes.OfType<ObsoleteAttr> ().Any ();
			var inObsoleteContext = isObsoleteContext ?? SourceWriterExtensions.IsTypeObsolete (type, opt);

			if (Name == "Finalize" && Parameters.Count == 0)
				SourceWriterExtensions.AddDisabledWarning (ref disabled_warnings, "0465");

			if (!inObsoleteContext && !hasObsoleteAttr && SourceWriterExtensions.ReferencesObsoleteType (method, opt))
				SourceWriterExtensions.AddDisabledWarning (ref disabled_warnings, "0618");

			if (!isInvoker && IsOverride && string.IsNullOrEmpty (ExplicitInterfaceImplementation) && hasObsoleteAttr &&
					!SourceWriterExtensions.WillBaseMethodEmitObsolete (type, method, opt))
				SourceWriterExtensions.AddDisabledWarning (ref disabled_warnings, "0809");
		}

		static string GetDeclaringTypeOfExplicitInterfaceMethod (Method method)
		{
			return method.OverriddenInterfaceMethod != null ?
				     GetDeclaringTypeOfExplicitInterfaceMethod (method.OverriddenInterfaceMethod) :
				     method.DeclaringType.FullName;
		}

		public override void Write (CodeWriter writer)
		{
			callback?.Write (writer);

			SourceWriterExtensions.WriteWithWarningsDisabled (writer, disabled_warnings, () => base.Write (writer));
		}
	}
}
