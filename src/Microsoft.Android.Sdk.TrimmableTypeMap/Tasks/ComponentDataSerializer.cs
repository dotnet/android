using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Serializes and deserializes component data (ComponentData from JavaPeerInfo)
/// to a simple text format for inter-target communication.
///
/// Format: one block per type, separated by blank lines.
/// Each block starts with "TYPE:{ManagedTypeName}" and contains key-value pairs.
/// </summary>
static class ComponentDataSerializer
{
	const string TypePrefix = "TYPE:";
	const string KindPrefix = "KIND:";
	const string JavaNamePrefix = "JAVA:";
	const string IsAbstractPrefix = "ABSTRACT:";
	const string HasDefaultCtorPrefix = "DEFCTOR:";
	const string AttrPrefix = "ATTR:";
	const string PropPrefix = "PROP:";
	const string CtorArgPrefix = "CTORARG:";
	const string SubAttrPrefix = "SUBATTR:";
	const string SubPropPrefix = "SUBPROP:";
	const string SubCtorArgPrefix = "SUBCTORARG:";
	const string EndAttrMarker = "ENDATTR";
	const string EndSubAttrMarker = "ENDSUBATTR";
	const string EndTypeMarker = "ENDTYPE";

	/// <summary>
	/// Serializes component data from scanned peers to a file.
	/// Only types with component attributes are included.
	/// </summary>
	public static void Serialize (IReadOnlyList<JavaPeerInfo> peers, string outputPath)
	{
		using var writer = new StreamWriter (outputPath, false, Encoding.UTF8);
		bool first = true;

		foreach (var peer in peers) {
			if (peer.ComponentData == null || peer.ComponentData.ComponentKind == ManifestComponentKind.None)
				continue;

			if (!first)
				writer.WriteLine ();
			first = false;

			writer.Write (TypePrefix);
			writer.WriteLine (peer.ManagedTypeName);

			writer.Write (KindPrefix);
			writer.WriteLine ((int) peer.ComponentData.ComponentKind);

			writer.Write (JavaNamePrefix);
			writer.WriteLine (peer.JavaName);

			writer.Write (IsAbstractPrefix);
			writer.WriteLine (peer.IsAbstract ? "1" : "0");

			writer.Write (HasDefaultCtorPrefix);
			// Check for activation ctor
			writer.WriteLine (peer.ActivationCtor != null ? "1" : "0");

			if (peer.ComponentData.ComponentAttribute != null) {
				WriteComponentAttributeInfo (writer, AttrPrefix, PropPrefix, CtorArgPrefix, EndAttrMarker, peer.ComponentData.ComponentAttribute);
			}

			foreach (var sub in peer.ComponentData.IntentFilters) {
				WriteComponentAttributeInfo (writer, SubAttrPrefix, SubPropPrefix, SubCtorArgPrefix, EndSubAttrMarker, sub);
			}

			foreach (var sub in peer.ComponentData.MetaDataEntries) {
				WriteComponentAttributeInfo (writer, SubAttrPrefix, SubPropPrefix, SubCtorArgPrefix, EndSubAttrMarker, sub);
			}

			foreach (var sub in peer.ComponentData.PropertyAttributes) {
				WriteComponentAttributeInfo (writer, SubAttrPrefix, SubPropPrefix, SubCtorArgPrefix, EndSubAttrMarker, sub);
			}

			if (peer.ComponentData.LayoutAttribute != null) {
				WriteComponentAttributeInfo (writer, SubAttrPrefix, SubPropPrefix, SubCtorArgPrefix, EndSubAttrMarker, peer.ComponentData.LayoutAttribute);
			}

			foreach (var sub in peer.ComponentData.GrantUriPermissions) {
				WriteComponentAttributeInfo (writer, SubAttrPrefix, SubPropPrefix, SubCtorArgPrefix, EndSubAttrMarker, sub);
			}

			writer.WriteLine (EndTypeMarker);
		}
	}

	static void WriteComponentAttributeInfo (
		TextWriter writer,
		string attrPrefix,
		string propPrefix,
		string ctorArgPrefix,
		string endMarker,
		ComponentAttributeInfo attr)
	{
		writer.Write (attrPrefix);
		writer.WriteLine (attr.AttributeType);

		foreach (var prop in attr.Properties) {
			writer.Write (propPrefix);
			writer.Write (prop.Key);
			writer.Write ('=');
			writer.WriteLine (SerializeValue (prop.Value));
		}

		foreach (var arg in attr.ConstructorArguments) {
			writer.Write (ctorArgPrefix);
			writer.WriteLine (SerializeValue (arg));
		}

		writer.WriteLine (endMarker);
	}

