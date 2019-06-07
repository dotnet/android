using System;

namespace Xamarin.Android.Prepare
{
	class CompressionFormat
	{
		public string Description { get; }
		public string Extension   { get; }
		public string Name        { get; }

		public CompressionFormat (string name, string description, string extension)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentNullException (nameof (name));

			if (String.IsNullOrEmpty (description))
				throw new ArgumentNullException (nameof (description));

			if (String.IsNullOrEmpty (extension))
				throw new ArgumentNullException (nameof (extension));

			Description = description;
			Extension = extension;
			Name = name;
		}
	}
}
