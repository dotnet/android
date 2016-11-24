using System;
using System.Globalization;
using System.Resources;

namespace LibraryResources {

	public static class SatelliteResources {

		static readonly ResourceManager Resources = new ResourceManager ("LibraryResources.strings", typeof (SatelliteResources).Assembly);

		public static string GetString (string resourceName, CultureInfo culture = null)
		{
			return Resources.GetString (resourceName, culture);
		}
	}
}

