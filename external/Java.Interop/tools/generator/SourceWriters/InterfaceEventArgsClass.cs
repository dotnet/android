using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class InterfaceEventArgsClass : ClassWriter
	{
		public InterfaceEventArgsClass (InterfaceGen iface, Method method)
		{
			Name = iface.GetArgsName (method);
			Inherits = "global::System.EventArgs";

			IsPublic = true;
			IsPartial = true;

			Comments.Add ($"// event args for {iface.JavaName}.{method.JavaName}");

			// Add: public bool Handled { get; set; }
			if (method.IsEventHandlerWithHandledProperty)
				Properties.Add (new PropertyWriter {
					Name = "Handled",
					PropertyType = TypeReferenceWriter.Bool,
					IsPublic = true,
					HasGet = true,
					HasSet = true,
					IsAutoProperty = true
				});
		}

		public void AddMembersFromMethod (InterfaceGen iface, Method method, CodeGenerationOptions opt)
		{
			AddConstructor (iface, method, opt);
			AddProperties (method, opt);
		}

		void AddConstructor (InterfaceGen iface, Method method, CodeGenerationOptions opt)
		{
			var ctor = new ConstructorWriter {
				Name = iface.GetArgsName (method),
				IsPublic = true
			};

			if (method.IsEventHandlerWithHandledProperty) {
				ctor.Parameters.Add (new MethodParameterWriter ("handled", TypeReferenceWriter.Bool));
				ctor.Body.Add ("this.Handled = handled;");
			}

			foreach (var p in method.Parameters) {
				if (p.IsSender)
					continue;

				ctor.Parameters.Add (new MethodParameterWriter (p.Name, new TypeReferenceWriter (opt.GetTypeReferenceName (p))));
				ctor.Body.Add ($"this.{p.PropertyName} = {opt.GetSafeIdentifier (p.Name)};");
			}

			Constructors.Add (ctor);
		}

		void AddProperties (Method method, CodeGenerationOptions opt)
		{
			foreach (var p in method.Parameters) {
				if (p.IsSender)
					continue;

				// We've already added this property from a different overload
				if (Properties.Any (prop => prop.Name == p.PropertyName))
					continue;

				var prop = new PropertyWriter {
					Name = p.PropertyName,
					PropertyType = new TypeReferenceWriter (opt.GetTypeReferenceName (p)),
					IsPublic = true,
					HasGet = true,
					HasSet = true,
					IsAutoProperty = true,
					AutoSetterVisibility = Visibility.Private
				};

				Properties.Add (prop);
			}
		}
	}
}
