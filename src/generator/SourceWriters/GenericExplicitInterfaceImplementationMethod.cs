using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	// This is supposed to generate instantiated generic method output, but I don't think it is done yet.
	public class GenericExplicitInterfaceImplementationMethod : MethodWriter
	{
		readonly Method method;
		readonly CodeGenerationOptions opt;
		readonly GenericSymbol gen;

		public GenericExplicitInterfaceImplementationMethod (Method method, GenericSymbol gen, CodeGenerationOptions opt)
		{
			this.method = method;
			this.opt = opt;
			this.gen = gen;

			Name = method.Name;

			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));
			ExplicitInterfaceImplementation = opt.GetOutputName (gen.Gen.FullName);

			Comments.Add ($"// This method is explicitly implemented as a member of an instantiated {gen.FullName}");

			SourceWriterExtensions.AddMethodCustomAttributes (Attributes, method);
			this.AddMethodParameters (method.Parameters, opt);
		}

		protected override void WriteBody (CodeWriter writer)
		{
			var mappings = new Dictionary<string, string> ();

			for (var i = 0; i < gen.TypeParams.Length; i++)
				mappings [gen.Gen.TypeParameters [i].Name] = gen.TypeParams [i].FullName;

			var call = method.Name + " (" + method.Parameters.GetGenericCall (opt, mappings) + ")";
			writer.WriteLine ($"{(method.IsVoid ? string.Empty : "return ")}{method.RetVal.GetGenericReturn (opt, call, mappings)};");
		}
	}
}
