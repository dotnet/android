using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class InterfaceConstsClass : ClassWriter
	{
		public InterfaceConstsClass (ClassGen klass, HashSet<string> seen, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			Name = "InterfaceConsts";

			IsPublic = true;
			IsStatic = true;

			UsePriorityOrder = true;

			foreach (var iface in klass.GetAllImplementedInterfaces ()
				.Except (klass.BaseGen?.GetAllImplementedInterfaces () ?? new InterfaceGen [0])
				.Where (i => i.Fields.Count > 0)) {

				AddInlineComment ($"// The following are fields from: {iface.JavaName}");

				SourceWriterExtensions.AddFields (this, iface, iface.Fields, seen, opt, context);
			}

			// This type flattens the constants of every implemented interface into a single
			// static class, so none of its members hide an inherited one.
			foreach (var field in Fields)
				field.IsShadow = false;
			foreach (var property in Properties)
				property.IsShadow = false;
		}

		public bool ShouldGenerate => Fields.Any () || Properties.Any ();
	}
}
