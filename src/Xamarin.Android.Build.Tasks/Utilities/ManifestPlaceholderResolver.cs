using System;
using System.Globalization;
using System.Linq;

namespace Xamarin.Android.Tasks;

static class ManifestPlaceholderResolver
{
	internal static string Replace (string []? placeholders, string text, Action<string, string>? logCodedWarning = null)
	{
		string result = text;
		if (placeholders is null) {
			return result;
		}
		foreach (var entry in placeholders.Select (e => e.Split (new char [] { '=' }, 2, StringSplitOptions.None))) {
			if (entry.Length == 2) {
				result = result.Replace ("${" + entry [0] + "}", entry [1]);
			} else if (logCodedWarning is not null) {
				logCodedWarning ("XA1010", string.Format (CultureInfo.CurrentCulture, Properties.Resources.XA1010, string.Join (";", placeholders)));
			}
		}
		return result;
	}
}
