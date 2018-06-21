using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Android.Runtime {
	internal static class ConstructorBuilder {
		static AssemblyBuilder builder;
		static ModuleBuilder module;
		static readonly object lazyLock = new object ();

		static MethodInfo newobject = typeof (System.Runtime.Serialization.FormatterServices).GetMethod ("GetUninitializedObject", BindingFlags.Public | BindingFlags.Static);
		static MethodInfo gettype = typeof (System.Type).GetMethod ("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static);
		static FieldInfo handlefld = typeof (Java.Lang.Object).GetField ("handle", BindingFlags.NonPublic | BindingFlags.Instance);
		static FieldInfo Throwable_handle = typeof (Java.Lang.Throwable).GetField ("handle", BindingFlags.NonPublic | BindingFlags.Instance);

		static void AssureBuilder ()
		{
			lock (lazyLock) {
				if (builder == null) {
					var tempBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly (new AssemblyName {Name = "MonoDroidConstructors"}, AssemblyBuilderAccess.Run, null, null, null,  null, null, true);
					module = tempBuilder.DefineDynamicModule ("Implementations", false);
					builder = tempBuilder;
				}
			}
		}

		internal static Action <IntPtr, object []> CreateDelegate (Type type, ConstructorInfo cinfo, Type [] parameter_types) {
			var handle = handlefld;
			if (typeof (Java.Lang.Throwable).IsAssignableFrom (type)) {
				handle = Throwable_handle;
			}

			AssureBuilder ();

			DynamicMethod method = new DynamicMethod (Guid.NewGuid ().ToString (), typeof (void), new Type [] {typeof (IntPtr), typeof (object []) }, module, true);
			ILGenerator il = method.GetILGenerator ();

			il.DeclareLocal (typeof (object));

			il.Emit (OpCodes.Ldtoken, type);
			il.Emit (OpCodes.Call, gettype);
			il.Emit (OpCodes.Call, newobject);
			il.Emit (OpCodes.Stloc_0);
			il.Emit (OpCodes.Ldloc_0);
			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Stfld, handle);

			il.Emit (OpCodes.Ldloc_0);
			for (int i = 0; i < parameter_types.Length; i++) {
				il.Emit (OpCodes.Ldarg, 1);
				il.Emit (OpCodes.Ldc_I4, i);
				il.Emit (OpCodes.Ldelem_Ref);
			}
			il.Emit (OpCodes.Call, cinfo);

			il.Emit (OpCodes.Ret);

			return (Action<IntPtr, object[]>) method.CreateDelegate (typeof (Action <IntPtr, object []>));
		}
	}
}
