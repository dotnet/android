using System;

namespace Xamarin.Android.Prepare
{
	class NDKTool
	{
		public string Name               { get; }
		public string DestinationName    { get; } = String.Empty;

		public NDKTool (string name, string? destinationName = null)
		{
			if (name.Trim ().Length == 0) {
				throw new ArgumentException (nameof (name), "must not be empty");
			}
			Name = name;
			if (String.IsNullOrWhiteSpace (destinationName)) {
				return;
			}
			DestinationName = destinationName!;
		}
	}
}
