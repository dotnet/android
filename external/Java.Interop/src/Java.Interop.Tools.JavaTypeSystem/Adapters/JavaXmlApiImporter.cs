using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Java.Interop.Tools.JavaTypeSystem.Models;

namespace Java.Interop.Tools.JavaTypeSystem
{
	public class JavaXmlApiImporter
	{
		public static JavaTypeCollection ParseString (string xml, JavaTypeCollection? collection = null)
		{
			var doc = XDocument.Parse (xml, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

			return Parse (doc, collection);
		}

		public static JavaTypeCollection Parse (TextReader reader, JavaTypeCollection? collection = null)
		{
			var doc = XDocument.Load (reader, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

			return Parse (doc, collection);
		}

		public static JavaTypeCollection Parse (string filename, JavaTypeCollection? collection = null)
		{
			var doc = XDocument.Load (filename, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);

			return Parse (doc, collection);
		}

		static JavaTypeCollection Parse (XDocument doc, JavaTypeCollection? collection = null)
		{
			collection ??= new JavaTypeCollection ();

			var root = doc.Root;

			if (root is null)
				throw new ArgumentException ("Invalid XML file doesn't contain a root node");

			collection.ApiSource = root.XGetAttributeOrNull ("api-source");
			collection.Platform = root.XGetAttributeOrNull ("platform");

			var packages = new List<JavaPackage> ();

			foreach (var elem in root.Elements ()) {
				switch (elem.Name.LocalName) {
					case "package":
						packages.Add (ParsePackage (elem, collection));
						break;
				}
			}

			// First add all non-nested types
			foreach (var type in packages.SelectMany (p => p.Types).Where (t => !t.NestedName.Contains ('.')))
				collection.AddType (type);

			// Add all nested types
			// This needs to be done ordered from least nested to most nested, in order for nesting to work.
			// That is, 'android.foo.Blah' needs to be added before 'android.foo.Blah.Bar'.
			foreach (var type in packages.SelectMany (p => p.Types).Where (t => t.NestedName.Contains ('.')).OrderBy (t => t.FullName.Count (c => c == '.')).ToArray ()) {
				collection.AddType (type);

				// Remove nested types from Package. In this model, Package only contains top-level types, which then contain nested types.
				type.Package.Types.Remove (type);
			}

			return collection;
		}

		public static JavaPackage ParsePackage (XElement package, JavaTypeCollection collection)
		{
			var pkg = collection.AddPackage (
				name: package.XGetAttribute ("name"),
				jniName: package.XGetAttribute ("jni-name"),
				managedName: package.XGetAttributeOrNull ("managedName")
			);

			if (package.XGetAttribute ("merge.SourceFile") is string source && source.HasValue ())
				pkg.PropertyBag.Add ("merge.SourceFile", source);

			foreach (var elem in package.Elements ()) {
				switch (elem.Name.LocalName) {
					case "class":
						if (elem.XGetAttributeAsBool ("obfuscated"))
							continue;

						pkg.Types.Add (ParseClass (pkg, elem));
						break;
					case "interface":
						if (elem.XGetAttributeAsBool ("obfuscated"))
							continue;

						pkg.Types.Add (ParseInterface (pkg, elem));
						break;
				}
			}

			return pkg;
		}

		public static JavaClassModel ParseClass (JavaPackage package, XElement element)
		{
			var model = new JavaClassModel (
				javaPackage: package,
				javaNestedName: element.XGetAttribute ("name"),
				javaVisibility: element.XGetAttribute ("visibility"),
				javaAbstract: element.XGetAttributeAsBool ("abstract"),
				javaFinal: element.XGetAttributeAsBool ("final"),
				javaBaseType: element.XGetAttribute ("extends"),
				javaBaseTypeGeneric: element.XGetAttribute ("extends-generic-aware"),
				javaDeprecated: element.XGetAttribute ("deprecated"),
				javaStatic: element.XGetAttributeAsBool ("static"),
				jniSignature: element.XGetAttribute ("jni-signature"),
				baseTypeJni: element.XGetAttribute ("jni-extends")
			);

			if (element.XGetAttribute ("merge.SourceFile") is string source && source.HasValue ())
				model.PropertyBag.Add ("merge.SourceFile", source);
			if (element.XGetAttribute ("deprecated-since") is string dep && dep.HasValue ())
				model.PropertyBag.Add ("deprecated-since", dep);

			if (element.Element ("typeParameters") is XElement tp)
				ParseTypeParameters (model.TypeParameters, tp);

			foreach (var child in element.Elements ()) {
				switch (child.Name.LocalName) {
					case "constructor":
						model.Constructors.Add (ParseConstructor (model, child));
						break;
					case "field":
						model.Fields.Add (ParseField (model, child));
						break;
					case "implements":
						model.Implements.Add (ParseImplements (child));
						break;
					case "method":
						model.Methods.Add (ParseMethod (model, child));
						break;
				}
			}

			return model;
		}

		public static JavaInterfaceModel ParseInterface (JavaPackage package, XElement element)
		{
			var nested_name = element.XGetAttribute ("name");
			var visibility = element.XGetAttribute ("visibility");
			var deprecated = element.XGetAttribute ("deprecated");
			var is_static = element.XGetAttribute ("static") == "true";
			var jni_signature = element.XGetAttribute ("jni-signature");

			var model = new JavaInterfaceModel (package, nested_name, visibility, deprecated, is_static, jni_signature);

			if (element.XGetAttribute ("merge.SourceFile") is string source && source.HasValue ())
				model.PropertyBag.Add ("merge.SourceFile", source);
			if (element.XGetAttribute ("deprecated-since") is string dep && dep.HasValue ())
				model.PropertyBag.Add ("deprecated-since", dep);

			if (element.Element ("typeParameters") is XElement tp)
				ParseTypeParameters (model.TypeParameters, tp);

			foreach (var child in element.Elements ()) {
				switch (child.Name.LocalName) {
					case "field":
						model.Fields.Add (ParseField (model, child));
						break;
					case "implements":
						model.Implements.Add (ParseImplements (child));
						break;
					case "method":
						if (child.XGetAttribute ("synthetic") != "true")
							model.Methods.Add (ParseMethod (model, child));
						break;
				}
			}

			return model;
		}

		public static JavaMethodModel ParseMethod (JavaTypeModel type, XElement element)
		{
			var method = new JavaMethodModel (
				javaName: element.XGetAttribute ("name"),
				javaVisibility: element.XGetAttribute ("visibility"),
				javaAbstract: element.XGetAttributeAsBool ("abstract"),
				javaFinal: element.XGetAttributeAsBool ("final"),
				javaStatic: element.XGetAttributeAsBool ("static"),
				javaReturn: element.XGetAttribute ("return"),
				javaDeclaringType: type,
				deprecated: element.XGetAttribute ("deprecated"),
				jniSignature: element.XGetAttribute ("jni-signature"),
				isSynthetic: element.XGetAttributeAsBool ("synthetic"),
				isBridge: element.XGetAttributeAsBool ("bridge"),
				returnJni: element.XGetAttribute ("jni-return"),
				isNative: element.XGetAttributeAsBool ("native"),
				isSynchronized: element.XGetAttributeAsBool ("synchronized"),
				returnNotNull: element.XGetAttributeAsBool ("return-not-null")
			);

			if (element.Element ("typeParameters") is XElement tp)
				ParseTypeParameters (method.TypeParameters, tp);

			foreach (var child in element.Elements ("parameter"))
				method.Parameters.Add (ParseParameter (method, child));
			foreach (var child in element.Elements ("exception"))
				method.Exceptions.Add (ParseException (child));

			if (element.XGetAttribute ("merge.SourceFile") is string source && source.HasValue ())
				method.PropertyBag.Add ("merge.SourceFile", source);
			if (element.XGetAttribute ("deprecated-since") is string dep && dep.HasValue ())
				method.PropertyBag.Add ("deprecated-since", dep);

			return method;
		}

		public static JavaConstructorModel ParseConstructor (JavaTypeModel type, XElement element)
		{
			var method = new JavaConstructorModel (
				javaName: element.XGetAttribute ("name"),
				javaVisibility: element.XGetAttribute ("visibility"),
				javaStatic: element.XGetAttributeAsBool ("static"),
				javaDeclaringType: type,
				deprecated: element.XGetAttribute ("deprecated"),
				jniSignature: element.XGetAttribute ("jni-signature"),
				isSynthetic: element.XGetAttributeAsBool ("synthetic"),
				isBridge: element.XGetAttributeAsBool ("bridge")
			);

			foreach (var child in element.Elements ("exception"))
				method.Exceptions.Add (ParseException (child));

			foreach (var child in element.Elements ("parameter"))
				method.Parameters.Add (ParseParameter (method, child));

			if (element.XGetAttribute ("merge.SourceFile") is string source && source.HasValue ())
				method.PropertyBag.Add ("merge.SourceFile", source);
			if (element.XGetAttribute ("deprecated-since") is string dep && dep.HasValue ())
				method.PropertyBag.Add ("deprecated-since", dep);

			return method;
		}

		public static JavaFieldModel ParseField (JavaTypeModel type, XElement element)
		{
			var field = new JavaFieldModel (
				name: element.XGetAttribute ("name"),
				visibility: element.XGetAttribute ("visibility"),
				type: element.XGetAttribute ("type"),
				typeGeneric: element.XGetAttribute ("type-generic-aware"),
				isStatic: element.XGetAttributeAsBool ("static"),
				value: element.Attribute ("value")?.Value,
				declaringType: type,
				isFinal: element.XGetAttributeAsBool ("final"),
				deprecated: element.XGetAttribute ("deprecated"),
				jniSignature: element.XGetAttribute ("jni-signature"),
				isTransient: element.XGetAttributeAsBool ("transient"),
				isVolatile: element.XGetAttributeAsBool ("volatile"),
				isNotNull: element.XGetAttributeAsBool ("not-null")
			);

			if (element.XGetAttribute ("merge.SourceFile") is string source && source.HasValue ())
				field.PropertyBag.Add ("merge.SourceFile", source);
			if (element.XGetAttribute ("deprecated-since") is string dep && dep.HasValue ())
				field.PropertyBag.Add ("deprecated-since", dep);

			return field;
		}

		public static JavaImplementsModel ParseImplements (XElement element)
		{
			var model = new JavaImplementsModel (
				name: element.XGetAttribute ("name"),
				nameGeneric: element.XGetAttribute ("name-generic-aware"),
				jniType: element.XGetAttribute ("jni-type")
			);

			if (element.XGetAttribute ("merge.SourceFile") is string source && source.HasValue ())
				model.PropertyBag.Add ("merge.SourceFile", source);

			return model;
		}

		public static JavaExceptionModel ParseException (XElement element)
		{
			return new JavaExceptionModel (
				name: element.XGetAttribute ("name"),
				type: element.XGetAttribute ("type-generic-aware")
			);
		}

		public static JavaParameterModel ParseParameter (JavaMethodModel method, XElement element)
		{
			var parameter = new JavaParameterModel (
				declaringMethod: method,
				javaName: element.XGetAttribute ("name"),
				javaType: element.XGetAttribute ("type"),
				jniType: element.XGetAttribute ("jni-type"),
				isNotNull: element.XGetAttributeAsBool ("not-null")
			);

			return parameter;
		}

		public static void ParseTypeParameters (JavaTypeParameters parameters, XElement element)
		{
			foreach (var elem in element.Elements ()) {
				if (elem.Name.LocalName == "typeParameter")
					ParseTypeParameter (parameters, elem);
			}

			if (element.XGetAttribute ("merge.SourceFile") is string source && source.HasValue ())
				parameters.PropertyBag.Add ("merge.SourceFile", source);
		}

		public static void ParseTypeParameter (JavaTypeParameters parameters, XElement element)
		{
			var parameter = new JavaTypeParameter (element.XGetAttribute ("name"), parameters) {
				ExtendedJniClassBound = element.XGetAttribute ("jni-classBound"),
				ExtendedClassBound = element.XGetAttribute ("classBound"),
				ExtendedInterfaceBounds = element.XGetAttribute ("interfaceBounds"),
				ExtendedJniInterfaceBounds = element.XGetAttribute ("jni-interfaceBounds")
			};

			parameters.Add (parameter);

			if (element.Element ("genericConstraints") is XElement gc) {
				parameter.GenericConstraints.AddRange (ParseGenericConstraints (gc));
				return;
			}

			// Now we have to deal with the format difference...
			// Some versions of class-parse stopped generating <genericConstraints> but started
			// generating "classBound" and "interfaceBounds" attributes instead.
			// They don't make sense and blocking this effort, but we have to deal with that...
			if (!string.IsNullOrEmpty (parameter.ExtendedClassBound) || !string.IsNullOrEmpty (parameter.ExtendedInterfaceBounds)) {
				if (!string.IsNullOrEmpty (parameter.ExtendedClassBound))
					parameter.GenericConstraints.Add (new JavaGenericConstraint (parameter.ExtendedClassBound));
				if (!string.IsNullOrEmpty (parameter.ExtendedInterfaceBounds))
					foreach (var ic in parameter.ExtendedInterfaceBounds.Split (':'))
						parameter.GenericConstraints.Add (new JavaGenericConstraint (ic));
			}
		}

		public static List<JavaGenericConstraint> ParseGenericConstraints (XElement element)
		{
			var list = new List<JavaGenericConstraint> ();

			foreach (var elem in element.Elements ()) {
				if (elem.Name.LocalName == "genericConstraint")
					list.Add (ParseGenericConstraint (elem));
			}

			return list;
		}

		public static JavaGenericConstraint ParseGenericConstraint (XElement element)
		{
			return new JavaGenericConstraint (
				element.XGetAttribute ("type")
			);
		}
	}
}
