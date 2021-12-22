// File must be "stand-alone"; it's included by
// `tools/remap-mam-json-to-xml`

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

using ReplacementTypesDict      = System.Collections.Generic.Dictionary<string, string>;
using ReplacementMethodsDict    = System.Collections.Generic.Dictionary<
	(string SourceType, string SourceName, string? SourceSignature),
	(string? TargetType, string? TargetName, string? TargetSignature, int? ParamCount, bool IsStatic)
>;

namespace Android.Runtime {

	class MamXmlParser {

		public static (ReplacementTypesDict ReplacementTypes, ReplacementMethodsDict ReplacementMethods) Parse (string xml)
		{
			var replacementTypes    = new ReplacementTypesDict ();
			var replacementMethods  = new ReplacementMethodsDict ();

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

		static void ParseReplaceMethodAttributes (ReplacementMethodsDict replacementMethods, XmlReader reader)
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
