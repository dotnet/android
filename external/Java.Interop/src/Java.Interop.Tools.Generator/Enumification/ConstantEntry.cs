using System;
using System.Xml.Linq;

namespace Java.Interop.Tools.Generator.Enumification
{
	/// <summary>
	/// This represents a Java int constant and/or a C# enum entry.
	/// </summary>
	public class ConstantEntry
	{
		public ConstantAction Action { get; set; }
		public int ApiLevel { get; set; }
		public string? JavaSignature { get; set; }
		public string? Value { get; set; }
		public string? EnumFullType { get; set; }
		public string? EnumMember { get; set; }
		public FieldAction FieldAction { get; set; }
		public bool IsFlags { get; set; }

		public string EnumNamespace {
			get {
				if (!EnumFullType.HasValue ())
					return string.Empty;

				var index = EnumFullType.LastIndexOf ('.');

				// There is no namespace, only a type
				if (index == -1)
					return string.Empty;

				return EnumFullType.Substring (0, index);
			}
		}

		public string EnumType {
			get {
				if (!EnumFullType.HasValue ())
					return string.Empty;

				var index = EnumFullType.LastIndexOf ('.');

				// There is no namespace, only a type
				if (index == -1)
					return EnumFullType;

				return EnumFullType.Substring (index + 1);
			}
		}

		public string JavaPackage {
			get {
				if (!JavaSignature.HasValue ())
					return string.Empty;

				var index = JavaSignature.LastIndexOf ('/');

				// There is no namespace, only a type
				if (index == -1)
					return string.Empty;

				return JavaSignature.Substring (0, index);
			}
		}

		public string JavaType {
			get {
				if (!JavaSignature.HasValue ())
					return string.Empty;

				var index = JavaSignature.LastIndexOf ('/');
				var dot_index = JavaSignature.LastIndexOf ('.');

				return JavaSignature.Substring (index + 1, dot_index - index - 1);
			}
		}

		public string JavaName {
			get {
				if (!JavaSignature.HasValue ())
					return string.Empty;

			var index = JavaSignature.LastIndexOf ('.');

				return JavaSignature.Substring (index + 1);
			}
		}

		public static ConstantEntry FromString (string line, bool transientMode = false)
		{
			var parser = new CsvParser (line);

			if (parser.GetField (0).In ("?", "I", "E", "A", "R"))
				return FromVersion2String (parser);

			return FromVersion1String (parser, transientMode);
		}

		static ConstantEntry FromVersion1String (CsvParser parser, bool transientMode)
		{
			var entry = new ConstantEntry {
				Action = ConstantAction.Enumify,
				ApiLevel = parser.GetFieldAsInt (0),
				EnumFullType = parser.GetField (1),
				EnumMember = parser.GetField (2),
				JavaSignature = parser.GetField (3),
				Value = parser.GetField (4),
				IsFlags = parser.GetField (5).ToLowerInvariant () == "flags",
				FieldAction = transientMode ? FieldAction.Remove : FieldAction.Keep
			};

			if (!entry.EnumNamespace.HasValue ()) {
				// This is a line that only deletes a const, not maps it to an enum
				entry.Action = ConstantAction.Remove;
				entry.FieldAction = FieldAction.Remove;
			} else if (!entry.JavaSignature.HasValue ()) {
				// This is a line that adds an unmapped enum member
				entry.Action = ConstantAction.Add;
				entry.FieldAction = transientMode ? FieldAction.Remove : FieldAction.None;
			}

			entry.NormalizeJavaSignature ();

			return entry;
		}

		static ConstantEntry FromVersion2String (CsvParser parser)
		{
			var entry = new ConstantEntry {
				Action = FromConstantActionString (parser.GetField (0)),
				ApiLevel = parser.GetFieldAsInt (1),
				JavaSignature = parser.GetField (2),
				Value = parser.GetField (3),
				EnumFullType = parser.GetField (4),
				EnumMember = parser.GetField (5),
				FieldAction = FromFieldActionString (parser.GetField (6)),
				IsFlags = parser.GetField (7).ToLowerInvariant () == "flags"
			};

			entry.NormalizeJavaSignature ();

			return entry;
		}

		public static ConstantEntry FromElement (XElement elem)
		{
			var entry = new ConstantEntry {
				Action = ConstantAction.None,
				ApiLevel = NamingConverter.ParseApiLevel (elem),
				JavaSignature = elem.Parent.Parent.Attribute ("name").Value,
				Value = elem.Attribute ("value")?.Value,
			};

			var java_package = elem.Parent.Parent.Attribute ("name").Value.Replace ('.', '/');
			var java_type = elem.Parent.Attribute ("name").Value.Replace ('.', '$');
			var java_member = elem.Attribute ("name").Value;

			entry.JavaSignature = $"{java_package}/{java_type}.{java_member}".TrimStart ('/');

			// Interfaces get an "I:" prefix
			if (elem.Parent.Name.LocalName == "interface")
				entry.JavaSignature = "I:" + entry.JavaSignature;

			return entry;
		}

		public string ToVersion2String ()
		{
			var fields = new [] {
				Action == ConstantAction.None ? "?" : Action.ToString ().Substring (0, 1),
				ApiLevel.ToString (),
				JavaSignature,
				Value,
				EnumFullType,
				EnumMember,
				ToConstantFieldActionString (FieldAction),
				IsFlags ? "flags" : string.Empty
			};

			return string.Join (",", fields);
		}

		static string ToConstantFieldActionString (FieldAction value)
		{
			return value switch
			{
				FieldAction.None => string.Empty,
				FieldAction.Remove => "remove",
				FieldAction.Keep => "keep",
				_ => string.Empty
			};

		}

		static ConstantAction FromConstantActionString (string value)
		{
			return value switch
			{
				"?" => ConstantAction.None,
				"I" => ConstantAction.Ignore,
				"E" => ConstantAction.Enumify,
				"A" => ConstantAction.Add,
				"R" => ConstantAction.Remove,
				_ => throw new ArgumentOutOfRangeException (nameof (value), $"Specified action '{value}' is not valid"),
			};
		}

		static FieldAction FromFieldActionString (string value)
		{
			return value.ToLowerInvariant () switch
			{
				"" => FieldAction.None,
				"remove" => FieldAction.Remove,
				"keep" => FieldAction.Keep,
				_ => throw new ArgumentOutOfRangeException (nameof (value), $"Specified field action '{value}' is not valid"),
			};
		}

		void NormalizeJavaSignature ()
		{
			// Somehow we got a mix of using dollar signs and periods
			// for nested classes. Normalize to dollar signs.
			if (JavaSignature.HasValue ())
				JavaSignature = $"{JavaPackage}/{JavaType.Replace ('.', '$')}.{JavaName}";
		}
	}
}
