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

		public BoundMethod (GenBase type, Method method, CodeGenerationOptions opt, bool generateCallbacks)
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

			if ((IsVirtual || !IsOverride) && type.RequiresNew (method.AdjustedName, method))
				IsShadow = true;

			// Allow user to override our virtual/override logic
			if (method.ManagedOverride?.ToLowerInvariant () == "virtual") {
				IsVirtual = true;
				IsOverride = false;
			} else if (method.ManagedOverride?.ToLowerInvariant () == "override") {
				IsVirtual = false;
				IsOverride = true;
			} else if (method.ManagedOverride?.ToLowerInvariant () == "none") {
				IsVirtual = false;
				IsOverride = false;
			}

			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));

			method.JavadocInfo?.AddJavadocs (Comments);

			if (method.DeclaringType.IsGeneratable)
				Comments.Add ($"// Metadata.xml XPath method reference: path=\"{method.GetMetadataXPathReference (method.DeclaringType)}\"");

			if (method.Deprecated.HasValue ())
				Attributes.Add (new ObsoleteAttr (method.Deprecated.Replace ("\"", "\"\"")));

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
