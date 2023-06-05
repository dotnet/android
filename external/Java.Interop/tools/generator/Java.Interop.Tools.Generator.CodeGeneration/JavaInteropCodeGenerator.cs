using System;
using System.IO;
using generator.SourceWriters;
using Xamarin.SourceWriter;

namespace MonoDroid.Generation
{
	class JavaInteropCodeGenerator
	{
		protected TextWriter writer;
		protected CodeGenerationOptions opt;

		public CodeGeneratorContext Context { get; } = new CodeGeneratorContext ();

		public JavaInteropCodeGenerator (TextWriter writer, CodeGenerationOptions options)
		{
			this.writer = writer;
			opt = options;
		}

		public static string GetInvokeType (string type)
		{
			return type switch
			{
				"Bool" => "Boolean",
				"Byte" => "SByte",
				"Int" => "Int32",
				"Short" => "Int16",
				"Long" => "Int64",
				"Float" => "Single",
				"UInt" => "Int32",
				"UShort" => "Int16",
				"ULong" => "Int64",
				"UByte" => "SByte",
				_ => type,
			};
		}

		public virtual void WriteType (GenBase gen, string indent, GenerationInfo gen_info)
		{
			TypeWriter type_writer;

			if (gen is InterfaceGen iface)
				type_writer = new BoundInterface (iface, opt, Context, gen_info);
			else if (gen is ClassGen klass)
				type_writer = new BoundClass (klass, opt, Context, gen_info);
			else
				throw new InvalidOperationException ("Unknown GenBase type");

			// We do this here because we only want to check for top-level types,
			// we should not check types nested in other types.
			SourceWriterExtensions.WarnIfTypeNameMatchesNamespace (type_writer, gen);

			var cw = new CodeWriter (writer, indent);
			type_writer.Write (cw);
		}
	}
}

