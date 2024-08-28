using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Xamarin.Android.Tasks.LLVMIR
{
	class LlvmIrMetadataField
	{
		readonly LlvmIrTypeCache cache;

		public string Contents { get; }
		public bool IsReference { get; }

		public LlvmIrMetadataField (LlvmIrMetadataField other)
		{
			this.cache = other.cache;
			Contents = other.Contents;
			IsReference = other.IsReference;
		}

		public LlvmIrMetadataField (LlvmIrTypeCache cache, string value, bool isReference = false)
		{
			this.cache = cache;
			if (isReference) {
				Contents = $"!{value}";
			} else {
				Contents = QuoteString (value);
			}

			IsReference = isReference;
		}

		public LlvmIrMetadataField (LlvmIrTypeCache cache, object value)
		{
			this.cache = cache;
			Contents = FormatValue (value);
			IsReference = false;
		}

		string FormatValue (object value)
		{
			Type vt = value.GetType ();

			if (vt == typeof(string)) {
				return QuoteString ((string)value);
			}

			string irType = LlvmIrGenerator.MapToIRType (vt, cache);
			return $"{irType} {MonoAndroidHelper.CultureInvariantToString (value)}";
		}

		string QuoteString (string value)
		{
			return $"!{LlvmIrGenerator.QuoteStringNoEscape (value)}";
		}
	}

	class LlvmIrMetadataItem
	{
		readonly LlvmIrTypeCache cache;

		List<LlvmIrMetadataField> fields;

		public string Name { get; }

		public LlvmIrMetadataItem (LlvmIrMetadataItem other)
		{
			this.cache = other.cache;
			Name = other.Name;
			fields = new List<LlvmIrMetadataField> ();
			foreach (LlvmIrMetadataField field in other.fields) {
				fields.Add (new LlvmIrMetadataField (field));
			}
		}

		public LlvmIrMetadataItem (LlvmIrTypeCache cache, string name)
		{
			if (name.Length == 0) {
				throw new ArgumentException ("must not be empty", nameof (name));
			}

			this.cache = cache;
			Name = name;
			fields = new List<LlvmIrMetadataField> ();
		}

		public void AddReferenceField (string referenceName)
		{
			fields.Add (new LlvmIrMetadataField (cache, referenceName, isReference: true));
		}

		public void AddReferenceField (LlvmIrMetadataItem referencedItem)
		{
			AddReferenceField (referencedItem.Name);
		}

		public void AddField (object value)
		{
			AddField (new LlvmIrMetadataField (cache, value));
		}

		public void AddField (LlvmIrMetadataField field)
		{
			fields.Add (field);
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
		readonly LlvmIrTypeCache cache;
		ulong counter = 0;
		List<LlvmIrMetadataItem> items = new List<LlvmIrMetadataItem> ();
		Dictionary<string, LlvmIrMetadataItem> nameToItem = new Dictionary<string, LlvmIrMetadataItem> (StringComparer.Ordinal);

		public List<LlvmIrMetadataItem> Items => items;

		public LlvmIrMetadataManager (LlvmIrTypeCache cache)
		{
			this.cache = cache;
		}

		public LlvmIrMetadataManager (LlvmIrMetadataManager other)
		{
			this.cache = other.cache;
			foreach (LlvmIrMetadataItem item in other.items) {
				var newItem = new LlvmIrMetadataItem (item);
				items.Add (newItem);
				nameToItem.Add (newItem.Name, newItem);
			}
			counter = other.counter;
		}

		public LlvmIrMetadataItem Add (string name, params object[]? values)
		{
			if (nameToItem.ContainsKey (name)) {
				throw new InvalidOperationException ($"Internal error: metadata item '{name}' has already been added");
			}

			var ret = new LlvmIrMetadataItem (cache, name);

			if (values != null && values.Length > 0) {
				foreach (object v in values) {
					ret.AddField (v);
				}
			}
			items.Add (ret);

			nameToItem.Add (name, ret);
			return ret;
		}

		public LlvmIrMetadataItem AddNumbered (params object[]? values)
		{
			string name = counter.ToString (CultureInfo.InvariantCulture);
			counter++;
			return Add (name, values);
		}

		public LlvmIrMetadataItem? GetItem (string name)
		{
			if (nameToItem.TryGetValue (name, out LlvmIrMetadataItem? item)) {
				return item;
			}

			return null;
		}
	}
}
