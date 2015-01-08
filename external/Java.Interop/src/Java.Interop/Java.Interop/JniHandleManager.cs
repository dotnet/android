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
			AssertCount (environment.LrefCount, "LREF", value.DangerousGetHandle ());
			environment.LrefCount++;
			return JniEnvironment.Handles.NewLocalRef (value);
		}

		public void DeleteLocalReference (JniEnvironment environment, IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			AssertCount (environment.LrefCount, "LREF", value);
			environment.LrefCount--;
			JniEnvironment.Handles.DeleteLocalRef (value);
		}

		public void CreatedLocalReference (JniEnvironment environment, JniLocalReference value)
		{
			if (value == null || value.IsInvalid)
				return;
			AssertCount (environment.LrefCount, "LREF", value.DangerousGetHandle ());
			environment.LrefCount++ ;
		}

		public IntPtr ReleaseLocalReference (JniEnvironment environment, JniLocalReference value)
		{
			if (value == null || value.IsInvalid)
				return IntPtr.Zero;
			AssertCount (environment.LrefCount, "LREF", value.DangerousGetHandle ());
			environment.LrefCount--;
			return value._GetAndClearHandle ();
		}

		public JniGlobalReference CreateGlobalReference (JniReferenceSafeHandle value)
		{
			if (value == null || value.IsInvalid)
				return null;
			AssertCount (grefc, "GREF", value.DangerousGetHandle ());
			Interlocked.Increment (ref grefc);
			return JniEnvironment.Handles.NewGlobalRef (value);
		}

		public void DeleteGlobalReference (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			AssertCount (grefc, "GREF", value);
			Interlocked.Decrement (ref grefc);
			JniEnvironment.Handles.DeleteGlobalRef (value);
		}

		public JniWeakGlobalReference CreateWeakGlobalReference (JniReferenceSafeHandle value)
		{
			if (value == null || value.IsInvalid)
				return null;
			AssertCount (wgrefc, "WGREF", value.DangerousGetHandle ());
			Interlocked.Increment (ref wgrefc);
			return JniEnvironment.Handles.NewWeakGlobalRef (value);
		}

		public void DeleteWeakGlobalReference (IntPtr value)
		{
			if (value == IntPtr.Zero)
				return;
			AssertCount (wgrefc, "WGREF", value);
			Interlocked.Decrement (ref wgrefc);
			JniEnvironment.Handles.DeleteWeakGlobalRef (value);
		}

		public void Dispose ()
		{
		}

		[Conditional ("DEBUG")]
		static void AssertCount (int count, string type, IntPtr value)
		{
			Debug.Assert (count >= 0,
					string.Format ("{0} count is {1}, expected to be >= 0 when dealing with handle 0x{2} on thread {3}",
						type, count, value.ToString ("x"), Thread.CurrentThread.ManagedThreadId));
		}
	}
}