	/// <summary>
	/// Deserializes component data from a file into a list of ManifestTypeInfo objects.
	/// </summary>
	public static List<ManifestTypeInfo> Deserialize (string inputPath)
	{
		var result = new List<ManifestTypeInfo> ();
		if (!File.Exists (inputPath))
			return result;

		var lines = File.ReadAllLines (inputPath);
		int i = 0;

		while (i < lines.Length) {
			// Skip blank lines
			if (string.IsNullOrWhiteSpace (lines [i])) {
				i++;
				continue;
			}

			if (!lines [i].StartsWith (TypePrefix, StringComparison.Ordinal))
				throw new FormatException ($"Expected '{TypePrefix}' at line {i + 1}, got: {lines [i]}");

			var info = new ManifestTypeInfo ();
			info.FullName = lines [i].Substring (TypePrefix.Length);
			i++;

			var intentFilters = new List<ComponentAttributeInfo> ();
			var metaDataEntries = new List<ComponentAttributeInfo> ();
			var propertyAttributes = new List<ComponentAttributeInfo> ();
			var grantUriPermissions = new List<ComponentAttributeInfo> ();

			while (i < lines.Length && lines [i] != EndTypeMarker) {
				var line = lines [i];

				if (line.StartsWith (KindPrefix, StringComparison.Ordinal)) {
					info.ComponentKind = (ManifestComponentKind) int.Parse (line.Substring (KindPrefix.Length));
				} else if (line.StartsWith (JavaNamePrefix, StringComparison.Ordinal)) {
					var jniName = line.Substring (JavaNamePrefix.Length);
					info.JavaName = jniName.Replace ('/', '.');
					info.CompatJavaName = info.JavaName;
				} else if (line.StartsWith (IsAbstractPrefix, StringComparison.Ordinal)) {
					info.IsAbstract = line.Substring (IsAbstractPrefix.Length) == "1";
				} else if (line.StartsWith (HasDefaultCtorPrefix, StringComparison.Ordinal)) {
					info.HasPublicParameterlessConstructor = line.Substring (HasDefaultCtorPrefix.Length) == "1";
				} else if (line.StartsWith (AttrPrefix, StringComparison.Ordinal)) {
					info.ComponentAttribute = ReadComponentAttributeInfo (lines, ref i, AttrPrefix, PropPrefix, CtorArgPrefix, EndAttrMarker);
					continue; // ReadComponentAttributeInfo advances i past ENDATTR
				} else if (line.StartsWith (SubAttrPrefix, StringComparison.Ordinal)) {
					var sub = ReadComponentAttributeInfo (lines, ref i, SubAttrPrefix, SubPropPrefix, SubCtorArgPrefix, EndSubAttrMarker);
					ClassifySubAttribute (sub, intentFilters, metaDataEntries, propertyAttributes, grantUriPermissions, info);
					continue;
				}

				i++;
			}

			if (i < lines.Length && lines [i] == EndTypeMarker)
				i++;

			info.IntentFilters = intentFilters;
			info.MetaDataEntries = metaDataEntries;
			info.PropertyAttributes = propertyAttributes;
			info.GrantUriPermissions = grantUriPermissions;

			// Derive namespace from full name
			int lastDot = info.FullName.LastIndexOf ('.');
			info.Namespace = lastDot >= 0 ? info.FullName.Substring (0, lastDot) : "";

			result.Add (info);
		}

		return result;
	}

	static ComponentAttributeInfo ReadComponentAttributeInfo (
		string [] lines,
		ref int i,
		string attrPrefix,
		string propPrefix,
		string ctorArgPrefix,
		string endMarker)
	{
		var attr = new ComponentAttributeInfo {
			AttributeType = lines [i].Substring (attrPrefix.Length),
		};
		i++;

		var props = new Dictionary<string, object> (StringComparer.Ordinal);
		var ctorArgs = new List<object> ();

		while (i < lines.Length && lines [i] != endMarker) {
			var line = lines [i];

			if (line.StartsWith (propPrefix, StringComparison.Ordinal)) {
				var rest = line.Substring (propPrefix.Length);
				var eqIdx = rest.IndexOf ('=');
				if (eqIdx >= 0) {
					var key = rest.Substring (0, eqIdx);
					var val = DeserializeValue (rest.Substring (eqIdx + 1));
					props [key] = val;
				}
			} else if (line.StartsWith (ctorArgPrefix, StringComparison.Ordinal)) {
				var val = DeserializeValue (line.Substring (ctorArgPrefix.Length));
				ctorArgs.Add (val);
			}

			i++;
		}

		if (i < lines.Length && lines [i] == endMarker)
			i++;

		attr.Properties = props;
		attr.ConstructorArguments = ctorArgs;
		return attr;
	}

