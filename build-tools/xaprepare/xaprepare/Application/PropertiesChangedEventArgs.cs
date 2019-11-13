using System;

namespace Xamarin.Android.Prepare
{
	class PropertiesChangedEventArgs : EventArgs
	{
		public string Name { get; }
		public string NewValue { get; }
		public string OldValue { get; }

		public PropertiesChangedEventArgs (string name, string newValue, string oldValue)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentException ("must not be null or empty", nameof (name));

			Name = name;
			NewValue = newValue;
			OldValue = oldValue;
		}
	}
}
