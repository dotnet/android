using System.IO;
using System.Reflection;

using Xamarin.Android.Utilities;

namespace Xamarin.Debug.Session.Prep;

static class Utilities
{
	public static string? ReadManifestResource (XamarinLoggingHelper log, string resourceName)
	{
		using (var from = Assembly.GetExecutingAssembly ().GetManifestResourceStream (resourceName)) {
			if (from == null) {
				log.ErrorLine ($"Manifest resource '{resourceName}' cannot be loaded");
				return null;
			}

			using (var sr = new StreamReader (from)) {
				return sr.ReadToEnd ();
			}
		}
	}
}
