using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using generator.SourceWriters;
using Mono.Options;
using Xamarin.SourceWriter;

namespace MonoDroid.Generation
{
	class JavaInteropCodeGenerator : CodeGenerator
	{

		public JavaInteropCodeGenerator (TextWriter writer, CodeGenerationOptions options) : base (writer, options)
		{
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

		public override void WriteType (GenBase gen, string indent, GenerationInfo gen_info)
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

		internal override void WriteClassHandle (ClassGen type, string indent, bool requireNew) => throw new NotImplementedException ();
		internal override void WriteClassHandle (InterfaceGen type, string indent, string declaringType) => throw new NotImplementedException ();
		internal override void WriteClassInvokerHandle (ClassGen type, string indent, string declaringType) => throw new NotImplementedException ();
		internal override void WriteConstructorBody (Ctor ctor, string indent, StringCollection call_cleanup) => throw new NotImplementedException ();
		internal override void WriteConstructorIdField (Ctor ctor, string indent) => throw new NotImplementedException ();
		internal override void WriteFieldGetBody (Field field, string indent, GenBase type) => throw new NotImplementedException ();
		internal override void WriteFieldIdField (Field field, string indent) => throw new NotImplementedException ();
		internal override void WriteFieldSetBody (Field field, string indent, GenBase type) => throw new NotImplementedException ();
		internal override void WriteInterfaceInvokerHandle (InterfaceGen type, string indent, string declaringType) => throw new NotImplementedException ();
		internal override void WriteMethodBody (Method method, string indent, GenBase type) => throw new NotImplementedException ();
		internal override void WriteMethodIdField (Method method, string indent) => throw new NotImplementedException ();
	}
}

