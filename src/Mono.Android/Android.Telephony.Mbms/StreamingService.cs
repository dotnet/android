using Android.Runtime;

namespace Android.Telephony.Mbms;

public partial class StreamingService
{
	// Metadata.xml XPath field reference: path="/api/package[@name='android.telephony.mbms']/class[@name='StreamingService']/field[@name='STATE_STALLED']"
	[Register ("STATE_STALLED", ApiSince = 28)]
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	[global::System.Obsolete (@"This constant will be removed in a future version. Use Android.Telephony.StreamingState enum directly instead of this field.", error: true)]
	public const int StateStalled = 3;

	// Metadata.xml XPath field reference: path="/api/package[@name='android.telephony.mbms']/class[@name='StreamingService']/field[@name='STATE_STARTED']"
	[Register ("STATE_STARTED", ApiSince = 28)]
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	[global::System.Obsolete (@"This constant will be removed in a future version. Use Android.Telephony.StreamingState enum directly instead of this field.", error: true)]
	public const int StateStarted = 2;

	// Metadata.xml XPath field reference: path="/api/package[@name='android.telephony.mbms']/class[@name='StreamingService']/field[@name='STATE_STOPPED']"
	[Register ("STATE_STOPPED", ApiSince = 28)]
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android28.0")]
	[global::System.Obsolete (@"This constant will be removed in a future version. Use Android.Telephony.StreamingState enum directly instead of this field.", error: true)]
	public const int StateStopped = 1;
}
