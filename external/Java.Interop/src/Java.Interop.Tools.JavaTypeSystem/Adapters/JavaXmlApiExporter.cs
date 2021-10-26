using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Java.Interop.Tools.JavaTypeSystem.Models;

namespace Java.Interop.Tools.JavaTypeSystem
{
	public static class JavaXmlApiExporter
	{
		public static void Save (JavaTypeCollection types, string xmlFile)
		{
			using (var writer = XmlWriter.Create (xmlFile, new XmlWriterSettings {
				Encoding = new UTF8Encoding (false, true),
				Indent = true,
				OmitXmlDeclaration = true,
			}))
				Save (types, writer);
		}

		public static void Save (JavaTypeCollection types, XmlWriter writer)
		{
			writer.WriteStartElement ("api");

			if (types.Platform.HasValue ())
				writer.WriteAttributeString ("platform", types.Platform);

			writer.WriteAttributeString ("api-source", "JavaTypeSystem");

			foreach (var pkg in types.Packages.Values) {

				if (!pkg.Types.Any (t => !t.IsReferencedOnly))
					continue;

				writer.WriteStartElement ("package");
				writer.WriteAttributeString ("name", pkg.Name);

				if (pkg.PropertyBag.TryGetValue ("merge.SourceFile", out var source))
					writer.WriteAttributeString ("merge.SourceFile", source);

				if (!string.IsNullOrEmpty (pkg.JniName))
					writer.WriteAttributeString ("jni-name", pkg.JniName);

				foreach (var type in pkg.Types) {
					if (type.IsReferencedOnly)
						continue; // skip reference only types

					SaveType (type, writer);
				}

				WriteFullEndElement (writer);
			}

			WriteFullEndElement (writer);
		}

		static void SaveType (JavaTypeModel type, XmlWriter writer)
		{
			if (type is JavaClassModel cls)
				SaveType (type, writer, "class", XmlConvert.ToString (cls.IsAbstract), cls.BaseType, cls.BaseTypeGeneric, cls.BaseTypeJni);
			else
				SaveType (type, writer, "interface", "true", null, null, null);

			foreach (var nested in type.NestedTypes)
				SaveType (nested, writer);
		}

