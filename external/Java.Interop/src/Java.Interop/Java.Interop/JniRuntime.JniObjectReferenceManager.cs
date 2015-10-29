using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	partial class JniRuntime {

		public class JniObjectReferenceManager : IDisposable, ISetRuntime {

			protected   JniRuntime  Runtime { get; private set; }

			void JniRuntime.ISetRuntime.SetRuntime (JniRuntime runtime)
			{
				Runtime = runtime;
			}

			int grefc;
			public virtual int GlobalReferenceCount {
				get {return grefc;}
			}

			int wgrefc;
			public virtual int WeakGlobalReferenceCount {
				 get {return wgrefc;}
			}

			public virtual void WriteLocalReferenceLine (string format, params object[] args)
			{
			}

			public virtual JniObjectReference CreateLocalReference (JniEnvironmentInfo environment, JniObjectReference reference)
			{
				if (!reference.IsValid)
					return reference;
				AssertCount (environment.LocalReferenceCount, "LREF", reference.ToString ());
				environment.LocalReferenceCount++;
				return JniEnvironment.References.NewLocalRef (reference);
			}

			public virtual void DeleteLocalReference (JniEnvironmentInfo environment, ref JniObjectReference reference)
			{
				if (!reference.IsValid)
					return;
				AssertReferenceType (ref reference, JniObjectReferenceType.Local);
				AssertCount (environment.LocalReferenceCount, "LREF", reference.ToString ());
				environment.LocalReferenceCount--;
				JniEnvironment.References.DeleteLocalRef (reference.Handle);
				reference.Invalidate ();
			}

			public virtual void CreatedLocalReference (JniEnvironmentInfo environment, JniObjectReference reference)
			{
				if (!reference.IsValid)
					return;
				AssertCount (environment.LocalReferenceCount, "LREF", reference.ToString ());
				environment.LocalReferenceCount++ ;
			}

			public virtual IntPtr ReleaseLocalReference (JniEnvironmentInfo environment, ref JniObjectReference reference)
			{
				if (!reference.IsValid)
					return IntPtr.Zero;
				AssertCount (environment.LocalReferenceCount, "LREF", reference.ToString ());
				environment.LocalReferenceCount--;
				var h           = reference.Handle;
				reference.Invalidate ();
				return h;
			}

			public virtual void WriteGlobalReferenceLine (string format, params object[] args)
			{
			}

			public virtual JniObjectReference CreateGlobalReference (JniObjectReference reference)
			{
				if (!reference.IsValid)
					return reference;
				AssertCount (grefc, "GREF", reference.ToString ());
				Interlocked.Increment (ref grefc);
				return JniEnvironment.References.NewGlobalRef (reference);
			}

			public virtual void DeleteGlobalReference (ref JniObjectReference reference)
			{
				if (!reference.IsValid)
					return;
				AssertReferenceType (ref reference, JniObjectReferenceType.Global);
				AssertCount (grefc, "GREF", reference.ToString ());
				Interlocked.Decrement (ref grefc);
				JniEnvironment.References.DeleteGlobalRef (reference.Handle);
				reference.Invalidate ();
			}

			public virtual JniObjectReference CreateWeakGlobalReference (JniObjectReference reference)
			{
				if (!reference.IsValid)
					return reference;
				AssertCount (wgrefc, "WGREF", reference.ToString ());
				Interlocked.Increment (ref wgrefc);
				return JniEnvironment.References.NewWeakGlobalRef (reference);
			}

			public virtual void DeleteWeakGlobalReference (ref JniObjectReference reference)
			{
				if (!reference.IsValid)
					return;
				AssertReferenceType (ref reference, JniObjectReferenceType.WeakGlobal);
				AssertCount (wgrefc, "WGREF", reference.ToString ());
				Interlocked.Decrement (ref wgrefc);
				JniEnvironment.References.DeleteWeakGlobalRef (reference.Handle);
				reference.Invalidate ();
			}

			public void Dispose ()
			{
				Dispose (false);
			}

			protected virtual void Dispose (bool disposing)
			{
			}

			[Conditional ("DEBUG")]
			static void AssertReferenceType (ref JniObjectReference reference, JniObjectReferenceType type)
			{
				Debug.Assert (reference.Type == type,
						string.Format ("Object reference {0} should be of type {1}, is instead {2}!",
							reference.ToString (), type, reference.Type));
			}

			[Conditional ("DEBUG")]
			static void AssertCount (int count, string type, string value)
			{
				Debug.Assert (count >= 0,
						string.Format ("{0} count is {1}, expected to be >= 0 when dealing with handle {2} on thread {3}",
							type, count, value, Thread.CurrentThread.ManagedThreadId));
			}
		}
	}
}
