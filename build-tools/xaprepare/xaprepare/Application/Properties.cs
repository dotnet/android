using System;
using System.Collections;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class Properties : AppObject, IEnumerable <KeyValuePair<string,string>>
	{
		SortedDictionary <string, string> properties = new SortedDictionary <string, string> (StringComparer.Ordinal);

		public event EventHandler<PropertiesChangedEventArgs> PropertiesChanged;

		public int Count => properties.Count;
		public bool IsDefined (string propertyName) => properties.ContainsKey (EnsureName (propertyName));
		public bool IsEmpty (string propertyName) => IsDefined (propertyName) && properties.TryGetValue (propertyName, out string v) && v != null;
		public string this [string name] => GetValue (name);

		public Properties ()
		{
			InitDefaults ();
		}

		public string GetValue (string propertyName, string defaultValue = null)
		{
			if (!properties.TryGetValue (EnsureName (propertyName), out string value))
				return defaultValue;

			return value;
		}

		public string GetRequiredValue (string propertyName)
		{
			string value = GetValue (propertyName);
			if (value == null)
				throw new InvalidOperationException ($"Required property '{propertyName}' has no defined value");
			return value;
		}

		public void Set (string name, string value)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));

			if (!properties.TryGetValue (name, out string oldValue))
				oldValue = null;

			string newValue = value ?? String.Empty;
			properties [name] = newValue;
			OnPropertiesChanged (name, newValue, oldValue);
		}

		string EnsureName (string propertyName)
		{
			if (String.IsNullOrEmpty (propertyName))
				throw new InvalidOperationException ("Property name is required");
			return propertyName;
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator ()
		{
			return properties.GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return (properties as IEnumerable).GetEnumerator ();
		}

		void OnPropertiesChanged (string name, string newValue, string oldValue)
		{
			if (PropertiesChanged == null)
				return;

			PropertiesChanged (this, new PropertiesChangedEventArgs (name, newValue, oldValue));
		}
    }
}
