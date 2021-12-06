using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace Java.Interop.Tools.Generator.Enumification
{
	public class MethodMapEntry
	{
		public MethodAction Action { get; set; }
		public int ApiLevel { get; set; }
		public string? JavaPackage { get; set; }
		public string? JavaType { get; set; }
		public string? JavaName { get; set; }
		public string? ParameterName { get; set; }
		public string? EnumFullType { get; set; }
		public string JavaSignature => $"{JavaPackage}/{JavaType}.{JavaName}.{ParameterName}";
		public bool IsInterface { get; set; }

		public static IEnumerable<MethodMapEntry> FromXml (XElement element)
		{
			// Handle fields first
			if (element.Name == "field") {
				yield return FromElement (element, element.XGetAttribute ("name")!);
				yield break;
			}

			// Now methods and constructors
			// There could be multiple entries, from the return type and multiple parameters
			if (element.XGetAttribute ("return") == "int")
				yield return FromElement (element, "return");

			foreach (var p in element.Elements ("parameter"))
				if (p.XGetAttribute ("type") == "int")
					yield return FromElement (element, p.XGetAttribute ("name")!);
		}

		static MethodMapEntry FromElement (XElement element, string parameterName)
		{
			var entry = new MethodMapEntry {
				JavaPackage = element.Parent.Parent.XGetAttribute ("name")?.Replace ('.', '/'),
				JavaType = element.Parent.XGetAttribute ("name")?.Replace ('.', '$'),
				JavaName = element.XGetAttribute ("name"),
				ParameterName = parameterName,
				ApiLevel = NamingConverter.ParseApiLevel (element),
				IsInterface = element.Parent.Name == "interface"
			};

			if (element.Name == "constructor")
				entry.JavaName = "ctor";

			return entry;
		}

		public static MethodMapEntry FromString (string line)
		{
			var parser = new CsvParser (line);

			if (parser.GetField (0).In ("?", "I", "E"))
				return FromVersion2String (parser);

			return FromVersion1String (parser);
		}

		static MethodMapEntry FromVersion1String (CsvParser parser)
		{
			var entry = new MethodMapEntry {
				Action = MethodAction.Enumify,
				ApiLevel = parser.GetFieldAsInt (0),
				JavaPackage = parser.GetField (1),
				JavaType = parser.GetField (2),
				JavaName = parser.GetField (3),
				ParameterName = parser.GetField (4),
				EnumFullType = parser.GetField (5)
			};

			if (entry.JavaType.StartsWith ("[Interface]", StringComparison.Ordinal)) {
				entry.IsInterface = true;
				entry.JavaType = entry.JavaType.Substring ("[Interface]".Length);
			}

			return entry;
		}

		static MethodMapEntry FromVersion2String (CsvParser parser)
		{
			var entry = new MethodMapEntry {
				Action = FromMethodActionString (parser.GetField (0)),
				ApiLevel = parser.GetFieldAsInt (1),
				JavaPackage = parser.GetField (2),
				JavaType = parser.GetField (3),
				JavaName = parser.GetField (4),
				ParameterName = parser.GetField (5),
				EnumFullType = parser.GetField (6)
			};

			if (entry.JavaType.StartsWith ("[Interface]", StringComparison.Ordinal)) {
				entry.IsInterface = true;
				entry.JavaType = entry.JavaType.Substring ("[Interface]".Length);
			}

			return entry;
		}

		static MethodAction FromMethodActionString (string value)
		{
			return value switch {
				"?" => MethodAction.None,
				"I" => MethodAction.Ignore,
				"E" => MethodAction.Enumify,
				_ => throw new ArgumentOutOfRangeException (nameof (value), $"Specified action '{value}' is not valid"),
			};
		}

		public string ToVersion1String ()
		{
			var fields = new [] {
				ApiLevel.ToString (),
				JavaPackage,
				(IsInterface ? "I:" : string.Empty) + JavaType,
				JavaName,
				ParameterName,
				EnumFullType,
			};

			return string.Join (",", fields);
		}

		public string ToVersion2String ()
		{
			var fields = new [] {
				Action == MethodAction.None ? "?" : Action.ToString ().Substring (0, 1),
				ApiLevel.ToString (),
				JavaPackage,
				(IsInterface ? "[Interface]" : string.Empty) + JavaType,
				JavaName,
				ParameterName,
				EnumFullType,
			};

			return string.Join (",", fields);
		}
	}
}
