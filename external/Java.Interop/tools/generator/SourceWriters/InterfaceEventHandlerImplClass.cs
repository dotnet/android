using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class InterfaceEventHandlerImplClass : ClassWriter
	{
		public InterfaceEventHandlerImplClass (InterfaceGen iface, CodeGenerationOptions opt, CodeGeneratorContext context)
		{
			var jni_class = "mono/" + iface.RawJniName.Replace ('$', '_') + "Implementor";

			Name = iface.Name + "Implementor";
			Inherits = "global::Java.Lang.Object";
			Implements.Add (iface.Name);

			IsInternal = true;
			IsSealed = true;
			IsPartial = true;

			Attributes.Add (new RegisterAttr (jni_class, additionalProperties: iface.AdditionalAttributeString ()) { UseGlobal = true });

			if (iface.NeedsSender)
				Fields.Add (new FieldWriter { Name = "sender", Type = TypeReferenceWriter.Object });

			AddConstructor (iface, jni_class, opt);
			AddMethods (iface, opt);
		}

		void AddConstructor (InterfaceGen iface, string jniClass, CodeGenerationOptions opt)
		{
			var ctor = new ConstructorWriter {
				Name = iface.Name + "Implementor",
				IsPublic = true
			};

			if (iface.NeedsSender)
				ctor.Parameters.Add (new MethodParameterWriter ("sender", TypeReferenceWriter.Object));

			ctor.BaseCall = $"base (global::Android.Runtime.JNIEnv.StartCreateInstance (\"{jniClass}\", \"()V\"), JniHandleOwnership.TransferLocalRef)";

			ctor.Body.Add ($"global::Android.Runtime.JNIEnv.FinishCreateInstance ({iface.GetObjectHandleProperty (opt, "this")}, \"()V\");");

			if (iface.NeedsSender)
				ctor.Body.Add ("this.sender = sender;");

			Constructors.Add (ctor);
		}

		void AddMethods (InterfaceGen iface, CodeGenerationOptions opt)
		{
			var handlers = new List<string> ();

			foreach (var m in iface.Methods)
				Methods.Add (new InterfaceEventHandlerImplMethod (iface, m, handlers, opt));

			var is_empty_method = new MethodWriter {
				Name = "__IsEmpty",
				IsInternal = true,
				IsStatic = true,
				ReturnType = TypeReferenceWriter.Bool
			};

			is_empty_method.Parameters.Add (new MethodParameterWriter ("value", new TypeReferenceWriter (iface.Name + "Implementor")));

			if (!iface.Methods.Any (m => m.EventName != string.Empty) || handlers.Count == 0)
				is_empty_method.Body.Add ("return true;");
			else
				is_empty_method.Body.Add ($"return {string.Join (" && ", handlers.Select (e => string.Format ("value.{0}Handler == null", e)))};");

			Methods.Add (is_empty_method);
		}
	}

	public class InterfaceEventHandlerImplMethod : MethodWriter
	{
		readonly InterfaceGen iface;
		readonly Method method;
		readonly CodeGenerationOptions opt;
		readonly bool needs_sender;
		readonly string method_spec;
		readonly string args_name;

		public InterfaceEventHandlerImplMethod (InterfaceGen iface, Method method, List<string> handlers, CodeGenerationOptions opt)
		{
			this.iface = iface;
			this.method = method;
			this.opt = opt;
			needs_sender = iface.NeedsSender;

			method_spec = iface.Methods.Count > 1 ? method.AdjustedName : string.Empty;
			args_name = iface.GetArgsName (method);

			handlers.Add (method_spec);

			Name = method.Name;
			ReturnType = new TypeReferenceWriter (opt.GetTypeReferenceName (method.RetVal));

			IsPublic = true;

			this.AddMethodParameters (method.Parameters, opt);
		}

		protected override void WriteBody (CodeWriter writer)
		{
			// generate nothing
			if (method.EventName == string.Empty)
				return;

			if (method.IsVoid) {
				writer.WriteLine ($"var __h = {method_spec}Handler;");
				writer.WriteLine ($"if (__h != null)");
				writer.WriteLine ($"\t__h ({(needs_sender ? "sender" : method.Parameters.SenderName)}, new {args_name} ({method.Parameters.CallDropSender}));");
				return;
			}

			if (method.IsEventHandlerWithHandledProperty) {
				writer.WriteLine ($"var __h = {method_spec}Handler;");
				writer.WriteLine ($"if (__h == null)");
				writer.WriteLine ($"\treturn {method.RetVal.DefaultValue};");

				var call = method.Parameters.CallDropSender;
				writer.WriteLine ($"var __e = new {args_name} (true{(call.Length != 0 ? ", " : "")}{call});");
				writer.WriteLine ($"__h ({(needs_sender ? "sender" : method.Parameters.SenderName)}, __e);");
				writer.WriteLine ($"return __e.Handled;");
				return;
			}

			writer.WriteLine ($"var __h = {method_spec}Handler;");
			writer.WriteLine ($"return __h != null ? __h ({method.Parameters.GetCall (opt)}) : default ({opt.GetTypeReferenceName (method.RetVal)});");
		}

		public override void Write (CodeWriter writer)
		{
			if (method.EventName != string.Empty) {
				writer.WriteLine ("#pragma warning disable 0649");
				writer.WriteLine ($"public {iface.GetEventDelegateName (method)}{opt.NullableOperator} {method_spec}Handler;");
				writer.WriteLine ("#pragma warning restore 0649");
				writer.WriteLine ();
			}

			base.Write (writer);
		}
	}
}
