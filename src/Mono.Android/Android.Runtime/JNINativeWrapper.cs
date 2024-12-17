using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Android.Runtime {
	public static partial class JNINativeWrapper {

		static MethodInfo? exception_handler_method;
		static MethodInfo? wait_for_bridge_processing_method;

		static void get_runtime_types ()
		{
			if (exception_handler_method != null)
				return;

			exception_handler_method = typeof (AndroidEnvironment).GetMethod (
				"UnhandledException", BindingFlags.NonPublic | BindingFlags.Static);
			if (exception_handler_method == null)
				AndroidEnvironment.FailFast ("Cannot find AndroidEnvironment.UnhandledException");

			wait_for_bridge_processing_method = typeof (AndroidRuntimeInternal).GetMethod ("WaitForBridgeProcessing", BindingFlags.Public | BindingFlags.Static);
			if (wait_for_bridge_processing_method == null)
				AndroidEnvironment.FailFast ("Cannot find AndroidRuntimeInternal.WaitForBridgeProcessing");
		}

		public static Delegate CreateDelegate (Delegate dlg)
		{
			if (dlg == null)
				throw new ArgumentNullException ();
			if (dlg.Target != null)
				throw new ArgumentException ();
			if (dlg.Method == null)
				throw new ArgumentException ();

			var delegateType = dlg.GetType ();
			var result = CreateBuiltInDelegate (dlg, delegateType);
			if (result != null)
				return result;

			if (Logger.LogAssembly) {
				RuntimeNativeMethods.monodroid_log (LogLevel.Debug, LogCategories.Assembly, $"Falling back to System.Reflection.Emit for delegate type '{delegateType}': {dlg.Method}");
			}

			get_runtime_types ();

			var ret_type = dlg.Method.ReturnType;
			var parameters = dlg.Method.GetParameters ();
			var param_types = new Type [parameters.Length];
			for (int i = 0; i < parameters.Length; i++) {
				param_types [i] = parameters [i].ParameterType;
			}

			// FIXME: https://github.com/xamarin/xamarin-android/issues/8724
			// IL3050 disabled in source: if someone uses NativeAOT, they will get the warning.
			#pragma warning disable IL3050
			var dynamic = new DynamicMethod (DynamicMethodNameCounter.GetUniqueName (), ret_type, param_types, typeof (DynamicMethodNameCounter), true);
			#pragma warning restore IL3050
			var ig = dynamic.GetILGenerator ();

			LocalBuilder? retval = null;
			if (ret_type != typeof (void))
				retval = ig.DeclareLocal (ret_type);

			ig.Emit (OpCodes.Call, wait_for_bridge_processing_method!);

			var label = ig.BeginExceptionBlock ();

			for (int i = 0; i < param_types.Length; i++)
				ig.Emit (OpCodes.Ldarg, i);
			ig.Emit (OpCodes.Call, dlg.Method);

			if (retval != null)
				ig.Emit (OpCodes.Stloc, retval);

			ig.Emit (OpCodes.Leave, label);

			bool  filter = Debugger.IsAttached || !JNIEnvInit.PropagateExceptions;
			if (filter) {
				ig.BeginExceptFilterBlock ();

				ig.Emit (OpCodes.Call, AndroidRuntimeInternal.mono_unhandled_exception.Method);
				ig.Emit (OpCodes.Ldc_I4_1);
				ig.BeginCatchBlock (null!);
			} else {
				ig.BeginCatchBlock (typeof (Exception));
			}

			ig.Emit (OpCodes.Dup);
			ig.Emit (OpCodes.Call, exception_handler_method!);

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
