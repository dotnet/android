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
		readonly bool suppress_obsolete_warning;

		public ExplicitInterfaceInvokerMethod (GenBase iface, Method method, CodeGenerationOptions opt, bool isObsoleteContext = false)
		{
			this.method = method;
			this.opt = opt;

			Name = method.Name;

			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));
			ExplicitInterfaceImplementation = opt.GetOutputName (iface.FullName);

			this.AddMethodParameters (method.Parameters, opt);
			SourceWriterExtensions.AddMethodBody (Body, method, opt);

			suppress_obsolete_warning = !isObsoleteContext && (SourceWriterExtensions.IsTypeObsolete (iface, opt) || SourceWriterExtensions.ReferencesObsoleteType (method, opt));
		}

		public override void Write (CodeWriter writer)
		{
			SourceWriterExtensions.WriteWithWarningsDisabled (writer, suppress_obsolete_warning, "0618", () => base.Write (writer));
		}
	}
}
