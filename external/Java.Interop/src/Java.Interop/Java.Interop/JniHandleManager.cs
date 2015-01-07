using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	public class JniHandleManager : IJniHandleManager {

		int grefc;
		public int GlobalReferenceCount {
			get {return grefc;}
		}

		int wgrefc;
		public int WeakGlobalReferenceCount {
			 get {return wgrefc;}
		}

		public JniLocalReference CreateLocalReference (JniEnvironment environment, JniReferenceSafeHandle value)
		{
			if (value == null || value.IsInvalid)
				return null;
			environment.LrefCount++;
			return JniEnvironment.Handles.NewLocalRef (value);
		}

		public void DeleteLocalReference (JniEnvironment environment, IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			environment.LrefCount--;
			JniEnvironment.Handles.DeleteLocalRef (value);
		}

		public void CreatedLocalReference (JniEnvironment environment, JniLocalReference value)
		{
			if (value == null || value.IsInvalid)
				return;
			environment.LrefCount++;
		}

		public IntPtr ReleaseLocalReference (JniEnvironment environment, JniLocalReference value)
		{
			if (value == null || value.IsInvalid)
				return IntPtr.Zero;
			environment.LrefCount--;
			return value._GetAndClearHandle ();
		}

		public JniGlobalReference CreateGlobalReference (JniReferenceSafeHandle value)
		{
			if (value == null || value.IsInvalid)
				return null;
			Interlocked.Increment (ref grefc);
			return JniEnvironment.Handles.NewGlobalRef (value);
		}

		public void DeleteGlobalReference (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			Debug.Assert (grefc > 0, "GREF count must be positive. (How could it be negative?!)");
			Interlocked.Decrement (ref grefc);
			JniEnvironment.Handles.DeleteGlobalRef (value);
		}

		public JniWeakGlobalReference CreateWeakGlobalReference (JniReferenceSafeHandle value)
		{
			if (value == null || value.IsInvalid)
				return null;
			Interlocked.Increment (ref wgrefc);
			return JniEnvironment.Handles.NewWeakGlobalRef (value);
		}

		public void DeleteWeakGlobalReference (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			Debug.Assert (wgrefc > 0, "Weak GREF count must be positive. (How could it be negative?!)");
			Interlocked.Decrement (ref wgrefc);
			JniEnvironment.Handles.DeleteWeakGlobalRef (value);
		}

		public void Dispose ()
		{
		}
	}
}
