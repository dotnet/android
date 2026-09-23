#nullable enable

namespace Xamarin.Android.Tasks
{
	public enum SequencePointsMode
	{
		None,
		Normal,
		Offline,
	}

	static class SequencePointsModeParser
	{
		public static bool TryParse (string? value, out SequencePointsMode mode)
		{
			mode = SequencePointsMode.None;
			switch ((value ?? "").ToLowerInvariant ().Trim ()) {
				case "none":
					return true;
				case "normal":
					mode = SequencePointsMode.Normal;
					return true;
				case "offline":
					mode = SequencePointsMode.Offline;
					return true;
			}

			return false;
		}
	}
}
