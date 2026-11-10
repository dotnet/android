using MonoDroid.Utils;

namespace Xamarin.SourceWriter
{
	static class VisibilityParser
	{
		public static Visibility Parse (string value, bool trim = false)
		{
			if (value == null)
				return Visibility.Default;

			var comparison = LowercaseComparison.PrepareInvariant (ref value);
			if (trim)
				value = value.Trim ();

			if (value.Equals ("public", comparison))
				return Visibility.Public;
			if (value.Equals ("internal", comparison))
				return Visibility.Internal;
			if (value.Equals ("protected", comparison))
				return Visibility.Protected;
			if (value.Equals ("protected internal", comparison))
				return Visibility.Protected | Visibility.Internal;
			if (value.Equals ("private", comparison))
				return Visibility.Private;
			return Visibility.Default;
		}
	}
}
