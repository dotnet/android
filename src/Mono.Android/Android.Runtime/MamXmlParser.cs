// File must be "stand-alone"; it's included by
// `tools/remap-mam-json-to-xml`

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

using ReplacementTypesDict              = System.Collections.Generic.Dictionary<string, string>;
using ReplacementMethodsDictStrings     = System.Collections.Generic.Dictionary<string, string>;
using ReplacementMethodsDictStructured  = System.Collections.Generic.Dictionary<
	(string SourceType, string SourceName, string? SourceSignature),
	(string? TargetType, string? TargetName, string? TargetSignature, int? ParamCount, bool IsStatic)
>;

namespace Android.Runtime {

	class MamXmlParser {

		// https://www.unicode.org/reports/tr15/tr15-18.html#Programming%20Language%20Identifiers
		//  <identifier> ::= <identifier_start> ( <identifier_start> | <identifier_extend> )*
		//  <identifier_start> ::= [{Lu}{Ll}{Lt}{Lm}{Lo}{Nl}]
		//  <identifier_extend> ::= [{Mn}{Mc}{Nd}{Pc}{Cf}]
		//
		// Categories which can't be part of an identifier: Cc, Me, No, Pd, Pe, Pf, Pi, Po, Ps, Sc, Sk, Sm, So, Zl, Zp, Zs
		//
		// Use `\t` U+0009, Category=Cc, to separate out items in ReplacementMethodsDictStrings

		public static (ReplacementTypesDict ReplacementTypes, ReplacementMethodsDictStrings ReplacementMethods) ParseStrings (string xml)
		{
			var (types, methodsStructured) = ParseStructured (xml);

			var methodsStrings  = new ReplacementMethodsDictStrings ();
			foreach (var e in methodsStructured) {
				var key     = $"{e.Key.SourceType}\t{e.Key.SourceName}\t{e.Key.SourceSignature}";
				var value   = $"{e.Value.TargetType}\t{e.Value.TargetName}\t{e.Value.TargetSignature}\t{e.Value.ParamCount?.ToString () ?? ""}\t{(e.Value.IsStatic ? "true" : "false")}";
				methodsStrings [key] = value;
			}

			return (types, methodsStrings);
		}

		public static (ReplacementTypesDict ReplacementTypes, ReplacementMethodsDictStructured ReplacementMethods) ParseStructured (string xml)
		{
			var replacementTypes    = new ReplacementTypesDict ();
			var replacementMethods  = new ReplacementMethodsDictStructured ();

			using var t             = new StringReader (xml);
			using var reader        = XmlReader.Create (t, new XmlReaderSettings { XmlResolver = null });
			while (reader.Read ()) {
				if (reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.Comment) {
					continue;
				}
				if (!reader.IsStartElement ()) {
					continue;
				}
				if (!reader.HasAttributes) {
					continue;
				}
				switch (reader.LocalName) {
					case "replace-type":
						ParseReplaceTypeAttributes (replacementTypes, reader);
						break;
					case "replace-method":
						ParseReplaceMethodAttributes (replacementMethods, reader);
						break;
				}
			}

			return (replacementTypes, replacementMethods);
		}

		static void ParseReplaceTypeAttributes (ReplacementTypesDict replacementTypes, XmlReader reader)
		{
			// <replace-type
			//     from="android/app/Activity"'
			//     to="com/microsoft/intune/mam/client/app/MAMActivity"
			// />
			string? from    = null;
			string? to      = null;
			while (reader.MoveToNextAttribute ()) {
				switch (reader.LocalName) {
				case "from":
					from    = reader.Value;
					break;
				case "to":
					to      = reader.Value;
					break;
				}
			}
			if (string.IsNullOrEmpty (from) || string.IsNullOrEmpty (to)) {
				return;
			}
			replacementTypes [from] = to;
		}

		static void ParseReplaceMethodAttributes (ReplacementMethodsDictStructured replacementMethods, XmlReader reader)
		{
			// <replace-method
			//     source-type="jni-simple-type"
			//     source-method-name="method-name"
			//     source-method-signature="jni-method-signature"
			//     target-type="jni-simple-type"
			//     target-method-name="method-name"
			//     target-method-signature="jni-method-signature"
			//     target-method-parameter-count="int"
			//     target-method-instance-to-static="bool"
			// />

			string? sourceType                      = null;
			string? sourceMethod                    = null;
			string? sourceMethodSig                 = null;
			string? targetType                      = null;
			string? targetMethod                    = null;
			string? targetMethodSig                 = null;
			int?    targetMethodParamCount          = null;
			bool    targetMethodInstanceToStatic    = false;

			while (reader.MoveToNextAttribute ()) {
				switch (reader.LocalName) {
				case "source-type":
					sourceType                      = reader.Value;
					break;
				case "source-method-name":
					sourceMethod                    = reader.Value;
					break;
				case "source-method-signature":
					sourceMethodSig                 = reader.Value;
					break;
				case "target-type":
					targetType                      = reader.Value;
					break;
				case "target-method-name":
					targetMethod                    = reader.Value;
					break;
				case "target-method-signature":
					targetMethodSig                 = reader.Value;
					break;
				case "target-method-parameter-count":
					if (int.TryParse (reader.Value, 0, CultureInfo.InvariantCulture, out var v)) {
						targetMethodParamCount      = v;
					}
					break;
				case "target-method-instance-to-static":
					targetMethodInstanceToStatic    = reader.Value == "true";
					break;
				}
			}
			if (string.IsNullOrEmpty (sourceType) || string.IsNullOrEmpty (sourceMethod)) {
				return;
			}
			replacementMethods [(sourceType, sourceMethod, sourceMethodSig)]
				= (targetType, targetMethod, targetMethodSig, targetMethodParamCount, targetMethodInstanceToStatic);
		}
	}
}
