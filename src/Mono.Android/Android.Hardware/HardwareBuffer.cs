using Android.Runtime;
using Java.Interop;

namespace Android.Hardware;

public partial class HardwareBuffer
{
	// These are manually bound because we do not have a way to bind the `long` enum values.
	// generator treats them as int, like:
	// __args [4] = new JniArgumentValue ((int) usage);

	// Metadata.xml XPath method reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/method[@name='create' and count(parameter)=5 and parameter[1][@type='int'] and parameter[2][@type='int'] and parameter[3][@type='int'] and parameter[4][@type='int'] and parameter[5][@type='long']]"
	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	[Register ("create", "(IIIIJ)Landroid/hardware/HardwareBuffer;", "", ApiSince = 26)]
	public static unsafe Android.Hardware.HardwareBuffer Create (int width, int height, [global::Android.Runtime.GeneratedEnum] Android.Hardware.HardwareBufferFormat format, int layers, Android.Hardware.HardwareBufferUsage usage)
	{
		const string __id = "create.(IIIIJ)Landroid/hardware/HardwareBuffer;";
		try {
			JniArgumentValue* __args = stackalloc JniArgumentValue [5];
			__args [0] = new JniArgumentValue (width);
			__args [1] = new JniArgumentValue (height);
			__args [2] = new JniArgumentValue ((int) format);
			__args [3] = new JniArgumentValue (layers);
			__args [4] = new JniArgumentValue ((long) usage);
			var __rm = _members.StaticMethods.InvokeObjectMethod (__id, __args);
			return global::Java.Lang.Object.GetObject<Android.Hardware.HardwareBuffer> (__rm.Handle, JniHandleOwnership.TransferLocalRef)!;
		} finally {
		}
	}

	[global::System.Runtime.Versioning.SupportedOSPlatformAttribute ("android26.0")]
	public unsafe Android.Hardware.HardwareBufferUsage Usage {
		// Metadata.xml XPath method reference: path="/api/package[@name='android.hardware']/class[@name='HardwareBuffer']/method[@name='getUsage' and count(parameter)=0]"
		[Register ("getUsage", "()J", "", ApiSince = 26)]
		get {
			const string __id = "getUsage.()J";
			try {
				var __rm = _members.InstanceMethods.InvokeAbstractInt64Method (__id, this, null);
				return (Android.Hardware.HardwareBufferUsage) __rm!;
			} finally {
			}
		}
	}
}
