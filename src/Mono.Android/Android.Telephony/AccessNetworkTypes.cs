using System;
using System.Collections.Generic;
using System.Text;

namespace Android.Telephony
{
#if ANDROID_31
	// These constants were added in API-28 and were missed in enumification.
	// Make an enum now because some new API-31 methods use them.
	public enum AccessNetworkTypes
	{
		Unknown = 0,
		Geran = 1,
		Utran = 2,
		Eutran = 3,
		Cdma2000 = 4,
		Iwlan = 5,
		Ngran = 6
	}
#endif
}
