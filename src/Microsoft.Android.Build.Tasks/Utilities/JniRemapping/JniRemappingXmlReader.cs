#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Properties = Xamarin.Android.Tasks.Properties;
using Xamarin.Android.Tasks;

namespace Microsoft.Android.Tasks;

sealed record JniRemappingTypeReplacement (string From, string To);

sealed record JniRemappingMethodReplacement (
	string SourceType, string SourceMethod, string SourceMethodSignature,
	string TargetType, string TargetMethod, string? TargetMethodSignature, bool TargetIsStatic);

sealed record JniRemappingFieldReplacement (
	string SourceType, string SourceField, string SourceFieldSignature,
	string TargetType, string TargetField, string? TargetFieldSignature);

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

		using var input = File.OpenRead (path);
		using var reader = XmlReader.Create (input, settings);
		if (reader.MoveToContent () != XmlNodeType.Element || reader.LocalName != "replacements") {
			log.LogCodedError ("XA1045", Properties.Resources.XA1045, path);
			return entries;
		}

		while (reader.Read ()) {
			if (reader.NodeType != XmlNodeType.Element)
				continue;

			bool haveAllAttributes = true;
			if (reader.LocalName == "replace-type" || reader.LocalName == "reverse-type") {
				haveAllAttributes &= GetRequiredAttribute ("from", out string from);
				haveAllAttributes &= GetRequiredAttribute ("to", out string to);
				if (!haveAllAttributes)
					continue;

				var entry = new JniRemappingTypeReplacement (from, to);
				if (reader.LocalName == "replace-type")
					entries.TypeReplacements.Add (entry);
				else
					entries.ReverseTypeReplacements.Add (entry);
			} else if (reader.LocalName == "replace-method") {
				haveAllAttributes &= GetRequiredAttribute ("source-type", out string sourceType);
				haveAllAttributes &= GetRequiredAttribute ("source-method-name", out string sourceMethodName);
				haveAllAttributes &= GetRequiredAttribute ("target-type", out string targetType);
				haveAllAttributes &= GetRequiredAttribute ("target-method-name", out string targetMethodName);
				haveAllAttributes &= GetRequiredAttribute ("target-method-instance-to-static", out string targetIsStatic);
				if (!haveAllAttributes)
					continue;

				if (!Boolean.TryParse (targetIsStatic, out bool isStatic)) {
					log.LogCodedError ("XA1046", Properties.Resources.XA1046, "target-method-instance-to-static", reader.LocalName, targetIsStatic, path, GetCurrentLineNumber ());
					continue;
				}

				entries.MethodReplacements.Add (new JniRemappingMethodReplacement (
					sourceType, sourceMethodName, reader.GetAttribute ("source-method-signature") ?? "",
					targetType, targetMethodName, reader.GetAttribute ("target-method-signature"), isStatic));
			} else if (reader.LocalName == "replace-field") {
				haveAllAttributes &= GetRequiredAttribute ("source-type", out string sourceType);
				haveAllAttributes &= GetRequiredAttribute ("source-field-name", out string sourceFieldName);
				haveAllAttributes &= GetRequiredAttribute ("target-type", out string targetType);
				haveAllAttributes &= GetRequiredAttribute ("target-field-name", out string targetFieldName);
				if (!haveAllAttributes)
					continue;

				entries.FieldReplacements.Add (new JniRemappingFieldReplacement (
					sourceType, sourceFieldName, reader.GetAttribute ("source-field-signature") ?? "",
					targetType, targetFieldName, reader.GetAttribute ("target-field-signature")));
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
