# nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop {

	partial class JniRuntime {

		public abstract class JniObjectReferenceManager : IDisposable, ISetRuntime {

			public JniObjectReferenceManager ()
			{
			}

			JniRuntime?             runtime;
			public  JniRuntime      Runtime {
				get => runtime ?? throw new NotSupportedException ();
			}

			public virtual void OnSetRuntime (JniRuntime runtime)
			{
				this.runtime = runtime;
			}

			public abstract int GlobalReferenceCount {
				get;
			}

			public abstract int WeakGlobalReferenceCount {
				get;
			}

			public virtual bool LogLocalReferenceMessages {
				get {return false;}
			}

			public virtual void WriteLocalReferenceLine (string format, params object[] args)
			{
			}

			internal       JniObjectReference CreateLocalReference (JniEnvironmentInfo environment, JniObjectReference reference)
			{
				var lrefc   = environment.LocalReferenceCount;
				var r       = CreateLocalReference (reference, ref lrefc);
				environment.LocalReferenceCount = lrefc;
				return r;
			}

			public virtual JniObjectReference CreateLocalReference (JniObjectReference reference, ref int localReferenceCount)
			{
				if (!reference.IsValid)
					return reference;
				if (localReferenceCount < 0)
					AssertCount(localReferenceCount, "LREF", reference.ToString());
				localReferenceCount++;
				return JniEnvironment.References.NewLocalRef (reference);
			}

			internal       void DeleteLocalReference (JniEnvironmentInfo environment, ref JniObjectReference reference)
			{
				var lrefc   = environment.LocalReferenceCount;
				DeleteLocalReference (ref reference, ref lrefc);
				environment.LocalReferenceCount = lrefc;
			}

			public virtual void DeleteLocalReference (ref JniObjectReference reference, ref int localReferenceCount)
			{
				if (!reference.IsValid)
					return;
				AssertReferenceType (ref reference, JniObjectReferenceType.Local);
				localReferenceCount--;
				if (localReferenceCount < 0)
					AssertCount(localReferenceCount, "LREF", reference.ToString());
				JniEnvironment.References.DeleteLocalRef (reference.Handle);
				reference.Invalidate ();
			}

			internal       void CreatedLocalReference (JniEnvironmentInfo environment, JniObjectReference reference)
			{
				var lrefc   = environment.LocalReferenceCount;
				CreatedLocalReference (reference, ref lrefc);
				environment.LocalReferenceCount = lrefc;
			}

			public virtual void CreatedLocalReference (JniObjectReference reference, ref int localReferenceCount)
			{
				if (!reference.IsValid)
					return;
				if (localReferenceCount < 0)
					AssertCount(localReferenceCount, "LREF", reference.ToString());
				localReferenceCount++ ;
			}

			internal       IntPtr ReleaseLocalReference (JniEnvironmentInfo environment, ref JniObjectReference reference)
			{
				var lrefc   = environment.LocalReferenceCount;
				var r       = ReleaseLocalReference (ref reference, ref lrefc);
				environment.LocalReferenceCount = lrefc;
				return r;
			}

			public virtual IntPtr ReleaseLocalReference (ref JniObjectReference reference, ref int localReferenceCount)
			{
				if (!reference.IsValid)
					return IntPtr.Zero;
				localReferenceCount--;
				if (localReferenceCount < 0)
					AssertCount (localReferenceCount, "LREF", reference.ToString ());
				var h           = reference.Handle;
				reference.Invalidate ();
				return h;
			}

			public virtual bool LogGlobalReferenceMessages {
				get {return false;}
			}

			public virtual void WriteGlobalReferenceLine (string format, params object?[] args)
			{
			}

			public virtual JniObjectReference CreateGlobalReference (JniObjectReference reference)
			{
				if (!reference.IsValid)
					return reference;
				var n   = JniEnvironment.References.NewGlobalRef (reference);
				if (GlobalReferenceCount < 0)
					AssertCount (GlobalReferenceCount, "GREF", reference.ToString ());
				return n;
			}

			public virtual void DeleteGlobalReference (ref JniObjectReference reference)
			{
				if (!reference.IsValid)
					return;
				AssertReferenceType (ref reference, JniObjectReferenceType.Global);
				if (GlobalReferenceCount < 0)
					AssertCount(GlobalReferenceCount, "GREF", reference.ToString());
				JniEnvironment.References.DeleteGlobalRef (reference.Handle);
				reference.Invalidate ();
			}

			public virtual JniObjectReference CreateWeakGlobalReference (JniObjectReference reference)
			{
				if (!reference.IsValid)
					return reference;
				var n   = JniEnvironment.References.NewWeakGlobalRef (reference);
				if (WeakGlobalReferenceCount < 0)
					AssertCount(WeakGlobalReferenceCount, "WGREF", reference.ToString());
				return n;
			}

			public virtual void DeleteWeakGlobalReference (ref JniObjectReference reference)
			{
				if (!reference.IsValid)
					return;
				AssertReferenceType (ref reference, JniObjectReferenceType.WeakGlobal);
				if (WeakGlobalReferenceCount < 0)
					AssertCount(WeakGlobalReferenceCount, "WGREF", reference.ToString());
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
				if (reference.Type == type)
					return;

				Debug.Assert (reference.Type == type,
						string.Format ("Object reference {0} should be of type {1}, is instead {2}!",
							reference.ToString (), type, reference.Type));
			}

			[Conditional ("DEBUG")]
			void AssertCount (int count, string type, string value)
			{
				Debug.Assert (count >= 0,
						string.Format ("{0} count is {1}, expected to be >= 0 when dealing with handle {2} on thread '{3}'({4}).",
							type, count, value, Runtime.GetCurrentManagedThreadName (), Environment.CurrentManagedThreadId));
				if (count < 0) {
					Debug.WriteLine ("Perhaps `Java.Interop.JniEnvironment.References.{0}()` should be used to note the creation of handle {1} on thread '{2}'({3})?",
							nameof (JniEnvironment.References.CreatedReference),
							value,
							Runtime.GetCurrentManagedThreadName (),
							Environment.CurrentManagedThreadId);
				}
			}
		}
	}
}
