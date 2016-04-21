using System;

using Android.Content;

namespace Android.Locations {

	public partial class LocationManager {

		public static LocationManager FromContext (Context context)
		{
			return context.GetSystemService (Context.LocationService) as LocationManager;
		}
	}
}


