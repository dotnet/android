using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Java.Interop.Tools.JavaCallableWrappers.CallableWrapperMembers;
using Java.Interop.Tools.JavaCallableWrappers.Extensions;
using Java.Interop.Tools.TypeNameMappings;

namespace Java.Interop.Tools.JavaCallableWrappers.Adapters;

public static class XmlImporter
{
	public static List<CallableWrapperType> Import (string filename)
	{
		using (var sr = new StreamReader (filename, Encoding.UTF8))
			return Import (sr);
	}

	public static List<CallableWrapperType> Import (TextReader sr)
	{
		using (var xml = XmlReader.Create (sr))
			return Import (xml);
	}

	public static List<CallableWrapperType> Import (XmlReader xml)
	{
		var doc = XDocument.Load (xml);

		var types = new List<CallableWrapperType> ();

		foreach (var type in doc.Root.Elements ("type"))
			types.Add (ImportType (type));

		return types;
	}


	public static List<CallableWrapperType> Import (XElement xml)
	{
		var types = new List<CallableWrapperType> ();

		foreach (var type in xml.Elements ("type"))
			types.Add (ImportType (type));

		return types;
	}

	public static CallableWrapperType ImportType (XElement xml)
	{
		var name = xml.GetRequiredAttribute ("name");
		var package = xml.GetAttributeOrDefault ("package", (string?) "");
		var partial_assembly_qualified_name = xml.GetRequiredAttribute ("partial_assembly_qualified_name");

		// If package is empty, generate a crc64 package name from the assembly qualified name
		if (string.IsNullOrEmpty (package))
			package = GeneratePackageFromAssemblyQualifiedName (partial_assembly_qualified_name);

		var type = new CallableWrapperType (name, package ?? "", partial_assembly_qualified_name) {
			ApplicationJavaClass = xml.GetAttributeOrDefault ("application_java_class", (string?) null),
			ExtendsType = xml.GetAttributeOrDefault ("extends_type", (string?) null),
			GenerateOnCreateOverrides = xml.GetAttributeOrDefault ("generate_on_create_overrides", false),
			HasExport = xml.GetAttributeOrDefault ("has_export", false),
			IsAbstract = xml.GetAttributeOrDefault ("is_abstract", false),
			IsApplication = xml.GetAttributeOrDefault ("is_application", false),
			IsInstrumentation = xml.GetAttributeOrDefault ("is_instrumentation", false),
			MonoRuntimeInitialization = xml.GetAttributeOrDefault ("mono_runtime_initialization", (string?) null),
		};

		if (xml.GetAttributeOrDefault ("application_constructor", (string?) null) is string applicationConstructor)
			type.ApplicationConstructor = new CallableWrapperApplicationConstructor (applicationConstructor);

		ImportAnnotations (type.Annotations, xml.Element ("annotations"));
		ImportImplementedInterfaces (type, xml.Element ("implemented_interfaces"));
		ImportConstructors (type, xml.Element ("constructors"));
		ImportMethods (type, xml.Element ("methods"));
		ImportFields (type, xml.Element ("fields"));

		foreach (var nestedType in xml.Elements ("nested_type"))
			type.NestedTypes.Add (ImportType (nestedType));

		return type;
	}

	static void ImportAnnotations (List<CallableWrapperTypeAnnotation> annotations, XElement? xml)
	{
		foreach (var annotation in xml?.Elements ("annotation") ?? []) {
			var a = ImportAnnotation (annotation);
			annotations.Add (a);
		}
	}

	static CallableWrapperTypeAnnotation ImportAnnotation (XElement xml)
	{
		var name = xml.GetRequiredAttribute ("name");
		var annotation = new CallableWrapperTypeAnnotation (name);

		foreach (var property in xml.Elements ("property")) {
			var p = ImportAnnotationProperty (property);
			annotation.Properties.Add (p);
		}

		return annotation;
	}

	static KeyValuePair<string, string> ImportAnnotationProperty (XElement xml)
	{
		var name = xml.GetRequiredAttribute ("name");
		var value = xml.GetRequiredAttribute ("value");

		return new KeyValuePair<string, string> (name, value);
	}

	static void ImportImplementedInterfaces (CallableWrapperType type, XElement? xml)
	{
		foreach (var iface in xml?.Elements ("interface") ?? []) {
			var name = iface.GetRequiredAttribute ("name");
			type.ImplementedInterfaces.Add (name);
		}
	}

	static void ImportConstructors (CallableWrapperType type, XElement? xml)
	{
		foreach (var ctor in xml?.Elements ("constructor") ?? []) {
			var c = ImportConstructor (type, ctor);
			type.Constructors.Add (c);
		}
	}

