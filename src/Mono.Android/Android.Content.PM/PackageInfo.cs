#if ANDROID_16
using System;

namespace Android.Content.PM
{
	public partial class PackageInfo
	{
		[Obsolete]
		public const RequestedPermission RequestedPermissionGranted = RequestedPermission.Granted;

		[Obsolete]
		public const RequestedPermission RequestedPermissionRequired = RequestedPermission.Required;
	}
}
#endif
