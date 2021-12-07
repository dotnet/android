#if !JAVA_INTEROP1

using System;

namespace Android.Runtime {

	public interface IJavaObject : IDisposable {
		IntPtr Handle { get; }
	}
}

#endif  // !JAVA_INTEROP1
