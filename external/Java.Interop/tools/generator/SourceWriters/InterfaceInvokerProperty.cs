using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

using CodeGenerationTarget = Xamarin.Android.Binder.CodeGenerationTarget;

namespace generator.SourceWriters
{
	public class InterfaceInvokerProperty : PropertyWriter
	{
		readonly MethodCallback getter_callback;
		readonly MethodCallback setter_callback;
		readonly Property property;
		readonly CodeGenerationOptions opt;
		readonly string context_this;

		public InterfaceInvokerProperty (InterfaceGen iface, Property property, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			this.property = property;
			this.opt = opt;

			Name = property.AdjustedName;
			PropertyType = new TypeReferenceWriter (opt.GetTypeReferenceName (property));

			IsPublic = true;
			IsUnsafe = true;

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, property.Getter, opt);

			HasGet = property.Getter != null;

			if (property.Getter != null) {
				HasGet = true;
				getter_callback = new MethodCallback (iface, property.Getter, opt, property.AdjustedName, false);
			}

			if (property.Setter != null) {
				HasSet = true;
				setter_callback = new MethodCallback (iface, property.Setter, opt, property.AdjustedName, false);
			}

			context_this = context.ContextType.GetObjectHandleProperty (opt, "this");

			if (!opt.EmitLegacyInterfaceInvokers) {
				if (HasGet) {
					SourceWriterExtensions.AddMethodBody (GetBody, property.Getter, opt, $"_members_{property.Getter.DeclaringType.JavaFullNameId}");
				}
				if (HasSet) {
					var pname = property.Setter.Parameters [0].Name;
					property.Setter.Parameters [0].Name = "value";
					SourceWriterExtensions.AddMethodBody (SetBody, property.Setter, opt, $"_members_{property.Setter.DeclaringType.JavaFullNameId}");
					property.Setter.Parameters [0].Name = pname;
				}
			}
		}

		public override void Write (CodeWriter writer)
		{
			if (opt.CodeGenerationTarget == CodeGenerationTarget.XAJavaInterop1) {
				getter_callback?.Write (writer);
				setter_callback?.Write (writer);
			}

			if (opt.EmitLegacyInterfaceInvokers) {
				if (property.Getter != null)
					writer.WriteLine ($"IntPtr {property.Getter.EscapedIdName};");

				if (property.Setter != null)
					writer.WriteLine ($"IntPtr {property.Setter.EscapedIdName};");
			}

			base.Write (writer);
		}

		protected override void WriteGetterBody (CodeWriter writer)
		{
			if (!opt.EmitLegacyInterfaceInvokers) {
				base.WriteGetterBody (writer);
				return;
			}
			SourceWriterExtensions.WriteMethodInvokerBodyLegacy (writer, property.Getter, opt, context_this);
		}

		protected override void WriteSetterBody (CodeWriter writer)
		{
			if (!opt.EmitLegacyInterfaceInvokers) {
				base.WriteSetterBody (writer);
				return;
			}
			var pname = property.Setter.Parameters [0].Name;
			property.Setter.Parameters [0].Name = "value";

			SourceWriterExtensions.WriteMethodInvokerBodyLegacy (writer, property.Setter, opt, context_this);

			property.Setter.Parameters [0].Name = pname;
		}
	}
}
