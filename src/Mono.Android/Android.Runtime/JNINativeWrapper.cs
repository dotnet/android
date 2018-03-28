using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace Android.Runtime {
	public static class JNINativeWrapper {

		static MethodInfo mono_unhandled_exception_method;
		static MethodInfo exception_handler_method;
		static MethodInfo wait_for_bridge_processing_method;

		static void get_runtime_types ()
		{
			if (mono_unhandled_exception_method != null)
				return;

			mono_unhandled_exception_method = typeof (System.Diagnostics.Debugger).GetMethod (
				"Mono_UnhandledException", BindingFlags.NonPublic | BindingFlags.Static);
			if (mono_unhandled_exception_method == null)
				AndroidEnvironment.FailFast ("Cannot find System.Diagnostics.Debugger.Mono_UnhandledException");

			exception_handler_method = typeof (AndroidEnvironment).GetMethod (
				"UnhandledException", BindingFlags.NonPublic | BindingFlags.Static);
			if (exception_handler_method == null)
				AndroidEnvironment.FailFast ("Cannot find AndroidEnvironment.UnhandledException");

			wait_for_bridge_processing_method = typeof (JNIEnv).GetMethod ("WaitForBridgeProcessing", BindingFlags.Public | BindingFlags.Static);
			if (wait_for_bridge_processing_method == null)
				AndroidEnvironment.FailFast ("Cannot find JNIEnv.WaitForBridgeProcessing");
		}

		public static Delegate CreateDelegate (Delegate dlg)
		{
			if (dlg == null)
				throw new ArgumentNullException ();
			if (dlg.Target != null)
				throw new ArgumentException ();
			if (dlg.Method == null)
				throw new ArgumentException ();

			get_runtime_types ();

			var ret_type = dlg.Method.ReturnType;
			var parameters = dlg.Method.GetParameters ();
			var param_types = new Type [parameters.Length];
			for (int i = 0; i < parameters.Length; i++) {
				param_types [i] = parameters [i].ParameterType;
			}

			var dynamic = new DynamicMethod (Guid.NewGuid ().ToString (), ret_type, param_types, typeof (object), true);
			var ig = dynamic.GetILGenerator ();

			LocalBuilder retval = null;
			if (ret_type != typeof (void))
				retval = ig.DeclareLocal (ret_type);

			ig.Emit (OpCodes.Call, wait_for_bridge_processing_method);

			var label = ig.BeginExceptionBlock ();

			for (int i = 0; i < param_types.Length; i++)
				ig.Emit (OpCodes.Ldarg, i);
			ig.Emit (OpCodes.Call, dlg.Method);

			if (retval != null)
				ig.Emit (OpCodes.Stloc, retval);

			ig.Emit (OpCodes.Leave, label);

			bool  filter = Debugger.IsAttached || !JNIEnv.PropagateExceptions;
			if (filter) {
				ig.BeginExceptFilterBlock ();

				ig.Emit (OpCodes.Call, mono_unhandled_exception_method);
				ig.Emit (OpCodes.Ldc_I4_1);
				ig.BeginCatchBlock (null);
			} else {
				ig.BeginCatchBlock (typeof (Exception));
			}

			ig.Emit (OpCodes.Dup);
			ig.Emit (OpCodes.Call, exception_handler_method);

			if (filter)
				ig.Emit (OpCodes.Throw);

			ig.EndExceptionBlock ();

			if (retval != null)
				ig.Emit (OpCodes.Ldloc, retval);

			ig.Emit (OpCodes.Ret);

			return dynamic.CreateDelegate (dlg.GetType ());
		}

	}
}

