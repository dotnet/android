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

		public BoundMethodAbstractDeclaration (GenBase gen, Method method, CodeGenerationOptions opt, GenBase impl)
		{
			this.method = method;
			this.opt = opt;

			if (method.RetVal.IsGeneric && gen != null) {
				Name = method.Name;
				ExplicitInterfaceImplementation = opt.GetOutputName (gen.FullName);

				SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);

				Body.Add ("throw new NotImplementedException ();");

				return;
			}

			Name = method.AdjustedName;

			IsAbstract = true;
			IsShadow = impl.RequiresNew (method.Name, method);
			SetVisibility (method.Visibility);
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));

			NewFirst = true;

			method_callback = new MethodCallback (impl, method, opt, null, method.IsReturnCharSequence);

			method.JavadocInfo?.AddJavadocs (Comments);

			if (method.DeclaringType.IsGeneratable)
				Comments.Add ($"// Metadata.xml XPath method reference: path=\"{method.GetMetadataXPathReference (method.DeclaringType)}\"");

			Attributes.Add (new RegisterAttr (method.JavaName, method.JniSignature, method.ConnectorName, additionalProperties: method.AdditionalAttributeString ()));

			SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);
			this.AddMethodParameters (method.Parameters, opt);
		}

		public override void Write (CodeWriter writer)
		{
			// Need to write our property callback first
			method_callback?.Write (writer);

			base.Write (writer);
		}
	}
}
