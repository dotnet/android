using System;
using System.Collections.Generic;
using System.Linq;
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
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));
			IsDeclaration = true;

			if (method.DeclaringType.IsGeneratable)
				Comments.Add ($"// Metadata.xml XPath method reference: path=\"{method.GetMetadataXPathReference (method.DeclaringType)}\"");
			if (method.Deprecated != null)
				Attributes.Add (new ObsoleteAttr (method.Deprecated.Replace ("\"", "\"\"")));
			if (method.IsReturnEnumified)
				Attributes.Add (new GeneratedEnumAttr (true));
			if (method.IsInterfaceDefaultMethod)
				Attributes.Add (new CustomAttr ("[global::Java.Interop.JavaInterfaceDefaultMethod]"));

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			if (opt.CodeGenerationTarget != CodeGenerationTarget.JavaInterop1) {
				Attributes.Add (new RegisterAttr (method.JavaName, method.JniSignature, method.ConnectorName + ":" + method.GetAdapterName (opt, adapter), additionalProperties: method.AdditionalAttributeString ()));
			}

			method.JavadocInfo?.AddJavadocs (Comments);

			SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);
			this.AddMethodParameters (method.Parameters, opt);
		}
	}
}
