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

		public InterfaceListenerEvent (InterfaceGen iface, string name, string nameSpec, string fullDelegateName, string wrefSuffix, string add, string remove, bool hasHandlerArgument, CodeGenerationOptions opt)
		{
			Name = name;
			EventType = new TypeReferenceWriter (opt.GetOutputName (fullDelegateName));

			IsPublic = true;

			HasAdd = true;

			AddBody.Add ($"global::Java.Interop.EventHelper.AddEventHandler<{opt.GetOutputName (iface.FullName)}, {opt.GetOutputName (iface.FullName)}Implementor>(");
			AddBody.Add ($"ref weak_implementor_{wrefSuffix},");
			AddBody.Add ($"__Create{iface.Name}Implementor,");
			AddBody.Add ($"{add + (hasHandlerArgument ? "_Event_With_Handler_Helper" : null)},");
			AddBody.Add ($"__h => __h.{nameSpec}Handler += value);");

			HasRemove = true;

			RemoveBody.Add ($"global::Java.Interop.EventHelper.RemoveEventHandler<{opt.GetOutputName (iface.FullName)}, {opt.GetOutputName (iface.FullName)}Implementor>(");
			RemoveBody.Add ($"ref weak_implementor_{wrefSuffix},");
			RemoveBody.Add ($"{opt.GetOutputName (iface.FullName)}Implementor.__IsEmpty,");
			RemoveBody.Add ($"{remove},");
			RemoveBody.Add ($"__h => __h.{nameSpec}Handler -= value);");

			if (hasHandlerArgument)
				helper_method = new InterfaceListenerEventHandlerHelper (iface, add, opt);
		}

		public override void Write (CodeWriter writer)
		{
			base.Write (writer);

			helper_method?.Write (writer);
		}
	}

	public class InterfaceListenerEventHandlerHelper : MethodWriter
	{
		public InterfaceListenerEventHandlerHelper (InterfaceGen iface, string add, CodeGenerationOptions opt)
		{
			Name = add + "_Event_With_Handler_Helper";
			Parameters.Add (new MethodParameterWriter ("value", new TypeReferenceWriter (opt.GetOutputName (iface.FullName))));
			ReturnType = TypeReferenceWriter.Void;

			Body.Add ($"{add} (value, null);");
		}
	}
}
