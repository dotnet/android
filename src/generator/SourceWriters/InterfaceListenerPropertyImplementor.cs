using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class InterfaceListenerPropertyImplementor : PropertyWriter
	{
		readonly string name;
		readonly CodeGenerationOptions opt;

		public InterfaceListenerPropertyImplementor (InterfaceGen iface, string name, CodeGenerationOptions opt)
		{
			this.name = name;
			this.opt = opt;

			Name = "Impl" + name;
			PropertyType = new TypeReferenceWriter (opt.GetOutputName (iface.FullName) + "Implementor") { Nullable = opt.SupportNullableReferenceTypes };

			HasGet = true;

			GetBody.Add ($"if (weak_implementor_{name} == null || !weak_implementor_{name}.IsAlive)");
			GetBody.Add ($"\treturn null;");
			GetBody.Add ($"return weak_implementor_{name}.Target as {opt.GetOutputName (iface.FullName)}Implementor;");

			HasSet = true;

			SetBody.Add ($"weak_implementor_{name} = new WeakReference (value, true);");
		}

		public override void Write (CodeWriter writer)
		{
			// Write our backing field first
			writer.WriteLine ($"WeakReference{opt.NullableOperator} weak_implementor_{name};");
			writer.WriteLine ();

			base.Write (writer);
		}
	}
}
