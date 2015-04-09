using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	// Implementations MUST be thread safe!
	public interface IJniHandleManager : IDisposable {

		int                     GlobalReferenceCount        {get;}
		int                     WeakGlobalReferenceCount    {get;}

		void                    WriteLocalReferenceLine (string format, params object[] args);

		JniLocalReference       CreateLocalReference (JniEnvironment environment, JniReferenceSafeHandle value);
		void                    DeleteLocalReference (JniEnvironment environment, IntPtr value);

		// JniLocalReference was created as a result of another JNI call,
		// e.g. JniEnvironment.Array.NewByteArray()
		void                    CreatedLocalReference (JniEnvironment environment, JniLocalReference value);

		// "Release" doesn't destroy the local ref; this is an "accounting" method
		// to give the VM to update local reference counts. The IntPtr returned
		// will be passed to the JVM as a JNI return value.
		IntPtr                  ReleaseLocalReference (JniEnvironment environment, JniLocalReference value);

		void                    WriteGlobalReferenceLine (string format, params object[] args);

		JniGlobalReference      CreateGlobalReference (JniReferenceSafeHandle value);
		void                    DeleteGlobalReference (IntPtr value);

		JniWeakGlobalReference  CreateWeakGlobalReference (JniReferenceSafeHandle value);
		void                    DeleteWeakGlobalReference (IntPtr value);
	}
}
