using System;

namespace Android.Runtime {

	public interface IJavaObject : IDisposable {
		IntPtr Handle { get; }
	}
}
