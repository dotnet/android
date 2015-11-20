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

			public static JniObjectReference    ToString (JniObjectReference value)
			{
				return JniEnvironment.InstanceMethods.CallObjectMethod (value, Object_toString);
			}
		}
	}
}

