using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoDroid.Generation;
using Xamarin.SourceWriter;

namespace generator.SourceWriters
{
	public class InterfaceListenerEvent : EventWriter
	{
		readonly InterfaceListenerEventHandlerHelper helper_method;

		public InterfaceListenerEvent (InterfaceGen iface, Method method, string name, string nameSpec, string fullDelegateName, string wrefSuffix, string add, string remove, Parameter handlerParameter, CodeGenerationOptions opt)
		{
			JavaProjectionWarnings.AddListenerEventSuppressions (this, iface, method, opt);

			Name = name;
			EventType = new TypeReferenceWriter (opt.GetOutputName (fullDelegateName));

			IsPublic = true;

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			HasAdd = true;

			AddBody.Add ($"global::Java.Interop.EventHelper.AddEventHandler<{opt.GetOutputName (iface.FullName)}, {opt.GetOutputName (iface.FullName)}Implementor>(");
			AddBody.Add ($"ref weak_implementor_{wrefSuffix},");
			AddBody.Add ($"__Create{iface.Name}Implementor,");
			AddBody.Add ($"{add + (handlerParameter != null ? "_Event_With_Handler_Helper" : null)},");
			AddBody.Add ($"__h => __h.{nameSpec}Handler += value);");

			HasRemove = true;

			RemoveBody.Add ($"global::Java.Interop.EventHelper.RemoveEventHandler<{opt.GetOutputName (iface.FullName)}, {opt.GetOutputName (iface.FullName)}Implementor>(");
			RemoveBody.Add ($"ref weak_implementor_{wrefSuffix},");
			RemoveBody.Add ($"{opt.GetOutputName (iface.FullName)}Implementor.__IsEmpty,");
			RemoveBody.Add ($"{remove},");
			RemoveBody.Add ($"__h => __h.{nameSpec}Handler -= value);");

			if (handlerParameter != null)
				helper_method = new InterfaceListenerEventHandlerHelper (iface, method, add, handlerParameter, opt);
		}

		public override void Write (CodeWriter writer)
		{
			base.Write (writer);

			helper_method?.Write (writer);
		}
	}

	public class InterfaceListenerEventHandlerHelper : MethodWriter
	{
		public InterfaceListenerEventHandlerHelper (InterfaceGen iface, Method method, string add, Parameter handlerParameter, CodeGenerationOptions opt)
		{
			JavaProjectionWarnings.AddListenerEventSuppressions (this, iface, method, opt);

			Name = add + "_Event_With_Handler_Helper";
			Parameters.Add (new MethodParameterWriter ("value", new TypeReferenceWriter (opt.GetOutputName (iface.FullName))));
			ReturnType = TypeReferenceWriter.Void;

			SourceWriterExtensions.AddSupportedOSPlatform (Attributes, method, opt);

			// The helper registers the listener without a `Handler`, which the Java API allows
			// even when the parameter is not annotated as nullable.
			JavaProjectionWarnings.AddNoHandlerArgumentSuppression (this, handlerParameter, opt);

			Body.Add ($"{add} (value, null);");
		}
	}
}
