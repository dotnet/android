using System;

namespace Xamarin.Android.Prepare
{
	class NDKTool
	{
		public string Name               { get; }
		public string DestinationName    { get; } = String.Empty;
		public bool Prefixed             { get; }

		public NDKTool (string name, string? destinationName = null, bool prefixed = false)
		{
			if (name.Trim ().Length == 0) {
				throw new ArgumentException (nameof (name), "must not be empty");
			}
			Prefixed = prefixed;
			Name = name;
			if (String.IsNullOrWhiteSpace (destinationName)) {
				return;
			}
			DestinationName = destinationName!;
		}
	}
}
