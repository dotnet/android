using System;

namespace Android.Runtime {

	[Java.Interop.JniValueMarshaler (typeof (IJavaObjectValueMarshaler))]
	public interface IJavaObject : IDisposable {
		IntPtr Handle { get; }
	}
}
