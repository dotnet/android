using System;
using System.Threading;

using Java.Interop;

namespace Java.Interop {

	public class JniType : IDisposable {

		public static unsafe JniType DefineClass (string name, JniReferenceSafeHandle loader, byte[] classFileData)
		{
			fixed (byte* buf = classFileData) {
				var lref = JniEnvironment.Types.DefineClass (name, loader, (IntPtr) buf, classFileData.Length);
				return new JniType (lref, JniHandleOwnership.Transfer);
			}
		}

		public JniGlobalReference SafeHandle {get; private set;}

		public JniType (string classname)
			: this (JniEnvironment.Types.FindClass (classname), JniHandleOwnership.Transfer)
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
				JniEnvironment.Current.JavaVM.Track (this);
			} finally {
				JniEnvironment.Handles.Dispose (safeHandle, transfer);
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
			JniEnvironment.Current.JavaVM.UnTrack (SafeHandle);
			SafeHandle.Dispose ();
			SafeHandle = null;
		}

		public JniType GetSuperclass ()
		{
			var lref = JniEnvironment.Types.GetSuperclass (SafeHandle);
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
			return JniEnvironment.Types.IsAssignableFrom (c.SafeHandle, SafeHandle);
		}

		public bool IsInstanceOfType (JniReferenceSafeHandle value)
		{
			return JniEnvironment.Types.IsInstanceOf (value, SafeHandle);
		}

#pragma warning disable 0414
		// This isn't used anywhere; it's just present so that the GC won't collect the referenced delegates.
		JniNativeMethodRegistration[] methods;
#pragma warning restore 0414

		public void RegisterNativeMethods (params JniNativeMethodRegistration[] methods)
		{
			if (methods == null)
				throw new ArgumentNullException ("methods");
			int r = JniEnvironment.Types.RegisterNatives (SafeHandle, methods, checked ((int)methods.Length));
			if (r != 0)
				throw new JniException ("Unable to register native methods.");
			// Prevents method delegates from being GC'd so long as this type remains
			this.methods = methods;
		}

		public void UnregisterNativeMethods ()
		{
			JniEnvironment.Types.UnregisterNatives (SafeHandle);
		}

		public JniInstanceMethodID GetConstructor (string signature)
		{
			return JniEnvironment.Members.GetMethodID (SafeHandle, "<init>", signature);
		}

		public JniInstanceMethodID GetCachedConstructor (ref JniInstanceMethodID cachedMethod, string signature)
		{
			return GetCachedInstanceMethod (ref cachedMethod, "<init>", signature);
		}

		public JniLocalReference AllocObject ()
		{
			return JniEnvironment.Activator.AllocObject (SafeHandle);
		}

		public JniLocalReference NewObject (JniInstanceMethodID constructor, params JValue[] @params)
		{
			return JniEnvironment.Activator.NewObject (SafeHandle, constructor, @params);
		}

		public JniInstanceFieldID GetInstanceField (string name, string signature)
		{
			return JniEnvironment.Members.GetFieldID (SafeHandle, name, signature);
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
			return JniEnvironment.Members.GetStaticFieldID (SafeHandle, name, signature);
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
			return JniEnvironment.Members.GetMethodID (SafeHandle, name, signature);
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
			return JniEnvironment.Members.GetStaticMethodID (SafeHandle, name, signature);
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
