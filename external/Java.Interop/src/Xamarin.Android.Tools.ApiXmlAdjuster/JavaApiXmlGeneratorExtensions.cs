using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiXmlGeneratorExtensions
	{
		public static void Save (this JavaApi api, string xmlfile)
		{
			using (var writer = XmlWriter.Create (xmlfile, new XmlWriterSettings () {
				Encoding = new UTF8Encoding (false, true),
				Indent = true,
				OmitXmlDeclaration = true,
				}))
				api.Save (writer);
		}
		
		public static void Save (this JavaApi api, XmlWriter writer)
		{
			writer.WriteStartElement ("api");
			
			foreach (var pkg in api.Packages) {
				if (!pkg.Types.Any (t => !t.IsReferenceOnly))
					continue;
				writer.WriteStartElement ("package");
				writer.WriteAttributeString ("name", pkg.Name);
				foreach (var type in pkg.Types) {
					if (type.IsReferenceOnly)
						continue; // skip reference only types
					if (type is JavaClass)
						((JavaClass) type).Save (writer);
					else
						((JavaInterface) type).Save (writer);
				}
				writer.WriteFullEndElement ();
			}
			writer.WriteFullEndElement ();
		}
		
		static void Save (this JavaClass cls, XmlWriter writer)
		{
			SaveTypeCommon (cls, writer, "class", XmlConvert.ToString (cls.Abstract), cls.Extends, cls.ExtendsGeneric);
		}
		
		static void Save (this JavaInterface iface, XmlWriter writer)
		{
			SaveTypeCommon (iface, writer, "interface", "true", null, null);
		}
		
		static void SaveTypeCommon (this JavaType cls, XmlWriter writer, string elementName, string abs, string ext, string extgen)
		{
			writer.WriteStartElement (elementName);
			if (abs != null)
				writer.WriteAttributeString ("abstract", abs);
			writer.WriteAttributeString ("deprecated", cls.Deprecated);
			if (ext != null)
				writer.WriteAttributeString ("extends", ext);
			if (ext != null)
				writer.WriteAttributeString ("extends-generic-aware", extgen);
			writer.WriteAttributeString ("final", XmlConvert.ToString (cls.Final));
			writer.WriteAttributeString ("name", cls.Name);
			writer.WriteAttributeString ("static", XmlConvert.ToString (cls.Static));
			writer.WriteAttributeString ("visibility", cls.Visibility);
			
			foreach (var imp in cls.Implements.OrderBy (i => i.Name, StringComparer.Ordinal)) {
				writer.WriteStartElement ("implements");
				writer.WriteAttributeString ("name", imp.Name);
				writer.WriteAttributeString ("name-generic-aware", imp.NameGeneric);
				writer.WriteString ("\n      ");
				writer.WriteFullEndElement ();
			}
			
			if (cls.TypeParameters != null)
				cls.TypeParameters.Save (writer, "      ");
			
			foreach (var m in cls.Members.OfType<JavaConstructor> ().OrderBy (m => m.Name, StringComparer.Ordinal).ThenBy (m => string.Join (", ", m.Parameters.Select (p => p.Type))).ThenBy (m => m.ExtendedSynthetic))
				m.Save (writer);
			foreach (var m in cls.Members.OfType<JavaMethod> ().OrderBy (m => m.Name, StringComparer.Ordinal).ThenBy (m => string.Join (", ", m.Parameters.Select (p => p.Type))).ThenBy (m => m.ExtendedSynthetic))
				m.Save (writer);
			foreach (var m in cls.Members.OfType<JavaField> ().OrderBy (m => m.Name, StringComparer.Ordinal))
				m.Save (writer);

			writer.WriteFullEndElement ();
		}
		
		static void Save (this JavaTypeParameters typeParameters, XmlWriter writer, string indent)
		{
			writer.WriteStartElement ("typeParameters");
			foreach (var tp in typeParameters.TypeParameters) {
				writer.WriteStartElement ("typeParameter");
				writer.WriteAttributeString ("name", tp.Name);
				if (tp.GenericConstraints != null) {
					// If there is only one generic constraint that specifies java.lang.Object,
					// that is not really a constraint, so skip that.
					// jar2xml does not emit that either.
					var gcs = tp.GenericConstraints.GenericConstraints;
					var gctr = gcs.Count == 1 ? gcs [0].ResolvedType : null;
					if (gctr == null || gctr.ReferencedType.FullName != "java.lang.Object")
					{
						writer.WriteStartElement ("genericConstraints");
						foreach (var g in tp.GenericConstraints.GenericConstraints) {
							writer.WriteStartElement ("genericConstraint");
							writer.WriteAttributeString ("type", g.Type);
							writer.WriteString ("\n" + indent + "      ");
							writer.WriteFullEndElement ();
						}
						writer.WriteFullEndElement ();
					}
				}
				else
					writer.WriteString ("\n" + indent + "  ");
				writer.WriteFullEndElement ();
			}
			writer.WriteString ("\n" + indent);
			writer.WriteFullEndElement ();
		}
		
		static void Save (this JavaField field, XmlWriter writer)
		{
			var value = field.Value;
			if (value != null && (field.Type == "double" || field.Type == "float"))
				value = value.Replace ("E+", "E");
			SaveCommon (field, writer, "field", null, null, null, null,
				XmlConvert.ToString (field.Transient),
				field.Type,
				field.TypeGeneric,
				value,
				XmlConvert.ToString (field.Volatile),
				null,
				null,
				null);
		}
		
		static void Save (this JavaConstructor ctor, XmlWriter writer)
		{
			SaveCommon (ctor, writer, "constructor", null, null, null, null, null, ctor.Type ?? ctor.Parent.FullName, null, null, null, null, ctor.Parameters, ctor.Exceptions);
		}
		
		static void Save (this JavaMethod method, XmlWriter writer)
		{
			Func<JavaMethod,bool> check = _ => _.BaseMethod.Method.Parent.Visibility == "public" &&
			    !method.Static &&
			    method.Parameters.All (p => p.InstantiatedGenericArgumentName == null);
			
			// skip synthetic methods, that's what jar2xml does.
			// However, jar2xml is based on Java reflection and it generates synthetic methods
			// that actually needs to be generated in the output XML (they are not marked as
			// "synthetic" either by asm or java reflection), when:
			// - the synthetic method is actually from non-public ancestor class
			//   (e.g. FileBackupHelperBase.writeNewStateDescription())
			// For such case, it does not skip generation.
			if (method.ExtendedSynthetic && (method.BaseMethod == null || check (method)))
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
			    !method.BaseMethod.Method.Abstract &&
			    method.BaseMethod.Method.Visibility == method.Visibility &&
			    method.BaseMethod.Method.Abstract == method.Abstract &&
			    method.BaseMethod.Method.Final == method.Final &&
			    !method.ExtendedSynthetic &&
			    check (method))
				return;
			
			SaveCommon (method, writer, "method",
				XmlConvert.ToString (method.Abstract),
				XmlConvert.ToString (method.Native),
				method.Return,
				XmlConvert.ToString (method.Synchronized),
				null,
				null,
				null,
				null,
				null,
				method.TypeParameters,
				method.Parameters,
				method.Exceptions);
		}
		
		static void SaveCommon (this JavaMember m, XmlWriter writer, string elementName,
					string abs, string native, string ret, string sync,
					string transient, string type, string typeGeneric,
					string value, string volat,
					JavaTypeParameters typeParameters,
					IEnumerable<JavaParameter> parameters,
					IEnumerable<JavaException> exceptions)
		{
			// If any of the parameters contain reference to non-public type, it cannot be generated.
			if (parameters != null && parameters.Any (p => p.ResolvedType.ReferencedType != null && string.IsNullOrEmpty (p.ResolvedType.ReferencedType.Visibility)))
				return;
			
			writer.WriteStartElement (elementName);
			if (abs != null)
				writer.WriteAttributeString ("abstract", abs);
			writer.WriteAttributeString ("deprecated", m.Deprecated);
			writer.WriteAttributeString ("final", XmlConvert.ToString (m.Final));
			writer.WriteAttributeString ("name", m.Name);
			if (native != null)
				writer.WriteAttributeString ("native", native);
			if (ret != null)
				writer.WriteAttributeString ("return", ret);
			writer.WriteAttributeString ("static", XmlConvert.ToString (m.Static));
			if (sync != null)
				writer.WriteAttributeString ("synchronized", sync);
			if (transient != null)
				writer.WriteAttributeString ("transient", transient);
			if (type != null)
				writer.WriteAttributeString ("type", type);
			if (typeGeneric != null)
				writer.WriteAttributeString ("type-generic-aware", typeGeneric);
			if (value != null)
				writer.WriteAttributeString ("value", value);
			writer.WriteAttributeString ("visibility", m.Visibility);
			if (volat != null)
				writer.WriteAttributeString ("volatile", volat);

			if (typeParameters != null)
				typeParameters.Save (writer, "      ");
			
			if (parameters != null) {
				foreach (var p in parameters) {
					writer.WriteStartElement ("parameter");
					writer.WriteAttributeString ("name", p.Name);
					writer.WriteAttributeString ("type", p.Type);
					writer.WriteString ("\n        ");			
					writer.WriteFullEndElement ();
				}
			}

			if (exceptions != null) {
				foreach (var e in exceptions.OrderBy (e => e.Name.Substring (e.Name.LastIndexOf ('/') + 1).Replace ('$', '.'), StringComparer.Ordinal)) {
					writer.WriteStartElement ("exception");
					writer.WriteAttributeString ("name", e.Name.Substring (e.Name.LastIndexOf ('/') + 1).Replace ('$', '.'));
					writer.WriteAttributeString ("type", e.Type);
					writer.WriteString ("\n        ");
					writer.WriteFullEndElement ();
				}
			}

			writer.WriteString ("\n      ");			
			writer.WriteFullEndElement ();
		}
		
	}
}

