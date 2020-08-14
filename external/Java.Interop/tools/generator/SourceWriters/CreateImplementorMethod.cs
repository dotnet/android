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
		public CreateImplementorMethod (InterfaceGen iface, CodeGenerationOptions opt)
		{
			Name = $"__Create{iface.Name}Implementor";

			ReturnType = new TypeReferenceWriter ($"{opt.GetOutputName (iface.FullName)}Implementor");

			Body.Add ($"return new {opt.GetOutputName (iface.FullName)}Implementor ({(iface.NeedsSender ? "this" : "")});");
		}
	}
}
