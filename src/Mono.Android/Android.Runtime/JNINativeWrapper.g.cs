using System;
using System.Diagnostics;

namespace Android.Runtime
{
	public static partial class JNINativeWrapper
	{
		static bool _unhandled_exception (Exception e)
		{
			if (Debugger.IsAttached || !JNIEnvInit.PropagateExceptions) {
				AndroidRuntimeInternal.mono_unhandled_exception?.Invoke (e);
				return false;
			}
			return true;
		}

		internal static void Wrap_JniMarshal_PP_V (this _JniMarshal_PP_V callback, IntPtr jnienv, IntPtr klazz)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PP_I (this _JniMarshal_PP_I callback, IntPtr jnienv, IntPtr klazz)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PP_Z (this _JniMarshal_PP_Z callback, IntPtr jnienv, IntPtr klazz)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPI_V (this _JniMarshal_PPI_V callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPI_L (this _JniMarshal_PPI_L callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPI_I (this _JniMarshal_PPI_I callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPI_J (this _JniMarshal_PPI_J callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPL_I (this _JniMarshal_PPL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPL_L (this _JniMarshal_PPL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPL_V (this _JniMarshal_PPL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPL_Z (this _JniMarshal_PPL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPJ_Z (this _JniMarshal_PPJ_Z callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPII_V (this _JniMarshal_PPII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPII_L (this _JniMarshal_PPII_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLI_V (this _JniMarshal_PPLI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZ_V (this _JniMarshal_PPLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLL_V (this _JniMarshal_PPLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLF_V (this _JniMarshal_PPLF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLI_L (this _JniMarshal_PPLI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLL_L (this _JniMarshal_PPLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLL_Z (this _JniMarshal_PPLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPIL_Z (this _JniMarshal_PPIL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIL_V (this _JniMarshal_PPIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLII_I (this _JniMarshal_PPLII_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLII_Z (this _JniMarshal_PPLII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLII_V (this _JniMarshal_PPLII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIII_V (this _JniMarshal_PPIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLJ_Z (this _JniMarshal_PPLLJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILL_V (this _JniMarshal_PPILL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLIL_Z (this _JniMarshal_PPLIL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLL_V (this _JniMarshal_PPLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLL_L (this _JniMarshal_PPLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLLL_Z (this _JniMarshal_PPLLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIZI_L (this _JniMarshal_PPIZI_L callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, int p2)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIII_V (this _JniMarshal_PPIIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLL_V (this _JniMarshal_PPLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLZZL_Z (this _JniMarshal_PPLZZL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, bool p2, IntPtr p3)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIII_V (this _JniMarshal_PPLIIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPZIIII_V (this _JniMarshal_PPZIIII_V callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, int p2, int p3, int p4)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIIIIIIII_V (this _JniMarshal_PPLIIIIIIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8)
		{
			Java.Interop.JniEnvironment.Runtime.ValueManager.WaitForGCBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		private static Delegate CreateBuiltInDelegate (Delegate dlg)
		{
			switch (dlg) {
				case _JniMarshal_PP_V callback:
					return new _JniMarshal_PP_V (callback.Wrap_JniMarshal_PP_V);
				case _JniMarshal_PP_I callback:
					return new _JniMarshal_PP_I (callback.Wrap_JniMarshal_PP_I);
				case _JniMarshal_PP_Z callback:
					return new _JniMarshal_PP_Z (callback.Wrap_JniMarshal_PP_Z);
				case _JniMarshal_PPI_V callback:
					return new _JniMarshal_PPI_V (callback.Wrap_JniMarshal_PPI_V);
				case _JniMarshal_PPI_L callback:
					return new _JniMarshal_PPI_L (callback.Wrap_JniMarshal_PPI_L);
				case _JniMarshal_PPI_I callback:
					return new _JniMarshal_PPI_I (callback.Wrap_JniMarshal_PPI_I);
				case _JniMarshal_PPI_J callback:
					return new _JniMarshal_PPI_J (callback.Wrap_JniMarshal_PPI_J);
				case _JniMarshal_PPL_I callback:
					return new _JniMarshal_PPL_I (callback.Wrap_JniMarshal_PPL_I);
				case _JniMarshal_PPL_L callback:
					return new _JniMarshal_PPL_L (callback.Wrap_JniMarshal_PPL_L);
				case _JniMarshal_PPL_V callback:
					return new _JniMarshal_PPL_V (callback.Wrap_JniMarshal_PPL_V);
				case _JniMarshal_PPL_Z callback:
					return new _JniMarshal_PPL_Z (callback.Wrap_JniMarshal_PPL_Z);
				case _JniMarshal_PPJ_Z callback:
					return new _JniMarshal_PPJ_Z (callback.Wrap_JniMarshal_PPJ_Z);
				case _JniMarshal_PPII_V callback:
					return new _JniMarshal_PPII_V (callback.Wrap_JniMarshal_PPII_V);
				case _JniMarshal_PPII_L callback:
					return new _JniMarshal_PPII_L (callback.Wrap_JniMarshal_PPII_L);
				case _JniMarshal_PPLI_V callback:
					return new _JniMarshal_PPLI_V (callback.Wrap_JniMarshal_PPLI_V);
				case _JniMarshal_PPLZ_V callback:
					return new _JniMarshal_PPLZ_V (callback.Wrap_JniMarshal_PPLZ_V);
				case _JniMarshal_PPLL_V callback:
					return new _JniMarshal_PPLL_V (callback.Wrap_JniMarshal_PPLL_V);
				case _JniMarshal_PPLF_V callback:
					return new _JniMarshal_PPLF_V (callback.Wrap_JniMarshal_PPLF_V);
				case _JniMarshal_PPLI_L callback:
					return new _JniMarshal_PPLI_L (callback.Wrap_JniMarshal_PPLI_L);
				case _JniMarshal_PPLL_L callback:
					return new _JniMarshal_PPLL_L (callback.Wrap_JniMarshal_PPLL_L);
				case _JniMarshal_PPLL_Z callback:
					return new _JniMarshal_PPLL_Z (callback.Wrap_JniMarshal_PPLL_Z);
				case _JniMarshal_PPIL_Z callback:
					return new _JniMarshal_PPIL_Z (callback.Wrap_JniMarshal_PPIL_Z);
				case _JniMarshal_PPIIL_V callback:
					return new _JniMarshal_PPIIL_V (callback.Wrap_JniMarshal_PPIIL_V);
				case _JniMarshal_PPLII_I callback:
					return new _JniMarshal_PPLII_I (callback.Wrap_JniMarshal_PPLII_I);
				case _JniMarshal_PPLII_Z callback:
					return new _JniMarshal_PPLII_Z (callback.Wrap_JniMarshal_PPLII_Z);
				case _JniMarshal_PPLII_V callback:
					return new _JniMarshal_PPLII_V (callback.Wrap_JniMarshal_PPLII_V);
				case _JniMarshal_PPIII_V callback:
					return new _JniMarshal_PPIII_V (callback.Wrap_JniMarshal_PPIII_V);
				case _JniMarshal_PPLLJ_Z callback:
					return new _JniMarshal_PPLLJ_Z (callback.Wrap_JniMarshal_PPLLJ_Z);
				case _JniMarshal_PPILL_V callback:
					return new _JniMarshal_PPILL_V (callback.Wrap_JniMarshal_PPILL_V);
				case _JniMarshal_PPLIL_Z callback:
					return new _JniMarshal_PPLIL_Z (callback.Wrap_JniMarshal_PPLIL_Z);
				case _JniMarshal_PPLLL_V callback:
					return new _JniMarshal_PPLLL_V (callback.Wrap_JniMarshal_PPLLL_V);
				case _JniMarshal_PPLLL_L callback:
					return new _JniMarshal_PPLLL_L (callback.Wrap_JniMarshal_PPLLL_L);
				case _JniMarshal_PPLLL_Z callback:
					return new _JniMarshal_PPLLL_Z (callback.Wrap_JniMarshal_PPLLL_Z);
				case _JniMarshal_PPIZI_L callback:
					return new _JniMarshal_PPIZI_L (callback.Wrap_JniMarshal_PPIZI_L);
				case _JniMarshal_PPIIII_V callback:
					return new _JniMarshal_PPIIII_V (callback.Wrap_JniMarshal_PPIIII_V);
				case _JniMarshal_PPLLLL_V callback:
					return new _JniMarshal_PPLLLL_V (callback.Wrap_JniMarshal_PPLLLL_V);
				case _JniMarshal_PPLZZL_Z callback:
					return new _JniMarshal_PPLZZL_Z (callback.Wrap_JniMarshal_PPLZZL_Z);
				case _JniMarshal_PPLIIII_V callback:
					return new _JniMarshal_PPLIIII_V (callback.Wrap_JniMarshal_PPLIIII_V);
				case _JniMarshal_PPZIIII_V callback:
					return new _JniMarshal_PPZIIII_V (callback.Wrap_JniMarshal_PPZIIII_V);
				case _JniMarshal_PPLIIIIIIII_V callback:
					return new _JniMarshal_PPLIIIIIIII_V (callback.Wrap_JniMarshal_PPLIIIIIIII_V);
				default:
					return null;
			}
		}
	}
}
