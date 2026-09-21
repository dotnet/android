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
	public class BoundMethod : MethodWriter
	{
		readonly MethodCallback callback;

		public Method JavaMethod { get; }

		public BoundMethod (GenBase type, Method method, CodeGenerationOptions opt, bool generateCallbacks, bool forceOverride = false)
		{
			JavaMethod = method;

			if (generateCallbacks && method.IsVirtual && opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1)
				callback = new MethodCallback (type, method, opt, null, method.IsReturnCharSequence);

			Name = method.AdjustedName;

			IsStatic = method.IsStatic;
			IsSealed = method.IsOverride && method.IsFinal;
			IsUnsafe = true;

			SetVisibility (type is InterfaceGen && !IsStatic ? string.Empty : method.Visibility);

			// TODO: Clean up this logic
			var is_explicit = opt.SupportDefaultInterfaceMethods && type is InterfaceGen && method.OverriddenInterfaceMethod != null;
			var virt_ov = is_explicit ? string.Empty : method.IsOverride ? (opt.SupportDefaultInterfaceMethods && method.OverriddenInterfaceMethod != null ? " virtual" : " override") : method.IsVirtual ? " virtual" : string.Empty;

			IsVirtual = virt_ov.Trim () == "virtual";
			IsOverride = virt_ov.Trim () == "override";

			// A method re-declaring a Java default interface method is emitted as `virtual`
			// rather than `override`, because a C# default interface member is not inherited
			// by the implementing class. That does not hold for a type deriving from such a
			// class, where the member is an ordinary inherited virtual method.
			if (forceOverride) {
				IsVirtual = false;
				IsOverride = true;
			}

			// When using DIM, don't generate "virtual sealed" methods, remove both modifiers instead
			if (opt.SupportDefaultInterfaceMethods && method.OverriddenInterfaceMethod != null && IsVirtual && IsSealed) {
				IsVirtual = false;
				IsSealed = false;
			}

			if (is_explicit)
				ExplicitInterfaceImplementation = GetDeclaringTypeOfExplicitInterfaceMethod (method.OverriddenInterfaceMethod);

			// Allow user to override our explicit interface logic
			if (method.ExplicitInterface.HasValue ())
				ExplicitInterfaceImplementation = method.ExplicitInterface;

			// Allow user to override our virtual/override logic
			var managed_override = method.ManagedOverride?.ToLowerInvariant ();
			var force_shadow = false;

			if (managed_override == "virtual") {
				IsVirtual = true;
				IsOverride = false;
			} else if (managed_override == "override") {
				IsVirtual = false;
				IsOverride = true;
			} else if (managed_override == "none") {
				IsVirtual = false;
				IsOverride = false;
			} else if (managed_override == "new") {
				// The hidden member is not visible to the generator, for example because it
				// was removed from the API description and hand-bound instead. Leave the
				// computed virtual-ness alone; only the `override` has to become a `new`.
				IsOverride = false;
				force_shadow = true;
			}

			// `new` hides an inherited member, so it is invalid on an override or on an
			// explicit interface implementation.
			if ((IsVirtual || !IsOverride) && !ExplicitInterfaceImplementation.HasValue () &&
					(force_shadow || type.RequiresNew (method.AdjustedName, method, opt)))
				IsShadow = true;

			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));

			method.JavadocInfo?.AddJavadocs (Comments);

			if (method.DeclaringType.IsGeneratable)
				Comments.Add ($"// Metadata.xml XPath method reference: path=\"{method.GetMetadataXPathReference (method.DeclaringType)}\"");

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);

			JavaProjectionWarnings.AddFinalizeSuppression (this, method);
			JavaProjectionWarnings.AddObsoleteSuppressions (this, method, opt);
			JavaProjectionWarnings.AddNullabilitySuppressions (this, type, method, opt, ExplicitInterfaceImplementation.HasValue ());
			SourceWriterExtensions.AddRestrictToWarning (Attributes, method.AnnotatedVisibility, false, opt);

			if (method.IsReturnEnumified)
				Attributes.Add (new GeneratedEnumAttr (true));

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Attributes.Add (new RegisterAttr (method.JavaName, method.JniSignature, method.IsVirtual ? method.GetConnectorNameFull (opt) : string.Empty, additionalProperties: method.AdditionalAttributeString ()) {
				MemberType	    = opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1 ? null : (MemberTypes?) MemberTypes.Method,
			});

			SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);
			this.AddMethodParameters (method.Parameters, opt);

			SourceWriterExtensions.AddMethodBody (Body, method, opt);
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

			base.Write (writer);
		}
	}
}
