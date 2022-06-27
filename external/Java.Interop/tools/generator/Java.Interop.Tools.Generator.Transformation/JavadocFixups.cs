using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Java.Interop.Tools.JavaSource;

using MonoDroid.Generation;
using MonoDroid.Utils;

using Xamarin.Android.Binder;

namespace Java.Interop.Tools.Generator.Transformation
{
	public static class JavadocFixups
	{
		public static void Fixup (List<GenBase> gens, CodeGeneratorOptions options)
		{
			if (options.JavadocXmlFiles == null || options.JavadocXmlFiles.Count == 0)
				return;

			var typeJavadocs = new Dictionary<string, XElement> ();

			foreach (var path in options.JavadocXmlFiles) {
				XDocument doc = null;
				try {
					doc = XDocument.Load (path);
				}
				catch (Exception e) {
					Report.LogCodedWarning (0, Report.WarningInvalidXmlFile, e, path, e.Message);
					continue;
				}

				var types = doc.Elements ("api")
					.Elements ("package")
					.Elements ();
				foreach (var typeXml in types) {
					var typeJniSig  = (string) typeXml.Attribute ("jni-signature");
					if (string.IsNullOrEmpty (typeJniSig))
						continue;
					if (!typeJavadocs.TryGetValue (typeJniSig, out _))
						typeJavadocs.Add (typeJniSig, typeXml);
				}
			}

			foreach (var type in gens) {
				AddJavadoc (type, typeJavadocs, options.XmldocStyle);
				foreach (var nested in type.NestedTypes) {
					AddJavadoc (nested, typeJavadocs, options.XmldocStyle);
				}
			}
		}

		static void AddJavadoc (GenBase type, Dictionary<string, XElement> typeJavadocs, XmldocStyle style)
		{
			if (!typeJavadocs.TryGetValue (type.JniName, out XElement typeJavadoc))
				return;
			if (typeJavadoc == null)
				return;

			if (type.JavadocInfo == null) {
				type.JavadocInfo    = JavadocInfo.CreateInfo (typeJavadoc, style);
			}

			foreach (var method in type.Methods) {
				if (method.JavadocInfo != null)
					continue;
				var methodJavadoc   = GetMemberJavadoc (typeJavadoc, "method", method.JavaName, method.JniSignature);
				method.JavadocInfo  = JavadocInfo.CreateInfo (methodJavadoc?.Parent, style);
			}

			foreach (var property in type.Properties) {
				if (property.Getter != null && property.Getter.JavadocInfo == null) {
					var getterJavadoc           = GetMemberJavadoc (typeJavadoc, "method", property.Getter.JavaName, property.Getter.JniSignature);
					property.Getter.JavadocInfo = JavadocInfo.CreateInfo (getterJavadoc?.Parent, style, appendCopyrightExtra: false);
				}
				if (property.Setter != null && property.Setter.JavadocInfo == null) {
					var setterJavadoc           = GetMemberJavadoc (typeJavadoc, "method", property.Setter.JavaName, property.Setter.JniSignature);
					property.Setter.JavadocInfo = JavadocInfo.CreateInfo (setterJavadoc?.Parent, style, appendCopyrightExtra: false);
				}
			}

			foreach (var field in type.Fields) {
				if (field.JavadocInfo != null)
					continue;
				var fieldJavadoc    = GetMemberJavadoc (typeJavadoc, "field", field.JavaName, field.JniSignature);
				field.JavadocInfo   = JavadocInfo.CreateInfo (fieldJavadoc?.Parent, style);
			}

			if (type is ClassGen @class) {
				foreach (var ctor in @class.Ctors) {
					if (ctor.JavadocInfo != null)
						continue;
					var ctorJavadoc     = GetMemberJavadoc (typeJavadoc, "constructor", null, ctor.JniSignature);
					ctor.JavadocInfo    = JavadocInfo.CreateInfo (ctorJavadoc?.Parent, style);
				}
			}
		}

		static XElement GetMemberJavadoc (XElement typeJavadoc, string elementName, string name, string jniSignature)
		{
			return typeJavadoc
				.Elements (elementName)
				.Where (e => jniSignature == (string) e.Attribute ("jni-signature") &&
						(name == null ? true : name == (string) e.Attribute ("name")))
				.Elements ("javadoc")
				.FirstOrDefault ();
		}
	}
}
