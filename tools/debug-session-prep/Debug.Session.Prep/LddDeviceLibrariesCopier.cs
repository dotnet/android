using System;

using Xamarin.Android.Utilities;
using Xamarin.Android.Tasks;

namespace Xamarin.Debug.Session.Prep;

class LddDeviceLibraryCopier : DeviceLibraryCopier
{
	public LddDeviceLibraryCopier (XamarinLoggingHelper log, AdbRunner adb, bool appIs64Bit, string localDestinationDir, int deviceApiLevel)
		: base (log, adb, appIs64Bit, localDestinationDir, deviceApiLevel)
	{}

	public override bool Copy ()
	{
		throw new NotImplementedException();
	}
}