	static void ImportMethods (CallableWrapperType type, XElement? xml)
	{
		foreach (var method in xml?.Elements ("method") ?? []) {
			var m = ImportMethod (type, method);
			type.Methods.Add (m);
		}
	}

	static CallableWrapperConstructor ImportConstructor (CallableWrapperType type, XElement xml)
	{
		var name = xml.GetRequiredAttribute ("name");
		var method = xml.GetRequiredAttribute ("method");
		var jniSig = xml.GetRequiredAttribute ("jni_signature");

		var ctor = new CallableWrapperConstructor (type, name, method, jniSig);
		FillInMethodDetails (ctor, xml);

		return ctor;
	}

	static CallableWrapperMethod ImportMethod (CallableWrapperType type, XElement xml)
	{
		var name = xml.GetRequiredAttribute ("name");
		var method = xml.GetRequiredAttribute ("method");
		var jniSig = xml.GetRequiredAttribute ("jni_signature");

		var m = new CallableWrapperMethod (type, name, method, jniSig);
		FillInMethodDetails (m, xml);

		return m;
	}

	static void FillInMethodDetails (CallableWrapperMethod method, XElement xml)
	{
		// Common between constructors and methods
		method.ManagedParameters = xml.GetAttributeOrDefault ("managed_parameters", (string?) null);
		method.JavaNameOverride = xml.GetAttributeOrDefault ("java_name_override", (string?) null);
		method.Params = xml.GetAttributeOrDefault ("params", (string?) null);
		method.Retval = xml.GetAttributeOrDefault ("retval", (string?) null);
		method.JavaAccess = xml.GetAttributeOrDefault ("java_access", (string?) null);
		method.IsExport = xml.GetAttributeOrDefault ("is_export", false);
		method.IsStatic = xml.GetAttributeOrDefault ("is_static", false);
		method.IsDynamicallyRegistered = xml.GetAttributeOrDefault ("is_dynamically_registered", false);
		method.SuperCall = xml.GetAttributeOrDefault ("super_call", (string?) null);
		method.ActivateCall = xml.GetAttributeOrDefault ("activate_call", (string?) null);

		if (xml.GetAttributeOrDefault ("thrown_type_names", (string?) null) is string thrownTypeNames)
			method.ThrownTypeNames = thrownTypeNames.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);

		ImportAnnotations (method.Annotations, xml.Element ("annotations"));
	}

	static void ImportFields (CallableWrapperType type, XElement? xml)
	{
		foreach (var field in xml?.Elements ("field") ?? []) {
			var f = ImportField (field);
			type.Fields.Add (f);
		}
	}

	static CallableWrapperField ImportField (XElement xml)
	{
		var name = xml.GetRequiredAttribute ("name");
		var type = xml.GetRequiredAttribute ("type");
		var visibility = xml.GetRequiredAttribute ("visibility");
		var initializer_name = xml.GetRequiredAttribute ("initializer_name");

		var field = new CallableWrapperField (name, type, visibility, initializer_name) {
			IsStatic = xml.GetAttributeOrDefault ("is_static", false),
		};

		ImportAnnotations (field.Annotations, xml.Element ("annotations"));

		return field;
	}

	/// <summary>
	/// Generates a package name from a partial assembly qualified name.
	/// This is used when the package attribute is missing or empty in the XML.
	/// </summary>
	/// <param name="partialAssemblyQualifiedName">The partial assembly qualified name in format "Namespace.TypeName, AssemblyName"</param>
	/// <returns>A package name based on the current <see cref="JavaNativeTypeManager.PackageNamingPolicy"/></returns>
	static string GeneratePackageFromAssemblyQualifiedName (string partialAssemblyQualifiedName)
	{
		// Format: "Namespace.TypeName, AssemblyName" or "Namespace.TypeName+NestedType, AssemblyName"
		var commaIndex = partialAssemblyQualifiedName.IndexOf (',');
		if (commaIndex < 0)
			return "";

		var fullTypeName = partialAssemblyQualifiedName.Substring (0, commaIndex).Trim ();
		var assemblyName = partialAssemblyQualifiedName.Substring (commaIndex + 1).Trim ();

		// Extract namespace from full type name
		// Handle nested types by using '+' as separator
		var plusIndex = fullTypeName.IndexOf ('+');
		var typeNameWithoutNested = plusIndex >= 0 ? fullTypeName.Substring (0, plusIndex) : fullTypeName;

		var lastDotIndex = typeNameWithoutNested.LastIndexOf ('.');
		var ns = lastDotIndex >= 0 ? typeNameWithoutNested.Substring (0, lastDotIndex) : "";

		// If no namespace, return empty (will use default package)
		if (string.IsNullOrEmpty (ns))
			return "";

		return JavaNativeTypeManager.GetPackageName (ns, assemblyName);
	}
}