	static void ClassifySubAttribute (
		ComponentAttributeInfo sub,
		List<ComponentAttributeInfo> intentFilters,
		List<ComponentAttributeInfo> metaDataEntries,
		List<ComponentAttributeInfo> propertyAttributes,
		List<ComponentAttributeInfo> grantUriPermissions,
		ManifestTypeInfo info)
	{
		if (sub.AttributeType.EndsWith ("IntentFilterAttribute", StringComparison.Ordinal)) {
			intentFilters.Add (sub);
		} else if (sub.AttributeType.EndsWith ("MetaDataAttribute", StringComparison.Ordinal)) {
			metaDataEntries.Add (sub);
		} else if (sub.AttributeType.EndsWith ("PropertyAttribute", StringComparison.Ordinal)) {
			propertyAttributes.Add (sub);
		} else if (sub.AttributeType.EndsWith ("LayoutAttribute", StringComparison.Ordinal)) {
			info.LayoutAttribute = sub;
		} else if (sub.AttributeType.EndsWith ("GrantUriPermissionAttribute", StringComparison.Ordinal)) {
			grantUriPermissions.Add (sub);
		}
	}

	static string SerializeValue (object value)
	{
		if (value == null)
			return "null:";

		if (value is string s) {
			var sb = new StringBuilder (s.Length + 4);
			sb.Append ("s:");
			foreach (char c in s) {
				switch (c) {
				case '\\': sb.Append ("\\\\"); break;
				case '\n': sb.Append ("\\n"); break;
				case '\r': sb.Append ("\\r"); break;
				default: sb.Append (c); break;
				}
			}
			return sb.ToString ();
		}

		if (value is bool b)
			return "b:" + (b ? "1" : "0");

		if (value is int i32)
			return "i:" + i32.ToString ();

		if (value is long i64)
			return "l:" + i64.ToString ();

		if (value is float f)
			return "f:" + f.ToString ("R");

		if (value is double d)
			return "d:" + d.ToString ("R");

		if (value is string [] sa) {
			return "sa:" + string.Join ("\x1F", sa.Select (v => v.Replace ("\\", "\\\\").Replace ("\x1F", "\\u001F")));
		}

		// Fallback: convert to string
		return "s:" + value.ToString ()!.Replace ("\\", "\\\\").Replace ("\n", "\\n").Replace ("\r", "\\r");
	}

	static object DeserializeValue (string encoded)
	{
		if (encoded.StartsWith ("null:", StringComparison.Ordinal))
			return "";

		if (encoded.StartsWith ("s:", StringComparison.Ordinal))
			return UnescapeString (encoded.Substring (2));

		if (encoded.StartsWith ("b:", StringComparison.Ordinal))
			return encoded.Substring (2) == "1";

		if (encoded.StartsWith ("i:", StringComparison.Ordinal))
			return int.Parse (encoded.Substring (2));

		if (encoded.StartsWith ("l:", StringComparison.Ordinal))
			return long.Parse (encoded.Substring (2));

		if (encoded.StartsWith ("f:", StringComparison.Ordinal))
			return float.Parse (encoded.Substring (2));

		if (encoded.StartsWith ("d:", StringComparison.Ordinal))
			return double.Parse (encoded.Substring (2));

		if (encoded.StartsWith ("sa:", StringComparison.Ordinal)) {
			var parts = encoded.Substring (3).Split ('\x1F');
			return parts.Select (p => p.Replace ("\\u001F", "\x1F").Replace ("\\\\", "\\")).ToArray ();
		}

		// Fallback
		return encoded;
	}

	static string UnescapeString (string s)
	{
		var sb = new StringBuilder (s.Length);
		for (int idx = 0; idx < s.Length; idx++) {
			if (s [idx] == '\\' && idx + 1 < s.Length) {
				switch (s [idx + 1]) {
				case '\\': sb.Append ('\\'); idx++; break;
				case 'n': sb.Append ('\n'); idx++; break;
				case 'r': sb.Append ('\r'); idx++; break;
				default: sb.Append (s [idx]); break;
				}
			} else {
				sb.Append (s [idx]);
			}
		}
		return sb.ToString ();
	}
}
