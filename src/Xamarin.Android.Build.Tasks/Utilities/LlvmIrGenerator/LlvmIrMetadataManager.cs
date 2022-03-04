using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class LlvmIrMetadataField
	{
		public string Contents { get; }
		public bool IsReference { get; }

		public LlvmIrMetadataField (string value, bool isReference = false)
		{
			if (isReference) {
				Contents = $"!{value}";
			} else {
				Contents = QuoteString (value);
			}

			IsReference = isReference;
		}

		public LlvmIrMetadataField (object value)
		{
			Contents = FormatValue (value);
			IsReference = false;
		}

		string FormatValue (object value)
		{
			Type vt = value.GetType ();

			if (vt == typeof(string)) {
				return QuoteString ((string)value);
			}

			string irType = LlvmIrGenerator.MapManagedTypeToIR (vt);
			return $"{irType} {value}";
		}

		string QuoteString (string value)
		{
			return $"!{LlvmIrGenerator.QuoteStringNoEscape (value)}";
		}
	}

	class LlvmIrMetadataItem
	{
		List<LlvmIrMetadataField> fields;

		public string Name { get; }

		public LlvmIrMetadataItem (string name)
		{
			if (name.Length == 0) {
				throw new ArgumentException ("must not be empty", nameof (name));
			}

			Name = name;
			fields = new List<LlvmIrMetadataField> ();
		}

		public void AddReferenceField (string referenceName)
		{
			fields.Add (new LlvmIrMetadataField (referenceName, isReference: true));
		}

		public void AddField (object value)
		{
			fields.Add (new LlvmIrMetadataField (value));
		}

		public string Render ()
		{
			var sb = new StringBuilder ($"!{Name} = !{{");
			bool first = true;

			foreach (LlvmIrMetadataField field in fields) {
				if (first) {
					first = false;
				} else {
					sb.Append (", ");
				}

				sb.Append (field.Contents);
			}

			sb.Append ('}');

			return sb.ToString ();
		}
	}

	class LlvmIrMetadataManager
	{
		ulong counter = 0;
		List<LlvmIrMetadataItem> items = new List<LlvmIrMetadataItem> ();

		public List<LlvmIrMetadataItem> Items => items;

		public LlvmIrMetadataItem Add (string name, params object[]? values)
		{
			var ret = new LlvmIrMetadataItem (name);

			if (values != null && values.Length > 0) {
				foreach (object v in values) {
					ret.AddField (v);
				}
			}
			items.Add (ret);

			return ret;
		}

		public LlvmIrMetadataItem AddNumbered (params object[]? values)
		{
			string name = counter.ToString ();
			counter++;
			return Add (name, values);
		}
	}
}
