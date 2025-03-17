using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
using Java.Interop.Tools.JavaCallableWrappers.Extensions;

namespace Java.Interop.Tools.JavaCallableWrappers.Adapters;

public static class XmlExporter
{
	static XmlWriterSettings settings = new XmlWriterSettings {
		Indent = true,
		NewLineOnAttributes = false,
		OmitXmlDeclaration = true,
	};

	public static void Export (string filename, IEnumerable<CallableWrapperType> types, bool wasScanned)
	{
		using (var sw = new StreamWriter (filename, false, Encoding.UTF8))
			Export (sw, types, wasScanned);
	}

	public static void Export (TextWriter sw, IEnumerable<CallableWrapperType> types, bool wasScanned)
	{
		using (var xml = XmlWriter.Create (sw, settings))
			Export (xml, types, wasScanned);
	}

	public static void Export (XmlWriter xml, IEnumerable<CallableWrapperType> types, bool wasScanned)
	{
		ExportTypes (xml, types, wasScanned);
	}

	static void ExportTypes (XmlWriter xml, IEnumerable<CallableWrapperType> types, bool wasScanned)
	{
		xml.WriteStartElement ("types");
		xml.WriteAttributeString ("was_scanned", wasScanned.ToString ());

		foreach (var type in types)
			ExportType (xml, type);

		xml.WriteEndElement ();
	}

	public static void ExportType (XmlWriter xml, CallableWrapperType type)
	{
		xml.WriteStartElement ("type");
		xml.WriteAttributeString ("name", type.Name);
		xml.WriteAttributeString ("package", type.Package);
		xml.WriteAttributeStringIfNotFalse ("is_abstract", type.IsAbstract);
		xml.WriteAttributeStringIfNotNull ("application_java_class", type.ApplicationJavaClass);
		xml.WriteAttributeStringIfNotFalse ("generate_on_create_overrides", type.GenerateOnCreateOverrides);
		xml.WriteAttributeStringIfNotNull ("mono_runtime_initialization", type.MonoRuntimeInitialization);
		xml.WriteAttributeStringIfNotNull ("extends_type", type.ExtendsType);
		xml.WriteAttributeStringIfNotFalse ("is_application", type.IsApplication);
		xml.WriteAttributeStringIfNotFalse ("is_instrumentation", type.IsInstrumentation);
		xml.WriteAttributeString ("partial_assembly_qualified_name", type.PartialAssemblyQualifiedName);
		xml.WriteAttributeStringIfNotFalse ("has_export", type.HasExport);

		if (type.ApplicationConstructor is not null)
			xml.WriteAttributeString ("application_constructor", type.ApplicationConstructor.Name);

		ExportAnnotations (xml, type.Annotations);
		ExportImplementedInterfaces (xml, type.ImplementedInterfaces);
		ExportConstructors (xml, type.Constructors);
		ExportFields (xml, type.Fields);
		ExportMethods (xml, type.Methods);
		ExportNestedTypes (xml, type.NestedTypes);

		xml.WriteEndElement ();
	}

	static void ExportAnnotations (XmlWriter writer, IEnumerable<CallableWrapperTypeAnnotation> annotations)
	{
		if (annotations.Count () == 0)
			return;

		writer.WriteStartElement ("annotations");

		foreach (var annotation in annotations) {
			writer.WriteStartElement ("annotation");
			writer.WriteAttributeString ("name", annotation.Name);

			foreach (var property in annotation.Properties) {
				writer.WriteStartElement ("property");
				writer.WriteAttributeString ("name", property.Key);
				writer.WriteAttributeString ("value", property.Value);
				writer.WriteEndElement ();
			}

			writer.WriteEndElement ();
		}

		writer.WriteEndElement ();
	}

	static void ExportImplementedInterfaces (XmlWriter writer, List<string> interfaces)
	{
		if (interfaces.Count == 0)
			return;

		writer.WriteStartElement ("implemented_interfaces");

		foreach (var @interface in interfaces) {
			writer.WriteStartElement ("interface");
			writer.WriteAttributeString ("name", @interface);
			writer.WriteEndElement ();
		}

		writer.WriteEndElement ();
	}

	static void ExportConstructors (XmlWriter xml, IEnumerable<CallableWrapperConstructor> constructors)
	{
		if (constructors.Count () == 0)
			return;

		xml.WriteStartElement ("constructors");

		foreach (var constructor in constructors)
			ExportMethod (xml, constructor);

		xml.WriteEndElement ();
	}

	static void ExportFields (XmlWriter xml, IEnumerable<CallableWrapperField> fields)
	{
		if (fields.Count () == 0)
			return;

		xml.WriteStartElement ("fields");

		foreach (var field in fields) {
			xml.WriteStartElement ("field");
			xml.WriteAttributeString ("name", field.FieldName);
			xml.WriteAttributeString ("type", field.TypeName);
			xml.WriteAttributeString ("visibility", field.Visibility);
			xml.WriteAttributeStringIfNotFalse ("is_static", field.IsStatic);
			xml.WriteAttributeString ("initializer_name", field.InitializerName);
			ExportAnnotations (xml, field.Annotations);
			xml.WriteEndElement ();
		}

		xml.WriteEndElement ();
	}

	static void ExportMethods (XmlWriter xml, IEnumerable<CallableWrapperMethod> methods)
	{
		if (methods.Count () == 0)
			return;

		xml.WriteStartElement ("methods");

		foreach (var method in methods)
			ExportMethod (xml, method);

		xml.WriteEndElement ();
	}

	static void ExportMethod (XmlWriter xml, CallableWrapperMethod method)
	{
		xml.WriteStartElement (method is CallableWrapperConstructor ? "constructor" : "method");
		xml.WriteAttributeString ("name", method.Name);
		xml.WriteAttributeString ("method", method.Method);
		xml.WriteAttributeString ("jni_signature", method.JniSignature);
		xml.WriteAttributeStringIfNotNull ("managed_parameters", method.ManagedParameters);
		xml.WriteAttributeStringIfNotNull ("java_name_override", method.JavaNameOverride);
		xml.WriteAttributeStringIfNotNull ("params", method.Params);
		xml.WriteAttributeStringIfNotNull ("retval", method.Retval);
		xml.WriteAttributeStringIfNotNull ("java_access", method.JavaAccess);
		xml.WriteAttributeStringIfNotFalse ("is_export", method.IsExport);
		xml.WriteAttributeStringIfNotFalse ("is_static", method.IsStatic);
		xml.WriteAttributeStringIfNotFalse ("is_dynamically_registered", method.IsDynamicallyRegistered);
		xml.WriteAttributeStringIfNotNull ("thrown_type_names", method.ThrownTypeNames != null ? string.Join (", ", method.ThrownTypeNames) : null);
		xml.WriteAttributeStringIfNotNull ("super_call", method.SuperCall);
		xml.WriteAttributeStringIfNotNull ("activate_call", method.ActivateCall);

		ExportAnnotations (xml, method.Annotations);

		xml.WriteEndElement ();
	}

	static void ExportNestedTypes (XmlWriter xml, IEnumerable<CallableWrapperType> nestedTypes)
	{
		if (nestedTypes.Count () == 0)
			return;

		xml.WriteStartElement ("nested_types");

		foreach (var nestedType in nestedTypes)
			ExportType (xml, nestedType);

		xml.WriteEndElement ();
	}
}
