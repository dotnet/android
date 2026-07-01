using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class ExplicitInterfaceInvokerMethod : MethodWriter
	{
		readonly Method method;
		readonly CodeGenerationOptions opt;

		public ExplicitInterfaceInvokerMethod (GenBase iface, Method method, CodeGenerationOptions opt)
		{
			this.method = method;
			this.opt = opt;

			Name = method.Name;

			IsUnsafe = true;

			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));
			ExplicitInterfaceImplementation = opt.GetOutputName (iface.FullName);

			this.AddMethodParameters (method.Parameters, opt);
			SourceWriterExtensions.AddMethodBody (Body, method, opt);
		}
	}
}
