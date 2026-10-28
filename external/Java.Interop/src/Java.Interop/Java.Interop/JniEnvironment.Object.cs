#nullable enable

using System;

namespace Java.Interop {

	partial class JniEnvironment {

		public static partial class Object {

			static  JniMethodInfo           Object_toString;

			static Object ()
			{
				using (var t = new JniType ("java/lang/Object")) {
					Object_toString     = t.GetInstanceMethod ("toString", "()Ljava/lang/String;");
				}
			}

			/// <safety>
			/// <paramref name="method"/> must identify a zero-argument constructor for
			/// <paramref name="type"/> with descriptor <c>()V</c>. Keep the class reference
			/// and constructor ID valid through return or failure; any local reference
			/// must belong to the calling attached JNI thread.
			/// </safety>
			public static unsafe JniObjectReference NewObject (JniObjectReference type, JniMethodInfo method)
			{
				JniEnvironment.WithinNewObjectScope = true;
				try {
					unsafe {
						// SAFETY: Caller supplies a live class and matching ()V constructor; JNI reads no argument slots.
						return _NewObject (type, method);
					}
				}
				finally {
					JniEnvironment.WithinNewObjectScope = false;
				}
			}

			/// <safety>
			/// The method must be a constructor for type. args must address initialized, readable JNI argument
			/// slots matching that constructor's descriptor, or be null only for a zero-argument constructor.
			/// Keep the buffer and any referenced Java objects live through the call, including failure;
			/// exclude writes or release through aliases while JNI reads it. The buffer is not retained.
			/// </safety>
			public static unsafe JniObjectReference NewObject (JniObjectReference type, JniMethodInfo method, JniArgumentValue* args)
			{
				JniEnvironment.WithinNewObjectScope = true;
				try {
					unsafe {
						// SAFETY: The caller supplies constructor-compatible slots and keeps their storage/references live.
						return _NewObject (type, method, args);
					}
				}
				finally {
					JniEnvironment.WithinNewObjectScope = false;
				}
			}

			public static JniObjectReference    ToString (JniObjectReference value)
			{
				unsafe {
					// SAFETY: Object_toString is the cached zero-argument Object.toString()String method.
					return JniEnvironment.InstanceMethods.CallObjectMethod (value, Object_toString);
				}
			}
		}
	}
}
