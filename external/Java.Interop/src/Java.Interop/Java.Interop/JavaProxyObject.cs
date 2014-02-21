using System;
using System.Runtime.CompilerServices;

namespace Java.Interop {

	[JniTypeInfo (JavaProxyObject.JniTypeName)]
	sealed class JavaProxyObject : JavaObject
	{
		internal const string JniTypeName = "com/xamarin/android/internal/JavaProxyObject";

		static  readonly    JniType                                         TypeRef;
		static  readonly    ConditionalWeakTable<object, JavaProxyObject>   CachedValues;

		static JavaProxyObject ()
		{
			TypeRef = new JniType (JniTypeName);
			TypeRef.RegisterWithVM ();
			TypeRef.RegisterNativeMethods (
					new JniNativeMethodRegistration ("equals",      "(Ljava/lang/Object;)Z",    (Func<IntPtr, IntPtr, IntPtr, bool>)    _Equals),
					new JniNativeMethodRegistration ("hashCode",    "()I",                      (Func<IntPtr, IntPtr, int>)             _GetHashCode),
					new JniNativeMethodRegistration ("toString",    "()Ljava/lang/String;",     (Func<IntPtr, IntPtr, IntPtr>)          _ToString));
			CachedValues = new ConditionalWeakTable<object, JavaProxyObject> ();
		}

		JavaProxyObject (object value)
		{
			if (value == null)
				throw new ArgumentNullException ("value");
			Value = value;
		}

		public object Value {get; private set;}

		public override int GetHashCode ()
		{
			return Value.GetHashCode ();
		}

		public override bool Equals (object obj)
		{
			var other = obj as JavaProxyObject;
			if (other != null)
				return object.Equals (Value, other.Value);
			return object.Equals (Value, obj);
		}

		public override string ToString ()
		{
			return Value.ToString ();
		}

		public static JavaProxyObject GetProxy (object value)
		{
			if (value == null)
				return null;

			lock (CachedValues) {
				JavaProxyObject proxy;
				if (CachedValues.TryGetValue (value, out proxy))
					return proxy;
				proxy = new JavaProxyObject (value);
				proxy.RegisterWithVM ();
				CachedValues.Add (value, proxy);
				return proxy;
			}
		}

		static bool _Equals (IntPtr jnienv, IntPtr n_self, IntPtr n_value)
		{
			JniEnvironment.CheckCurrent (jnienv);
			var self    = JniEnvironment.Current.JavaVM.GetObject<JavaProxyObject> (n_self);
			var value   = JniEnvironment.Current.JavaVM.GetObject (n_value);
			return self.Equals (value);
		}

		static int _GetHashCode (IntPtr jnienv, IntPtr n_self)
		{
			JniEnvironment.CheckCurrent (jnienv);
			var self = JniEnvironment.Current.JavaVM.GetObject<JavaProxyObject> (n_self);
			return self.GetHashCode ();
		}

		static IntPtr _ToString (IntPtr jnienv, IntPtr n_self)
		{
			JniEnvironment.CheckCurrent (jnienv);
			var self    = JniEnvironment.Current.JavaVM.GetObject<JavaProxyObject> (n_self);
			var s       = self.ToString ();
			using (var r = JniEnvironment.Strings.NewString (s))
				return JniEnvironment.Handles.NewReturnToJniRef (r);
		}
	}
}

