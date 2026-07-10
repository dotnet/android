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
	public class BoundInterfaceMethodDeclaration : MethodWriter
	{
		readonly Method method;
		readonly CodeGenerationOptions opt;

		public BoundInterfaceMethodDeclaration (Method method, string adapter, CodeGenerationOptions opt)
		{
			this.method = method;
			this.opt = opt;

			Name = method.AdjustedName;
			ExplicitInterfaceImplementation = method.ExplicitInterface;

			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));
			IsDeclaration = true;

			// Allow user to force adding the 'abstract' keyword for "reabstraction"
			if (method.ManagedOverride?.ToLowerInvariant () == "reabstract")
				IsAbstract = true;

			if (method.DeclaringType.IsGeneratable)
				Comments.Add ($"// Metadata.xml XPath method reference: path=\"{method.GetMetadataXPathReference (method.DeclaringType)}\"");

			SourceWriterExtensions.AddObsolete (Attributes, method.Deprecated, opt, deprecatedSince: method.DeprecatedSince);
			SourceWriterExtensions.AddRestrictToWarning (Attributes, method.AnnotatedVisibility, false, opt);

			if (method.IsReturnEnumified)
				Attributes.Add (new GeneratedEnumAttr (true));
			if (method.IsInterfaceDefaultMethod)
				Attributes.Add (new CustomAttr ("[global::Java.Interop.JavaInterfaceDefaultMethod]"));

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			Attributes.Add (new RegisterAttr (method.JavaName, method.JniSignature, method.ConnectorName + ":" + method.GetAdapterName (opt, adapter), additionalProperties: method.AdditionalAttributeString ()) {
				MemberType	    = opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1 ? null : (MemberTypes?) MemberTypes.Method,
			});

			method.JavadocInfo?.AddJavadocs (Comments);

			SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);
			this.AddMethodParameters (method.Parameters, opt);
		}
	}
}
