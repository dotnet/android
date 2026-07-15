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

			public static JniObjectReference NewObject (JniObjectReference type, JniMethodInfo method)
			{
				JniEnvironment.WithinNewObjectScope = true;
				try {
					return _NewObject (type, method);
				}
				finally {
					JniEnvironment.WithinNewObjectScope = false;
				}
			}

			public static unsafe JniObjectReference NewObject (JniObjectReference type, JniMethodInfo method, JniArgumentValue* args)
			{
				JniEnvironment.WithinNewObjectScope = true;
				try {
					return _NewObject (type, method, args);
				}
				finally {
					JniEnvironment.WithinNewObjectScope = false;
				}
			}

			public static JniObjectReference    ToString (JniObjectReference value)
			{
				return JniEnvironment.InstanceMethods.CallObjectMethod (value, Object_toString);
			}
		}
	}
}

