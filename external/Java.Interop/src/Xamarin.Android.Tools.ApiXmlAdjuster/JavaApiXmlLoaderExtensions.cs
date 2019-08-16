using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiLoaderExtensions
	{
		public static void Load (this JavaApi api, string xmlfile)
		{
			using (var reader = XmlReader.Create (xmlfile))
				api.Load (reader, false);
		}
		
		public static void Load (this JavaApi api, XmlReader reader, bool isReferenceOnly)
		{
			reader.MoveToContent ();
			if (reader.LocalName != "api")
				throw XmlUtil.UnexpectedElementOrContent (null, reader, "api");
			api.ExtendedApiSource = reader.GetAttribute ("api-source");
			api.Platform = reader.GetAttribute ("platform");
			XmlUtil.CheckExtraneousAttributes ("api", reader, "api-source", "platform");
			if (reader.IsEmptyElement)
				reader.Read ();
			else {
				reader.Read ();
				do {
					reader.MoveToContent ();
					if (reader.NodeType == XmlNodeType.EndElement)
						break; // </api>
					if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "package")
						throw XmlUtil.UnexpectedElementOrContent ("api", reader, "package");
					var pkg = api.Packages.FirstOrDefault (p => p.Name == reader.GetAttribute ("name"));
					if (pkg == null) {
						pkg = new JavaPackage (api);
						api.Packages.Add (pkg);
					}
					pkg.Load (reader, isReferenceOnly);
				} while (true);
	
				XmlUtil.VerifyEndElement (reader, "api");
				reader.Read ();
			}
		}
	
		public static void Load (this JavaPackage package, XmlReader reader, bool isReferenceOnly)
		{
			reader.MoveToContent ();
			package.Name = XmlUtil.GetRequiredAttribute (reader, "name");
			package.JniName = reader.GetAttribute ("jni-name");
			if (reader.MoveToFirstAttribute ())
				if (reader.LocalName != "name")
					throw XmlUtil.UnexpectedAttribute (reader, "package");
			reader.MoveToElement ();
			if (reader.IsEmptyElement)
				reader.Read ();
			else {
				reader.Read ();
				do {
					reader.MoveToContent ();
					if (reader.NodeType == XmlNodeType.EndElement)
						break; // </package>
					if (reader.NodeType != XmlNodeType.Element)
						throw XmlUtil.UnexpectedElementOrContent ("package", reader, "class", "interface");
					if (reader.LocalName == "class") {
						var kls = new JavaClass (package) { IsReferenceOnly = isReferenceOnly };
						kls.Load (reader);
						package.Types.Add (kls);
					} else if (reader.LocalName == "interface") {
						var iface = new JavaInterface (package) { IsReferenceOnly = isReferenceOnly };
						iface.Load (reader);
						package.Types.Add (iface);
					} else
						throw XmlUtil.UnexpectedElementOrContent ("package", reader, "class", "interface");
				} while (true);
	
				XmlUtil.VerifyEndElement (reader, "package");
				reader.Read ();
			}
		}

		static readonly string [] expected_type_attributes = new String [] {
			"abstract",
			"deprecated",
			"enclosing-method-jni-type",
			"enclosing-method-name",
			"enclosing-method-signature",
			"final",
			"jni-signature",
			"name",
			"source-file-name",
			"static",
			"visibility",
		};

		internal static void LoadTypeAttributes (this JavaType type, XmlReader reader, params string [] otherAllowedAttributes)
		{
			type.Abstract = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "abstract"));
			type.Deprecated = XmlUtil.GetRequiredAttribute (reader, "deprecated");
			type.Final = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "final"));
			type.Name = XmlUtil.GetRequiredAttribute (reader, "name");
			type.Static = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "static"));
			type.Visibility = XmlUtil.GetRequiredAttribute (reader, "visibility");
			type.ExtendedJniSignature = reader.GetAttribute ("jni-signature");
			XmlUtil.CheckExtraneousAttributes (reader.LocalName, reader, expected_type_attributes.Concat (otherAllowedAttributes).ToArray ());
		}

		internal static bool TryLoadCommonElement (this JavaType type, XmlReader reader)
		{
			if (reader.LocalName == "implements") {
				var implements = new JavaImplements ();
				implements.Load (reader);
				type.Implements.Add (implements);
			} else if (reader.LocalName == "typeParameters") {
				var tp = new JavaTypeParameters (type);
				tp.Load (reader);
				type.TypeParameters = tp;
			} else if (reader.LocalName == "field") {
				var field = new JavaField (type);
				field.Load (reader);
				type.Members.Add (field);
			} else if (reader.LocalName == "method") {
				var method = new JavaMethod (type);
				method.Load (reader);
				type.Members.Add (method);
			} else
				return false;
			return true;
		}
		
		public static void Load (this JavaInterface iface, XmlReader reader)
		{
			reader.MoveToContent ();
			iface.LoadTypeAttributes (reader);
			if (reader.IsEmptyElement)
				reader.Read ();
			else {
				reader.Read ();
				do {
					reader.MoveToContent ();
					if (reader.NodeType == XmlNodeType.EndElement)
						break; // </interface>
					if (reader.NodeType != XmlNodeType.Element)
						throw XmlUtil.UnexpectedElementOrContent ("interface", reader, "implements", "typeParameters", "field", "method");
					if (!iface.TryLoadCommonElement (reader))
						throw XmlUtil.UnexpectedElementOrContent ("interface", reader, "implements", "typeParameters", "field", "method");
				} while (true);

				XmlUtil.VerifyEndElement (reader, "interface");
				reader.Read ();
			}
		}

		public static void Load (this JavaClass kls, XmlReader reader)
		{
			reader.MoveToContent ();
			kls.LoadTypeAttributes (reader, "extends", "extends-generic-aware", "jni-extends");
			// they are not mandatory; Java.Lang.Object doesn't have them.
			kls.Extends = reader.GetAttribute ("extends");
			kls.ExtendsGeneric = reader.GetAttribute ("extends-generic-aware");
			kls.ExtendedJniExtends = reader.GetAttribute ("jni-extends");

			reader.MoveToElement ();
			if (reader.IsEmptyElement)
				reader.Read ();
			else {
				reader.Read ();
				do {
					reader.MoveToContent ();
					if (reader.NodeType == XmlNodeType.EndElement)
						break; // </interface>
					if (reader.NodeType != XmlNodeType.Element)
						throw XmlUtil.UnexpectedElementOrContent ("class", reader, "implements", "typeParameters", "field", "constructor", "method");
					if (!kls.TryLoadCommonElement (reader)) {
						if (reader.LocalName == "constructor") {
							var constructor = new JavaConstructor (kls);
							constructor.Load (reader);
							kls.Members.Add (constructor);
						} else
							throw XmlUtil.UnexpectedElementOrContent ("class", reader, "implements", "typeParameters", "field", "constructor", "method");
					}
				} while (true);
				XmlUtil.VerifyEndElement (reader, "class");
				reader.Read ();
			}
		}

		public static void Load (this JavaImplements implements, XmlReader reader)
		{
			implements.Name = XmlUtil.GetRequiredAttribute (reader, "name");
			implements.NameGeneric = XmlUtil.GetRequiredAttribute (reader, "name-generic-aware");
			implements.ExtendedJniType = reader.GetAttribute ("jni-type");
			XmlUtil.CheckExtraneousAttributes (reader.LocalName, reader, "name", "name-generic-aware", "jni-type");
			if (!reader.IsEmptyElement) {
				reader.Read ();
				reader.MoveToContent ();
				XmlUtil.VerifyEndElement (reader, "implements");
			}
			reader.Read ();
		}

		public static void LoadMemberAttributes (this JavaMember member, XmlReader reader)
		{
			member.Deprecated = XmlUtil.GetRequiredAttribute (reader, "deprecated");
			member.Final = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "final"));
			member.Name = XmlUtil.GetRequiredAttribute (reader, "name");
			member.Static = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "static"));
			member.Visibility = XmlUtil.GetRequiredAttribute (reader, "visibility");
			member.ExtendedJniSignature = reader.GetAttribute ("jni-signature");
		}

		public static void Load (this JavaField field, XmlReader reader)
		{
			field.LoadMemberAttributes (reader);
			field.Transient = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "transient"));
			field.Volatile = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "transient"));
			field.Type = XmlUtil.GetRequiredAttribute (reader, "type");
			field.TypeGeneric = XmlUtil.GetRequiredAttribute (reader, "type-generic-aware");
			field.Value = reader.GetAttribute ("value");

			reader.Skip ();
		}

		static void LoadMethodBase (this JavaMethodBase methodBase, string elementName, XmlReader reader)
		{
			var method = methodBase as JavaMethod; // kind of ugly hack yeah...
			
			methodBase.LoadMemberAttributes (reader);
			methodBase.ExtendedJniReturn = reader.GetAttribute ("jni-return");
			methodBase.ExtendedSynthetic = XmlConvert.ToBoolean (reader.GetAttribute ("synthetic") ?? "false");
			methodBase.ExtendedBridge = XmlConvert.ToBoolean (reader.GetAttribute ("bridge") ?? "false");

			reader.MoveToElement ();
			if (reader.IsEmptyElement)
				reader.Read ();
			else {
				reader.Read ();
				do {
					reader.MoveToContent ();
					if (reader.NodeType == XmlNodeType.EndElement)
						break;
					if (reader.NodeType != XmlNodeType.Element)
						throw XmlUtil.UnexpectedElementOrContent (elementName, reader, "parameter");
					if (reader.LocalName == "typeParameters") {
						var tp = new JavaTypeParameters (methodBase);
						tp.Load (reader);
						methodBase.TypeParameters = tp;
					} else if (reader.LocalName == "parameter") {
						var p = new JavaParameter (methodBase);
						p.Load (reader);
						methodBase.Parameters.Add (p);
					} else if (reader.LocalName == "exception") {
						var p = new JavaException ();
						p.Load (reader);
						methodBase.Exceptions.Add (p);
					} else
						throw XmlUtil.UnexpectedElementOrContent (elementName, reader, "parameter");
				} while (true);
				XmlUtil.VerifyEndElement (reader, elementName);
				reader.Read ();
			}
		}

		public static void Load (this JavaConstructor constructor, XmlReader reader)
		{
			// it was required in the original API XML, but removed in class-parsed...
			constructor.Type = reader.GetAttribute ("type");
			XmlUtil.CheckExtraneousAttributes ("constructor", reader, "deprecated", "final", "name", "static", "visibility", "jni-signature", "jni-return", "synthetic", "bridge",
				"type");
			constructor.LoadMethodBase ("constructor", reader);
		}

		public static void Load (this JavaMethod method, XmlReader reader)
		{
			method.Abstract = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "abstract"));
			method.Native = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "native"));
			method.Return = XmlUtil.GetRequiredAttribute (reader, "return");
			method.Synchronized = XmlConvert.ToBoolean (XmlUtil.GetRequiredAttribute (reader, "synchronized"));
			XmlUtil.CheckExtraneousAttributes ("method", reader, "deprecated", "final", "name", "static", "visibility", "jni-signature", "jni-return", "synthetic", "bridge",
				"abstract", "native", "return", "synchronized");
			method.LoadMethodBase ("method", reader);
		}

		internal static void Load (this JavaParameter p, XmlReader reader)
		{
			p.Name = XmlUtil.GetRequiredAttribute (reader, "name");
			p.Type = XmlUtil.GetRequiredAttribute (reader, "type");
			p.JniType = reader.GetAttribute ("jni-type");
			reader.Skip ();
		}

		internal static void Load (this JavaException e, XmlReader reader)
		{
			e.Name = XmlUtil.GetRequiredAttribute (reader, "name");
			e.Type = XmlUtil.GetRequiredAttribute (reader, "type");
			e.Type = reader.GetAttribute ("type-generic-aware");
			reader.Skip ();
		}
		
		internal static void Load (this JavaTypeParameters tps, XmlReader reader)
		{
			reader.MoveToContent ();
			if (reader.IsEmptyElement)
				reader.Read ();
			else {
				reader.Read ();
				do {
					reader.MoveToContent ();
					if (reader.NodeType == XmlNodeType.EndElement)
						break; // </typeParameters>
					if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "typeParameter")
						throw XmlUtil.UnexpectedElementOrContent ("typeParameters", reader, "typeParameter");
					var tp = new JavaTypeParameter (tps);
					tp.Load (reader);
					tps.TypeParameters.Add (tp);
				} while (true);
	
				XmlUtil.VerifyEndElement (reader, "typeParameters");
				reader.Read ();
			}
		}

		public static void Load (this JavaTypeParameter tp, XmlReader reader)
		{
			tp.Name = XmlUtil.GetRequiredAttribute (reader, "name");
			tp.ExtendedJniClassBound = reader.GetAttribute ("jni-classBound");
			// such an ill-named attribute...
			tp.ExtendedClassBound = reader.GetAttribute ("classBound");
			// and un-structuring attribute...
			tp.ExtendedInterfaceBounds = reader.GetAttribute ("interfaceBounds");
			tp.ExtendedJniInterfaceBounds = reader.GetAttribute ("jni-interfaceBounds");
			XmlUtil.CheckExtraneousAttributes ("typeParameter", reader, "name", "jni-classBound", "jni-interfaceBounds", "classBound", "interfaceBounds");
			if (reader.IsEmptyElement)
				reader.Read ();
			else {
				reader.Read ();
				do {
					reader.MoveToContent ();
					if (reader.NodeType == XmlNodeType.EndElement)
						break; // </typeParameter>
					if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "genericConstraints")
						throw XmlUtil.UnexpectedElementOrContent ("typeParameter", reader, "genericConstraints");
					
					var gc = new JavaGenericConstraints ();
					gc.Load (reader);
					tp.GenericConstraints = gc;
				} while (true);

				XmlUtil.VerifyEndElement (reader, "typeParameter");
				reader.Read ();
			}
			// Now we have to deal with the format difference...
			// Some versions of class-parse stopped generating <genericConstraints> but started
			// generating "classBound" and "interfaceBounds" attributes instead.
			// They don't make sense and blocking this effort, but we have to deal with that...
			if (!string.IsNullOrEmpty (tp.ExtendedClassBound) || !string.IsNullOrEmpty (tp.ExtendedInterfaceBounds)) {
				var gcs = new JavaGenericConstraints ();
				if (!string.IsNullOrEmpty (tp.ExtendedClassBound))
					gcs.GenericConstraints.Add (new JavaGenericConstraint () { Type = tp.ExtendedClassBound });
				if (!string.IsNullOrEmpty (tp.ExtendedInterfaceBounds))
					foreach (var ic in tp.ExtendedInterfaceBounds.Split (':'))
						gcs.GenericConstraints.Add (new JavaGenericConstraint () { Type = ic });
				tp.GenericConstraints = gcs;
			}
		}
		
		public static void Load (this JavaGenericConstraints gcs, XmlReader reader)
		{
			reader.MoveToContent ();

			if (reader.IsEmptyElement)
				reader.Read ();
			else {
				reader.Read ();

				do {
					reader.MoveToContent ();
					if (reader.NodeType == XmlNodeType.EndElement)
						break; // </genericConstraints>
					if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "genericConstraint")
						throw XmlUtil.UnexpectedElementOrContent ("genericConstraints", reader, "genericConstraint");
					var gc = new JavaGenericConstraint ();
					gc.Load (reader);
					gcs.GenericConstraints.Add (gc);
				} while (true);

				XmlUtil.VerifyEndElement (reader, "genericConstraints");
				reader.Read ();
			}
		}
		
		public static void Load (this JavaGenericConstraint gc, XmlReader reader)
		{
			gc.Type = XmlUtil.GetRequiredAttribute (reader, "type");
			XmlUtil.CheckExtraneousAttributes ("genericConstraint", reader, "type");
			reader.Skip ();
		}
	}
}
