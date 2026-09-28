#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	sealed class JniRemappingTypeReplacement
	{
		public string From { get; }
		public string To { get; }

		public JniRemappingTypeReplacement (string from, string to)
		{
			From = from;
			To = to;
		}
	}

	sealed class JniRemappingMethodReplacement
	{
		public string SourceType { get; }
		public string SourceMethod { get; }
		public string SourceMethodSignature { get; }
		public string TargetType { get; }
		public string TargetMethod { get; }
		public string? TargetMethodSignature { get; }
		public bool TargetIsStatic { get; }

		public JniRemappingMethodReplacement (string sourceType, string sourceMethod, string sourceMethodSignature,
		                                      string targetType, string targetMethod, string? targetMethodSignature,
		                                      bool targetIsStatic)
		{
			SourceType = sourceType;
			SourceMethod = sourceMethod;
			SourceMethodSignature = sourceMethodSignature;
			TargetType = targetType;
			TargetMethod = targetMethod;
			TargetMethodSignature = targetMethodSignature;
			TargetIsStatic = targetIsStatic;
		}
	}

	sealed class JniRemappingFieldReplacement
	{
		public string SourceType { get; }
		public string SourceField { get; }
		public string SourceFieldSignature { get; }
		public string TargetType { get; }
		public string TargetField { get; }
		public string? TargetFieldSignature { get; }

		public JniRemappingFieldReplacement (string sourceType, string sourceField, string sourceFieldSignature,
		                                     string targetType, string targetField, string? targetFieldSignature)
		{
			SourceType = sourceType;
			SourceField = sourceField;
			SourceFieldSignature = sourceFieldSignature;
			TargetType = targetType;
			TargetField = targetField;
			TargetFieldSignature = targetFieldSignature;
		}
	}

	sealed class JniRemappingEntries
	{
		public List<JniRemappingTypeReplacement> TypeReplacements { get; } = new ();
		public List<JniRemappingTypeReplacement> ReverseTypeReplacements { get; } = new ();
		public List<JniRemappingMethodReplacement> MethodReplacements { get; } = new ();
		public List<JniRemappingFieldReplacement> FieldReplacements { get; } = new ();
	}

	static class JniRemappingXmlReader
	{
		public static JniRemappingEntries Read (string path, TaskLoggingHelper log)
		{
			var entries = new JniRemappingEntries ();
			var settings = new XmlReaderSettings {
				XmlResolver = null,
				DtdProcessing = DtdProcessing.Prohibit,
			};

			using var reader = XmlReader.Create (File.OpenRead (path), settings);
			if (reader.MoveToContent () != XmlNodeType.Element || reader.LocalName != "replacements") {
				log.LogCodedError ("XA1045", Properties.Resources.XA1045, path);
				return entries;
			}

			while (reader.Read ()) {
				if (reader.NodeType != XmlNodeType.Element) {
					continue;
				}

				bool haveAllAttributes = true;
				if (reader.LocalName == "replace-type" || reader.LocalName == "reverse-type") {
					haveAllAttributes &= GetRequiredAttribute ("from", out string from);
					haveAllAttributes &= GetRequiredAttribute ("to", out string to);
					if (!haveAllAttributes) {
						continue;
					}

					var entry = new JniRemappingTypeReplacement (from, to);
					if (reader.LocalName == "replace-type") {
						entries.TypeReplacements.Add (entry);
					} else {
						entries.ReverseTypeReplacements.Add (entry);
					}
				} else if (reader.LocalName == "replace-method") {
					haveAllAttributes &= GetRequiredAttribute ("source-type", out string sourceType);
					haveAllAttributes &= GetRequiredAttribute ("source-method-name", out string sourceMethodName);
					haveAllAttributes &= GetRequiredAttribute ("target-type", out string targetType);
					haveAllAttributes &= GetRequiredAttribute ("target-method-name", out string targetMethodName);
					haveAllAttributes &= GetRequiredAttribute ("target-method-instance-to-static", out string targetIsStatic);
					if (!haveAllAttributes) {
						continue;
					}

					if (!Boolean.TryParse (targetIsStatic, out bool isStatic)) {
						log.LogCodedError ("XA1046", Properties.Resources.XA1046, "target-method-instance-to-static", reader.LocalName, targetIsStatic, path, GetCurrentLineNumber ());
						continue;
					}

					entries.MethodReplacements.Add (new JniRemappingMethodReplacement (
						sourceType, sourceMethodName, reader.GetAttribute ("source-method-signature") ?? "",
						targetType, targetMethodName, reader.GetAttribute ("target-method-signature"), isStatic
					));
				} else if (reader.LocalName == "replace-field") {
					haveAllAttributes &= GetRequiredAttribute ("source-type", out string sourceType);
					haveAllAttributes &= GetRequiredAttribute ("source-field-name", out string sourceFieldName);
					haveAllAttributes &= GetRequiredAttribute ("target-type", out string targetType);
					haveAllAttributes &= GetRequiredAttribute ("target-field-name", out string targetFieldName);
					if (!haveAllAttributes) {
						continue;
					}

					entries.FieldReplacements.Add (new JniRemappingFieldReplacement (
						sourceType, sourceFieldName, reader.GetAttribute ("source-field-signature") ?? "",
						targetType, targetFieldName, reader.GetAttribute ("target-field-signature")
					));
				}
			}

			return entries;

			bool GetRequiredAttribute (string attributeName, out string attributeValue)
			{
				string? value = reader.GetAttribute (attributeName);
				if (!value.IsNullOrEmpty ()) {
					attributeValue = value;
					return true;
				}

				attributeValue = "";
				log.LogCodedError ("XA1047", Properties.Resources.XA1047, attributeName, reader.LocalName, path, GetCurrentLineNumber ());
				return false;
			}

			int GetCurrentLineNumber () => ((IXmlLineInfo)reader).LineNumber;
		}
	}
}
