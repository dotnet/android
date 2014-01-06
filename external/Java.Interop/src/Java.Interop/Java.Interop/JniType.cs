using System;
using System.Threading;

using Java.Interop;

namespace Java.Interop {

	public class JniType : IDisposable {

		public static unsafe JniType DefineClass (string name, JniReferenceSafeHandle loader, byte[] classFileData)
		{
			fixed (byte* buf = classFileData) {
				var lref = JniTypes.DefineClass (name, loader, (IntPtr)buf, checked((int) classFileData.Length));
				return new JniType (lref, JniHandleOwnership.Transfer);
			}
		}

		public JniGlobalReference SafeHandle {get; private set;}

		public JniType (string classname)
			: this (JniTypes.FindClass (classname), JniHandleOwnership.Transfer)
		{
		}

		public JniType (JniReferenceSafeHandle safeHandle, JniHandleOwnership transfer)
		{
			if (safeHandle == null)
				throw new ArgumentNullException ("safeHandle");
			if (safeHandle.IsInvalid)
				throw new ArgumentException ("safeHandle must be valid.", "safeHandle");
			try {
				SafeHandle = safeHandle.NewGlobalRef ();
			} finally {
				JniHandles.Dispose (safeHandle, transfer);
			}
		}

		public static JniType GetCachedJniType (ref JniType cachedType, string classname)
		{
			if (cachedType != null && !cachedType.SafeHandle.IsInvalid)
				return cachedType;
			var t = new JniType (classname);
			if (Interlocked.CompareExchange (ref cachedType, t, null) != null)
				t.Dispose ();
			return cachedType;
		}

		public void Dispose ()
		{
			if (SafeHandle == null)
				return;
			SafeHandle.Dispose ();
			SafeHandle = null;
		}

		public JniType GetSuperclass ()
		{
			var lref = JniTypes.GetSuperclass (SafeHandle);
			if (!lref.IsInvalid)
				return new JniType (lref, JniHandleOwnership.Transfer);
			return null;
		}

		public bool IsAssignableFrom (JniType c)
		{
			if (c == null)
				throw new ArgumentNullException ("c");
			if (c.SafeHandle == null || c.SafeHandle.IsInvalid)
				throw new ArgumentException ("'c' has an invalid handle.", "c");
			return JniTypes.IsAssignableFrom (c.SafeHandle, SafeHandle);
		}

		public bool IsInstanceOfType (JniReferenceSafeHandle value)
		{
			return JniTypes.IsInstanceOf (value, SafeHandle);
		}

		public JniInstanceMethodID GetConstructor (string signature)
		{
			return JniMembers.GetMethodID (SafeHandle, "<init>", signature);
		}

		public JniInstanceMethodID GetCachedConstructor (ref JniInstanceMethodID cachedMethod, string signature)
		{
			return GetCachedInstanceMethod (ref cachedMethod, "<init>", signature);
		}

		public JniLocalReference AllocObject ()
		{
			return JniActivator.AllocObject (SafeHandle);
		}

		public JniLocalReference NewObject (JniInstanceMethodID constructor, params JValue[] @params)
		{
			return JniActivator.NewObject (SafeHandle, constructor, @params);
		}

		public JniInstanceFieldID GetInstanceField (string name, string signature)
		{
			return JniMembers.GetFieldID (SafeHandle, name, signature);
		}

		public JniInstanceFieldID GetCachedInstanceField (ref JniInstanceFieldID cachedField, string name, string signature)
		{
			if (cachedField != null && !cachedField.IsInvalid)
				return cachedField;
			var m = GetInstanceField (name, signature);
			if (Interlocked.CompareExchange (ref cachedField, m, null) != null)
				m.Dispose ();
			return cachedField;
		}

		public JniStaticFieldID GetStaticField (string name, string signature)
		{
			return JniMembers.GetStaticFieldID (SafeHandle, name, signature);
		}

		public JniStaticFieldID GetCachedStaticField (ref JniStaticFieldID cachedField, string name, string signature)
		{
			if (cachedField != null && !cachedField.IsInvalid)
				return cachedField;
			var m = GetStaticField (name, signature);
			if (Interlocked.CompareExchange (ref cachedField, m, null) != null)
				m.Dispose ();
			return cachedField;
		}

		public JniInstanceMethodID GetInstanceMethod (string name, string signature)
		{
			return JniMembers.GetMethodID (SafeHandle, name, signature);
		}

		public JniInstanceMethodID GetCachedInstanceMethod (ref JniInstanceMethodID cachedMethod, string name, string signature)
		{
			if (cachedMethod != null && !cachedMethod.IsInvalid)
				return cachedMethod;
			var m = GetInstanceMethod (name, signature);
			if (Interlocked.CompareExchange (ref cachedMethod, m, null) != null)
				m.Dispose ();
			return cachedMethod;
		}

		public JniStaticMethodID GetStaticMethod (string name, string signature)
		{
			return JniMembers.GetStaticMethodID (SafeHandle, name, signature);
		}

		public JniStaticMethodID GetCachedStaticMethod (ref JniStaticMethodID cachedMethod, string name, string signature)
		{
			if (cachedMethod != null && !cachedMethod.IsInvalid)
				return cachedMethod;
			var m = GetStaticMethod (name, signature);
			if (Interlocked.CompareExchange (ref cachedMethod, m, null) != null)
				m.Dispose ();
			return cachedMethod;
		}
	}
}
