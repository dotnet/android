using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class CreateImplementorMethod : MethodWriter
	{
		readonly bool suppress_obsolete_warning;

		public CreateImplementorMethod (InterfaceGen iface, ClassGen target, CodeGenerationOptions opt)
		{
			Name = $"__Create{iface.Name}Implementor";

			ReturnType = new TypeReferenceWriter ($"{opt.GetOutputName (iface.FullName)}Implementor");

			Body.Add ($"return new {opt.GetOutputName (iface.FullName)}Implementor ({(iface.NeedsSender ? "this" : "")});");

			suppress_obsolete_warning = !SourceWriterExtensions.IsTypeObsolete (target, opt) && SourceWriterExtensions.IsTypeObsolete (iface, opt);
		}

		public override void Write (CodeWriter writer)
		{
			SourceWriterExtensions.WriteWithWarningsDisabled (writer, suppress_obsolete_warning, "0618", () => base.Write (writer));
		}
	}
}
