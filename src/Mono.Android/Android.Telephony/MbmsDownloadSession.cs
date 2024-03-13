using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Runtime;

namespace Android.Telephony;

public partial class MbmsDownloadSession
{
	// Metadata.xml XPath field reference: path="/api/package[@name='android.telephony']/class[@name='MbmsDownloadSession']/field[@name='STATUS_ACTIVELY_DOWNLOADING']"
	[Register ("STATUS_ACTIVELY_DOWNLOADING", ApiSince = 28)]
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	[global::System.Obsolete (@"This constant will be removed in the future version. Use Android.Telephony.Mbms.DownloadStatus enum directly instead of this field.", error: true)]
	public const int StatusActivelyDownloading = 1;

	// Metadata.xml XPath field reference: path="/api/package[@name='android.telephony']/class[@name='MbmsDownloadSession']/field[@name='STATUS_PENDING_DOWNLOAD']"
	[Register ("STATUS_PENDING_DOWNLOAD", ApiSince = 28)]
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	[global::System.Obsolete (@"This constant will be removed in the future version. Use Android.Telephony.Mbms.DownloadStatus enum directly instead of this field.", error: true)]
	public const int StatusPendingDownload = 2;

	// Metadata.xml XPath field reference: path="/api/package[@name='android.telephony']/class[@name='MbmsDownloadSession']/field[@name='STATUS_PENDING_DOWNLOAD_WINDOW']"
	[Register ("STATUS_PENDING_DOWNLOAD_WINDOW", ApiSince = 28)]
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	[global::System.Obsolete (@"This constant will be removed in the future version. Use Android.Telephony.Mbms.DownloadStatus enum directly instead of this field.", error: true)]
	public const int StatusPendingDownloadWindow = 4;

	// Metadata.xml XPath field reference: path="/api/package[@name='android.telephony']/class[@name='MbmsDownloadSession']/field[@name='STATUS_PENDING_REPAIR']"
	[Register ("STATUS_PENDING_REPAIR", ApiSince = 28)]
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	[global::System.Obsolete (@"This constant will be removed in the future version. Use Android.Telephony.Mbms.DownloadStatus enum directly instead of this field.", error: true)]
	public const int StatusPendingRepair = 3;

	// Metadata.xml XPath field reference: path="/api/package[@name='android.telephony']/class[@name='MbmsDownloadSession']/field[@name='STATUS_UNKNOWN']"
	[Register ("STATUS_UNKNOWN", ApiSince = 28)]
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	[global::System.Obsolete (@"This constant will be removed in the future version. Use Android.Telephony.Mbms.DownloadStatus enum directly instead of this field.", error: true)]
	public const int StatusUnknown = 0;
}
