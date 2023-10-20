using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PP_I (this _JniMarshal_PP_I callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PP_Z (this _JniMarshal_PP_Z callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPI_V (this _JniMarshal_PPI_V callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPI_L (this _JniMarshal_PPI_L callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPI_I (this _JniMarshal_PPI_I callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPI_J (this _JniMarshal_PPI_J callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPL_I (this _JniMarshal_PPL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPL_L (this _JniMarshal_PPL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPL_V (this _JniMarshal_PPL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPL_Z (this _JniMarshal_PPL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPJ_Z (this _JniMarshal_PPJ_Z callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPII_V (this _JniMarshal_PPII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPII_L (this _JniMarshal_PPII_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLI_V (this _JniMarshal_PPLI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZ_V (this _JniMarshal_PPLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLL_V (this _JniMarshal_PPLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLF_V (this _JniMarshal_PPLF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLI_L (this _JniMarshal_PPLI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLL_L (this _JniMarshal_PPLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLL_Z (this _JniMarshal_PPLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPIL_Z (this _JniMarshal_PPIL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIL_V (this _JniMarshal_PPIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLII_I (this _JniMarshal_PPLII_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLII_Z (this _JniMarshal_PPLII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLII_V (this _JniMarshal_PPLII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIII_V (this _JniMarshal_PPIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLJ_Z (this _JniMarshal_PPLLJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILL_V (this _JniMarshal_PPILL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLIL_Z (this _JniMarshal_PPLIL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLL_V (this _JniMarshal_PPLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLL_L (this _JniMarshal_PPLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLLL_Z (this _JniMarshal_PPLLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIZI_L (this _JniMarshal_PPIZI_L callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIII_V (this _JniMarshal_PPIIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLL_V (this _JniMarshal_PPLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLZZL_Z (this _JniMarshal_PPLZZL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, bool p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIII_V (this _JniMarshal_PPLIIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPZIIII_V (this _JniMarshal_PPZIIII_V callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIIIIIIII_V (this _JniMarshal_PPLIIIIIIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		private static Delegate CreateBuiltInDelegate (Delegate dlg, Type delegateType)
		{
			switch (delegateType.Name) {
				case nameof (_JniMarshal_PP_V):
					return new _JniMarshal_PP_V (Unsafe.As<_JniMarshal_PP_V> (dlg).Wrap_JniMarshal_PP_V);
				case nameof (_JniMarshal_PP_I):
					return new _JniMarshal_PP_I (Unsafe.As<_JniMarshal_PP_I> (dlg).Wrap_JniMarshal_PP_I);
				case nameof (_JniMarshal_PP_Z):
					return new _JniMarshal_PP_Z (Unsafe.As<_JniMarshal_PP_Z> (dlg).Wrap_JniMarshal_PP_Z);
				case nameof (_JniMarshal_PPI_V):
					return new _JniMarshal_PPI_V (Unsafe.As<_JniMarshal_PPI_V> (dlg).Wrap_JniMarshal_PPI_V);
				case nameof (_JniMarshal_PPI_L):
					return new _JniMarshal_PPI_L (Unsafe.As<_JniMarshal_PPI_L> (dlg).Wrap_JniMarshal_PPI_L);
				case nameof (_JniMarshal_PPI_I):
					return new _JniMarshal_PPI_I (Unsafe.As<_JniMarshal_PPI_I> (dlg).Wrap_JniMarshal_PPI_I);
				case nameof (_JniMarshal_PPI_J):
					return new _JniMarshal_PPI_J (Unsafe.As<_JniMarshal_PPI_J> (dlg).Wrap_JniMarshal_PPI_J);
				case nameof (_JniMarshal_PPL_I):
					return new _JniMarshal_PPL_I (Unsafe.As<_JniMarshal_PPL_I> (dlg).Wrap_JniMarshal_PPL_I);
				case nameof (_JniMarshal_PPL_L):
					return new _JniMarshal_PPL_L (Unsafe.As<_JniMarshal_PPL_L> (dlg).Wrap_JniMarshal_PPL_L);
				case nameof (_JniMarshal_PPL_V):
					return new _JniMarshal_PPL_V (Unsafe.As<_JniMarshal_PPL_V> (dlg).Wrap_JniMarshal_PPL_V);
				case nameof (_JniMarshal_PPL_Z):
					return new _JniMarshal_PPL_Z (Unsafe.As<_JniMarshal_PPL_Z> (dlg).Wrap_JniMarshal_PPL_Z);
				case nameof (_JniMarshal_PPJ_Z):
					return new _JniMarshal_PPJ_Z (Unsafe.As<_JniMarshal_PPJ_Z> (dlg).Wrap_JniMarshal_PPJ_Z);
				case nameof (_JniMarshal_PPII_V):
					return new _JniMarshal_PPII_V (Unsafe.As<_JniMarshal_PPII_V> (dlg).Wrap_JniMarshal_PPII_V);
				case nameof (_JniMarshal_PPII_L):
					return new _JniMarshal_PPII_L (Unsafe.As<_JniMarshal_PPII_L> (dlg).Wrap_JniMarshal_PPII_L);
				case nameof (_JniMarshal_PPLI_V):
					return new _JniMarshal_PPLI_V (Unsafe.As<_JniMarshal_PPLI_V> (dlg).Wrap_JniMarshal_PPLI_V);
				case nameof (_JniMarshal_PPLZ_V):
					return new _JniMarshal_PPLZ_V (Unsafe.As<_JniMarshal_PPLZ_V> (dlg).Wrap_JniMarshal_PPLZ_V);
				case nameof (_JniMarshal_PPLL_V):
					return new _JniMarshal_PPLL_V (Unsafe.As<_JniMarshal_PPLL_V> (dlg).Wrap_JniMarshal_PPLL_V);
				case nameof (_JniMarshal_PPLF_V):
					return new _JniMarshal_PPLF_V (Unsafe.As<_JniMarshal_PPLF_V> (dlg).Wrap_JniMarshal_PPLF_V);
				case nameof (_JniMarshal_PPLI_L):
					return new _JniMarshal_PPLI_L (Unsafe.As<_JniMarshal_PPLI_L> (dlg).Wrap_JniMarshal_PPLI_L);
				case nameof (_JniMarshal_PPLL_L):
					return new _JniMarshal_PPLL_L (Unsafe.As<_JniMarshal_PPLL_L> (dlg).Wrap_JniMarshal_PPLL_L);
				case nameof (_JniMarshal_PPLL_Z):
					return new _JniMarshal_PPLL_Z (Unsafe.As<_JniMarshal_PPLL_Z> (dlg).Wrap_JniMarshal_PPLL_Z);
				case nameof (_JniMarshal_PPIL_Z):
					return new _JniMarshal_PPIL_Z (Unsafe.As<_JniMarshal_PPIL_Z> (dlg).Wrap_JniMarshal_PPIL_Z);
				case nameof (_JniMarshal_PPIIL_V):
					return new _JniMarshal_PPIIL_V (Unsafe.As<_JniMarshal_PPIIL_V> (dlg).Wrap_JniMarshal_PPIIL_V);
				case nameof (_JniMarshal_PPLII_I):
					return new _JniMarshal_PPLII_I (Unsafe.As<_JniMarshal_PPLII_I> (dlg).Wrap_JniMarshal_PPLII_I);
				case nameof (_JniMarshal_PPLII_Z):
					return new _JniMarshal_PPLII_Z (Unsafe.As<_JniMarshal_PPLII_Z> (dlg).Wrap_JniMarshal_PPLII_Z);
				case nameof (_JniMarshal_PPLII_V):
					return new _JniMarshal_PPLII_V (Unsafe.As<_JniMarshal_PPLII_V> (dlg).Wrap_JniMarshal_PPLII_V);
				case nameof (_JniMarshal_PPIII_V):
					return new _JniMarshal_PPIII_V (Unsafe.As<_JniMarshal_PPIII_V> (dlg).Wrap_JniMarshal_PPIII_V);
				case nameof (_JniMarshal_PPLLJ_Z):
					return new _JniMarshal_PPLLJ_Z (Unsafe.As<_JniMarshal_PPLLJ_Z> (dlg).Wrap_JniMarshal_PPLLJ_Z);
				case nameof (_JniMarshal_PPILL_V):
					return new _JniMarshal_PPILL_V (Unsafe.As<_JniMarshal_PPILL_V> (dlg).Wrap_JniMarshal_PPILL_V);
				case nameof (_JniMarshal_PPLIL_Z):
					return new _JniMarshal_PPLIL_Z (Unsafe.As<_JniMarshal_PPLIL_Z> (dlg).Wrap_JniMarshal_PPLIL_Z);
				case nameof (_JniMarshal_PPLLL_V):
					return new _JniMarshal_PPLLL_V (Unsafe.As<_JniMarshal_PPLLL_V> (dlg).Wrap_JniMarshal_PPLLL_V);
				case nameof (_JniMarshal_PPLLL_L):
					return new _JniMarshal_PPLLL_L (Unsafe.As<_JniMarshal_PPLLL_L> (dlg).Wrap_JniMarshal_PPLLL_L);
				case nameof (_JniMarshal_PPLLL_Z):
					return new _JniMarshal_PPLLL_Z (Unsafe.As<_JniMarshal_PPLLL_Z> (dlg).Wrap_JniMarshal_PPLLL_Z);
				case nameof (_JniMarshal_PPIZI_L):
					return new _JniMarshal_PPIZI_L (Unsafe.As<_JniMarshal_PPIZI_L> (dlg).Wrap_JniMarshal_PPIZI_L);
				case nameof (_JniMarshal_PPIIII_V):
					return new _JniMarshal_PPIIII_V (Unsafe.As<_JniMarshal_PPIIII_V> (dlg).Wrap_JniMarshal_PPIIII_V);
				case nameof (_JniMarshal_PPLLLL_V):
					return new _JniMarshal_PPLLLL_V (Unsafe.As<_JniMarshal_PPLLLL_V> (dlg).Wrap_JniMarshal_PPLLLL_V);
				case nameof (_JniMarshal_PPLZZL_Z):
					return new _JniMarshal_PPLZZL_Z (Unsafe.As<_JniMarshal_PPLZZL_Z> (dlg).Wrap_JniMarshal_PPLZZL_Z);
				case nameof (_JniMarshal_PPLIIII_V):
					return new _JniMarshal_PPLIIII_V (Unsafe.As<_JniMarshal_PPLIIII_V> (dlg).Wrap_JniMarshal_PPLIIII_V);
				case nameof (_JniMarshal_PPZIIII_V):
					return new _JniMarshal_PPZIIII_V (Unsafe.As<_JniMarshal_PPZIIII_V> (dlg).Wrap_JniMarshal_PPZIIII_V);
				case nameof (_JniMarshal_PPLIIIIIIII_V):
					return new _JniMarshal_PPLIIIIIIII_V (Unsafe.As<_JniMarshal_PPLIIIIIIII_V> (dlg).Wrap_JniMarshal_PPLIIIIIIII_V);
				default:
					return null;
			}
		}
	}
}
