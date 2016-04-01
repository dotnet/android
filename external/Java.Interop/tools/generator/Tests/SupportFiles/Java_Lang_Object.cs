using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

using Java.Interop;

using Android.Runtime;

namespace Java.Lang
{
	public partial class Object : IDisposable, IJavaObject, IJavaPeerable
	{
		public virtual JniPeerMembers JniPeerMembers {
			get { return null; }
		}

		public int JniIdentityHashCode {
			get { return 0; }
		}

		public JniObjectReference PeerReference {
			get { return default (JniObjectReference); }
		}

		public JniManagedPeerStates JniManagedPeerState {
			get {return 0;}
		}

		public void DisposeUnlessReferenced ()
		{
		}

		public void UnregisterFromRuntime ()
		{
		}

		public void Disposed ()
		{
		}

		public void Finalized ()
		{
		}

		public void SetJniIdentityHashCode (int value)
		{
		}

		public void SetJniManagedPeerState (JniManagedPeerStates value)
		{
		}

		public void SetPeerReference (JniObjectReference value)
		{
		}

		internal void SetHandleOnDeserialized (StreamingContext context)
		{
			throw new NotImplementedException ();
		}

		internal static void RegisterInstance (IJavaObject instance, IntPtr value, JniHandleOwnership transfer, out IntPtr handle)
		{
			throw new NotImplementedException ();
		}

		static void _RegisterInstance (IJavaObject instance, IntPtr key, IntPtr handle)
		{
			throw new NotImplementedException ();
		}

		static bool ShouldReplaceMapping (WeakReference current)
		{
			throw new NotImplementedException ();
		}

		static void DeregisterInstance (object instance, IntPtr key_handle)
		{
			throw new NotImplementedException ();
		}

		internal static List<WeakReference> GetSurfacedObjects_ForDiagnosticsOnly ()
		{
			throw new NotImplementedException ();
		}

		internal static IJavaObject PeekObject (IntPtr handle)
		{
			throw new NotImplementedException ();
		}

		internal static T _GetObject<T> (IntPtr handle, JniHandleOwnership transfer)
		{
			throw new NotImplementedException ();
		}

		internal static IJavaObject GetObject (IntPtr handle, JniHandleOwnership transfer, Type type = null)
		{
			throw new NotImplementedException ();
		}

		public T[] ToArray<T> ()
		{
			throw new NotImplementedException ();
		}

		public static Java.Lang.Object FromArray<T> (T[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (bool value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (sbyte value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (char value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (int value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (long value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (float value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (double value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (string value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator bool (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator sbyte (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator char (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator int (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator long (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator float (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator double (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator string (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (Java.Lang.Object[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (bool[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (byte[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (char[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (int[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (long[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (float[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (double[] value)
		{
			throw new NotImplementedException ();
		}

		public static implicit operator Java.Lang.Object (string[] value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator Java.Lang.Object[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator bool[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator byte[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator char[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator int[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator long[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator float[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator double[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}

		public static explicit operator string[] (Java.Lang.Object value)
		{
			throw new NotImplementedException ();
		}
	}
}