		static void SaveType (JavaTypeModel cls, XmlWriter writer, string elementName, string abs, string? ext, string? extgen, string? jniExt)
		{
			writer.WriteStartElement (elementName);

			writer.WriteAttributeStringIfValue ("abstract", abs);
			writer.WriteAttributeString ("deprecated", cls.Deprecated);
			writer.WriteAttributeStringIfValue ("extends", ext);
			writer.WriteAttributeStringIfValue ("extends-generic-aware", extgen);
			writer.WriteAttributeStringIfValue ("jni-extends", jniExt);
			writer.WriteAttributeString ("final", XmlConvert.ToString (cls.IsFinal));
			writer.WriteAttributeString ("name", cls.NestedName);
			writer.WriteAttributeString ("static", XmlConvert.ToString (cls.IsStatic));
			writer.WriteAttributeString ("visibility", cls.Visibility);
			writer.WriteAttributeStringIfValue ("jni-signature", cls.ExtendedJniSignature);

			if (cls.PropertyBag.TryGetValue ("merge.SourceFile", out var source))
				writer.WriteAttributeString ("merge.SourceFile", source);
			if (cls.PropertyBag.TryGetValue ("deprecated-since", out var dep))
				writer.WriteAttributeString ("deprecated-since", dep);

			SaveTypeParameters (cls.TypeParameters, writer);

			foreach (var imp in cls.Implements.OrderBy (i => i.Name, StringComparer.Ordinal)) {
				writer.WriteStartElement ("implements");
				writer.WriteAttributeString ("name", imp.Name);
				writer.WriteAttributeString ("name-generic-aware", imp.NameGeneric);
				writer.WriteAttributeStringIfValue ("jni-type", imp.JniType);

				if (imp.PropertyBag.TryGetValue ("merge.SourceFile", out var imp_source))
					writer.WriteAttributeString ("merge.SourceFile", imp_source);

				WriteFullEndElement (writer);
			}

			if (cls is JavaClassModel klass)
				foreach (var m in klass.Constructors.OrderBy (m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy (m => string.Join (", ", m.Parameters.Select (p => p.Type))).ThenBy (m => m.IsSynthetic))
					SaveConstructor (m, writer);

			foreach (var m in cls.Methods.OrderBy (m => m.Name, StringComparer.Ordinal).ThenBy (m => string.Join (", ", m.Parameters.Select (p => p.Type))).ThenBy (m => m.IsSynthetic))
				SaveMethod (m, writer);

			foreach (var m in cls.Fields.OrderBy (m => m.Name, StringComparer.OrdinalIgnoreCase))
				SaveField (m, writer);

			WriteFullEndElement (writer);
		}

		static void SaveTypeParameters (JavaTypeParameters parameters, XmlWriter writer)
		{
			if (parameters.Count == 0)
				return;

			writer.WriteStartElement ("typeParameters");

			if (parameters.PropertyBag.TryGetValue ("merge.SourceFile", out var source))
				writer.WriteAttributeString ("merge.SourceFile", source);

			foreach (var tp in parameters) {
				writer.WriteStartElement ("typeParameter");
				writer.WriteAttributeString ("name", tp.Name);
				writer.WriteAttributeStringIfValue ("classBound", tp.ExtendedClassBound);
				writer.WriteAttributeStringIfValue ("jni-classBound", tp.ExtendedJniClassBound);
				writer.WriteAttributeStringIfValue ("interfaceBounds", tp.ExtendedInterfaceBounds);
				writer.WriteAttributeStringIfValue ("jni-interfaceBounds", tp.ExtendedJniInterfaceBounds);

				if (tp.GenericConstraints.Count > 0) {
					// If there is only one generic constraint that specifies java.lang.Object,
					// that is not really a constraint, so skip that.
					// jar2xml does not emit that either.
					if (tp.GenericConstraints.Count == 1 && tp.GenericConstraints[0].Type == "java.lang.Object") {
						WriteFullEndElement (writer);
						continue;
					}

					writer.WriteStartElement ("genericConstraints");

					foreach (var g in tp.GenericConstraints) {
						writer.WriteStartElement ("genericConstraint");
						writer.WriteAttributeString ("type", g.Type);
						WriteFullEndElement (writer);
					}

					WriteFullEndElement (writer);
				}

				WriteFullEndElement (writer);
			}

			WriteFullEndElement (writer);
		}

		static void SaveConstructor (JavaConstructorModel ctor, XmlWriter writer)
			=> SaveMember (ctor, writer, "constructor", null, null, null, null, null, ctor.DeclaringType.FullName, null, null, null, ctor.Parameters, ctor.IsBridge, null, ctor.IsSynthetic, null);

		static void SaveField (JavaFieldModel field, XmlWriter writer)
		{
			var value = field.Value;

			if (value != null && (field.Type == "double" || field.Type == "float"))
				value = value.Replace ("E+", "E");

			SaveMember (field, writer, "field", null, null, null, null,
				XmlConvert.ToString (field.IsTransient),
				field.Type,
				field.TypeGeneric,
				value,
				XmlConvert.ToString (field.IsVolatile),
				null,
				null,
				null,
				null,
				field.IsNotNull);
		}

		static void SaveMethod (JavaMethodModel method, XmlWriter writer)
		{
			bool check (JavaMethodModel _) => _.BaseMethod?.DeclaringType?.Visibility == "public" &&
				!method.IsStatic &&
				method.Parameters.All (p => p.InstantiatedGenericArgumentName == null);

			// skip synthetic methods, that's what jar2xml does.
			// However, jar2xml is based on Java reflection and it generates synthetic methods
			// that actually needs to be generated in the output XML (they are not marked as
			// "synthetic" either by asm or java reflection), when:
			// - the synthetic method is actually from non-public ancestor class
			//   (e.g. FileBackupHelperBase.writeNewStateDescription())
			// For such case, it does not skip generation.
			if (method.IsSynthetic && (method.BaseMethod == null || check (method)))
				return;

			// Here we skip most of the overriding methods of a virtual method, unless
			// - the method visibility or final-ity has changed: protected Object#clone() is often
			//   overriden as public. In that case, we need a "new" method.
			// - the method is covariant. In that case we need another overload.
			// - they differ in "abstract" or "final" method attribute.
			// - the derived method is static.
			// - the base method is in the NON-public class.
			// - none of the arguments are type parameters.
			// - finally, it is the synthetic method already checked above.
			if (method.BaseMethod != null &&
					!method.BaseMethod.IsAbstract &&
					method.BaseMethod.Visibility == method.Visibility &&
					method.BaseMethod.IsAbstract == method.IsAbstract &&
					method.BaseMethod.IsFinal == method.IsFinal &&
					!method.IsSynthetic &&
					check (method))
				return;

			SaveMember (m: method, writer: writer, elementName: "method",
				abs: XmlConvert.ToString (method.IsAbstract),
				native: XmlConvert.ToString (method.IsNative),
				ret: GetVisibleReturnTypeString (method),
				sync: XmlConvert.ToString (method.IsSynchronized),
				transient: null,
				type: null,
				typeGeneric: null,
				value: null,
				volat: null,
				parameters: method.Parameters,
				extBridge: method.IsBridge,
				jniReturn: method.ReturnJni,
				extSynthetic: method.IsSynthetic,
				notNull: method.ReturnNotNull);
		}

		static string GetVisibleReturnTypeString (JavaMethodModel method)
		{
			if (GetVisibleNonSpecialType (method, method.ReturnTypeModel) is JavaTypeReference jtr)
				return jtr.ToString ();

			return method.Return;
		}

		public static string? GetVisibleParamterTypeName (this JavaParameterModel parameter)
		{
			if (GetVisibleNonSpecialType (parameter.DeclaringMethod, parameter.TypeModel) is JavaTypeReference jtr)
				return jtr.ToString ();

			return parameter.GenericType;
		}

		static JavaTypeReference? GetVisibleNonSpecialType (JavaMethodModel method, JavaTypeReference? r)
		{
			if (r == null || r.SpecialName != null || r.ReferencedTypeParameter != null || r.ArrayPart != null)
				return null;

			var requiredVisibility = method?.Visibility == "public" && method.DeclaringType?.Visibility == "public" ? "public" : method?.Visibility;

			for (var t = r; t != null; t = (t.ReferencedType as JavaClassModel)?.BaseTypeReference) {
				if (t.ReferencedType == null)
					break;
				if (IsAcceptableVisibility (required: requiredVisibility, actual: t.ReferencedType.Visibility))
					return t;
			}

			return null;
		}

		static bool IsAcceptableVisibility (string? required, string? actual)
		{
			if (required == "public")
				return actual == "public";
			else
				return true;
		}

		static void SaveMember (JavaMemberModel m, XmlWriter writer, string elementName,
				string? abs, string? native, string? ret, string? sync,
				string? transient, string? type, string? typeGeneric,
				string? value, string? volat,
				IEnumerable<JavaParameterModel>? parameters,
				bool? extBridge, string? jniReturn, bool? extSynthetic, bool? notNull)
		{
			// If any of the parameters contain reference to non-public type, it cannot be generated.
			// TODO
			//if (parameters != null && parameters.Any (p => p.ResolvedType?.ReferencedType != null && string.IsNullOrEmpty (p.ResolvedType.ReferencedType.Visibility)))
			//	return;

			if (parameters != null && parameters.Any (p => p.TypeModel != null && p.TypeModel.ReferencedType?.Visibility.HasValue () == false))
				return;

			writer.WriteStartElement (elementName);

			writer.WriteAttributeStringIfValue ("abstract", abs);
			writer.WriteAttributeString ("deprecated", m.Deprecated);
			writer.WriteAttributeString ("final", XmlConvert.ToString (m.IsFinal));
			writer.WriteAttributeString ("name", m.Name);
			writer.WriteAttributeString ("jni-signature", m.JniSignature);

			if (notNull.GetValueOrDefault () && m is JavaFieldModel)
				writer.WriteAttributeString (m is JavaFieldModel ? "not-null" : "return-not-null", "true");

			if (extBridge.HasValue)
				writer.WriteAttributeString ("bridge", extBridge.Value ? "true" : "false");

			writer.WriteAttributeStringIfValue ("native", native);
			writer.WriteAttributeStringIfValue ("return", ret);
			writer.WriteAttributeStringIfValue ("jni-return", jniReturn);
			writer.WriteAttributeString ("static", XmlConvert.ToString (m.IsStatic));
			writer.WriteAttributeStringIfValue ("synchronized", sync);
			writer.WriteAttributeStringIfValue ("transient", transient);
			writer.WriteAttributeStringIfValue ("type", type);
			writer.WriteAttributeStringIfValue ("type-generic-aware", typeGeneric);
			writer.WriteAttributeStringIfValue ("value", value);

			if (extSynthetic.HasValue)
				writer.WriteAttributeString ("synthetic", extSynthetic.Value ? "true" : "false");

			writer.WriteAttributeString ("visibility", m.Visibility);
			writer.WriteAttributeStringIfValue ("volatile", volat);

			if (m.PropertyBag.TryGetValue ("merge.SourceFile", out var source))
				writer.WriteAttributeString ("merge.SourceFile", source);
			if (m.PropertyBag.TryGetValue ("deprecated-since", out var dep))
				writer.WriteAttributeString ("deprecated-since", dep);

			if (notNull.GetValueOrDefault () && !(m is JavaFieldModel))
				writer.WriteAttributeString (m is JavaFieldModel ? "not-null" : "return-not-null", "true");

			if (m is JavaMethodModel m2)
				SaveTypeParameters (m2.TypeParameters, writer);

			if (parameters != null) {
				foreach (var p in parameters) {
					writer.WriteStartElement ("parameter");
					writer.WriteAttributeString ("name", p.Name);
					writer.WriteAttributeString ("type", GetVisibleParamterTypeName (p));
					writer.WriteAttributeStringIfValue ("jni-type", p.JniType);

					if (p.IsNotNull == true)
						writer.WriteAttributeString ("not-null", "true");

					WriteFullEndElement (writer);
				}
			}

			if (m is JavaMethodModel method) {
				foreach (var e in method.Exceptions.OrderBy (e => e.Name.LastSubset ('/'), StringComparer.Ordinal)) {
					writer.WriteStartElement ("exception");
					writer.WriteAttributeString ("name", e.Name.LastSubset ('/'));
					writer.WriteAttributeString ("type", e.Type);
					WriteFullEndElement (writer);
				}
			}

			WriteFullEndElement (writer);
		}

		static void WriteFullEndElement (XmlWriter writer) => writer.WriteEndElement ();
	}
}
