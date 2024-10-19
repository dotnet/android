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

		internal static sbyte Wrap_JniMarshal_PP_B (this _JniMarshal_PP_B callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static char Wrap_JniMarshal_PP_C (this _JniMarshal_PP_C callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static double Wrap_JniMarshal_PP_D (this _JniMarshal_PP_D callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PP_F (this _JniMarshal_PP_F callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static long Wrap_JniMarshal_PP_J (this _JniMarshal_PP_J callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PP_L (this _JniMarshal_PP_L callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static short Wrap_JniMarshal_PP_S (this _JniMarshal_PP_S callback, IntPtr jnienv, IntPtr klazz)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
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

		internal static long Wrap_JniMarshal_PPB_J (this _JniMarshal_PPB_J callback, IntPtr jnienv, IntPtr klazz, sbyte p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPB_L (this _JniMarshal_PPB_L callback, IntPtr jnienv, IntPtr klazz, sbyte p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPB_V (this _JniMarshal_PPB_V callback, IntPtr jnienv, IntPtr klazz, sbyte p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPBBBB_V (this _JniMarshal_PPBBBB_V callback, IntPtr jnienv, IntPtr klazz, sbyte p0, sbyte p1, sbyte p2, sbyte p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static sbyte Wrap_JniMarshal_PPBI_B (this _JniMarshal_PPBI_B callback, IntPtr jnienv, IntPtr klazz, sbyte p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPBI_V (this _JniMarshal_PPBI_V callback, IntPtr jnienv, IntPtr klazz, sbyte p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPBJ_J (this _JniMarshal_PPBJ_J callback, IntPtr jnienv, IntPtr klazz, sbyte p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPBJJ_J (this _JniMarshal_PPBJJ_J callback, IntPtr jnienv, IntPtr klazz, sbyte p0, long p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static char Wrap_JniMarshal_PPC_C (this _JniMarshal_PPC_C callback, IntPtr jnienv, IntPtr klazz, char p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPC_I (this _JniMarshal_PPC_I callback, IntPtr jnienv, IntPtr klazz, char p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPC_L (this _JniMarshal_PPC_L callback, IntPtr jnienv, IntPtr klazz, char p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPC_V (this _JniMarshal_PPC_V callback, IntPtr jnienv, IntPtr klazz, char p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPC_Z (this _JniMarshal_PPC_Z callback, IntPtr jnienv, IntPtr klazz, char p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPCC_L (this _JniMarshal_PPCC_L callback, IntPtr jnienv, IntPtr klazz, char p0, char p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPCCII_L (this _JniMarshal_PPCCII_L callback, IntPtr jnienv, IntPtr klazz, char p0, char p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPCI_L (this _JniMarshal_PPCI_L callback, IntPtr jnienv, IntPtr klazz, char p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPCIILLL_L (this _JniMarshal_PPCIILLL_L callback, IntPtr jnienv, IntPtr klazz, char p0, int p1, int p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPCJ_Z (this _JniMarshal_PPCJ_Z callback, IntPtr jnienv, IntPtr klazz, char p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static double Wrap_JniMarshal_PPD_D (this _JniMarshal_PPD_D callback, IntPtr jnienv, IntPtr klazz, double p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPD_I (this _JniMarshal_PPD_I callback, IntPtr jnienv, IntPtr klazz, double p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPD_J (this _JniMarshal_PPD_J callback, IntPtr jnienv, IntPtr klazz, double p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPD_L (this _JniMarshal_PPD_L callback, IntPtr jnienv, IntPtr klazz, double p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPD_V (this _JniMarshal_PPD_V callback, IntPtr jnienv, IntPtr klazz, double p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPD_Z (this _JniMarshal_PPD_Z callback, IntPtr jnienv, IntPtr klazz, double p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static double Wrap_JniMarshal_PPDD_D (this _JniMarshal_PPDD_D callback, IntPtr jnienv, IntPtr klazz, double p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPDD_L (this _JniMarshal_PPDD_L callback, IntPtr jnienv, IntPtr klazz, double p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPDD_V (this _JniMarshal_PPDD_V callback, IntPtr jnienv, IntPtr klazz, double p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static double Wrap_JniMarshal_PPDDD_D (this _JniMarshal_PPDDD_D callback, IntPtr jnienv, IntPtr klazz, double p0, double p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPDDFJL_V (this _JniMarshal_PPDDFJL_V callback, IntPtr jnienv, IntPtr klazz, double p0, double p1, float p2, long p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPDF_V (this _JniMarshal_PPDF_V callback, IntPtr jnienv, IntPtr klazz, double p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static double Wrap_JniMarshal_PPDI_D (this _JniMarshal_PPDI_D callback, IntPtr jnienv, IntPtr klazz, double p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPDI_V (this _JniMarshal_PPDI_V callback, IntPtr jnienv, IntPtr klazz, double p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPDII_L (this _JniMarshal_PPDII_L callback, IntPtr jnienv, IntPtr klazz, double p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPDII_Z (this _JniMarshal_PPDII_Z callback, IntPtr jnienv, IntPtr klazz, double p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPDL_L (this _JniMarshal_PPDL_L callback, IntPtr jnienv, IntPtr klazz, double p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPDL_V (this _JniMarshal_PPDL_V callback, IntPtr jnienv, IntPtr klazz, double p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPDLL_L (this _JniMarshal_PPDLL_L callback, IntPtr jnienv, IntPtr klazz, double p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPDLL_V (this _JniMarshal_PPDLL_V callback, IntPtr jnienv, IntPtr klazz, double p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPF_F (this _JniMarshal_PPF_F callback, IntPtr jnienv, IntPtr klazz, float p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPF_I (this _JniMarshal_PPF_I callback, IntPtr jnienv, IntPtr klazz, float p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPF_L (this _JniMarshal_PPF_L callback, IntPtr jnienv, IntPtr klazz, float p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPF_V (this _JniMarshal_PPF_V callback, IntPtr jnienv, IntPtr klazz, float p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPF_Z (this _JniMarshal_PPF_Z callback, IntPtr jnienv, IntPtr klazz, float p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPFF_F (this _JniMarshal_PPFF_F callback, IntPtr jnienv, IntPtr klazz, float p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPFF_I (this _JniMarshal_PPFF_I callback, IntPtr jnienv, IntPtr klazz, float p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPFF_L (this _JniMarshal_PPFF_L callback, IntPtr jnienv, IntPtr klazz, float p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFF_V (this _JniMarshal_PPFF_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPFF_Z (this _JniMarshal_PPFF_Z callback, IntPtr jnienv, IntPtr klazz, float p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPFFF_F (this _JniMarshal_PPFFF_F callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPFFF_L (this _JniMarshal_PPFFF_L callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFFF_V (this _JniMarshal_PPFFF_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPFFF_Z (this _JniMarshal_PPFFF_Z callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPFFFF_L (this _JniMarshal_PPFFFF_L callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFFFF_V (this _JniMarshal_PPFFFF_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPFFFF_Z (this _JniMarshal_PPFFFF_Z callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFFFFF_V (this _JniMarshal_PPFFFFF_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFFFFF_V (this _JniMarshal_PPFFFFFF_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, float p4, float p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFFFFFFI_V (this _JniMarshal_PPFFFFFFFI_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, float p4, float p5, float p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFFFFFL_V (this _JniMarshal_PPFFFFFFL_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, float p4, float p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFFFFFZ_V (this _JniMarshal_PPFFFFFFZ_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, float p4, float p5, bool p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFFFFFZL_V (this _JniMarshal_PPFFFFFFZL_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, float p4, float p5, bool p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPFFFFI_I (this _JniMarshal_PPFFFFI_I callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPFFFFII_I (this _JniMarshal_PPFFFFII_I callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFFFFII_V (this _JniMarshal_PPFFFFII_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPFFFFL_I (this _JniMarshal_PPFFFFL_I callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFFFFL_V (this _JniMarshal_PPFFFFL_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPFFFFL_Z (this _JniMarshal_PPFFFFL_Z callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPFFFFLI_I (this _JniMarshal_PPFFFFLI_I callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, IntPtr p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFFFFLL_V (this _JniMarshal_PPFFFFLL_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, float p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFFI_V (this _JniMarshal_PPFFFI_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFFJ_V (this _JniMarshal_PPFFFJ_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFFL_V (this _JniMarshal_PPFFFL_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPFFFLILILILI_L (this _JniMarshal_PPFFFLILILILI_L callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, IntPtr p3, int p4, IntPtr p5, int p6, IntPtr p7, int p8, IntPtr p9, int p10)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPFFFLLLL_L (this _JniMarshal_PPFFFLLLL_L callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, float p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPFFI_L (this _JniMarshal_PPFFI_L callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFFIIL_V (this _JniMarshal_PPFFIIL_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFFL_V (this _JniMarshal_PPFFL_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPFFLZ_Z (this _JniMarshal_PPFFLZ_Z callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFFZ_V (this _JniMarshal_PPFFZ_V callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPFFZ_Z (this _JniMarshal_PPFFZ_Z callback, IntPtr jnienv, IntPtr klazz, float p0, float p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPFI_F (this _JniMarshal_PPFI_F callback, IntPtr jnienv, IntPtr klazz, float p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFI_V (this _JniMarshal_PPFI_V callback, IntPtr jnienv, IntPtr klazz, float p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFII_V (this _JniMarshal_PPFII_V callback, IntPtr jnienv, IntPtr klazz, float p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFIII_V (this _JniMarshal_PPFIII_V callback, IntPtr jnienv, IntPtr klazz, float p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPFJLL_F (this _JniMarshal_PPFJLL_F callback, IntPtr jnienv, IntPtr klazz, float p0, long p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPFL_I (this _JniMarshal_PPFL_I callback, IntPtr jnienv, IntPtr klazz, float p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPFL_L (this _JniMarshal_PPFL_L callback, IntPtr jnienv, IntPtr klazz, float p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFL_V (this _JniMarshal_PPFL_V callback, IntPtr jnienv, IntPtr klazz, float p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFLI_V (this _JniMarshal_PPFLI_V callback, IntPtr jnienv, IntPtr klazz, float p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPFLI_Z (this _JniMarshal_PPFLI_Z callback, IntPtr jnienv, IntPtr klazz, float p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPFLL_L (this _JniMarshal_PPFLL_L callback, IntPtr jnienv, IntPtr klazz, float p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPFLL_Z (this _JniMarshal_PPFLL_Z callback, IntPtr jnienv, IntPtr klazz, float p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPFZ_V (this _JniMarshal_PPFZ_V callback, IntPtr jnienv, IntPtr klazz, float p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFZFF_V (this _JniMarshal_PPFZFF_V callback, IntPtr jnienv, IntPtr klazz, float p0, bool p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPFZI_V (this _JniMarshal_PPFZI_V callback, IntPtr jnienv, IntPtr klazz, float p0, bool p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static sbyte Wrap_JniMarshal_PPI_B (this _JniMarshal_PPI_B callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static char Wrap_JniMarshal_PPI_C (this _JniMarshal_PPI_C callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static double Wrap_JniMarshal_PPI_D (this _JniMarshal_PPI_D callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPI_F (this _JniMarshal_PPI_F callback, IntPtr jnienv, IntPtr klazz, int p0)
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

		internal static short Wrap_JniMarshal_PPI_S (this _JniMarshal_PPI_S callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
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

		internal static bool Wrap_JniMarshal_PPI_Z (this _JniMarshal_PPI_Z callback, IntPtr jnienv, IntPtr klazz, int p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIB_L (this _JniMarshal_PPIB_L callback, IntPtr jnienv, IntPtr klazz, int p0, sbyte p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIB_V (this _JniMarshal_PPIB_V callback, IntPtr jnienv, IntPtr klazz, int p0, sbyte p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIBI_V (this _JniMarshal_PPIBI_V callback, IntPtr jnienv, IntPtr klazz, int p0, sbyte p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIC_L (this _JniMarshal_PPIC_L callback, IntPtr jnienv, IntPtr klazz, int p0, char p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIC_V (this _JniMarshal_PPIC_V callback, IntPtr jnienv, IntPtr klazz, int p0, char p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static double Wrap_JniMarshal_PPID_D (this _JniMarshal_PPID_D callback, IntPtr jnienv, IntPtr klazz, int p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPID_L (this _JniMarshal_PPID_L callback, IntPtr jnienv, IntPtr klazz, int p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPID_V (this _JniMarshal_PPID_V callback, IntPtr jnienv, IntPtr klazz, int p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPID_Z (this _JniMarshal_PPID_Z callback, IntPtr jnienv, IntPtr klazz, int p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIDD_V (this _JniMarshal_PPIDD_V callback, IntPtr jnienv, IntPtr klazz, int p0, double p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPIF_F (this _JniMarshal_PPIF_F callback, IntPtr jnienv, IntPtr klazz, int p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIF_I (this _JniMarshal_PPIF_I callback, IntPtr jnienv, IntPtr klazz, int p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIF_L (this _JniMarshal_PPIF_L callback, IntPtr jnienv, IntPtr klazz, int p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIF_V (this _JniMarshal_PPIF_V callback, IntPtr jnienv, IntPtr klazz, int p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIF_Z (this _JniMarshal_PPIF_Z callback, IntPtr jnienv, IntPtr klazz, int p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIFD_V (this _JniMarshal_PPIFD_V callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIFF_V (this _JniMarshal_PPIFF_V callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIFF_Z (this _JniMarshal_PPIFF_Z callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIFFFF_V (this _JniMarshal_PPIFFFF_V callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, float p2, float p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIFFIF_V (this _JniMarshal_PPIFFIF_V callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, float p2, int p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIFFL_L (this _JniMarshal_PPIFFL_L callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, float p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIFI_V (this _JniMarshal_PPIFI_V callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPIFII_F (this _JniMarshal_PPIFII_F callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIFL_I (this _JniMarshal_PPIFL_I callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIFZ_V (this _JniMarshal_PPIFZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIFZZ_V (this _JniMarshal_PPIFZZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, float p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static double Wrap_JniMarshal_PPII_D (this _JniMarshal_PPII_D callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPII_F (this _JniMarshal_PPII_F callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPII_I (this _JniMarshal_PPII_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPII_J (this _JniMarshal_PPII_J callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static short Wrap_JniMarshal_PPII_S (this _JniMarshal_PPII_S callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
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

		internal static bool Wrap_JniMarshal_PPII_Z (this _JniMarshal_PPII_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIF_V (this _JniMarshal_PPIIF_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPIIFF_I (this _JniMarshal_PPIIFF_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIFF_V (this _JniMarshal_PPIIFF_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIFI_V (this _JniMarshal_PPIIFI_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, float p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIFJ_V (this _JniMarshal_PPIIFJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, float p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPIII_F (this _JniMarshal_PPIII_F callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIII_I (this _JniMarshal_PPIII_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIII_L (this _JniMarshal_PPIII_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static bool Wrap_JniMarshal_PPIII_Z (this _JniMarshal_PPIII_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPIIIF_F (this _JniMarshal_PPIIIF_F callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIF_V (this _JniMarshal_PPIIIF_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPIIII_F (this _JniMarshal_PPIIII_F callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIII_L (this _JniMarshal_PPIIII_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
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

		internal static bool Wrap_JniMarshal_PPIIII_Z (this _JniMarshal_PPIIII_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIIIII_I (this _JniMarshal_PPIIIII_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIIII_L (this _JniMarshal_PPIIIII_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIII_V (this _JniMarshal_PPIIIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIIIII_Z (this _JniMarshal_PPIIIII_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPIIIIIB_Z (this _JniMarshal_PPIIIIIB_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, sbyte p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIIIIII_I (this _JniMarshal_PPIIIIII_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIIIII_L (this _JniMarshal_PPIIIIII_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIIII_V (this _JniMarshal_PPIIIIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIIIIII_Z (this _JniMarshal_PPIIIIII_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIIIIIID_I (this _JniMarshal_PPIIIIIID_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, double p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIIIIIF_L (this _JniMarshal_PPIIIIIIF_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, float p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIIIIIF_V (this _JniMarshal_PPIIIIIIIF_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, int p6, float p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIIIIIII_L (this _JniMarshal_PPIIIIIIII_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIIIIII_V (this _JniMarshal_PPIIIIIIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPIIIIIIIII_J (this _JniMarshal_PPIIIIIIIII_J callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIIIIIIII_V (this _JniMarshal_PPIIIIIIIIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8, int p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIIIIIIIL_V (this _JniMarshal_PPIIIIIIIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIIIIIIIIZ_Z (this _JniMarshal_PPIIIIIIIIZ_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7, bool p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIIIIIL_V (this _JniMarshal_PPIIIIIIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, int p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPIIIIIIL_J (this _JniMarshal_PPIIIIIIL_J callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIIIIIL_L (this _JniMarshal_PPIIIIIIL_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIIIIL_V (this _JniMarshal_PPIIIIIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIIIIILIII_V (this _JniMarshal_PPIIIIIILIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, int p5, IntPtr p6, int p7, int p8, int p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPIIIIIL_I (this _JniMarshal_PPIIIIIL_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIIIIL_L (this _JniMarshal_PPIIIIIL_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIIIL_V (this _JniMarshal_PPIIIIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIIIJ_L (this _JniMarshal_PPIIIIJ_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIIL_V (this _JniMarshal_PPIIIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIIIIL_Z (this _JniMarshal_PPIIIIL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPIIIILB_Z (this _JniMarshal_PPIIIILB_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4, sbyte p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPIIIILI_Z (this _JniMarshal_PPIIIILI_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIIIILII_I (this _JniMarshal_PPIIIILII_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4, int p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIILII_V (this _JniMarshal_PPIIIILII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4, int p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPIIIILIII_I (this _JniMarshal_PPIIIILIII_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4, int p5, int p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIILIL_V (this _JniMarshal_PPIIIILIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIIILLI_V (this _JniMarshal_PPIIIILLI_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, IntPtr p4, IntPtr p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIIIZ_V (this _JniMarshal_PPIIIIZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIIIZZ_V (this _JniMarshal_PPIIIIZZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, int p3, bool p4, bool p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIIJI_V (this _JniMarshal_PPIIIJI_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, long p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIIL_L (this _JniMarshal_PPIIIL_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIL_V (this _JniMarshal_PPIIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIILI_L (this _JniMarshal_PPIIILI_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIILI_V (this _JniMarshal_PPIIILI_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIILL_L (this _JniMarshal_PPIIILL_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIIILLLIL_I (this _JniMarshal_PPIIILLLIL_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, IntPtr p3, IntPtr p4, IntPtr p5, int p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIIZ_V (this _JniMarshal_PPIIIZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, int p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIJ_V (this _JniMarshal_PPIIJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIIJL_V (this _JniMarshal_PPIIJL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPIIL_I (this _JniMarshal_PPIIL_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIL_L (this _JniMarshal_PPIIL_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
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

		internal static bool Wrap_JniMarshal_PPIIL_Z (this _JniMarshal_PPIIL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIILI_V (this _JniMarshal_PPIILI_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIILI_Z (this _JniMarshal_PPIILI_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIILIFFFF_V (this _JniMarshal_PPIILIFFFF_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, int p3, float p4, float p5, float p6, float p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIILIFFFFL_V (this _JniMarshal_PPIILIFFFFL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, int p3, float p4, float p5, float p6, float p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIILII_L (this _JniMarshal_PPIILII_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIILII_V (this _JniMarshal_PPIILII_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIILIL_V (this _JniMarshal_PPIILIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIILIL_Z (this _JniMarshal_PPIILIL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIILJI_V (this _JniMarshal_PPIILJI_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, long p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIILL_L (this _JniMarshal_PPIILL_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIILL_V (this _JniMarshal_PPIILL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIILL_Z (this _JniMarshal_PPIILL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPIILLFF_Z (this _JniMarshal_PPIILLFF_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, IntPtr p3, float p4, float p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPIILLI_Z (this _JniMarshal_PPIILLI_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIILLLL_V (this _JniMarshal_PPIILLLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIILZ_V (this _JniMarshal_PPIILZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPIIZ_I (this _JniMarshal_PPIIZ_I callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIZ_L (this _JniMarshal_PPIIZ_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIZ_V (this _JniMarshal_PPIIZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIIZ_Z (this _JniMarshal_PPIIZ_Z callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIZFF_L (this _JniMarshal_PPIIZFF_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, bool p2, float p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIZFFI_L (this _JniMarshal_PPIIZFFI_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, bool p2, float p3, float p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIIZLL_L (this _JniMarshal_PPIIZLL_L callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, bool p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIIZZ_V (this _JniMarshal_PPIIZZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, int p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPIJ_I (this _JniMarshal_PPIJ_I callback, IntPtr jnienv, IntPtr klazz, int p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPIJ_J (this _JniMarshal_PPIJ_J callback, IntPtr jnienv, IntPtr klazz, int p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIJ_L (this _JniMarshal_PPIJ_L callback, IntPtr jnienv, IntPtr klazz, int p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIJ_V (this _JniMarshal_PPIJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIJ_Z (this _JniMarshal_PPIJ_Z callback, IntPtr jnienv, IntPtr klazz, int p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIJF_V (this _JniMarshal_PPIJF_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIJI_L (this _JniMarshal_PPIJI_L callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIJI_V (this _JniMarshal_PPIJI_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIJIIIL_V (this _JniMarshal_PPIJIIIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, int p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIJIZ_V (this _JniMarshal_PPIJIZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, int p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIJJ_V (this _JniMarshal_PPIJJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIJJF_V (this _JniMarshal_PPIJJF_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, long p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIJJI_V (this _JniMarshal_PPIJJI_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, long p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIJJJ_V (this _JniMarshal_PPIJJJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, long p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPIJJL_J (this _JniMarshal_PPIJJL_J callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIJJL_V (this _JniMarshal_PPIJJL_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIJJLLL_V (this _JniMarshal_PPIJJLLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, long p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPIJL_I (this _JniMarshal_PPIJL_I callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIJL_L (this _JniMarshal_PPIJL_L callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIJL_V (this _JniMarshal_PPIJL_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIJL_Z (this _JniMarshal_PPIJL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIJLLL_V (this _JniMarshal_PPIJLLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIJLZ_V (this _JniMarshal_PPIJLZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, long p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static char Wrap_JniMarshal_PPIL_C (this _JniMarshal_PPIL_C callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPIL_I (this _JniMarshal_PPIL_I callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIL_L (this _JniMarshal_PPIL_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIL_V (this _JniMarshal_PPIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

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

		internal static void Wrap_JniMarshal_PPILB_V (this _JniMarshal_PPILB_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILC_V (this _JniMarshal_PPILC_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILD_V (this _JniMarshal_PPILD_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILF_V (this _JniMarshal_PPILF_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILFF_V (this _JniMarshal_PPILFF_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILFI_V (this _JniMarshal_PPILFI_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, float p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILFIF_V (this _JniMarshal_PPILFIF_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, float p2, int p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static char Wrap_JniMarshal_PPILI_C (this _JniMarshal_PPILI_C callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPILI_I (this _JniMarshal_PPILI_I callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILI_L (this _JniMarshal_PPILI_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILI_V (this _JniMarshal_PPILI_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPILI_Z (this _JniMarshal_PPILI_Z callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILII_L (this _JniMarshal_PPILII_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILII_V (this _JniMarshal_PPILII_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPILII_Z (this _JniMarshal_PPILII_Z callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILIII_V (this _JniMarshal_PPILIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILIJJJ_V (this _JniMarshal_PPILIJJJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2, long p3, long p4, long p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILIL_L (this _JniMarshal_PPILIL_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILIL_V (this _JniMarshal_PPILIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILILJ_V (this _JniMarshal_PPILILJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, int p2, IntPtr p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILJ_L (this _JniMarshal_PPILJ_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILJ_V (this _JniMarshal_PPILJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILJI_V (this _JniMarshal_PPILJI_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILJJ_L (this _JniMarshal_PPILJJ_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILJJI_L (this _JniMarshal_PPILJJI_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2, long p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILJJII_L (this _JniMarshal_PPILJJII_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2, long p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILJJIII_L (this _JniMarshal_PPILJJIII_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2, long p3, int p4, int p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILJL_V (this _JniMarshal_PPILJL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILJLL_V (this _JniMarshal_PPILJLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, long p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPILL_I (this _JniMarshal_PPILL_I callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILL_L (this _JniMarshal_PPILL_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2)
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

		internal static bool Wrap_JniMarshal_PPILL_Z (this _JniMarshal_PPILL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILLF_V (this _JniMarshal_PPILLF_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILLI_V (this _JniMarshal_PPILLI_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPILLI_Z (this _JniMarshal_PPILLI_Z callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILLIII_V (this _JniMarshal_PPILLIII_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILLIL_V (this _JniMarshal_PPILLIL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILLJ_V (this _JniMarshal_PPILLJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPILLL_I (this _JniMarshal_PPILLL_I callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILLL_L (this _JniMarshal_PPILLL_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILLL_V (this _JniMarshal_PPILLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPILLL_Z (this _JniMarshal_PPILLL_Z callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILLLIJ_V (this _JniMarshal_PPILLLIJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4, long p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPILLLILI_I (this _JniMarshal_PPILLLILI_I callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4, IntPtr p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILLLJ_V (this _JniMarshal_PPILLLJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILLLL_L (this _JniMarshal_PPILLLL_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILLLL_V (this _JniMarshal_PPILLLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILLLLL_V (this _JniMarshal_PPILLLLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILLLLLL_V (this _JniMarshal_PPILLLLLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILLLLLZ_L (this _JniMarshal_PPILLLLLZ_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, bool p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILLLLZ_V (this _JniMarshal_PPILLLLZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, bool p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPILLLZ_I (this _JniMarshal_PPILLLZ_I callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILLLZ_V (this _JniMarshal_PPILLLZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, IntPtr p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILLZ_L (this _JniMarshal_PPILLZ_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILS_V (this _JniMarshal_PPILS_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILZ_L (this _JniMarshal_PPILZ_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPILZ_V (this _JniMarshal_PPILZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPILZL_V (this _JniMarshal_PPILZL_V callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, bool p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPILZLLL_L (this _JniMarshal_PPILZLLL_L callback, IntPtr jnienv, IntPtr klazz, int p0, IntPtr p1, bool p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIS_L (this _JniMarshal_PPIS_L callback, IntPtr jnienv, IntPtr klazz, int p0, short p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIS_V (this _JniMarshal_PPIS_V callback, IntPtr jnienv, IntPtr klazz, int p0, short p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIS_Z (this _JniMarshal_PPIS_Z callback, IntPtr jnienv, IntPtr klazz, int p0, short p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPISI_V (this _JniMarshal_PPISI_V callback, IntPtr jnienv, IntPtr klazz, int p0, short p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPIZ_I (this _JniMarshal_PPIZ_I callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIZ_L (this _JniMarshal_PPIZ_L callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIZ_V (this _JniMarshal_PPIZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIZ_Z (this _JniMarshal_PPIZ_Z callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIZF_V (this _JniMarshal_PPIZF_V callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

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

		internal static void Wrap_JniMarshal_PPIZJ_V (this _JniMarshal_PPIZJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIZL_V (this _JniMarshal_PPIZL_V callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIZLJ_V (this _JniMarshal_PPIZLJ_V callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, IntPtr p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPIZLL_L (this _JniMarshal_PPIZLL_L callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPIZLL_V (this _JniMarshal_PPIZLL_V callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPIZZ_V (this _JniMarshal_PPIZZ_V callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPIZZ_Z (this _JniMarshal_PPIZZ_Z callback, IntPtr jnienv, IntPtr klazz, int p0, bool p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static sbyte Wrap_JniMarshal_PPJ_B (this _JniMarshal_PPJ_B callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static char Wrap_JniMarshal_PPJ_C (this _JniMarshal_PPJ_C callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static double Wrap_JniMarshal_PPJ_D (this _JniMarshal_PPJ_D callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPJ_F (this _JniMarshal_PPJ_F callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPJ_I (this _JniMarshal_PPJ_I callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPJ_J (this _JniMarshal_PPJ_J callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJ_L (this _JniMarshal_PPJ_L callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static short Wrap_JniMarshal_PPJ_S (this _JniMarshal_PPJ_S callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJ_V (this _JniMarshal_PPJ_V callback, IntPtr jnienv, IntPtr klazz, long p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

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

		internal static void Wrap_JniMarshal_PPJB_V (this _JniMarshal_PPJB_V callback, IntPtr jnienv, IntPtr klazz, long p0, sbyte p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPJC_V (this _JniMarshal_PPJC_V callback, IntPtr jnienv, IntPtr klazz, long p0, char p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPJD_V (this _JniMarshal_PPJD_V callback, IntPtr jnienv, IntPtr klazz, long p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPJF_V (this _JniMarshal_PPJF_V callback, IntPtr jnienv, IntPtr klazz, long p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPJF_Z (this _JniMarshal_PPJF_Z callback, IntPtr jnienv, IntPtr klazz, long p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJFLL_V (this _JniMarshal_PPJFLL_V callback, IntPtr jnienv, IntPtr klazz, long p0, float p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPJFLLL_V (this _JniMarshal_PPJFLLL_V callback, IntPtr jnienv, IntPtr klazz, long p0, float p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPJFZJ_Z (this _JniMarshal_PPJFZJ_Z callback, IntPtr jnienv, IntPtr klazz, long p0, float p1, bool p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPJI_I (this _JniMarshal_PPJI_I callback, IntPtr jnienv, IntPtr klazz, long p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPJI_J (this _JniMarshal_PPJI_J callback, IntPtr jnienv, IntPtr klazz, long p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJI_L (this _JniMarshal_PPJI_L callback, IntPtr jnienv, IntPtr klazz, long p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJI_V (this _JniMarshal_PPJI_V callback, IntPtr jnienv, IntPtr klazz, long p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJII_L (this _JniMarshal_PPJII_L callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPJII_Z (this _JniMarshal_PPJII_Z callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJIII_L (this _JniMarshal_PPJIII_L callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJIIII_V (this _JniMarshal_PPJIIII_V callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJIIIL_L (this _JniMarshal_PPJIIIL_L callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJIIIL_V (this _JniMarshal_PPJIIIL_V callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJIIL_L (this _JniMarshal_PPJIIL_L callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPJIL_I (this _JniMarshal_PPJIL_I callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJIL_L (this _JniMarshal_PPJIL_L callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJIL_V (this _JniMarshal_PPJIL_V callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJILLLL_L (this _JniMarshal_PPJILLLL_L callback, IntPtr jnienv, IntPtr klazz, long p0, int p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPJJ_F (this _JniMarshal_PPJJ_F callback, IntPtr jnienv, IntPtr klazz, long p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPJJ_J (this _JniMarshal_PPJJ_J callback, IntPtr jnienv, IntPtr klazz, long p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJJ_L (this _JniMarshal_PPJJ_L callback, IntPtr jnienv, IntPtr klazz, long p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJJ_V (this _JniMarshal_PPJJ_V callback, IntPtr jnienv, IntPtr klazz, long p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPJJ_Z (this _JniMarshal_PPJJ_Z callback, IntPtr jnienv, IntPtr klazz, long p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJJF_V (this _JniMarshal_PPJJF_V callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPJJF_Z (this _JniMarshal_PPJJF_Z callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJJJ_V (this _JniMarshal_PPJJJ_V callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPJJJLL_V (this _JniMarshal_PPJJJLL_V callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, long p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJJJZJJJJLLLLL_L (this _JniMarshal_PPJJJZJJJJLLLLL_L callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, long p2, bool p3, long p4, long p5, long p6, long p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPJJJZZ_Z (this _JniMarshal_PPJJJZZ_Z callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, long p2, bool p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPJJL_J (this _JniMarshal_PPJJL_J callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJJL_L (this _JniMarshal_PPJJL_L callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJJL_V (this _JniMarshal_PPJJL_V callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPJJLL_V (this _JniMarshal_PPJJLL_V callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJJZ_L (this _JniMarshal_PPJJZ_L callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPJJZ_Z (this _JniMarshal_PPJJZ_Z callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJJZLL_V (this _JniMarshal_PPJJZLL_V callback, IntPtr jnienv, IntPtr klazz, long p0, long p1, bool p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPJL_I (this _JniMarshal_PPJL_I callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPJL_J (this _JniMarshal_PPJL_J callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJL_L (this _JniMarshal_PPJL_L callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJL_V (this _JniMarshal_PPJL_V callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPJL_Z (this _JniMarshal_PPJL_Z callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static double Wrap_JniMarshal_PPJLDL_D (this _JniMarshal_PPJLDL_D callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, double p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPJLF_Z (this _JniMarshal_PPJLF_Z callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJLFFLIZ_L (this _JniMarshal_PPJLFFLIZ_L callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, float p2, float p3, IntPtr p4, int p5, bool p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJLI_V (this _JniMarshal_PPJLI_V callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPJLII_I (this _JniMarshal_PPJLII_I callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJLII_V (this _JniMarshal_PPJLII_V callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPJLII_Z (this _JniMarshal_PPJLII_Z callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPJLIL_I (this _JniMarshal_PPJLIL_I callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJLJ_V (this _JniMarshal_PPJLJ_V callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPJLJL_J (this _JniMarshal_PPJLJL_J callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJLL_L (this _JniMarshal_PPJLL_L callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJLL_V (this _JniMarshal_PPJLL_V callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPJLL_Z (this _JniMarshal_PPJLL_Z callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPJLLL_I (this _JniMarshal_PPJLLL_I callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJLLL_V (this _JniMarshal_PPJLLL_V callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJLLLL_L (this _JniMarshal_PPJLLLL_L callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJLZZL_V (this _JniMarshal_PPJLZZL_V callback, IntPtr jnienv, IntPtr klazz, long p0, IntPtr p1, bool p2, bool p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPJS_V (this _JniMarshal_PPJS_V callback, IntPtr jnienv, IntPtr klazz, long p0, short p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPJZ_J (this _JniMarshal_PPJZ_J callback, IntPtr jnienv, IntPtr klazz, long p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJZ_L (this _JniMarshal_PPJZ_L callback, IntPtr jnienv, IntPtr klazz, long p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJZ_V (this _JniMarshal_PPJZ_V callback, IntPtr jnienv, IntPtr klazz, long p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJZL_L (this _JniMarshal_PPJZL_L callback, IntPtr jnienv, IntPtr klazz, long p0, bool p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPJZL_V (this _JniMarshal_PPJZL_V callback, IntPtr jnienv, IntPtr klazz, long p0, bool p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPJZZ_V (this _JniMarshal_PPJZZ_V callback, IntPtr jnienv, IntPtr klazz, long p0, bool p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPJZZL_L (this _JniMarshal_PPJZZL_L callback, IntPtr jnienv, IntPtr klazz, long p0, bool p1, bool p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static sbyte Wrap_JniMarshal_PPL_B (this _JniMarshal_PPL_B callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static char Wrap_JniMarshal_PPL_C (this _JniMarshal_PPL_C callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static double Wrap_JniMarshal_PPL_D (this _JniMarshal_PPL_D callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPL_F (this _JniMarshal_PPL_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
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

		internal static long Wrap_JniMarshal_PPL_J (this _JniMarshal_PPL_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
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

		internal static short Wrap_JniMarshal_PPL_S (this _JniMarshal_PPL_S callback, IntPtr jnienv, IntPtr klazz, IntPtr p0)
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

		internal static sbyte Wrap_JniMarshal_PPLB_B (this _JniMarshal_PPLB_B callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLB_L (this _JniMarshal_PPLB_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLB_V (this _JniMarshal_PPLB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBB_V (this _JniMarshal_PPLBB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBBI_V (this _JniMarshal_PPLBBI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, sbyte p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBBL_V (this _JniMarshal_PPLBBL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, sbyte p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBC_V (this _JniMarshal_PPLBC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBD_V (this _JniMarshal_PPLBD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBF_V (this _JniMarshal_PPLBF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBI_V (this _JniMarshal_PPLBI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBJ_V (this _JniMarshal_PPLBJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBL_V (this _JniMarshal_PPLBL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBS_V (this _JniMarshal_PPLBS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLBZ_V (this _JniMarshal_PPLBZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, sbyte p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static char Wrap_JniMarshal_PPLC_C (this _JniMarshal_PPLC_C callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLC_L (this _JniMarshal_PPLC_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLC_V (this _JniMarshal_PPLC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCB_V (this _JniMarshal_PPLCB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCC_V (this _JniMarshal_PPLCC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCD_V (this _JniMarshal_PPLCD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCF_V (this _JniMarshal_PPLCF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCI_V (this _JniMarshal_PPLCI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCJ_V (this _JniMarshal_PPLCJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCL_V (this _JniMarshal_PPLCL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCS_V (this _JniMarshal_PPLCS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLCZ_V (this _JniMarshal_PPLCZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, char p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static double Wrap_JniMarshal_PPLD_D (this _JniMarshal_PPLD_D callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLD_L (this _JniMarshal_PPLD_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLD_V (this _JniMarshal_PPLD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLD_Z (this _JniMarshal_PPLD_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLDB_V (this _JniMarshal_PPLDB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLDC_V (this _JniMarshal_PPLDC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLDD_V (this _JniMarshal_PPLDD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLDF_V (this _JniMarshal_PPLDF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLDI_V (this _JniMarshal_PPLDI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLDJ_V (this _JniMarshal_PPLDJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLDL_L (this _JniMarshal_PPLDL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLDL_V (this _JniMarshal_PPLDL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLDS_V (this _JniMarshal_PPLDS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLDZ_V (this _JniMarshal_PPLDZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, double p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPLF_F (this _JniMarshal_PPLF_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLF_L (this _JniMarshal_PPLF_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static void Wrap_JniMarshal_PPLFB_V (this _JniMarshal_PPLFB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFC_V (this _JniMarshal_PPLFC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFD_V (this _JniMarshal_PPLFD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFDD_V (this _JniMarshal_PPLFDD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, double p2, double p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLFF_L (this _JniMarshal_PPLFF_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLFF_V (this _JniMarshal_PPLFF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLFF_Z (this _JniMarshal_PPLFF_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLFFF_V (this _JniMarshal_PPLFFF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFFFF_V (this _JniMarshal_PPLFFFF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, float p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFFL_V (this _JniMarshal_PPLFFL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFFLFFL_V (this _JniMarshal_PPLFFLFFL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, IntPtr p3, float p4, float p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFFLI_V (this _JniMarshal_PPLFFLI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFFLL_V (this _JniMarshal_PPLFFLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFFZ_V (this _JniMarshal_PPLFFZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLFFZ_Z (this _JniMarshal_PPLFFZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLFFZL_V (this _JniMarshal_PPLFFZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, float p2, bool p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLFI_L (this _JniMarshal_PPLFI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLFI_V (this _JniMarshal_PPLFI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLFII_L (this _JniMarshal_PPLFII_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLFJ_V (this _JniMarshal_PPLFJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLFJL_Z (this _JniMarshal_PPLFJL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLFL_V (this _JniMarshal_PPLFL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLFL_Z (this _JniMarshal_PPLFL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLFLL_V (this _JniMarshal_PPLFLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFLLL_V (this _JniMarshal_PPLFLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFS_V (this _JniMarshal_PPLFS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLFZ_V (this _JniMarshal_PPLFZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, float p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static sbyte Wrap_JniMarshal_PPLI_B (this _JniMarshal_PPLI_B callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static char Wrap_JniMarshal_PPLI_C (this _JniMarshal_PPLI_C callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static double Wrap_JniMarshal_PPLI_D (this _JniMarshal_PPLI_D callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPLI_F (this _JniMarshal_PPLI_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLI_I (this _JniMarshal_PPLI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLI_J (this _JniMarshal_PPLI_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static short Wrap_JniMarshal_PPLI_S (this _JniMarshal_PPLI_S callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
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

		internal static bool Wrap_JniMarshal_PPLI_Z (this _JniMarshal_PPLI_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIB_V (this _JniMarshal_PPLIB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIC_V (this _JniMarshal_PPLIC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLICIZZLL_I (this _JniMarshal_PPLICIZZLL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, char p2, int p3, bool p4, bool p5, IntPtr p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLID_V (this _JniMarshal_PPLID_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIF_V (this _JniMarshal_PPLIF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPLIFF_F (this _JniMarshal_PPLIFF_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLIFF_J (this _JniMarshal_PPLIFF_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPLII_F (this _JniMarshal_PPLII_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static long Wrap_JniMarshal_PPLII_J (this _JniMarshal_PPLII_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLII_L (this _JniMarshal_PPLII_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2)
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

		internal static void Wrap_JniMarshal_PPLIIF_V (this _JniMarshal_PPLIIF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIIFF_V (this _JniMarshal_PPLIIFF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, float p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLIIFF_Z (this _JniMarshal_PPLIIFF_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, float p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIFFFFFFFFFFFFFFFF_L (this _JniMarshal_PPLIIFFFFFFFFFFFFFFFF_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, float p3, float p4, float p5, float p6, float p7, float p8, float p9, float p10, float p11, float p12, float p13, float p14, float p15, float p16, float p17, float p18)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIFFIIZL_V (this _JniMarshal_PPLIIFFIIZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, float p3, float p4, int p5, int p6, bool p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIIFFL_V (this _JniMarshal_PPLIIFFL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, float p3, float p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIIFL_I (this _JniMarshal_PPLIIFL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, float p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLIII_I (this _JniMarshal_PPLIII_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIII_L (this _JniMarshal_PPLIII_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIII_V (this _JniMarshal_PPLIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLIII_Z (this _JniMarshal_PPLIII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIIF_V (this _JniMarshal_PPLIIIF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIII_L (this _JniMarshal_PPLIIII_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
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

		internal static bool Wrap_JniMarshal_PPLIIII_Z (this _JniMarshal_PPLIIII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIIIFFZL_V (this _JniMarshal_PPLIIIIFFZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, float p5, float p6, bool p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIIIII_V (this _JniMarshal_PPLIIIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
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

		internal static long Wrap_JniMarshal_PPLIIIIIIIII_J (this _JniMarshal_PPLIIIIIIIII_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, int p5, int p6, int p7, int p8, int p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLIIIIIIL_J (this _JniMarshal_PPLIIIIIIL_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, int p5, int p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIIIIIZL_V (this _JniMarshal_PPLIIIIIIZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, int p5, int p6, bool p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIIIIIL_V (this _JniMarshal_PPLIIIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIIIILI_L (this _JniMarshal_PPLIIIIILI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, int p5, IntPtr p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIIIL_V (this _JniMarshal_PPLIIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIIIILL_V (this _JniMarshal_PPLIIIILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIIIIZF_I (this _JniMarshal_PPLIIIIZF_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, bool p5, float p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPLIIIIZI_F (this _JniMarshal_PPLIIIIZI_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, bool p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPLIIIIZILI_F (this _JniMarshal_PPLIIIIZILI_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, bool p5, int p6, IntPtr p7, int p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIIIZL_V (this _JniMarshal_PPLIIIIZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, bool p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPLIIIIZLI_F (this _JniMarshal_PPLIIIIZLI_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, int p4, bool p5, IntPtr p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLIIIJ_I (this _JniMarshal_PPLIIIJ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIIL_L (this _JniMarshal_PPLIIIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIIL_V (this _JniMarshal_PPLIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIILZ_L (this _JniMarshal_PPLIIILZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, IntPtr p4, bool p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLIIIZ_I (this _JniMarshal_PPLIIIZ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIIZ_L (this _JniMarshal_PPLIIIZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIIZZ_L (this _JniMarshal_PPLIIIZZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, bool p4, bool p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIIZZL_L (this _JniMarshal_PPLIIIZZL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, int p3, bool p4, bool p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLIIJ_I (this _JniMarshal_PPLIIJ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIJ_V (this _JniMarshal_PPLIIJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIJL_L (this _JniMarshal_PPLIIJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, long p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIJLLL_V (this _JniMarshal_PPLIIJLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, long p3, IntPtr p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIIL_I (this _JniMarshal_PPLIIL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIIL_L (this _JniMarshal_PPLIIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIIL_V (this _JniMarshal_PPLIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLIIL_Z (this _JniMarshal_PPLIIL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIILFFL_V (this _JniMarshal_PPLIILFFL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, float p4, float p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIILI_I (this _JniMarshal_PPLIILI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIILI_V (this _JniMarshal_PPLIILI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIILII_L (this _JniMarshal_PPLIILII_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIILIL_L (this _JniMarshal_PPLIILIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIILILIL_V (this _JniMarshal_PPLIILILIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, int p4, IntPtr p5, int p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIILILJJ_V (this _JniMarshal_PPLIILILJJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, int p4, IntPtr p5, long p6, long p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIILILJJLZ_V (this _JniMarshal_PPLIILILJJLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, int p4, IntPtr p5, long p6, long p7, IntPtr p8, bool p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIILL_I (this _JniMarshal_PPLIILL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIILL_L (this _JniMarshal_PPLIILL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIILL_V (this _JniMarshal_PPLIILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIILLL_V (this _JniMarshal_PPLIILLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIIS_V (this _JniMarshal_PPLIIS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, short p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLIIZ_Z (this _JniMarshal_PPLIIZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLIIZFL_I (this _JniMarshal_PPLIIZFL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, bool p3, float p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLIIZII_I (this _JniMarshal_PPLIIZII_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, bool p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLIIZZ_Z (this _JniMarshal_PPLIIZZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, int p2, bool p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLIJ_I (this _JniMarshal_PPLIJ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIJ_L (this _JniMarshal_PPLIJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIJ_V (this _JniMarshal_PPLIJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLIJ_Z (this _JniMarshal_PPLIJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIJJ_V (this _JniMarshal_PPLIJJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, long p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIJL_L (this _JniMarshal_PPLIJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIJZ_V (this _JniMarshal_PPLIJZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, long p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIL_I (this _JniMarshal_PPLIL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLIL_J (this _JniMarshal_PPLIL_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIL_L (this _JniMarshal_PPLIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIL_V (this _JniMarshal_PPLIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2)
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

		internal static int Wrap_JniMarshal_PPLILI_I (this _JniMarshal_PPLILI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLILI_L (this _JniMarshal_PPLILI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILI_V (this _JniMarshal_PPLILI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLILI_Z (this _JniMarshal_PPLILI_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILII_V (this _JniMarshal_PPLILII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLILII_Z (this _JniMarshal_PPLILII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLILIII_I (this _JniMarshal_PPLILIII_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILIII_V (this _JniMarshal_PPLILIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILIIIL_V (this _JniMarshal_PPLILIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, int p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILIIL_V (this _JniMarshal_PPLILIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILIILL_V (this _JniMarshal_PPLILIILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, int p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILIJ_V (this _JniMarshal_PPLILIJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILIL_V (this _JniMarshal_PPLILIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILILILILIIL_V (this _JniMarshal_PPLILILILILIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, IntPtr p4, int p5, IntPtr p6, int p7, IntPtr p8, int p9, int p10, IntPtr p11)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLILIZL_L (this _JniMarshal_PPLILIZL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, int p3, bool p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILJ_V (this _JniMarshal_PPLILJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLILL_I (this _JniMarshal_PPLILL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLILL_L (this _JniMarshal_PPLILL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILL_V (this _JniMarshal_PPLILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLILL_Z (this _JniMarshal_PPLILL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILLIZII_V (this _JniMarshal_PPLILLIZII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3, int p4, bool p5, int p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLILLL_I (this _JniMarshal_PPLILLL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILLL_V (this _JniMarshal_PPLILLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLILLL_Z (this _JniMarshal_PPLILLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILLLL_V (this _JniMarshal_PPLILLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILLLLLLL_V (this _JniMarshal_PPLILLLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILZ_V (this _JniMarshal_PPLILZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLILZ_Z (this _JniMarshal_PPLILZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLILZLLJL_V (this _JniMarshal_PPLILZLLJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, bool p3, IntPtr p4, IntPtr p5, long p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILZLLLL_V (this _JniMarshal_PPLILZLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, bool p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLILZZIL_V (this _JniMarshal_PPLILZZIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, IntPtr p2, bool p3, bool p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIS_V (this _JniMarshal_PPLIS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIZ_I (this _JniMarshal_PPLIZ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLIZ_L (this _JniMarshal_PPLIZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIZ_V (this _JniMarshal_PPLIZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIZF_V (this _JniMarshal_PPLIZF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIZI_I (this _JniMarshal_PPLIZI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIZI_V (this _JniMarshal_PPLIZI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLIZL_V (this _JniMarshal_PPLIZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLIZZ_I (this _JniMarshal_PPLIZZ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLIZZ_V (this _JniMarshal_PPLIZZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, int p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLJ_I (this _JniMarshal_PPLJ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLJ_J (this _JniMarshal_PPLJ_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJ_L (this _JniMarshal_PPLJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJ_V (this _JniMarshal_PPLJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLJ_Z (this _JniMarshal_PPLJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJB_V (this _JniMarshal_PPLJB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLJC_V (this _JniMarshal_PPLJC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLJD_V (this _JniMarshal_PPLJD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLJF_V (this _JniMarshal_PPLJF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLJFL_V (this _JniMarshal_PPLJFL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, float p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLJFLL_V (this _JniMarshal_PPLJFLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, float p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPLJI_J (this _JniMarshal_PPLJI_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJI_L (this _JniMarshal_PPLJI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJI_V (this _JniMarshal_PPLJI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLJI_Z (this _JniMarshal_PPLJI_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLJIIII_J (this _JniMarshal_PPLJIIII_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJIL_L (this _JniMarshal_PPLJIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJIL_V (this _JniMarshal_PPLJIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLJIZ_V (this _JniMarshal_PPLJIZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, int p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPLJJ_J (this _JniMarshal_PPLJJ_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJJ_L (this _JniMarshal_PPLJJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJJ_V (this _JniMarshal_PPLJJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLJJ_Z (this _JniMarshal_PPLJJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLJJI_I (this _JniMarshal_PPLJJI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJJJJ_L (this _JniMarshal_PPLJJJJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, long p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJJJJJLJLLJJ_L (this _JniMarshal_PPLJJJJJLJLLJJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, long p3, long p4, long p5, IntPtr p6, long p7, IntPtr p8, IntPtr p9, long p10, long p11)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJJJJLJLJJ_L (this _JniMarshal_PPLJJJJLJLJJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, long p3, long p4, IntPtr p5, long p6, IntPtr p7, long p8, long p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJJL_L (this _JniMarshal_PPLJJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJJL_V (this _JniMarshal_PPLJJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJJLI_L (this _JniMarshal_PPLJJLI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJJZ_L (this _JniMarshal_PPLJJZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJJZ_V (this _JniMarshal_PPLJJZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, long p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPLJL_J (this _JniMarshal_PPLJL_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJL_L (this _JniMarshal_PPLJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJL_V (this _JniMarshal_PPLJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLJL_Z (this _JniMarshal_PPLJL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJLIJJ_V (this _JniMarshal_PPLJLIJJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2, int p3, long p4, long p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLJLL_I (this _JniMarshal_PPLJLL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJLL_L (this _JniMarshal_PPLJLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJLL_V (this _JniMarshal_PPLJLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLJLLL_L (this _JniMarshal_PPLJLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJLLL_V (this _JniMarshal_PPLJLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLJLLLL_V (this _JniMarshal_PPLJLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLJS_V (this _JniMarshal_PPLJS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPLJZ_J (this _JniMarshal_PPLJZ_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLJZ_V (this _JniMarshal_PPLJZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, long p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static double Wrap_JniMarshal_PPLL_D (this _JniMarshal_PPLL_D callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPLL_F (this _JniMarshal_PPLL_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLL_I (this _JniMarshal_PPLL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLL_J (this _JniMarshal_PPLL_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
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

		internal static void Wrap_JniMarshal_PPLL_V (this _JniMarshal_PPLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

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

		internal static void Wrap_JniMarshal_PPLLB_V (this _JniMarshal_PPLLB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLC_V (this _JniMarshal_PPLLC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLD_V (this _JniMarshal_PPLLD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPLLF_F (this _JniMarshal_PPLLF_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLF_V (this _JniMarshal_PPLLF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLFF_Z (this _JniMarshal_PPLLFF_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLFFF_V (this _JniMarshal_PPLLFFF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, float p2, float p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLFFL_V (this _JniMarshal_PPLLFFL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, float p2, float p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLFFLL_L (this _JniMarshal_PPLLFFLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, float p2, float p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLFI_V (this _JniMarshal_PPLLFI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, float p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLFJDD_Z (this _JniMarshal_PPLLFJDD_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, float p2, long p3, double p4, double p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPLLI_F (this _JniMarshal_PPLLI_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLI_I (this _JniMarshal_PPLLI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLI_L (this _JniMarshal_PPLLI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLI_V (this _JniMarshal_PPLLI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLI_Z (this _JniMarshal_PPLLI_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIF_V (this _JniMarshal_PPLLIF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLIFLLII_V (this _JniMarshal_PPLLIFLLII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, float p3, IntPtr p4, IntPtr p5, int p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLLII_I (this _JniMarshal_PPLLII_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLII_L (this _JniMarshal_PPLLII_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLII_V (this _JniMarshal_PPLLII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLII_Z (this _JniMarshal_PPLLII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIIFIIIL_V (this _JniMarshal_PPLLIIFIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, float p4, int p5, int p6, int p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLIIFIIILLLLLL_L (this _JniMarshal_PPLLIIFIIILLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, float p4, int p5, int p6, int p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLIII_I (this _JniMarshal_PPLLIII_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLIII_L (this _JniMarshal_PPLLIII_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIII_V (this _JniMarshal_PPLLIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLIIII_V (this _JniMarshal_PPLLIIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLIIII_Z (this _JniMarshal_PPLLIIII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIIIIILIII_V (this _JniMarshal_PPLLIIIIILIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4, int p5, int p6, IntPtr p7, int p8, int p9, int p10)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLIIIIILIIZL_V (this _JniMarshal_PPLLIIIIILIIZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4, int p5, int p6, IntPtr p7, int p8, int p9, bool p10, IntPtr p11)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLIIIILLII_V (this _JniMarshal_PPLLIIIILLII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4, int p5, IntPtr p6, IntPtr p7, int p8, int p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLIIIL_V (this _JniMarshal_PPLLIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLIIILL_V (this _JniMarshal_PPLLIIILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLIIIZ_L (this _JniMarshal_PPLLIIIZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, int p4, bool p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLLIIJ_Z (this _JniMarshal_PPLLIIJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLIIJL_L (this _JniMarshal_PPLLIIJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, long p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLIIL_I (this _JniMarshal_PPLLIIL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLIIL_L (this _JniMarshal_PPLLIIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIIL_V (this _JniMarshal_PPLLIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLIIL_Z (this _JniMarshal_PPLLIIL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIILI_V (this _JniMarshal_PPLLIILI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, IntPtr p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLIIZ_Z (this _JniMarshal_PPLLIIZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, int p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIJ_V (this _JniMarshal_PPLLIJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLIJ_Z (this _JniMarshal_PPLLIJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLIJL_L (this _JniMarshal_PPLLIJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, long p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLIL_L (this _JniMarshal_PPLLIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIL_V (this _JniMarshal_PPLLIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLIL_Z (this _JniMarshal_PPLLIL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLILFFLZ_L (this _JniMarshal_PPLLILFFLZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, float p4, float p5, IntPtr p6, bool p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLILFFLZLI_L (this _JniMarshal_PPLLILFFLZLI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, float p4, float p5, IntPtr p6, bool p7, IntPtr p8, int p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLILI_L (this _JniMarshal_PPLLILI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLILI_V (this _JniMarshal_PPLLILI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLILIII_V (this _JniMarshal_PPLLILIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, int p4, int p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLILIIIL_V (this _JniMarshal_PPLLILIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, int p4, int p5, int p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLILIL_L (this _JniMarshal_PPLLILIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLILIL_V (this _JniMarshal_PPLLILIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLILILJIJJL_L (this _JniMarshal_PPLLILILJIJJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, int p4, IntPtr p5, long p6, int p7, long p8, long p9, IntPtr p10)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLILL_I (this _JniMarshal_PPLLILL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLILL_L (this _JniMarshal_PPLLILL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLILL_V (this _JniMarshal_PPLLILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLILLI_V (this _JniMarshal_PPLLILLI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, IntPtr p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLILLLIL_V (this _JniMarshal_PPLLILLLIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, IntPtr p4, IntPtr p5, int p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLILLLILJ_V (this _JniMarshal_PPLLILLLILJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, IntPtr p4, IntPtr p5, int p6, IntPtr p7, long p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLILLZLIZ_L (this _JniMarshal_PPLLILLZLIZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, IntPtr p3, IntPtr p4, bool p5, IntPtr p6, int p7, bool p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLIZ_I (this _JniMarshal_PPLLIZ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLIZ_L (this _JniMarshal_PPLLIZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLIZ_V (this _JniMarshal_PPLLIZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, int p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJ_L (this _JniMarshal_PPLLJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLJ_V (this _JniMarshal_PPLLJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2)
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

		internal static bool Wrap_JniMarshal_PPLLJFZJ_Z (this _JniMarshal_PPLLJFZJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, float p3, bool p4, long p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJI_L (this _JniMarshal_PPLLJI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLJI_V (this _JniMarshal_PPLLJI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJJ_L (this _JniMarshal_PPLLJJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLJJ_V (this _JniMarshal_PPLLJJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJJJJJ_L (this _JniMarshal_PPLLJJJJJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, long p3, long p4, long p5, long p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJJJJZ_L (this _JniMarshal_PPLLJJJJZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, long p3, long p4, long p5, bool p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLJJJL_V (this _JniMarshal_PPLLJJJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, long p3, long p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJJL_L (this _JniMarshal_PPLLJJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, long p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLJJL_V (this _JniMarshal_PPLLJJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, long p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJL_L (this _JniMarshal_PPLLJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLJL_V (this _JniMarshal_PPLLJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLJL_Z (this _JniMarshal_PPLLJL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJLJ_L (this _JniMarshal_PPLLJLJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, IntPtr p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLJLL_L (this _JniMarshal_PPLLJLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, long p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLL_I (this _JniMarshal_PPLLL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLLL_J (this _JniMarshal_PPLLL_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static void Wrap_JniMarshal_PPLLL_V (this _JniMarshal_PPLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

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

		internal static bool Wrap_JniMarshal_PPLLLFF_Z (this _JniMarshal_PPLLLFF_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, float p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLFFIZ_V (this _JniMarshal_PPLLLFFIZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, float p3, float p4, int p5, bool p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLFFLL_V (this _JniMarshal_PPLLLFFLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, float p3, float p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLFFZ_Z (this _JniMarshal_PPLLLFFZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, float p3, float p4, bool p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLLI_I (this _JniMarshal_PPLLLI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLLLI_J (this _JniMarshal_PPLLLI_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLI_L (this _JniMarshal_PPLLLI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLI_V (this _JniMarshal_PPLLLI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLI_Z (this _JniMarshal_PPLLLI_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLIFF_V (this _JniMarshal_PPLLLIFF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, float p4, float p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLIFFL_V (this _JniMarshal_PPLLLIFFL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, float p4, float p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLII_L (this _JniMarshal_PPLLLII_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLII_V (this _JniMarshal_PPLLLII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLLLIII_I (this _JniMarshal_PPLLLIII_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLIII_V (this _JniMarshal_PPLLLIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLIIII_V (this _JniMarshal_PPLLLIIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, int p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLIIIII_V (this _JniMarshal_PPLLLIIIII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, int p5, int p6, int p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLIIIIIL_V (this _JniMarshal_PPLLLIIIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, int p5, int p6, int p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLIIIL_V (this _JniMarshal_PPLLLIIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLIIL_L (this _JniMarshal_PPLLLIIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLIIL_V (this _JniMarshal_PPLLLIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLIILI_V (this _JniMarshal_PPLLLIILI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, IntPtr p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLIILLLLLZZLZZZZLL_L (this _JniMarshal_PPLLLIILLLLLZZLZZZZLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, int p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, bool p10, bool p11, IntPtr p12, bool p13, bool p14, bool p15, bool p16, IntPtr p17, IntPtr p18)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLIJ_L (this _JniMarshal_PPLLLIJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLIL_L (this _JniMarshal_PPLLLIL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLIL_V (this _JniMarshal_PPLLLIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLIL_Z (this _JniMarshal_PPLLLIL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLILL_V (this _JniMarshal_PPLLLILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLILLLL_L (this _JniMarshal_PPLLLILLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLILLLLL_L (this _JniMarshal_PPLLLILLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLILLLLL_V (this _JniMarshal_PPLLLILLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLILZZZZIZILIJ_L (this _JniMarshal_PPLLLILZZZZIZILIJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, IntPtr p4, bool p5, bool p6, bool p7, bool p8, int p9, bool p10, int p11, IntPtr p12, int p13, long p14)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLIZ_L (this _JniMarshal_PPLLLIZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLIZ_V (this _JniMarshal_PPLLLIZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, int p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLJ_L (this _JniMarshal_PPLLLJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLJ_V (this _JniMarshal_PPLLLJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLJIIZ_L (this _JniMarshal_PPLLLJIIZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, int p4, int p5, bool p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLJJIL_V (this _JniMarshal_PPLLLJJIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, long p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLJJJJJ_L (this _JniMarshal_PPLLLJJJJJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, long p4, long p5, long p6, long p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLJJJJJZ_L (this _JniMarshal_PPLLLJJJJJZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, long p4, long p5, long p6, long p7, bool p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLJJL_L (this _JniMarshal_PPLLLJJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, long p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLJJL_V (this _JniMarshal_PPLLLJJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, long p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLJL_L (this _JniMarshal_PPLLLJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLJLL_V (this _JniMarshal_PPLLLJLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLJZ_L (this _JniMarshal_PPLLLJZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLJZZ_L (this _JniMarshal_PPLLLJZZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, bool p4, bool p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLJZZJJL_V (this _JniMarshal_PPLLLJZZJJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, long p3, bool p4, bool p5, long p6, long p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLLLL_I (this _JniMarshal_PPLLLL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLLLL_J (this _JniMarshal_PPLLLL_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLL_L (this _JniMarshal_PPLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static bool Wrap_JniMarshal_PPLLLL_Z (this _JniMarshal_PPLLLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLF_L (this _JniMarshal_PPLLLLF_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, float p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLFI_V (this _JniMarshal_PPLLLLFI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, float p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLLLLI_I (this _JniMarshal_PPLLLLI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLI_L (this _JniMarshal_PPLLLLI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLI_V (this _JniMarshal_PPLLLLI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLLI_Z (this _JniMarshal_PPLLLLI_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLII_V (this _JniMarshal_PPLLLLII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLLII_Z (this _JniMarshal_PPLLLLII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLIIFIILLLLLLJJJJJZ_L (this _JniMarshal_PPLLLLIIFIILLLLLLJJJJJZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4, int p5, float p6, int p7, int p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14, long p15, long p16, long p17, long p18, long p19, bool p20)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18, p19, p20);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLILL_V (this _JniMarshal_PPLLLLILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLLILZJ_V (this _JniMarshal_PPLLLLILZJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, int p4, IntPtr p5, bool p6, long p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPLLLLJ_J (this _JniMarshal_PPLLLLJ_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLJ_V (this _JniMarshal_PPLLLLJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, long p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLLJJIL_V (this _JniMarshal_PPLLLLJJIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, long p4, long p5, int p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLL_L (this _JniMarshal_PPLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLL_V (this _JniMarshal_PPLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLLL_Z (this _JniMarshal_PPLLLLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLI_V (this _JniMarshal_PPLLLLLI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLIL_V (this _JniMarshal_PPLLLLLIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, int p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLILL_V (this _JniMarshal_PPLLLLLILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, int p5, IntPtr p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLIZLZIZIJ_L (this _JniMarshal_PPLLLLLIZLZIZIJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, int p5, bool p6, IntPtr p7, bool p8, int p9, bool p10, int p11, long p12)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLLLLLJ_Z (this _JniMarshal_PPLLLLLJ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, long p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLL_L (this _JniMarshal_PPLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLL_V (this _JniMarshal_PPLLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLLLL_Z (this _JniMarshal_PPLLLLLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLI_L (this _JniMarshal_PPLLLLLLI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLL_L (this _JniMarshal_PPLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLLL_V (this _JniMarshal_PPLLLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLJ_L (this _JniMarshal_PPLLLLLLLJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, long p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLL_L (this _JniMarshal_PPLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLLLL_V (this _JniMarshal_PPLLLLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLLLLL_V (this _JniMarshal_PPLLLLLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLLLLLL_V (this _JniMarshal_PPLLLLLLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLLLLLLL_V (this _JniMarshal_PPLLLLLLLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLLLLLLLL_V (this _JniMarshal_PPLLLLLLLLLLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14, IntPtr p15)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14, IntPtr p15, IntPtr p16)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14, IntPtr p15, IntPtr p16, IntPtr p17)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14, IntPtr p15, IntPtr p16, IntPtr p17, IntPtr p18)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14, IntPtr p15, IntPtr p16, IntPtr p17, IntPtr p18, IntPtr p19)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18, p19);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14, IntPtr p15, IntPtr p16, IntPtr p17, IntPtr p18, IntPtr p19, IntPtr p20)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18, p19, p20);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLLLLLL_L (this _JniMarshal_PPLLLLLLLLLLLLLLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10, IntPtr p11, IntPtr p12, IntPtr p13, IntPtr p14, IntPtr p15, IntPtr p16, IntPtr p17, IntPtr p18, IntPtr p19, IntPtr p20, IntPtr p21)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, p15, p16, p17, p18, p19, p20, p21);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLLZ_V (this _JniMarshal_PPLLLLLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, bool p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLLZ_L (this _JniMarshal_PPLLLLZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLLZ_V (this _JniMarshal_PPLLLLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLLZ_Z (this _JniMarshal_PPLLLLZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, IntPtr p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLLZ_I (this _JniMarshal_PPLLLZ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLZ_L (this _JniMarshal_PPLLLZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLZ_V (this _JniMarshal_PPLLLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLLZ_Z (this _JniMarshal_PPLLLZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLZL_L (this _JniMarshal_PPLLLZL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLLZL_V (this _JniMarshal_PPLLLZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLZLL_L (this _JniMarshal_PPLLLZLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLLZZ_L (this _JniMarshal_PPLLLZZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLLLZZ_Z (this _JniMarshal_PPLLLZZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, IntPtr p2, bool p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLS_L (this _JniMarshal_PPLLS_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLS_V (this _JniMarshal_PPLLS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLLZ_I (this _JniMarshal_PPLLZ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZ_L (this _JniMarshal_PPLLZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLZ_V (this _JniMarshal_PPLLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLLZ_Z (this _JniMarshal_PPLLZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLLZI_I (this _JniMarshal_PPLLZI_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZI_L (this _JniMarshal_PPLLZI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLZI_V (this _JniMarshal_PPLLZI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLLZIL_V (this _JniMarshal_PPLLZIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZJ_L (this _JniMarshal_PPLLZJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZJL_L (this _JniMarshal_PPLLZJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, long p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZJLL_L (this _JniMarshal_PPLLZJLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, long p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZL_L (this _JniMarshal_PPLLZL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLLZL_V (this _JniMarshal_PPLLZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZLL_L (this _JniMarshal_PPLLZLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLLZLLJZ_J (this _JniMarshal_PPLLZLLJZ_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, IntPtr p3, IntPtr p4, long p5, bool p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPLLZLLJZLL_J (this _JniMarshal_PPLLZLLJZLL_J callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, IntPtr p3, IntPtr p4, long p5, bool p6, IntPtr p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZZ_L (this _JniMarshal_PPLLZZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLLZZ_Z (this _JniMarshal_PPLLZZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLLZZI_L (this _JniMarshal_PPLLZZI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, IntPtr p1, bool p2, bool p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLS_L (this _JniMarshal_PPLS_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static short Wrap_JniMarshal_PPLS_S (this _JniMarshal_PPLS_S callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLS_V (this _JniMarshal_PPLS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSB_V (this _JniMarshal_PPLSB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSBBBB_V (this _JniMarshal_PPLSBBBB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, sbyte p2, sbyte p3, sbyte p4, sbyte p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSC_V (this _JniMarshal_PPLSC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSD_V (this _JniMarshal_PPLSD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSF_V (this _JniMarshal_PPLSF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSI_V (this _JniMarshal_PPLSI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSJ_V (this _JniMarshal_PPLSJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSL_V (this _JniMarshal_PPLSL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSS_V (this _JniMarshal_PPLSS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLSZ_V (this _JniMarshal_PPLSZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, short p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static float Wrap_JniMarshal_PPLZ_F (this _JniMarshal_PPLZ_F callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPLZ_I (this _JniMarshal_PPLZ_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZ_L (this _JniMarshal_PPLZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static bool Wrap_JniMarshal_PPLZ_Z (this _JniMarshal_PPLZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZB_V (this _JniMarshal_PPLZB_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, sbyte p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZC_V (this _JniMarshal_PPLZC_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, char p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZD_V (this _JniMarshal_PPLZD_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, double p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZF_V (this _JniMarshal_PPLZF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, float p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZFF_V (this _JniMarshal_PPLZFF_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, float p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPLZFL_I (this _JniMarshal_PPLZFL_I callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, float p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZI_L (this _JniMarshal_PPLZI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZI_V (this _JniMarshal_PPLZI_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLZI_Z (this _JniMarshal_PPLZI_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLZIII_Z (this _JniMarshal_PPLZIII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static bool Wrap_JniMarshal_PPLZIIII_Z (this _JniMarshal_PPLZIIII_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, int p2, int p3, int p4, int p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZIIL_V (this _JniMarshal_PPLZIIL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZILL_V (this _JniMarshal_PPLZILL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, int p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZJ_L (this _JniMarshal_PPLZJ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZJ_V (this _JniMarshal_PPLZJ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, long p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZJL_L (this _JniMarshal_PPLZJL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZJL_V (this _JniMarshal_PPLZJL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, long p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZJLL_L (this _JniMarshal_PPLZJLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, long p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZL_L (this _JniMarshal_PPLZL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZL_V (this _JniMarshal_PPLZL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLZL_Z (this _JniMarshal_PPLZL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZLL_V (this _JniMarshal_PPLZLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLZLL_Z (this _JniMarshal_PPLZLL_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZLLL_V (this _JniMarshal_PPLZLLL_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZLLLLLLLL_L (this _JniMarshal_PPLZLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZLLLLLLLLL_L (this _JniMarshal_PPLZLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9, IntPtr p10)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZLLZ_V (this _JniMarshal_PPLZLLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2, IntPtr p3, bool p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZLZ_L (this _JniMarshal_PPLZLZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZLZ_V (this _JniMarshal_PPLZLZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, IntPtr p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZS_V (this _JniMarshal_PPLZS_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, short p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZZ_L (this _JniMarshal_PPLZZ_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPLZZ_V (this _JniMarshal_PPLZZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPLZZ_Z (this _JniMarshal_PPLZZ_Z callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPLZZI_L (this _JniMarshal_PPLZZI_L callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, bool p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static void Wrap_JniMarshal_PPLZZZ_V (this _JniMarshal_PPLZZZ_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPLZZZZZZZII_V (this _JniMarshal_PPLZZZZZZZII_V callback, IntPtr jnienv, IntPtr klazz, IntPtr p0, bool p1, bool p2, bool p3, bool p4, bool p5, bool p6, bool p7, int p8, int p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPS_I (this _JniMarshal_PPS_I callback, IntPtr jnienv, IntPtr klazz, short p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPS_L (this _JniMarshal_PPS_L callback, IntPtr jnienv, IntPtr klazz, short p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static short Wrap_JniMarshal_PPS_S (this _JniMarshal_PPS_S callback, IntPtr jnienv, IntPtr klazz, short p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPS_V (this _JniMarshal_PPS_V callback, IntPtr jnienv, IntPtr klazz, short p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPSL_L (this _JniMarshal_PPSL_L callback, IntPtr jnienv, IntPtr klazz, short p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPSLLLL_V (this _JniMarshal_PPSLLLL_V callback, IntPtr jnienv, IntPtr klazz, short p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPSS_V (this _JniMarshal_PPSS_V callback, IntPtr jnienv, IntPtr klazz, short p0, short p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPSSSSS_V (this _JniMarshal_PPSSSSS_V callback, IntPtr jnienv, IntPtr klazz, short p0, short p1, short p2, short p3, short p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static int Wrap_JniMarshal_PPZ_I (this _JniMarshal_PPZ_I callback, IntPtr jnienv, IntPtr klazz, bool p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static long Wrap_JniMarshal_PPZ_J (this _JniMarshal_PPZ_J callback, IntPtr jnienv, IntPtr klazz, bool p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZ_L (this _JniMarshal_PPZ_L callback, IntPtr jnienv, IntPtr klazz, bool p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZ_V (this _JniMarshal_PPZ_V callback, IntPtr jnienv, IntPtr klazz, bool p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPZ_Z (this _JniMarshal_PPZ_Z callback, IntPtr jnienv, IntPtr klazz, bool p0)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZC_V (this _JniMarshal_PPZC_V callback, IntPtr jnienv, IntPtr klazz, bool p0, char p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static double Wrap_JniMarshal_PPZDZD_D (this _JniMarshal_PPZDZD_D callback, IntPtr jnienv, IntPtr klazz, bool p0, double p1, bool p2, double p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static float Wrap_JniMarshal_PPZFZF_F (this _JniMarshal_PPZFZF_F callback, IntPtr jnienv, IntPtr klazz, bool p0, float p1, bool p2, float p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZI_L (this _JniMarshal_PPZI_L callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZI_V (this _JniMarshal_PPZI_V callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPZI_Z (this _JniMarshal_PPZI_Z callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZII_L (this _JniMarshal_PPZII_L callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZII_V (this _JniMarshal_PPZII_V callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZIIII_L (this _JniMarshal_PPZIIII_L callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, int p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
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

		internal static IntPtr Wrap_JniMarshal_PPZIIIIII_L (this _JniMarshal_PPZIIIIII_L callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, int p2, int p3, int p4, int p5, int p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZIL_L (this _JniMarshal_PPZIL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZIL_V (this _JniMarshal_PPZIL_V callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPZILI_V (this _JniMarshal_PPZILI_V callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, IntPtr p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPZILII_V (this _JniMarshal_PPZILII_V callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, IntPtr p2, int p3, int p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZIZ_L (this _JniMarshal_PPZIZ_L callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static int Wrap_JniMarshal_PPZIZI_I (this _JniMarshal_PPZIZI_I callback, IntPtr jnienv, IntPtr klazz, bool p0, int p1, bool p2, int p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZJ_V (this _JniMarshal_PPZJ_V callback, IntPtr jnienv, IntPtr klazz, bool p0, long p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static long Wrap_JniMarshal_PPZJZJ_J (this _JniMarshal_PPZJZJ_J callback, IntPtr jnienv, IntPtr klazz, bool p0, long p1, bool p2, long p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZL_L (this _JniMarshal_PPZL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZL_V (this _JniMarshal_PPZL_V callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPZL_Z (this _JniMarshal_PPZL_Z callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZLI_V (this _JniMarshal_PPZLI_V callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPZLI_Z (this _JniMarshal_PPZLI_Z callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, int p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZLL_L (this _JniMarshal_PPZLL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZLL_V (this _JniMarshal_PPZLL_V callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPZLL_Z (this _JniMarshal_PPZLL_Z callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZLLL_V (this _JniMarshal_PPZLLL_V callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZLLLL_L (this _JniMarshal_PPZLLLL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZLLLL_V (this _JniMarshal_PPZLLLL_V callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZLLLLLL_L (this _JniMarshal_PPZLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZLLLLLLLL_L (this _JniMarshal_PPZLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZLLLLLLLLL_L (this _JniMarshal_PPZLLLLLLLLL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, IntPtr p2, IntPtr p3, IntPtr p4, IntPtr p5, IntPtr p6, IntPtr p7, IntPtr p8, IntPtr p9)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8, p9);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZLZL_L (this _JniMarshal_PPZLZL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, IntPtr p1, bool p2, IntPtr p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZZ_V (this _JniMarshal_PPZZ_V callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPZZ_Z (this _JniMarshal_PPZZ_Z callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZZIIL_V (this _JniMarshal_PPZZIIL_V callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1, int p2, int p3, IntPtr p4)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPZZIILL_V (this _JniMarshal_PPZZIILL_V callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1, int p2, int p3, IntPtr p4, IntPtr p5)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3, p4, p5);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZZL_L (this _JniMarshal_PPZZL_L callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZZL_V (this _JniMarshal_PPZZL_V callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1, IntPtr p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static void Wrap_JniMarshal_PPZZZ_V (this _JniMarshal_PPZZZ_V callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1, bool p2)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static IntPtr Wrap_JniMarshal_PPZZZZ_L (this _JniMarshal_PPZZZZ_L callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		internal static void Wrap_JniMarshal_PPZZZZ_V (this _JniMarshal_PPZZZZ_V callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);

			}
		}

		internal static bool Wrap_JniMarshal_PPZZZZ_Z (this _JniMarshal_PPZZZZ_Z callback, IntPtr jnienv, IntPtr klazz, bool p0, bool p1, bool p2, bool p3)
		{
			AndroidRuntimeInternal.WaitForBridgeProcessing ();
			try {
				return callback (jnienv, klazz, p0, p1, p2, p3);
			} catch (Exception e) when (_unhandled_exception (e)) {
				AndroidEnvironment.UnhandledException (e);
				return default;
			}
		}

		private static Delegate CreateBuiltInDelegate (Delegate dlg, Type delegateType)
		{
			switch (delegateType.Name) {
				case nameof (_JniMarshal_PP_B):
					return new _JniMarshal_PP_B (Unsafe.As<_JniMarshal_PP_B> (dlg).Wrap_JniMarshal_PP_B);
				case nameof (_JniMarshal_PP_C):
					return new _JniMarshal_PP_C (Unsafe.As<_JniMarshal_PP_C> (dlg).Wrap_JniMarshal_PP_C);
				case nameof (_JniMarshal_PP_D):
					return new _JniMarshal_PP_D (Unsafe.As<_JniMarshal_PP_D> (dlg).Wrap_JniMarshal_PP_D);
				case nameof (_JniMarshal_PP_F):
					return new _JniMarshal_PP_F (Unsafe.As<_JniMarshal_PP_F> (dlg).Wrap_JniMarshal_PP_F);
				case nameof (_JniMarshal_PP_I):
					return new _JniMarshal_PP_I (Unsafe.As<_JniMarshal_PP_I> (dlg).Wrap_JniMarshal_PP_I);
				case nameof (_JniMarshal_PP_J):
					return new _JniMarshal_PP_J (Unsafe.As<_JniMarshal_PP_J> (dlg).Wrap_JniMarshal_PP_J);
				case nameof (_JniMarshal_PP_L):
					return new _JniMarshal_PP_L (Unsafe.As<_JniMarshal_PP_L> (dlg).Wrap_JniMarshal_PP_L);
				case nameof (_JniMarshal_PP_S):
					return new _JniMarshal_PP_S (Unsafe.As<_JniMarshal_PP_S> (dlg).Wrap_JniMarshal_PP_S);
				case nameof (_JniMarshal_PP_V):
					return new _JniMarshal_PP_V (Unsafe.As<_JniMarshal_PP_V> (dlg).Wrap_JniMarshal_PP_V);
				case nameof (_JniMarshal_PP_Z):
					return new _JniMarshal_PP_Z (Unsafe.As<_JniMarshal_PP_Z> (dlg).Wrap_JniMarshal_PP_Z);
				case nameof (_JniMarshal_PPB_J):
					return new _JniMarshal_PPB_J (Unsafe.As<_JniMarshal_PPB_J> (dlg).Wrap_JniMarshal_PPB_J);
				case nameof (_JniMarshal_PPB_L):
					return new _JniMarshal_PPB_L (Unsafe.As<_JniMarshal_PPB_L> (dlg).Wrap_JniMarshal_PPB_L);
				case nameof (_JniMarshal_PPB_V):
					return new _JniMarshal_PPB_V (Unsafe.As<_JniMarshal_PPB_V> (dlg).Wrap_JniMarshal_PPB_V);
				case nameof (_JniMarshal_PPBBBB_V):
					return new _JniMarshal_PPBBBB_V (Unsafe.As<_JniMarshal_PPBBBB_V> (dlg).Wrap_JniMarshal_PPBBBB_V);
				case nameof (_JniMarshal_PPBI_B):
					return new _JniMarshal_PPBI_B (Unsafe.As<_JniMarshal_PPBI_B> (dlg).Wrap_JniMarshal_PPBI_B);
				case nameof (_JniMarshal_PPBI_V):
					return new _JniMarshal_PPBI_V (Unsafe.As<_JniMarshal_PPBI_V> (dlg).Wrap_JniMarshal_PPBI_V);
				case nameof (_JniMarshal_PPBJ_J):
					return new _JniMarshal_PPBJ_J (Unsafe.As<_JniMarshal_PPBJ_J> (dlg).Wrap_JniMarshal_PPBJ_J);
				case nameof (_JniMarshal_PPBJJ_J):
					return new _JniMarshal_PPBJJ_J (Unsafe.As<_JniMarshal_PPBJJ_J> (dlg).Wrap_JniMarshal_PPBJJ_J);
				case nameof (_JniMarshal_PPC_C):
					return new _JniMarshal_PPC_C (Unsafe.As<_JniMarshal_PPC_C> (dlg).Wrap_JniMarshal_PPC_C);
				case nameof (_JniMarshal_PPC_I):
					return new _JniMarshal_PPC_I (Unsafe.As<_JniMarshal_PPC_I> (dlg).Wrap_JniMarshal_PPC_I);
				case nameof (_JniMarshal_PPC_L):
					return new _JniMarshal_PPC_L (Unsafe.As<_JniMarshal_PPC_L> (dlg).Wrap_JniMarshal_PPC_L);
				case nameof (_JniMarshal_PPC_V):
					return new _JniMarshal_PPC_V (Unsafe.As<_JniMarshal_PPC_V> (dlg).Wrap_JniMarshal_PPC_V);
				case nameof (_JniMarshal_PPC_Z):
					return new _JniMarshal_PPC_Z (Unsafe.As<_JniMarshal_PPC_Z> (dlg).Wrap_JniMarshal_PPC_Z);
				case nameof (_JniMarshal_PPCC_L):
					return new _JniMarshal_PPCC_L (Unsafe.As<_JniMarshal_PPCC_L> (dlg).Wrap_JniMarshal_PPCC_L);
				case nameof (_JniMarshal_PPCCII_L):
					return new _JniMarshal_PPCCII_L (Unsafe.As<_JniMarshal_PPCCII_L> (dlg).Wrap_JniMarshal_PPCCII_L);
				case nameof (_JniMarshal_PPCI_L):
					return new _JniMarshal_PPCI_L (Unsafe.As<_JniMarshal_PPCI_L> (dlg).Wrap_JniMarshal_PPCI_L);
				case nameof (_JniMarshal_PPCIILLL_L):
					return new _JniMarshal_PPCIILLL_L (Unsafe.As<_JniMarshal_PPCIILLL_L> (dlg).Wrap_JniMarshal_PPCIILLL_L);
				case nameof (_JniMarshal_PPCJ_Z):
					return new _JniMarshal_PPCJ_Z (Unsafe.As<_JniMarshal_PPCJ_Z> (dlg).Wrap_JniMarshal_PPCJ_Z);
				case nameof (_JniMarshal_PPD_D):
					return new _JniMarshal_PPD_D (Unsafe.As<_JniMarshal_PPD_D> (dlg).Wrap_JniMarshal_PPD_D);
				case nameof (_JniMarshal_PPD_I):
					return new _JniMarshal_PPD_I (Unsafe.As<_JniMarshal_PPD_I> (dlg).Wrap_JniMarshal_PPD_I);
				case nameof (_JniMarshal_PPD_J):
					return new _JniMarshal_PPD_J (Unsafe.As<_JniMarshal_PPD_J> (dlg).Wrap_JniMarshal_PPD_J);
				case nameof (_JniMarshal_PPD_L):
					return new _JniMarshal_PPD_L (Unsafe.As<_JniMarshal_PPD_L> (dlg).Wrap_JniMarshal_PPD_L);
				case nameof (_JniMarshal_PPD_V):
					return new _JniMarshal_PPD_V (Unsafe.As<_JniMarshal_PPD_V> (dlg).Wrap_JniMarshal_PPD_V);
				case nameof (_JniMarshal_PPD_Z):
					return new _JniMarshal_PPD_Z (Unsafe.As<_JniMarshal_PPD_Z> (dlg).Wrap_JniMarshal_PPD_Z);
				case nameof (_JniMarshal_PPDD_D):
					return new _JniMarshal_PPDD_D (Unsafe.As<_JniMarshal_PPDD_D> (dlg).Wrap_JniMarshal_PPDD_D);
				case nameof (_JniMarshal_PPDD_L):
					return new _JniMarshal_PPDD_L (Unsafe.As<_JniMarshal_PPDD_L> (dlg).Wrap_JniMarshal_PPDD_L);
				case nameof (_JniMarshal_PPDD_V):
					return new _JniMarshal_PPDD_V (Unsafe.As<_JniMarshal_PPDD_V> (dlg).Wrap_JniMarshal_PPDD_V);
				case nameof (_JniMarshal_PPDDD_D):
					return new _JniMarshal_PPDDD_D (Unsafe.As<_JniMarshal_PPDDD_D> (dlg).Wrap_JniMarshal_PPDDD_D);
				case nameof (_JniMarshal_PPDDFJL_V):
					return new _JniMarshal_PPDDFJL_V (Unsafe.As<_JniMarshal_PPDDFJL_V> (dlg).Wrap_JniMarshal_PPDDFJL_V);
				case nameof (_JniMarshal_PPDF_V):
					return new _JniMarshal_PPDF_V (Unsafe.As<_JniMarshal_PPDF_V> (dlg).Wrap_JniMarshal_PPDF_V);
				case nameof (_JniMarshal_PPDI_D):
					return new _JniMarshal_PPDI_D (Unsafe.As<_JniMarshal_PPDI_D> (dlg).Wrap_JniMarshal_PPDI_D);
				case nameof (_JniMarshal_PPDI_V):
					return new _JniMarshal_PPDI_V (Unsafe.As<_JniMarshal_PPDI_V> (dlg).Wrap_JniMarshal_PPDI_V);
				case nameof (_JniMarshal_PPDII_L):
					return new _JniMarshal_PPDII_L (Unsafe.As<_JniMarshal_PPDII_L> (dlg).Wrap_JniMarshal_PPDII_L);
				case nameof (_JniMarshal_PPDII_Z):
					return new _JniMarshal_PPDII_Z (Unsafe.As<_JniMarshal_PPDII_Z> (dlg).Wrap_JniMarshal_PPDII_Z);
				case nameof (_JniMarshal_PPDL_L):
					return new _JniMarshal_PPDL_L (Unsafe.As<_JniMarshal_PPDL_L> (dlg).Wrap_JniMarshal_PPDL_L);
				case nameof (_JniMarshal_PPDL_V):
					return new _JniMarshal_PPDL_V (Unsafe.As<_JniMarshal_PPDL_V> (dlg).Wrap_JniMarshal_PPDL_V);
				case nameof (_JniMarshal_PPDLL_L):
					return new _JniMarshal_PPDLL_L (Unsafe.As<_JniMarshal_PPDLL_L> (dlg).Wrap_JniMarshal_PPDLL_L);
				case nameof (_JniMarshal_PPDLL_V):
					return new _JniMarshal_PPDLL_V (Unsafe.As<_JniMarshal_PPDLL_V> (dlg).Wrap_JniMarshal_PPDLL_V);
				case nameof (_JniMarshal_PPF_F):
					return new _JniMarshal_PPF_F (Unsafe.As<_JniMarshal_PPF_F> (dlg).Wrap_JniMarshal_PPF_F);
				case nameof (_JniMarshal_PPF_I):
					return new _JniMarshal_PPF_I (Unsafe.As<_JniMarshal_PPF_I> (dlg).Wrap_JniMarshal_PPF_I);
				case nameof (_JniMarshal_PPF_L):
					return new _JniMarshal_PPF_L (Unsafe.As<_JniMarshal_PPF_L> (dlg).Wrap_JniMarshal_PPF_L);
				case nameof (_JniMarshal_PPF_V):
					return new _JniMarshal_PPF_V (Unsafe.As<_JniMarshal_PPF_V> (dlg).Wrap_JniMarshal_PPF_V);
				case nameof (_JniMarshal_PPF_Z):
					return new _JniMarshal_PPF_Z (Unsafe.As<_JniMarshal_PPF_Z> (dlg).Wrap_JniMarshal_PPF_Z);
				case nameof (_JniMarshal_PPFF_F):
					return new _JniMarshal_PPFF_F (Unsafe.As<_JniMarshal_PPFF_F> (dlg).Wrap_JniMarshal_PPFF_F);
				case nameof (_JniMarshal_PPFF_I):
					return new _JniMarshal_PPFF_I (Unsafe.As<_JniMarshal_PPFF_I> (dlg).Wrap_JniMarshal_PPFF_I);
				case nameof (_JniMarshal_PPFF_L):
					return new _JniMarshal_PPFF_L (Unsafe.As<_JniMarshal_PPFF_L> (dlg).Wrap_JniMarshal_PPFF_L);
				case nameof (_JniMarshal_PPFF_V):
					return new _JniMarshal_PPFF_V (Unsafe.As<_JniMarshal_PPFF_V> (dlg).Wrap_JniMarshal_PPFF_V);
				case nameof (_JniMarshal_PPFF_Z):
					return new _JniMarshal_PPFF_Z (Unsafe.As<_JniMarshal_PPFF_Z> (dlg).Wrap_JniMarshal_PPFF_Z);
				case nameof (_JniMarshal_PPFFF_F):
					return new _JniMarshal_PPFFF_F (Unsafe.As<_JniMarshal_PPFFF_F> (dlg).Wrap_JniMarshal_PPFFF_F);
				case nameof (_JniMarshal_PPFFF_L):
					return new _JniMarshal_PPFFF_L (Unsafe.As<_JniMarshal_PPFFF_L> (dlg).Wrap_JniMarshal_PPFFF_L);
				case nameof (_JniMarshal_PPFFF_V):
					return new _JniMarshal_PPFFF_V (Unsafe.As<_JniMarshal_PPFFF_V> (dlg).Wrap_JniMarshal_PPFFF_V);
				case nameof (_JniMarshal_PPFFF_Z):
					return new _JniMarshal_PPFFF_Z (Unsafe.As<_JniMarshal_PPFFF_Z> (dlg).Wrap_JniMarshal_PPFFF_Z);
				case nameof (_JniMarshal_PPFFFF_L):
					return new _JniMarshal_PPFFFF_L (Unsafe.As<_JniMarshal_PPFFFF_L> (dlg).Wrap_JniMarshal_PPFFFF_L);
				case nameof (_JniMarshal_PPFFFF_V):
					return new _JniMarshal_PPFFFF_V (Unsafe.As<_JniMarshal_PPFFFF_V> (dlg).Wrap_JniMarshal_PPFFFF_V);
				case nameof (_JniMarshal_PPFFFF_Z):
					return new _JniMarshal_PPFFFF_Z (Unsafe.As<_JniMarshal_PPFFFF_Z> (dlg).Wrap_JniMarshal_PPFFFF_Z);
				case nameof (_JniMarshal_PPFFFFF_V):
					return new _JniMarshal_PPFFFFF_V (Unsafe.As<_JniMarshal_PPFFFFF_V> (dlg).Wrap_JniMarshal_PPFFFFF_V);
				case nameof (_JniMarshal_PPFFFFFF_V):
					return new _JniMarshal_PPFFFFFF_V (Unsafe.As<_JniMarshal_PPFFFFFF_V> (dlg).Wrap_JniMarshal_PPFFFFFF_V);
				case nameof (_JniMarshal_PPFFFFFFFI_V):
					return new _JniMarshal_PPFFFFFFFI_V (Unsafe.As<_JniMarshal_PPFFFFFFFI_V> (dlg).Wrap_JniMarshal_PPFFFFFFFI_V);
				case nameof (_JniMarshal_PPFFFFFFL_V):
					return new _JniMarshal_PPFFFFFFL_V (Unsafe.As<_JniMarshal_PPFFFFFFL_V> (dlg).Wrap_JniMarshal_PPFFFFFFL_V);
				case nameof (_JniMarshal_PPFFFFFFZ_V):
					return new _JniMarshal_PPFFFFFFZ_V (Unsafe.As<_JniMarshal_PPFFFFFFZ_V> (dlg).Wrap_JniMarshal_PPFFFFFFZ_V);
				case nameof (_JniMarshal_PPFFFFFFZL_V):
					return new _JniMarshal_PPFFFFFFZL_V (Unsafe.As<_JniMarshal_PPFFFFFFZL_V> (dlg).Wrap_JniMarshal_PPFFFFFFZL_V);
				case nameof (_JniMarshal_PPFFFFI_I):
					return new _JniMarshal_PPFFFFI_I (Unsafe.As<_JniMarshal_PPFFFFI_I> (dlg).Wrap_JniMarshal_PPFFFFI_I);
				case nameof (_JniMarshal_PPFFFFII_I):
					return new _JniMarshal_PPFFFFII_I (Unsafe.As<_JniMarshal_PPFFFFII_I> (dlg).Wrap_JniMarshal_PPFFFFII_I);
				case nameof (_JniMarshal_PPFFFFII_V):
					return new _JniMarshal_PPFFFFII_V (Unsafe.As<_JniMarshal_PPFFFFII_V> (dlg).Wrap_JniMarshal_PPFFFFII_V);
				case nameof (_JniMarshal_PPFFFFL_I):
					return new _JniMarshal_PPFFFFL_I (Unsafe.As<_JniMarshal_PPFFFFL_I> (dlg).Wrap_JniMarshal_PPFFFFL_I);
				case nameof (_JniMarshal_PPFFFFL_V):
					return new _JniMarshal_PPFFFFL_V (Unsafe.As<_JniMarshal_PPFFFFL_V> (dlg).Wrap_JniMarshal_PPFFFFL_V);
				case nameof (_JniMarshal_PPFFFFL_Z):
					return new _JniMarshal_PPFFFFL_Z (Unsafe.As<_JniMarshal_PPFFFFL_Z> (dlg).Wrap_JniMarshal_PPFFFFL_Z);
				case nameof (_JniMarshal_PPFFFFLI_I):
					return new _JniMarshal_PPFFFFLI_I (Unsafe.As<_JniMarshal_PPFFFFLI_I> (dlg).Wrap_JniMarshal_PPFFFFLI_I);
				case nameof (_JniMarshal_PPFFFFLL_V):
					return new _JniMarshal_PPFFFFLL_V (Unsafe.As<_JniMarshal_PPFFFFLL_V> (dlg).Wrap_JniMarshal_PPFFFFLL_V);
				case nameof (_JniMarshal_PPFFFI_V):
					return new _JniMarshal_PPFFFI_V (Unsafe.As<_JniMarshal_PPFFFI_V> (dlg).Wrap_JniMarshal_PPFFFI_V);
				case nameof (_JniMarshal_PPFFFJ_V):
					return new _JniMarshal_PPFFFJ_V (Unsafe.As<_JniMarshal_PPFFFJ_V> (dlg).Wrap_JniMarshal_PPFFFJ_V);
				case nameof (_JniMarshal_PPFFFL_V):
					return new _JniMarshal_PPFFFL_V (Unsafe.As<_JniMarshal_PPFFFL_V> (dlg).Wrap_JniMarshal_PPFFFL_V);
				case nameof (_JniMarshal_PPFFFLILILILI_L):
					return new _JniMarshal_PPFFFLILILILI_L (Unsafe.As<_JniMarshal_PPFFFLILILILI_L> (dlg).Wrap_JniMarshal_PPFFFLILILILI_L);
				case nameof (_JniMarshal_PPFFFLLLL_L):
					return new _JniMarshal_PPFFFLLLL_L (Unsafe.As<_JniMarshal_PPFFFLLLL_L> (dlg).Wrap_JniMarshal_PPFFFLLLL_L);
				case nameof (_JniMarshal_PPFFI_L):
					return new _JniMarshal_PPFFI_L (Unsafe.As<_JniMarshal_PPFFI_L> (dlg).Wrap_JniMarshal_PPFFI_L);
				case nameof (_JniMarshal_PPFFIIL_V):
					return new _JniMarshal_PPFFIIL_V (Unsafe.As<_JniMarshal_PPFFIIL_V> (dlg).Wrap_JniMarshal_PPFFIIL_V);
				case nameof (_JniMarshal_PPFFL_V):
					return new _JniMarshal_PPFFL_V (Unsafe.As<_JniMarshal_PPFFL_V> (dlg).Wrap_JniMarshal_PPFFL_V);
				case nameof (_JniMarshal_PPFFLZ_Z):
					return new _JniMarshal_PPFFLZ_Z (Unsafe.As<_JniMarshal_PPFFLZ_Z> (dlg).Wrap_JniMarshal_PPFFLZ_Z);
				case nameof (_JniMarshal_PPFFZ_V):
					return new _JniMarshal_PPFFZ_V (Unsafe.As<_JniMarshal_PPFFZ_V> (dlg).Wrap_JniMarshal_PPFFZ_V);
				case nameof (_JniMarshal_PPFFZ_Z):
					return new _JniMarshal_PPFFZ_Z (Unsafe.As<_JniMarshal_PPFFZ_Z> (dlg).Wrap_JniMarshal_PPFFZ_Z);
				case nameof (_JniMarshal_PPFI_F):
					return new _JniMarshal_PPFI_F (Unsafe.As<_JniMarshal_PPFI_F> (dlg).Wrap_JniMarshal_PPFI_F);
				case nameof (_JniMarshal_PPFI_V):
					return new _JniMarshal_PPFI_V (Unsafe.As<_JniMarshal_PPFI_V> (dlg).Wrap_JniMarshal_PPFI_V);
				case nameof (_JniMarshal_PPFII_V):
					return new _JniMarshal_PPFII_V (Unsafe.As<_JniMarshal_PPFII_V> (dlg).Wrap_JniMarshal_PPFII_V);
				case nameof (_JniMarshal_PPFIII_V):
					return new _JniMarshal_PPFIII_V (Unsafe.As<_JniMarshal_PPFIII_V> (dlg).Wrap_JniMarshal_PPFIII_V);
				case nameof (_JniMarshal_PPFJLL_F):
					return new _JniMarshal_PPFJLL_F (Unsafe.As<_JniMarshal_PPFJLL_F> (dlg).Wrap_JniMarshal_PPFJLL_F);
				case nameof (_JniMarshal_PPFL_I):
					return new _JniMarshal_PPFL_I (Unsafe.As<_JniMarshal_PPFL_I> (dlg).Wrap_JniMarshal_PPFL_I);
				case nameof (_JniMarshal_PPFL_L):
					return new _JniMarshal_PPFL_L (Unsafe.As<_JniMarshal_PPFL_L> (dlg).Wrap_JniMarshal_PPFL_L);
				case nameof (_JniMarshal_PPFL_V):
					return new _JniMarshal_PPFL_V (Unsafe.As<_JniMarshal_PPFL_V> (dlg).Wrap_JniMarshal_PPFL_V);
				case nameof (_JniMarshal_PPFLI_V):
					return new _JniMarshal_PPFLI_V (Unsafe.As<_JniMarshal_PPFLI_V> (dlg).Wrap_JniMarshal_PPFLI_V);
				case nameof (_JniMarshal_PPFLI_Z):
					return new _JniMarshal_PPFLI_Z (Unsafe.As<_JniMarshal_PPFLI_Z> (dlg).Wrap_JniMarshal_PPFLI_Z);
				case nameof (_JniMarshal_PPFLL_L):
					return new _JniMarshal_PPFLL_L (Unsafe.As<_JniMarshal_PPFLL_L> (dlg).Wrap_JniMarshal_PPFLL_L);
				case nameof (_JniMarshal_PPFLL_Z):
					return new _JniMarshal_PPFLL_Z (Unsafe.As<_JniMarshal_PPFLL_Z> (dlg).Wrap_JniMarshal_PPFLL_Z);
				case nameof (_JniMarshal_PPFZ_V):
					return new _JniMarshal_PPFZ_V (Unsafe.As<_JniMarshal_PPFZ_V> (dlg).Wrap_JniMarshal_PPFZ_V);
				case nameof (_JniMarshal_PPFZFF_V):
					return new _JniMarshal_PPFZFF_V (Unsafe.As<_JniMarshal_PPFZFF_V> (dlg).Wrap_JniMarshal_PPFZFF_V);
				case nameof (_JniMarshal_PPFZI_V):
					return new _JniMarshal_PPFZI_V (Unsafe.As<_JniMarshal_PPFZI_V> (dlg).Wrap_JniMarshal_PPFZI_V);
				case nameof (_JniMarshal_PPI_B):
					return new _JniMarshal_PPI_B (Unsafe.As<_JniMarshal_PPI_B> (dlg).Wrap_JniMarshal_PPI_B);
				case nameof (_JniMarshal_PPI_C):
					return new _JniMarshal_PPI_C (Unsafe.As<_JniMarshal_PPI_C> (dlg).Wrap_JniMarshal_PPI_C);
				case nameof (_JniMarshal_PPI_D):
					return new _JniMarshal_PPI_D (Unsafe.As<_JniMarshal_PPI_D> (dlg).Wrap_JniMarshal_PPI_D);
				case nameof (_JniMarshal_PPI_F):
					return new _JniMarshal_PPI_F (Unsafe.As<_JniMarshal_PPI_F> (dlg).Wrap_JniMarshal_PPI_F);
				case nameof (_JniMarshal_PPI_I):
					return new _JniMarshal_PPI_I (Unsafe.As<_JniMarshal_PPI_I> (dlg).Wrap_JniMarshal_PPI_I);
				case nameof (_JniMarshal_PPI_J):
					return new _JniMarshal_PPI_J (Unsafe.As<_JniMarshal_PPI_J> (dlg).Wrap_JniMarshal_PPI_J);
				case nameof (_JniMarshal_PPI_L):
					return new _JniMarshal_PPI_L (Unsafe.As<_JniMarshal_PPI_L> (dlg).Wrap_JniMarshal_PPI_L);
				case nameof (_JniMarshal_PPI_S):
					return new _JniMarshal_PPI_S (Unsafe.As<_JniMarshal_PPI_S> (dlg).Wrap_JniMarshal_PPI_S);
				case nameof (_JniMarshal_PPI_V):
					return new _JniMarshal_PPI_V (Unsafe.As<_JniMarshal_PPI_V> (dlg).Wrap_JniMarshal_PPI_V);
				case nameof (_JniMarshal_PPI_Z):
					return new _JniMarshal_PPI_Z (Unsafe.As<_JniMarshal_PPI_Z> (dlg).Wrap_JniMarshal_PPI_Z);
				case nameof (_JniMarshal_PPIB_L):
					return new _JniMarshal_PPIB_L (Unsafe.As<_JniMarshal_PPIB_L> (dlg).Wrap_JniMarshal_PPIB_L);
				case nameof (_JniMarshal_PPIB_V):
					return new _JniMarshal_PPIB_V (Unsafe.As<_JniMarshal_PPIB_V> (dlg).Wrap_JniMarshal_PPIB_V);
				case nameof (_JniMarshal_PPIBI_V):
					return new _JniMarshal_PPIBI_V (Unsafe.As<_JniMarshal_PPIBI_V> (dlg).Wrap_JniMarshal_PPIBI_V);
				case nameof (_JniMarshal_PPIC_L):
					return new _JniMarshal_PPIC_L (Unsafe.As<_JniMarshal_PPIC_L> (dlg).Wrap_JniMarshal_PPIC_L);
				case nameof (_JniMarshal_PPIC_V):
					return new _JniMarshal_PPIC_V (Unsafe.As<_JniMarshal_PPIC_V> (dlg).Wrap_JniMarshal_PPIC_V);
				case nameof (_JniMarshal_PPID_D):
					return new _JniMarshal_PPID_D (Unsafe.As<_JniMarshal_PPID_D> (dlg).Wrap_JniMarshal_PPID_D);
				case nameof (_JniMarshal_PPID_L):
					return new _JniMarshal_PPID_L (Unsafe.As<_JniMarshal_PPID_L> (dlg).Wrap_JniMarshal_PPID_L);
				case nameof (_JniMarshal_PPID_V):
					return new _JniMarshal_PPID_V (Unsafe.As<_JniMarshal_PPID_V> (dlg).Wrap_JniMarshal_PPID_V);
				case nameof (_JniMarshal_PPID_Z):
					return new _JniMarshal_PPID_Z (Unsafe.As<_JniMarshal_PPID_Z> (dlg).Wrap_JniMarshal_PPID_Z);
				case nameof (_JniMarshal_PPIDD_V):
					return new _JniMarshal_PPIDD_V (Unsafe.As<_JniMarshal_PPIDD_V> (dlg).Wrap_JniMarshal_PPIDD_V);
				case nameof (_JniMarshal_PPIF_F):
					return new _JniMarshal_PPIF_F (Unsafe.As<_JniMarshal_PPIF_F> (dlg).Wrap_JniMarshal_PPIF_F);
				case nameof (_JniMarshal_PPIF_I):
					return new _JniMarshal_PPIF_I (Unsafe.As<_JniMarshal_PPIF_I> (dlg).Wrap_JniMarshal_PPIF_I);
				case nameof (_JniMarshal_PPIF_L):
					return new _JniMarshal_PPIF_L (Unsafe.As<_JniMarshal_PPIF_L> (dlg).Wrap_JniMarshal_PPIF_L);
				case nameof (_JniMarshal_PPIF_V):
					return new _JniMarshal_PPIF_V (Unsafe.As<_JniMarshal_PPIF_V> (dlg).Wrap_JniMarshal_PPIF_V);
				case nameof (_JniMarshal_PPIF_Z):
					return new _JniMarshal_PPIF_Z (Unsafe.As<_JniMarshal_PPIF_Z> (dlg).Wrap_JniMarshal_PPIF_Z);
				case nameof (_JniMarshal_PPIFD_V):
					return new _JniMarshal_PPIFD_V (Unsafe.As<_JniMarshal_PPIFD_V> (dlg).Wrap_JniMarshal_PPIFD_V);
				case nameof (_JniMarshal_PPIFF_V):
					return new _JniMarshal_PPIFF_V (Unsafe.As<_JniMarshal_PPIFF_V> (dlg).Wrap_JniMarshal_PPIFF_V);
				case nameof (_JniMarshal_PPIFF_Z):
					return new _JniMarshal_PPIFF_Z (Unsafe.As<_JniMarshal_PPIFF_Z> (dlg).Wrap_JniMarshal_PPIFF_Z);
				case nameof (_JniMarshal_PPIFFFF_V):
					return new _JniMarshal_PPIFFFF_V (Unsafe.As<_JniMarshal_PPIFFFF_V> (dlg).Wrap_JniMarshal_PPIFFFF_V);
				case nameof (_JniMarshal_PPIFFIF_V):
					return new _JniMarshal_PPIFFIF_V (Unsafe.As<_JniMarshal_PPIFFIF_V> (dlg).Wrap_JniMarshal_PPIFFIF_V);
				case nameof (_JniMarshal_PPIFFL_L):
					return new _JniMarshal_PPIFFL_L (Unsafe.As<_JniMarshal_PPIFFL_L> (dlg).Wrap_JniMarshal_PPIFFL_L);
				case nameof (_JniMarshal_PPIFI_V):
					return new _JniMarshal_PPIFI_V (Unsafe.As<_JniMarshal_PPIFI_V> (dlg).Wrap_JniMarshal_PPIFI_V);
				case nameof (_JniMarshal_PPIFII_F):
					return new _JniMarshal_PPIFII_F (Unsafe.As<_JniMarshal_PPIFII_F> (dlg).Wrap_JniMarshal_PPIFII_F);
				case nameof (_JniMarshal_PPIFL_I):
					return new _JniMarshal_PPIFL_I (Unsafe.As<_JniMarshal_PPIFL_I> (dlg).Wrap_JniMarshal_PPIFL_I);
				case nameof (_JniMarshal_PPIFZ_V):
					return new _JniMarshal_PPIFZ_V (Unsafe.As<_JniMarshal_PPIFZ_V> (dlg).Wrap_JniMarshal_PPIFZ_V);
				case nameof (_JniMarshal_PPIFZZ_V):
					return new _JniMarshal_PPIFZZ_V (Unsafe.As<_JniMarshal_PPIFZZ_V> (dlg).Wrap_JniMarshal_PPIFZZ_V);
				case nameof (_JniMarshal_PPII_D):
					return new _JniMarshal_PPII_D (Unsafe.As<_JniMarshal_PPII_D> (dlg).Wrap_JniMarshal_PPII_D);
				case nameof (_JniMarshal_PPII_F):
					return new _JniMarshal_PPII_F (Unsafe.As<_JniMarshal_PPII_F> (dlg).Wrap_JniMarshal_PPII_F);
				case nameof (_JniMarshal_PPII_I):
					return new _JniMarshal_PPII_I (Unsafe.As<_JniMarshal_PPII_I> (dlg).Wrap_JniMarshal_PPII_I);
				case nameof (_JniMarshal_PPII_J):
					return new _JniMarshal_PPII_J (Unsafe.As<_JniMarshal_PPII_J> (dlg).Wrap_JniMarshal_PPII_J);
				case nameof (_JniMarshal_PPII_L):
					return new _JniMarshal_PPII_L (Unsafe.As<_JniMarshal_PPII_L> (dlg).Wrap_JniMarshal_PPII_L);
				case nameof (_JniMarshal_PPII_S):
					return new _JniMarshal_PPII_S (Unsafe.As<_JniMarshal_PPII_S> (dlg).Wrap_JniMarshal_PPII_S);
				case nameof (_JniMarshal_PPII_V):
					return new _JniMarshal_PPII_V (Unsafe.As<_JniMarshal_PPII_V> (dlg).Wrap_JniMarshal_PPII_V);
				case nameof (_JniMarshal_PPII_Z):
					return new _JniMarshal_PPII_Z (Unsafe.As<_JniMarshal_PPII_Z> (dlg).Wrap_JniMarshal_PPII_Z);
				case nameof (_JniMarshal_PPIIF_V):
					return new _JniMarshal_PPIIF_V (Unsafe.As<_JniMarshal_PPIIF_V> (dlg).Wrap_JniMarshal_PPIIF_V);
				case nameof (_JniMarshal_PPIIFF_I):
					return new _JniMarshal_PPIIFF_I (Unsafe.As<_JniMarshal_PPIIFF_I> (dlg).Wrap_JniMarshal_PPIIFF_I);
				case nameof (_JniMarshal_PPIIFF_V):
					return new _JniMarshal_PPIIFF_V (Unsafe.As<_JniMarshal_PPIIFF_V> (dlg).Wrap_JniMarshal_PPIIFF_V);
				case nameof (_JniMarshal_PPIIFI_V):
					return new _JniMarshal_PPIIFI_V (Unsafe.As<_JniMarshal_PPIIFI_V> (dlg).Wrap_JniMarshal_PPIIFI_V);
				case nameof (_JniMarshal_PPIIFJ_V):
					return new _JniMarshal_PPIIFJ_V (Unsafe.As<_JniMarshal_PPIIFJ_V> (dlg).Wrap_JniMarshal_PPIIFJ_V);
				case nameof (_JniMarshal_PPIII_F):
					return new _JniMarshal_PPIII_F (Unsafe.As<_JniMarshal_PPIII_F> (dlg).Wrap_JniMarshal_PPIII_F);
				case nameof (_JniMarshal_PPIII_I):
					return new _JniMarshal_PPIII_I (Unsafe.As<_JniMarshal_PPIII_I> (dlg).Wrap_JniMarshal_PPIII_I);
				case nameof (_JniMarshal_PPIII_L):
					return new _JniMarshal_PPIII_L (Unsafe.As<_JniMarshal_PPIII_L> (dlg).Wrap_JniMarshal_PPIII_L);
				case nameof (_JniMarshal_PPIII_V):
					return new _JniMarshal_PPIII_V (Unsafe.As<_JniMarshal_PPIII_V> (dlg).Wrap_JniMarshal_PPIII_V);
				case nameof (_JniMarshal_PPIII_Z):
					return new _JniMarshal_PPIII_Z (Unsafe.As<_JniMarshal_PPIII_Z> (dlg).Wrap_JniMarshal_PPIII_Z);
				case nameof (_JniMarshal_PPIIIF_F):
					return new _JniMarshal_PPIIIF_F (Unsafe.As<_JniMarshal_PPIIIF_F> (dlg).Wrap_JniMarshal_PPIIIF_F);
				case nameof (_JniMarshal_PPIIIF_V):
					return new _JniMarshal_PPIIIF_V (Unsafe.As<_JniMarshal_PPIIIF_V> (dlg).Wrap_JniMarshal_PPIIIF_V);
				case nameof (_JniMarshal_PPIIII_F):
					return new _JniMarshal_PPIIII_F (Unsafe.As<_JniMarshal_PPIIII_F> (dlg).Wrap_JniMarshal_PPIIII_F);
				case nameof (_JniMarshal_PPIIII_L):
					return new _JniMarshal_PPIIII_L (Unsafe.As<_JniMarshal_PPIIII_L> (dlg).Wrap_JniMarshal_PPIIII_L);
				case nameof (_JniMarshal_PPIIII_V):
					return new _JniMarshal_PPIIII_V (Unsafe.As<_JniMarshal_PPIIII_V> (dlg).Wrap_JniMarshal_PPIIII_V);
				case nameof (_JniMarshal_PPIIII_Z):
					return new _JniMarshal_PPIIII_Z (Unsafe.As<_JniMarshal_PPIIII_Z> (dlg).Wrap_JniMarshal_PPIIII_Z);
				case nameof (_JniMarshal_PPIIIII_I):
					return new _JniMarshal_PPIIIII_I (Unsafe.As<_JniMarshal_PPIIIII_I> (dlg).Wrap_JniMarshal_PPIIIII_I);
				case nameof (_JniMarshal_PPIIIII_L):
					return new _JniMarshal_PPIIIII_L (Unsafe.As<_JniMarshal_PPIIIII_L> (dlg).Wrap_JniMarshal_PPIIIII_L);
				case nameof (_JniMarshal_PPIIIII_V):
					return new _JniMarshal_PPIIIII_V (Unsafe.As<_JniMarshal_PPIIIII_V> (dlg).Wrap_JniMarshal_PPIIIII_V);
				case nameof (_JniMarshal_PPIIIII_Z):
					return new _JniMarshal_PPIIIII_Z (Unsafe.As<_JniMarshal_PPIIIII_Z> (dlg).Wrap_JniMarshal_PPIIIII_Z);
				case nameof (_JniMarshal_PPIIIIIB_Z):
					return new _JniMarshal_PPIIIIIB_Z (Unsafe.As<_JniMarshal_PPIIIIIB_Z> (dlg).Wrap_JniMarshal_PPIIIIIB_Z);
				case nameof (_JniMarshal_PPIIIIII_I):
					return new _JniMarshal_PPIIIIII_I (Unsafe.As<_JniMarshal_PPIIIIII_I> (dlg).Wrap_JniMarshal_PPIIIIII_I);
				case nameof (_JniMarshal_PPIIIIII_L):
					return new _JniMarshal_PPIIIIII_L (Unsafe.As<_JniMarshal_PPIIIIII_L> (dlg).Wrap_JniMarshal_PPIIIIII_L);
				case nameof (_JniMarshal_PPIIIIII_V):
					return new _JniMarshal_PPIIIIII_V (Unsafe.As<_JniMarshal_PPIIIIII_V> (dlg).Wrap_JniMarshal_PPIIIIII_V);
				case nameof (_JniMarshal_PPIIIIII_Z):
					return new _JniMarshal_PPIIIIII_Z (Unsafe.As<_JniMarshal_PPIIIIII_Z> (dlg).Wrap_JniMarshal_PPIIIIII_Z);
				case nameof (_JniMarshal_PPIIIIIID_I):
					return new _JniMarshal_PPIIIIIID_I (Unsafe.As<_JniMarshal_PPIIIIIID_I> (dlg).Wrap_JniMarshal_PPIIIIIID_I);
				case nameof (_JniMarshal_PPIIIIIIF_L):
					return new _JniMarshal_PPIIIIIIF_L (Unsafe.As<_JniMarshal_PPIIIIIIF_L> (dlg).Wrap_JniMarshal_PPIIIIIIF_L);
				case nameof (_JniMarshal_PPIIIIIIIF_V):
					return new _JniMarshal_PPIIIIIIIF_V (Unsafe.As<_JniMarshal_PPIIIIIIIF_V> (dlg).Wrap_JniMarshal_PPIIIIIIIF_V);
				case nameof (_JniMarshal_PPIIIIIIII_L):
					return new _JniMarshal_PPIIIIIIII_L (Unsafe.As<_JniMarshal_PPIIIIIIII_L> (dlg).Wrap_JniMarshal_PPIIIIIIII_L);
				case nameof (_JniMarshal_PPIIIIIIII_V):
					return new _JniMarshal_PPIIIIIIII_V (Unsafe.As<_JniMarshal_PPIIIIIIII_V> (dlg).Wrap_JniMarshal_PPIIIIIIII_V);
				case nameof (_JniMarshal_PPIIIIIIIII_J):
					return new _JniMarshal_PPIIIIIIIII_J (Unsafe.As<_JniMarshal_PPIIIIIIIII_J> (dlg).Wrap_JniMarshal_PPIIIIIIIII_J);
				case nameof (_JniMarshal_PPIIIIIIIIII_V):
					return new _JniMarshal_PPIIIIIIIIII_V (Unsafe.As<_JniMarshal_PPIIIIIIIIII_V> (dlg).Wrap_JniMarshal_PPIIIIIIIIII_V);
				case nameof (_JniMarshal_PPIIIIIIIIL_V):
					return new _JniMarshal_PPIIIIIIIIL_V (Unsafe.As<_JniMarshal_PPIIIIIIIIL_V> (dlg).Wrap_JniMarshal_PPIIIIIIIIL_V);
				case nameof (_JniMarshal_PPIIIIIIIIZ_Z):
					return new _JniMarshal_PPIIIIIIIIZ_Z (Unsafe.As<_JniMarshal_PPIIIIIIIIZ_Z> (dlg).Wrap_JniMarshal_PPIIIIIIIIZ_Z);
				case nameof (_JniMarshal_PPIIIIIIIL_V):
					return new _JniMarshal_PPIIIIIIIL_V (Unsafe.As<_JniMarshal_PPIIIIIIIL_V> (dlg).Wrap_JniMarshal_PPIIIIIIIL_V);
				case nameof (_JniMarshal_PPIIIIIIL_J):
					return new _JniMarshal_PPIIIIIIL_J (Unsafe.As<_JniMarshal_PPIIIIIIL_J> (dlg).Wrap_JniMarshal_PPIIIIIIL_J);
				case nameof (_JniMarshal_PPIIIIIIL_L):
					return new _JniMarshal_PPIIIIIIL_L (Unsafe.As<_JniMarshal_PPIIIIIIL_L> (dlg).Wrap_JniMarshal_PPIIIIIIL_L);
				case nameof (_JniMarshal_PPIIIIIIL_V):
					return new _JniMarshal_PPIIIIIIL_V (Unsafe.As<_JniMarshal_PPIIIIIIL_V> (dlg).Wrap_JniMarshal_PPIIIIIIL_V);
				case nameof (_JniMarshal_PPIIIIIILIII_V):
					return new _JniMarshal_PPIIIIIILIII_V (Unsafe.As<_JniMarshal_PPIIIIIILIII_V> (dlg).Wrap_JniMarshal_PPIIIIIILIII_V);
				case nameof (_JniMarshal_PPIIIIIL_I):
					return new _JniMarshal_PPIIIIIL_I (Unsafe.As<_JniMarshal_PPIIIIIL_I> (dlg).Wrap_JniMarshal_PPIIIIIL_I);
				case nameof (_JniMarshal_PPIIIIIL_L):
					return new _JniMarshal_PPIIIIIL_L (Unsafe.As<_JniMarshal_PPIIIIIL_L> (dlg).Wrap_JniMarshal_PPIIIIIL_L);
				case nameof (_JniMarshal_PPIIIIIL_V):
					return new _JniMarshal_PPIIIIIL_V (Unsafe.As<_JniMarshal_PPIIIIIL_V> (dlg).Wrap_JniMarshal_PPIIIIIL_V);
				case nameof (_JniMarshal_PPIIIIJ_L):
					return new _JniMarshal_PPIIIIJ_L (Unsafe.As<_JniMarshal_PPIIIIJ_L> (dlg).Wrap_JniMarshal_PPIIIIJ_L);
				case nameof (_JniMarshal_PPIIIIL_V):
					return new _JniMarshal_PPIIIIL_V (Unsafe.As<_JniMarshal_PPIIIIL_V> (dlg).Wrap_JniMarshal_PPIIIIL_V);
				case nameof (_JniMarshal_PPIIIIL_Z):
					return new _JniMarshal_PPIIIIL_Z (Unsafe.As<_JniMarshal_PPIIIIL_Z> (dlg).Wrap_JniMarshal_PPIIIIL_Z);
				case nameof (_JniMarshal_PPIIIILB_Z):
					return new _JniMarshal_PPIIIILB_Z (Unsafe.As<_JniMarshal_PPIIIILB_Z> (dlg).Wrap_JniMarshal_PPIIIILB_Z);
				case nameof (_JniMarshal_PPIIIILI_Z):
					return new _JniMarshal_PPIIIILI_Z (Unsafe.As<_JniMarshal_PPIIIILI_Z> (dlg).Wrap_JniMarshal_PPIIIILI_Z);
				case nameof (_JniMarshal_PPIIIILII_I):
					return new _JniMarshal_PPIIIILII_I (Unsafe.As<_JniMarshal_PPIIIILII_I> (dlg).Wrap_JniMarshal_PPIIIILII_I);
				case nameof (_JniMarshal_PPIIIILII_V):
					return new _JniMarshal_PPIIIILII_V (Unsafe.As<_JniMarshal_PPIIIILII_V> (dlg).Wrap_JniMarshal_PPIIIILII_V);
				case nameof (_JniMarshal_PPIIIILIII_I):
					return new _JniMarshal_PPIIIILIII_I (Unsafe.As<_JniMarshal_PPIIIILIII_I> (dlg).Wrap_JniMarshal_PPIIIILIII_I);
				case nameof (_JniMarshal_PPIIIILIL_V):
					return new _JniMarshal_PPIIIILIL_V (Unsafe.As<_JniMarshal_PPIIIILIL_V> (dlg).Wrap_JniMarshal_PPIIIILIL_V);
				case nameof (_JniMarshal_PPIIIILLI_V):
					return new _JniMarshal_PPIIIILLI_V (Unsafe.As<_JniMarshal_PPIIIILLI_V> (dlg).Wrap_JniMarshal_PPIIIILLI_V);
				case nameof (_JniMarshal_PPIIIIZ_V):
					return new _JniMarshal_PPIIIIZ_V (Unsafe.As<_JniMarshal_PPIIIIZ_V> (dlg).Wrap_JniMarshal_PPIIIIZ_V);
				case nameof (_JniMarshal_PPIIIIZZ_V):
					return new _JniMarshal_PPIIIIZZ_V (Unsafe.As<_JniMarshal_PPIIIIZZ_V> (dlg).Wrap_JniMarshal_PPIIIIZZ_V);
				case nameof (_JniMarshal_PPIIIJI_V):
					return new _JniMarshal_PPIIIJI_V (Unsafe.As<_JniMarshal_PPIIIJI_V> (dlg).Wrap_JniMarshal_PPIIIJI_V);
				case nameof (_JniMarshal_PPIIIL_L):
					return new _JniMarshal_PPIIIL_L (Unsafe.As<_JniMarshal_PPIIIL_L> (dlg).Wrap_JniMarshal_PPIIIL_L);
				case nameof (_JniMarshal_PPIIIL_V):
					return new _JniMarshal_PPIIIL_V (Unsafe.As<_JniMarshal_PPIIIL_V> (dlg).Wrap_JniMarshal_PPIIIL_V);
				case nameof (_JniMarshal_PPIIILI_L):
					return new _JniMarshal_PPIIILI_L (Unsafe.As<_JniMarshal_PPIIILI_L> (dlg).Wrap_JniMarshal_PPIIILI_L);
				case nameof (_JniMarshal_PPIIILI_V):
					return new _JniMarshal_PPIIILI_V (Unsafe.As<_JniMarshal_PPIIILI_V> (dlg).Wrap_JniMarshal_PPIIILI_V);
				case nameof (_JniMarshal_PPIIILL_L):
					return new _JniMarshal_PPIIILL_L (Unsafe.As<_JniMarshal_PPIIILL_L> (dlg).Wrap_JniMarshal_PPIIILL_L);
				case nameof (_JniMarshal_PPIIILLLIL_I):
					return new _JniMarshal_PPIIILLLIL_I (Unsafe.As<_JniMarshal_PPIIILLLIL_I> (dlg).Wrap_JniMarshal_PPIIILLLIL_I);
				case nameof (_JniMarshal_PPIIIZ_V):
					return new _JniMarshal_PPIIIZ_V (Unsafe.As<_JniMarshal_PPIIIZ_V> (dlg).Wrap_JniMarshal_PPIIIZ_V);
				case nameof (_JniMarshal_PPIIJ_V):
					return new _JniMarshal_PPIIJ_V (Unsafe.As<_JniMarshal_PPIIJ_V> (dlg).Wrap_JniMarshal_PPIIJ_V);
				case nameof (_JniMarshal_PPIIJL_V):
					return new _JniMarshal_PPIIJL_V (Unsafe.As<_JniMarshal_PPIIJL_V> (dlg).Wrap_JniMarshal_PPIIJL_V);
				case nameof (_JniMarshal_PPIIL_I):
					return new _JniMarshal_PPIIL_I (Unsafe.As<_JniMarshal_PPIIL_I> (dlg).Wrap_JniMarshal_PPIIL_I);
				case nameof (_JniMarshal_PPIIL_L):
					return new _JniMarshal_PPIIL_L (Unsafe.As<_JniMarshal_PPIIL_L> (dlg).Wrap_JniMarshal_PPIIL_L);
				case nameof (_JniMarshal_PPIIL_V):
					return new _JniMarshal_PPIIL_V (Unsafe.As<_JniMarshal_PPIIL_V> (dlg).Wrap_JniMarshal_PPIIL_V);
				case nameof (_JniMarshal_PPIIL_Z):
					return new _JniMarshal_PPIIL_Z (Unsafe.As<_JniMarshal_PPIIL_Z> (dlg).Wrap_JniMarshal_PPIIL_Z);
				case nameof (_JniMarshal_PPIILI_V):
					return new _JniMarshal_PPIILI_V (Unsafe.As<_JniMarshal_PPIILI_V> (dlg).Wrap_JniMarshal_PPIILI_V);
				case nameof (_JniMarshal_PPIILI_Z):
					return new _JniMarshal_PPIILI_Z (Unsafe.As<_JniMarshal_PPIILI_Z> (dlg).Wrap_JniMarshal_PPIILI_Z);
				case nameof (_JniMarshal_PPIILIFFFF_V):
					return new _JniMarshal_PPIILIFFFF_V (Unsafe.As<_JniMarshal_PPIILIFFFF_V> (dlg).Wrap_JniMarshal_PPIILIFFFF_V);
				case nameof (_JniMarshal_PPIILIFFFFL_V):
					return new _JniMarshal_PPIILIFFFFL_V (Unsafe.As<_JniMarshal_PPIILIFFFFL_V> (dlg).Wrap_JniMarshal_PPIILIFFFFL_V);
				case nameof (_JniMarshal_PPIILII_L):
					return new _JniMarshal_PPIILII_L (Unsafe.As<_JniMarshal_PPIILII_L> (dlg).Wrap_JniMarshal_PPIILII_L);
				case nameof (_JniMarshal_PPIILII_V):
					return new _JniMarshal_PPIILII_V (Unsafe.As<_JniMarshal_PPIILII_V> (dlg).Wrap_JniMarshal_PPIILII_V);
				case nameof (_JniMarshal_PPIILIL_V):
					return new _JniMarshal_PPIILIL_V (Unsafe.As<_JniMarshal_PPIILIL_V> (dlg).Wrap_JniMarshal_PPIILIL_V);
				case nameof (_JniMarshal_PPIILIL_Z):
					return new _JniMarshal_PPIILIL_Z (Unsafe.As<_JniMarshal_PPIILIL_Z> (dlg).Wrap_JniMarshal_PPIILIL_Z);
				case nameof (_JniMarshal_PPIILJI_V):
					return new _JniMarshal_PPIILJI_V (Unsafe.As<_JniMarshal_PPIILJI_V> (dlg).Wrap_JniMarshal_PPIILJI_V);
				case nameof (_JniMarshal_PPIILL_L):
					return new _JniMarshal_PPIILL_L (Unsafe.As<_JniMarshal_PPIILL_L> (dlg).Wrap_JniMarshal_PPIILL_L);
				case nameof (_JniMarshal_PPIILL_V):
					return new _JniMarshal_PPIILL_V (Unsafe.As<_JniMarshal_PPIILL_V> (dlg).Wrap_JniMarshal_PPIILL_V);
				case nameof (_JniMarshal_PPIILL_Z):
					return new _JniMarshal_PPIILL_Z (Unsafe.As<_JniMarshal_PPIILL_Z> (dlg).Wrap_JniMarshal_PPIILL_Z);
				case nameof (_JniMarshal_PPIILLFF_Z):
					return new _JniMarshal_PPIILLFF_Z (Unsafe.As<_JniMarshal_PPIILLFF_Z> (dlg).Wrap_JniMarshal_PPIILLFF_Z);
				case nameof (_JniMarshal_PPIILLI_Z):
					return new _JniMarshal_PPIILLI_Z (Unsafe.As<_JniMarshal_PPIILLI_Z> (dlg).Wrap_JniMarshal_PPIILLI_Z);
				case nameof (_JniMarshal_PPIILLLL_V):
					return new _JniMarshal_PPIILLLL_V (Unsafe.As<_JniMarshal_PPIILLLL_V> (dlg).Wrap_JniMarshal_PPIILLLL_V);
				case nameof (_JniMarshal_PPIILZ_V):
					return new _JniMarshal_PPIILZ_V (Unsafe.As<_JniMarshal_PPIILZ_V> (dlg).Wrap_JniMarshal_PPIILZ_V);
				case nameof (_JniMarshal_PPIIZ_I):
					return new _JniMarshal_PPIIZ_I (Unsafe.As<_JniMarshal_PPIIZ_I> (dlg).Wrap_JniMarshal_PPIIZ_I);
				case nameof (_JniMarshal_PPIIZ_L):
					return new _JniMarshal_PPIIZ_L (Unsafe.As<_JniMarshal_PPIIZ_L> (dlg).Wrap_JniMarshal_PPIIZ_L);
				case nameof (_JniMarshal_PPIIZ_V):
					return new _JniMarshal_PPIIZ_V (Unsafe.As<_JniMarshal_PPIIZ_V> (dlg).Wrap_JniMarshal_PPIIZ_V);
				case nameof (_JniMarshal_PPIIZ_Z):
					return new _JniMarshal_PPIIZ_Z (Unsafe.As<_JniMarshal_PPIIZ_Z> (dlg).Wrap_JniMarshal_PPIIZ_Z);
				case nameof (_JniMarshal_PPIIZFF_L):
					return new _JniMarshal_PPIIZFF_L (Unsafe.As<_JniMarshal_PPIIZFF_L> (dlg).Wrap_JniMarshal_PPIIZFF_L);
				case nameof (_JniMarshal_PPIIZFFI_L):
					return new _JniMarshal_PPIIZFFI_L (Unsafe.As<_JniMarshal_PPIIZFFI_L> (dlg).Wrap_JniMarshal_PPIIZFFI_L);
				case nameof (_JniMarshal_PPIIZLL_L):
					return new _JniMarshal_PPIIZLL_L (Unsafe.As<_JniMarshal_PPIIZLL_L> (dlg).Wrap_JniMarshal_PPIIZLL_L);
				case nameof (_JniMarshal_PPIIZZ_V):
					return new _JniMarshal_PPIIZZ_V (Unsafe.As<_JniMarshal_PPIIZZ_V> (dlg).Wrap_JniMarshal_PPIIZZ_V);
				case nameof (_JniMarshal_PPIJ_I):
					return new _JniMarshal_PPIJ_I (Unsafe.As<_JniMarshal_PPIJ_I> (dlg).Wrap_JniMarshal_PPIJ_I);
				case nameof (_JniMarshal_PPIJ_J):
					return new _JniMarshal_PPIJ_J (Unsafe.As<_JniMarshal_PPIJ_J> (dlg).Wrap_JniMarshal_PPIJ_J);
				case nameof (_JniMarshal_PPIJ_L):
					return new _JniMarshal_PPIJ_L (Unsafe.As<_JniMarshal_PPIJ_L> (dlg).Wrap_JniMarshal_PPIJ_L);
				case nameof (_JniMarshal_PPIJ_V):
					return new _JniMarshal_PPIJ_V (Unsafe.As<_JniMarshal_PPIJ_V> (dlg).Wrap_JniMarshal_PPIJ_V);
				case nameof (_JniMarshal_PPIJ_Z):
					return new _JniMarshal_PPIJ_Z (Unsafe.As<_JniMarshal_PPIJ_Z> (dlg).Wrap_JniMarshal_PPIJ_Z);
				case nameof (_JniMarshal_PPIJF_V):
					return new _JniMarshal_PPIJF_V (Unsafe.As<_JniMarshal_PPIJF_V> (dlg).Wrap_JniMarshal_PPIJF_V);
				case nameof (_JniMarshal_PPIJI_L):
					return new _JniMarshal_PPIJI_L (Unsafe.As<_JniMarshal_PPIJI_L> (dlg).Wrap_JniMarshal_PPIJI_L);
				case nameof (_JniMarshal_PPIJI_V):
					return new _JniMarshal_PPIJI_V (Unsafe.As<_JniMarshal_PPIJI_V> (dlg).Wrap_JniMarshal_PPIJI_V);
				case nameof (_JniMarshal_PPIJIIIL_V):
					return new _JniMarshal_PPIJIIIL_V (Unsafe.As<_JniMarshal_PPIJIIIL_V> (dlg).Wrap_JniMarshal_PPIJIIIL_V);
				case nameof (_JniMarshal_PPIJIZ_V):
					return new _JniMarshal_PPIJIZ_V (Unsafe.As<_JniMarshal_PPIJIZ_V> (dlg).Wrap_JniMarshal_PPIJIZ_V);
				case nameof (_JniMarshal_PPIJJ_V):
					return new _JniMarshal_PPIJJ_V (Unsafe.As<_JniMarshal_PPIJJ_V> (dlg).Wrap_JniMarshal_PPIJJ_V);
				case nameof (_JniMarshal_PPIJJF_V):
					return new _JniMarshal_PPIJJF_V (Unsafe.As<_JniMarshal_PPIJJF_V> (dlg).Wrap_JniMarshal_PPIJJF_V);
				case nameof (_JniMarshal_PPIJJI_V):
					return new _JniMarshal_PPIJJI_V (Unsafe.As<_JniMarshal_PPIJJI_V> (dlg).Wrap_JniMarshal_PPIJJI_V);
				case nameof (_JniMarshal_PPIJJJ_V):
					return new _JniMarshal_PPIJJJ_V (Unsafe.As<_JniMarshal_PPIJJJ_V> (dlg).Wrap_JniMarshal_PPIJJJ_V);
				case nameof (_JniMarshal_PPIJJL_J):
					return new _JniMarshal_PPIJJL_J (Unsafe.As<_JniMarshal_PPIJJL_J> (dlg).Wrap_JniMarshal_PPIJJL_J);
				case nameof (_JniMarshal_PPIJJL_V):
					return new _JniMarshal_PPIJJL_V (Unsafe.As<_JniMarshal_PPIJJL_V> (dlg).Wrap_JniMarshal_PPIJJL_V);
				case nameof (_JniMarshal_PPIJJLLL_V):
					return new _JniMarshal_PPIJJLLL_V (Unsafe.As<_JniMarshal_PPIJJLLL_V> (dlg).Wrap_JniMarshal_PPIJJLLL_V);
				case nameof (_JniMarshal_PPIJL_I):
					return new _JniMarshal_PPIJL_I (Unsafe.As<_JniMarshal_PPIJL_I> (dlg).Wrap_JniMarshal_PPIJL_I);
				case nameof (_JniMarshal_PPIJL_L):
					return new _JniMarshal_PPIJL_L (Unsafe.As<_JniMarshal_PPIJL_L> (dlg).Wrap_JniMarshal_PPIJL_L);
				case nameof (_JniMarshal_PPIJL_V):
					return new _JniMarshal_PPIJL_V (Unsafe.As<_JniMarshal_PPIJL_V> (dlg).Wrap_JniMarshal_PPIJL_V);
				case nameof (_JniMarshal_PPIJL_Z):
					return new _JniMarshal_PPIJL_Z (Unsafe.As<_JniMarshal_PPIJL_Z> (dlg).Wrap_JniMarshal_PPIJL_Z);
				case nameof (_JniMarshal_PPIJLLL_V):
					return new _JniMarshal_PPIJLLL_V (Unsafe.As<_JniMarshal_PPIJLLL_V> (dlg).Wrap_JniMarshal_PPIJLLL_V);
				case nameof (_JniMarshal_PPIJLZ_V):
					return new _JniMarshal_PPIJLZ_V (Unsafe.As<_JniMarshal_PPIJLZ_V> (dlg).Wrap_JniMarshal_PPIJLZ_V);
				case nameof (_JniMarshal_PPIL_C):
					return new _JniMarshal_PPIL_C (Unsafe.As<_JniMarshal_PPIL_C> (dlg).Wrap_JniMarshal_PPIL_C);
				case nameof (_JniMarshal_PPIL_I):
					return new _JniMarshal_PPIL_I (Unsafe.As<_JniMarshal_PPIL_I> (dlg).Wrap_JniMarshal_PPIL_I);
				case nameof (_JniMarshal_PPIL_L):
					return new _JniMarshal_PPIL_L (Unsafe.As<_JniMarshal_PPIL_L> (dlg).Wrap_JniMarshal_PPIL_L);
				case nameof (_JniMarshal_PPIL_V):
					return new _JniMarshal_PPIL_V (Unsafe.As<_JniMarshal_PPIL_V> (dlg).Wrap_JniMarshal_PPIL_V);
				case nameof (_JniMarshal_PPIL_Z):
					return new _JniMarshal_PPIL_Z (Unsafe.As<_JniMarshal_PPIL_Z> (dlg).Wrap_JniMarshal_PPIL_Z);
				case nameof (_JniMarshal_PPILB_V):
					return new _JniMarshal_PPILB_V (Unsafe.As<_JniMarshal_PPILB_V> (dlg).Wrap_JniMarshal_PPILB_V);
				case nameof (_JniMarshal_PPILC_V):
					return new _JniMarshal_PPILC_V (Unsafe.As<_JniMarshal_PPILC_V> (dlg).Wrap_JniMarshal_PPILC_V);
				case nameof (_JniMarshal_PPILD_V):
					return new _JniMarshal_PPILD_V (Unsafe.As<_JniMarshal_PPILD_V> (dlg).Wrap_JniMarshal_PPILD_V);
				case nameof (_JniMarshal_PPILF_V):
					return new _JniMarshal_PPILF_V (Unsafe.As<_JniMarshal_PPILF_V> (dlg).Wrap_JniMarshal_PPILF_V);
				case nameof (_JniMarshal_PPILFF_V):
					return new _JniMarshal_PPILFF_V (Unsafe.As<_JniMarshal_PPILFF_V> (dlg).Wrap_JniMarshal_PPILFF_V);
				case nameof (_JniMarshal_PPILFI_V):
					return new _JniMarshal_PPILFI_V (Unsafe.As<_JniMarshal_PPILFI_V> (dlg).Wrap_JniMarshal_PPILFI_V);
				case nameof (_JniMarshal_PPILFIF_V):
					return new _JniMarshal_PPILFIF_V (Unsafe.As<_JniMarshal_PPILFIF_V> (dlg).Wrap_JniMarshal_PPILFIF_V);
				case nameof (_JniMarshal_PPILI_C):
					return new _JniMarshal_PPILI_C (Unsafe.As<_JniMarshal_PPILI_C> (dlg).Wrap_JniMarshal_PPILI_C);
				case nameof (_JniMarshal_PPILI_I):
					return new _JniMarshal_PPILI_I (Unsafe.As<_JniMarshal_PPILI_I> (dlg).Wrap_JniMarshal_PPILI_I);
				case nameof (_JniMarshal_PPILI_L):
					return new _JniMarshal_PPILI_L (Unsafe.As<_JniMarshal_PPILI_L> (dlg).Wrap_JniMarshal_PPILI_L);
				case nameof (_JniMarshal_PPILI_V):
					return new _JniMarshal_PPILI_V (Unsafe.As<_JniMarshal_PPILI_V> (dlg).Wrap_JniMarshal_PPILI_V);
				case nameof (_JniMarshal_PPILI_Z):
					return new _JniMarshal_PPILI_Z (Unsafe.As<_JniMarshal_PPILI_Z> (dlg).Wrap_JniMarshal_PPILI_Z);
				case nameof (_JniMarshal_PPILII_L):
					return new _JniMarshal_PPILII_L (Unsafe.As<_JniMarshal_PPILII_L> (dlg).Wrap_JniMarshal_PPILII_L);
				case nameof (_JniMarshal_PPILII_V):
					return new _JniMarshal_PPILII_V (Unsafe.As<_JniMarshal_PPILII_V> (dlg).Wrap_JniMarshal_PPILII_V);
				case nameof (_JniMarshal_PPILII_Z):
					return new _JniMarshal_PPILII_Z (Unsafe.As<_JniMarshal_PPILII_Z> (dlg).Wrap_JniMarshal_PPILII_Z);
				case nameof (_JniMarshal_PPILIII_V):
					return new _JniMarshal_PPILIII_V (Unsafe.As<_JniMarshal_PPILIII_V> (dlg).Wrap_JniMarshal_PPILIII_V);
				case nameof (_JniMarshal_PPILIJJJ_V):
					return new _JniMarshal_PPILIJJJ_V (Unsafe.As<_JniMarshal_PPILIJJJ_V> (dlg).Wrap_JniMarshal_PPILIJJJ_V);
				case nameof (_JniMarshal_PPILIL_L):
					return new _JniMarshal_PPILIL_L (Unsafe.As<_JniMarshal_PPILIL_L> (dlg).Wrap_JniMarshal_PPILIL_L);
				case nameof (_JniMarshal_PPILIL_V):
					return new _JniMarshal_PPILIL_V (Unsafe.As<_JniMarshal_PPILIL_V> (dlg).Wrap_JniMarshal_PPILIL_V);
				case nameof (_JniMarshal_PPILILJ_V):
					return new _JniMarshal_PPILILJ_V (Unsafe.As<_JniMarshal_PPILILJ_V> (dlg).Wrap_JniMarshal_PPILILJ_V);
				case nameof (_JniMarshal_PPILJ_L):
					return new _JniMarshal_PPILJ_L (Unsafe.As<_JniMarshal_PPILJ_L> (dlg).Wrap_JniMarshal_PPILJ_L);
				case nameof (_JniMarshal_PPILJ_V):
					return new _JniMarshal_PPILJ_V (Unsafe.As<_JniMarshal_PPILJ_V> (dlg).Wrap_JniMarshal_PPILJ_V);
				case nameof (_JniMarshal_PPILJI_V):
					return new _JniMarshal_PPILJI_V (Unsafe.As<_JniMarshal_PPILJI_V> (dlg).Wrap_JniMarshal_PPILJI_V);
				case nameof (_JniMarshal_PPILJJ_L):
					return new _JniMarshal_PPILJJ_L (Unsafe.As<_JniMarshal_PPILJJ_L> (dlg).Wrap_JniMarshal_PPILJJ_L);
				case nameof (_JniMarshal_PPILJJI_L):
					return new _JniMarshal_PPILJJI_L (Unsafe.As<_JniMarshal_PPILJJI_L> (dlg).Wrap_JniMarshal_PPILJJI_L);
				case nameof (_JniMarshal_PPILJJII_L):
					return new _JniMarshal_PPILJJII_L (Unsafe.As<_JniMarshal_PPILJJII_L> (dlg).Wrap_JniMarshal_PPILJJII_L);
				case nameof (_JniMarshal_PPILJJIII_L):
					return new _JniMarshal_PPILJJIII_L (Unsafe.As<_JniMarshal_PPILJJIII_L> (dlg).Wrap_JniMarshal_PPILJJIII_L);
				case nameof (_JniMarshal_PPILJL_V):
					return new _JniMarshal_PPILJL_V (Unsafe.As<_JniMarshal_PPILJL_V> (dlg).Wrap_JniMarshal_PPILJL_V);
				case nameof (_JniMarshal_PPILJLL_V):
					return new _JniMarshal_PPILJLL_V (Unsafe.As<_JniMarshal_PPILJLL_V> (dlg).Wrap_JniMarshal_PPILJLL_V);
				case nameof (_JniMarshal_PPILL_I):
					return new _JniMarshal_PPILL_I (Unsafe.As<_JniMarshal_PPILL_I> (dlg).Wrap_JniMarshal_PPILL_I);
				case nameof (_JniMarshal_PPILL_L):
					return new _JniMarshal_PPILL_L (Unsafe.As<_JniMarshal_PPILL_L> (dlg).Wrap_JniMarshal_PPILL_L);
				case nameof (_JniMarshal_PPILL_V):
					return new _JniMarshal_PPILL_V (Unsafe.As<_JniMarshal_PPILL_V> (dlg).Wrap_JniMarshal_PPILL_V);
				case nameof (_JniMarshal_PPILL_Z):
					return new _JniMarshal_PPILL_Z (Unsafe.As<_JniMarshal_PPILL_Z> (dlg).Wrap_JniMarshal_PPILL_Z);
				case nameof (_JniMarshal_PPILLF_V):
					return new _JniMarshal_PPILLF_V (Unsafe.As<_JniMarshal_PPILLF_V> (dlg).Wrap_JniMarshal_PPILLF_V);
				case nameof (_JniMarshal_PPILLI_V):
					return new _JniMarshal_PPILLI_V (Unsafe.As<_JniMarshal_PPILLI_V> (dlg).Wrap_JniMarshal_PPILLI_V);
				case nameof (_JniMarshal_PPILLI_Z):
					return new _JniMarshal_PPILLI_Z (Unsafe.As<_JniMarshal_PPILLI_Z> (dlg).Wrap_JniMarshal_PPILLI_Z);
				case nameof (_JniMarshal_PPILLIII_V):
					return new _JniMarshal_PPILLIII_V (Unsafe.As<_JniMarshal_PPILLIII_V> (dlg).Wrap_JniMarshal_PPILLIII_V);
				case nameof (_JniMarshal_PPILLIL_V):
					return new _JniMarshal_PPILLIL_V (Unsafe.As<_JniMarshal_PPILLIL_V> (dlg).Wrap_JniMarshal_PPILLIL_V);
				case nameof (_JniMarshal_PPILLJ_V):
					return new _JniMarshal_PPILLJ_V (Unsafe.As<_JniMarshal_PPILLJ_V> (dlg).Wrap_JniMarshal_PPILLJ_V);
				case nameof (_JniMarshal_PPILLL_I):
					return new _JniMarshal_PPILLL_I (Unsafe.As<_JniMarshal_PPILLL_I> (dlg).Wrap_JniMarshal_PPILLL_I);
				case nameof (_JniMarshal_PPILLL_L):
					return new _JniMarshal_PPILLL_L (Unsafe.As<_JniMarshal_PPILLL_L> (dlg).Wrap_JniMarshal_PPILLL_L);
				case nameof (_JniMarshal_PPILLL_V):
					return new _JniMarshal_PPILLL_V (Unsafe.As<_JniMarshal_PPILLL_V> (dlg).Wrap_JniMarshal_PPILLL_V);
				case nameof (_JniMarshal_PPILLL_Z):
					return new _JniMarshal_PPILLL_Z (Unsafe.As<_JniMarshal_PPILLL_Z> (dlg).Wrap_JniMarshal_PPILLL_Z);
				case nameof (_JniMarshal_PPILLLIJ_V):
					return new _JniMarshal_PPILLLIJ_V (Unsafe.As<_JniMarshal_PPILLLIJ_V> (dlg).Wrap_JniMarshal_PPILLLIJ_V);
				case nameof (_JniMarshal_PPILLLILI_I):
					return new _JniMarshal_PPILLLILI_I (Unsafe.As<_JniMarshal_PPILLLILI_I> (dlg).Wrap_JniMarshal_PPILLLILI_I);
				case nameof (_JniMarshal_PPILLLJ_V):
					return new _JniMarshal_PPILLLJ_V (Unsafe.As<_JniMarshal_PPILLLJ_V> (dlg).Wrap_JniMarshal_PPILLLJ_V);
				case nameof (_JniMarshal_PPILLLL_L):
					return new _JniMarshal_PPILLLL_L (Unsafe.As<_JniMarshal_PPILLLL_L> (dlg).Wrap_JniMarshal_PPILLLL_L);
				case nameof (_JniMarshal_PPILLLL_V):
					return new _JniMarshal_PPILLLL_V (Unsafe.As<_JniMarshal_PPILLLL_V> (dlg).Wrap_JniMarshal_PPILLLL_V);
				case nameof (_JniMarshal_PPILLLLL_V):
					return new _JniMarshal_PPILLLLL_V (Unsafe.As<_JniMarshal_PPILLLLL_V> (dlg).Wrap_JniMarshal_PPILLLLL_V);
				case nameof (_JniMarshal_PPILLLLLL_V):
					return new _JniMarshal_PPILLLLLL_V (Unsafe.As<_JniMarshal_PPILLLLLL_V> (dlg).Wrap_JniMarshal_PPILLLLLL_V);
				case nameof (_JniMarshal_PPILLLLLZ_L):
					return new _JniMarshal_PPILLLLLZ_L (Unsafe.As<_JniMarshal_PPILLLLLZ_L> (dlg).Wrap_JniMarshal_PPILLLLLZ_L);
				case nameof (_JniMarshal_PPILLLLZ_V):
					return new _JniMarshal_PPILLLLZ_V (Unsafe.As<_JniMarshal_PPILLLLZ_V> (dlg).Wrap_JniMarshal_PPILLLLZ_V);
				case nameof (_JniMarshal_PPILLLZ_I):
					return new _JniMarshal_PPILLLZ_I (Unsafe.As<_JniMarshal_PPILLLZ_I> (dlg).Wrap_JniMarshal_PPILLLZ_I);
				case nameof (_JniMarshal_PPILLLZ_V):
					return new _JniMarshal_PPILLLZ_V (Unsafe.As<_JniMarshal_PPILLLZ_V> (dlg).Wrap_JniMarshal_PPILLLZ_V);
				case nameof (_JniMarshal_PPILLZ_L):
					return new _JniMarshal_PPILLZ_L (Unsafe.As<_JniMarshal_PPILLZ_L> (dlg).Wrap_JniMarshal_PPILLZ_L);
				case nameof (_JniMarshal_PPILS_V):
					return new _JniMarshal_PPILS_V (Unsafe.As<_JniMarshal_PPILS_V> (dlg).Wrap_JniMarshal_PPILS_V);
				case nameof (_JniMarshal_PPILZ_L):
					return new _JniMarshal_PPILZ_L (Unsafe.As<_JniMarshal_PPILZ_L> (dlg).Wrap_JniMarshal_PPILZ_L);
				case nameof (_JniMarshal_PPILZ_V):
					return new _JniMarshal_PPILZ_V (Unsafe.As<_JniMarshal_PPILZ_V> (dlg).Wrap_JniMarshal_PPILZ_V);
				case nameof (_JniMarshal_PPILZL_V):
					return new _JniMarshal_PPILZL_V (Unsafe.As<_JniMarshal_PPILZL_V> (dlg).Wrap_JniMarshal_PPILZL_V);
				case nameof (_JniMarshal_PPILZLLL_L):
					return new _JniMarshal_PPILZLLL_L (Unsafe.As<_JniMarshal_PPILZLLL_L> (dlg).Wrap_JniMarshal_PPILZLLL_L);
				case nameof (_JniMarshal_PPIS_L):
					return new _JniMarshal_PPIS_L (Unsafe.As<_JniMarshal_PPIS_L> (dlg).Wrap_JniMarshal_PPIS_L);
				case nameof (_JniMarshal_PPIS_V):
					return new _JniMarshal_PPIS_V (Unsafe.As<_JniMarshal_PPIS_V> (dlg).Wrap_JniMarshal_PPIS_V);
				case nameof (_JniMarshal_PPIS_Z):
					return new _JniMarshal_PPIS_Z (Unsafe.As<_JniMarshal_PPIS_Z> (dlg).Wrap_JniMarshal_PPIS_Z);
				case nameof (_JniMarshal_PPISI_V):
					return new _JniMarshal_PPISI_V (Unsafe.As<_JniMarshal_PPISI_V> (dlg).Wrap_JniMarshal_PPISI_V);
				case nameof (_JniMarshal_PPIZ_I):
					return new _JniMarshal_PPIZ_I (Unsafe.As<_JniMarshal_PPIZ_I> (dlg).Wrap_JniMarshal_PPIZ_I);
				case nameof (_JniMarshal_PPIZ_L):
					return new _JniMarshal_PPIZ_L (Unsafe.As<_JniMarshal_PPIZ_L> (dlg).Wrap_JniMarshal_PPIZ_L);
				case nameof (_JniMarshal_PPIZ_V):
					return new _JniMarshal_PPIZ_V (Unsafe.As<_JniMarshal_PPIZ_V> (dlg).Wrap_JniMarshal_PPIZ_V);
				case nameof (_JniMarshal_PPIZ_Z):
					return new _JniMarshal_PPIZ_Z (Unsafe.As<_JniMarshal_PPIZ_Z> (dlg).Wrap_JniMarshal_PPIZ_Z);
				case nameof (_JniMarshal_PPIZF_V):
					return new _JniMarshal_PPIZF_V (Unsafe.As<_JniMarshal_PPIZF_V> (dlg).Wrap_JniMarshal_PPIZF_V);
				case nameof (_JniMarshal_PPIZI_L):
					return new _JniMarshal_PPIZI_L (Unsafe.As<_JniMarshal_PPIZI_L> (dlg).Wrap_JniMarshal_PPIZI_L);
				case nameof (_JniMarshal_PPIZJ_V):
					return new _JniMarshal_PPIZJ_V (Unsafe.As<_JniMarshal_PPIZJ_V> (dlg).Wrap_JniMarshal_PPIZJ_V);
				case nameof (_JniMarshal_PPIZL_V):
					return new _JniMarshal_PPIZL_V (Unsafe.As<_JniMarshal_PPIZL_V> (dlg).Wrap_JniMarshal_PPIZL_V);
				case nameof (_JniMarshal_PPIZLJ_V):
					return new _JniMarshal_PPIZLJ_V (Unsafe.As<_JniMarshal_PPIZLJ_V> (dlg).Wrap_JniMarshal_PPIZLJ_V);
				case nameof (_JniMarshal_PPIZLL_L):
					return new _JniMarshal_PPIZLL_L (Unsafe.As<_JniMarshal_PPIZLL_L> (dlg).Wrap_JniMarshal_PPIZLL_L);
				case nameof (_JniMarshal_PPIZLL_V):
					return new _JniMarshal_PPIZLL_V (Unsafe.As<_JniMarshal_PPIZLL_V> (dlg).Wrap_JniMarshal_PPIZLL_V);
				case nameof (_JniMarshal_PPIZZ_V):
					return new _JniMarshal_PPIZZ_V (Unsafe.As<_JniMarshal_PPIZZ_V> (dlg).Wrap_JniMarshal_PPIZZ_V);
				case nameof (_JniMarshal_PPIZZ_Z):
					return new _JniMarshal_PPIZZ_Z (Unsafe.As<_JniMarshal_PPIZZ_Z> (dlg).Wrap_JniMarshal_PPIZZ_Z);
				case nameof (_JniMarshal_PPJ_B):
					return new _JniMarshal_PPJ_B (Unsafe.As<_JniMarshal_PPJ_B> (dlg).Wrap_JniMarshal_PPJ_B);
				case nameof (_JniMarshal_PPJ_C):
					return new _JniMarshal_PPJ_C (Unsafe.As<_JniMarshal_PPJ_C> (dlg).Wrap_JniMarshal_PPJ_C);
				case nameof (_JniMarshal_PPJ_D):
					return new _JniMarshal_PPJ_D (Unsafe.As<_JniMarshal_PPJ_D> (dlg).Wrap_JniMarshal_PPJ_D);
				case nameof (_JniMarshal_PPJ_F):
					return new _JniMarshal_PPJ_F (Unsafe.As<_JniMarshal_PPJ_F> (dlg).Wrap_JniMarshal_PPJ_F);
				case nameof (_JniMarshal_PPJ_I):
					return new _JniMarshal_PPJ_I (Unsafe.As<_JniMarshal_PPJ_I> (dlg).Wrap_JniMarshal_PPJ_I);
				case nameof (_JniMarshal_PPJ_J):
					return new _JniMarshal_PPJ_J (Unsafe.As<_JniMarshal_PPJ_J> (dlg).Wrap_JniMarshal_PPJ_J);
				case nameof (_JniMarshal_PPJ_L):
					return new _JniMarshal_PPJ_L (Unsafe.As<_JniMarshal_PPJ_L> (dlg).Wrap_JniMarshal_PPJ_L);
				case nameof (_JniMarshal_PPJ_S):
					return new _JniMarshal_PPJ_S (Unsafe.As<_JniMarshal_PPJ_S> (dlg).Wrap_JniMarshal_PPJ_S);
				case nameof (_JniMarshal_PPJ_V):
					return new _JniMarshal_PPJ_V (Unsafe.As<_JniMarshal_PPJ_V> (dlg).Wrap_JniMarshal_PPJ_V);
				case nameof (_JniMarshal_PPJ_Z):
					return new _JniMarshal_PPJ_Z (Unsafe.As<_JniMarshal_PPJ_Z> (dlg).Wrap_JniMarshal_PPJ_Z);
				case nameof (_JniMarshal_PPJB_V):
					return new _JniMarshal_PPJB_V (Unsafe.As<_JniMarshal_PPJB_V> (dlg).Wrap_JniMarshal_PPJB_V);
				case nameof (_JniMarshal_PPJC_V):
					return new _JniMarshal_PPJC_V (Unsafe.As<_JniMarshal_PPJC_V> (dlg).Wrap_JniMarshal_PPJC_V);
				case nameof (_JniMarshal_PPJD_V):
					return new _JniMarshal_PPJD_V (Unsafe.As<_JniMarshal_PPJD_V> (dlg).Wrap_JniMarshal_PPJD_V);
				case nameof (_JniMarshal_PPJF_V):
					return new _JniMarshal_PPJF_V (Unsafe.As<_JniMarshal_PPJF_V> (dlg).Wrap_JniMarshal_PPJF_V);
				case nameof (_JniMarshal_PPJF_Z):
					return new _JniMarshal_PPJF_Z (Unsafe.As<_JniMarshal_PPJF_Z> (dlg).Wrap_JniMarshal_PPJF_Z);
				case nameof (_JniMarshal_PPJFLL_V):
					return new _JniMarshal_PPJFLL_V (Unsafe.As<_JniMarshal_PPJFLL_V> (dlg).Wrap_JniMarshal_PPJFLL_V);
				case nameof (_JniMarshal_PPJFLLL_V):
					return new _JniMarshal_PPJFLLL_V (Unsafe.As<_JniMarshal_PPJFLLL_V> (dlg).Wrap_JniMarshal_PPJFLLL_V);
				case nameof (_JniMarshal_PPJFZJ_Z):
					return new _JniMarshal_PPJFZJ_Z (Unsafe.As<_JniMarshal_PPJFZJ_Z> (dlg).Wrap_JniMarshal_PPJFZJ_Z);
				case nameof (_JniMarshal_PPJI_I):
					return new _JniMarshal_PPJI_I (Unsafe.As<_JniMarshal_PPJI_I> (dlg).Wrap_JniMarshal_PPJI_I);
				case nameof (_JniMarshal_PPJI_J):
					return new _JniMarshal_PPJI_J (Unsafe.As<_JniMarshal_PPJI_J> (dlg).Wrap_JniMarshal_PPJI_J);
				case nameof (_JniMarshal_PPJI_L):
					return new _JniMarshal_PPJI_L (Unsafe.As<_JniMarshal_PPJI_L> (dlg).Wrap_JniMarshal_PPJI_L);
				case nameof (_JniMarshal_PPJI_V):
					return new _JniMarshal_PPJI_V (Unsafe.As<_JniMarshal_PPJI_V> (dlg).Wrap_JniMarshal_PPJI_V);
				case nameof (_JniMarshal_PPJII_L):
					return new _JniMarshal_PPJII_L (Unsafe.As<_JniMarshal_PPJII_L> (dlg).Wrap_JniMarshal_PPJII_L);
				case nameof (_JniMarshal_PPJII_Z):
					return new _JniMarshal_PPJII_Z (Unsafe.As<_JniMarshal_PPJII_Z> (dlg).Wrap_JniMarshal_PPJII_Z);
				case nameof (_JniMarshal_PPJIII_L):
					return new _JniMarshal_PPJIII_L (Unsafe.As<_JniMarshal_PPJIII_L> (dlg).Wrap_JniMarshal_PPJIII_L);
				case nameof (_JniMarshal_PPJIIII_V):
					return new _JniMarshal_PPJIIII_V (Unsafe.As<_JniMarshal_PPJIIII_V> (dlg).Wrap_JniMarshal_PPJIIII_V);
				case nameof (_JniMarshal_PPJIIIL_L):
					return new _JniMarshal_PPJIIIL_L (Unsafe.As<_JniMarshal_PPJIIIL_L> (dlg).Wrap_JniMarshal_PPJIIIL_L);
				case nameof (_JniMarshal_PPJIIIL_V):
					return new _JniMarshal_PPJIIIL_V (Unsafe.As<_JniMarshal_PPJIIIL_V> (dlg).Wrap_JniMarshal_PPJIIIL_V);
				case nameof (_JniMarshal_PPJIIL_L):
					return new _JniMarshal_PPJIIL_L (Unsafe.As<_JniMarshal_PPJIIL_L> (dlg).Wrap_JniMarshal_PPJIIL_L);
				case nameof (_JniMarshal_PPJIL_I):
					return new _JniMarshal_PPJIL_I (Unsafe.As<_JniMarshal_PPJIL_I> (dlg).Wrap_JniMarshal_PPJIL_I);
				case nameof (_JniMarshal_PPJIL_L):
					return new _JniMarshal_PPJIL_L (Unsafe.As<_JniMarshal_PPJIL_L> (dlg).Wrap_JniMarshal_PPJIL_L);
				case nameof (_JniMarshal_PPJIL_V):
					return new _JniMarshal_PPJIL_V (Unsafe.As<_JniMarshal_PPJIL_V> (dlg).Wrap_JniMarshal_PPJIL_V);
				case nameof (_JniMarshal_PPJILLLL_L):
					return new _JniMarshal_PPJILLLL_L (Unsafe.As<_JniMarshal_PPJILLLL_L> (dlg).Wrap_JniMarshal_PPJILLLL_L);
				case nameof (_JniMarshal_PPJJ_F):
					return new _JniMarshal_PPJJ_F (Unsafe.As<_JniMarshal_PPJJ_F> (dlg).Wrap_JniMarshal_PPJJ_F);
				case nameof (_JniMarshal_PPJJ_J):
					return new _JniMarshal_PPJJ_J (Unsafe.As<_JniMarshal_PPJJ_J> (dlg).Wrap_JniMarshal_PPJJ_J);
				case nameof (_JniMarshal_PPJJ_L):
					return new _JniMarshal_PPJJ_L (Unsafe.As<_JniMarshal_PPJJ_L> (dlg).Wrap_JniMarshal_PPJJ_L);
				case nameof (_JniMarshal_PPJJ_V):
					return new _JniMarshal_PPJJ_V (Unsafe.As<_JniMarshal_PPJJ_V> (dlg).Wrap_JniMarshal_PPJJ_V);
				case nameof (_JniMarshal_PPJJ_Z):
					return new _JniMarshal_PPJJ_Z (Unsafe.As<_JniMarshal_PPJJ_Z> (dlg).Wrap_JniMarshal_PPJJ_Z);
				case nameof (_JniMarshal_PPJJF_V):
					return new _JniMarshal_PPJJF_V (Unsafe.As<_JniMarshal_PPJJF_V> (dlg).Wrap_JniMarshal_PPJJF_V);
				case nameof (_JniMarshal_PPJJF_Z):
					return new _JniMarshal_PPJJF_Z (Unsafe.As<_JniMarshal_PPJJF_Z> (dlg).Wrap_JniMarshal_PPJJF_Z);
				case nameof (_JniMarshal_PPJJJ_V):
					return new _JniMarshal_PPJJJ_V (Unsafe.As<_JniMarshal_PPJJJ_V> (dlg).Wrap_JniMarshal_PPJJJ_V);
				case nameof (_JniMarshal_PPJJJLL_V):
					return new _JniMarshal_PPJJJLL_V (Unsafe.As<_JniMarshal_PPJJJLL_V> (dlg).Wrap_JniMarshal_PPJJJLL_V);
				case nameof (_JniMarshal_PPJJJZJJJJLLLLL_L):
					return new _JniMarshal_PPJJJZJJJJLLLLL_L (Unsafe.As<_JniMarshal_PPJJJZJJJJLLLLL_L> (dlg).Wrap_JniMarshal_PPJJJZJJJJLLLLL_L);
				case nameof (_JniMarshal_PPJJJZZ_Z):
					return new _JniMarshal_PPJJJZZ_Z (Unsafe.As<_JniMarshal_PPJJJZZ_Z> (dlg).Wrap_JniMarshal_PPJJJZZ_Z);
				case nameof (_JniMarshal_PPJJL_J):
					return new _JniMarshal_PPJJL_J (Unsafe.As<_JniMarshal_PPJJL_J> (dlg).Wrap_JniMarshal_PPJJL_J);
				case nameof (_JniMarshal_PPJJL_L):
					return new _JniMarshal_PPJJL_L (Unsafe.As<_JniMarshal_PPJJL_L> (dlg).Wrap_JniMarshal_PPJJL_L);
				case nameof (_JniMarshal_PPJJL_V):
					return new _JniMarshal_PPJJL_V (Unsafe.As<_JniMarshal_PPJJL_V> (dlg).Wrap_JniMarshal_PPJJL_V);
				case nameof (_JniMarshal_PPJJLL_V):
					return new _JniMarshal_PPJJLL_V (Unsafe.As<_JniMarshal_PPJJLL_V> (dlg).Wrap_JniMarshal_PPJJLL_V);
				case nameof (_JniMarshal_PPJJZ_L):
					return new _JniMarshal_PPJJZ_L (Unsafe.As<_JniMarshal_PPJJZ_L> (dlg).Wrap_JniMarshal_PPJJZ_L);
				case nameof (_JniMarshal_PPJJZ_Z):
					return new _JniMarshal_PPJJZ_Z (Unsafe.As<_JniMarshal_PPJJZ_Z> (dlg).Wrap_JniMarshal_PPJJZ_Z);
				case nameof (_JniMarshal_PPJJZLL_V):
					return new _JniMarshal_PPJJZLL_V (Unsafe.As<_JniMarshal_PPJJZLL_V> (dlg).Wrap_JniMarshal_PPJJZLL_V);
				case nameof (_JniMarshal_PPJL_I):
					return new _JniMarshal_PPJL_I (Unsafe.As<_JniMarshal_PPJL_I> (dlg).Wrap_JniMarshal_PPJL_I);
				case nameof (_JniMarshal_PPJL_J):
					return new _JniMarshal_PPJL_J (Unsafe.As<_JniMarshal_PPJL_J> (dlg).Wrap_JniMarshal_PPJL_J);
				case nameof (_JniMarshal_PPJL_L):
					return new _JniMarshal_PPJL_L (Unsafe.As<_JniMarshal_PPJL_L> (dlg).Wrap_JniMarshal_PPJL_L);
				case nameof (_JniMarshal_PPJL_V):
					return new _JniMarshal_PPJL_V (Unsafe.As<_JniMarshal_PPJL_V> (dlg).Wrap_JniMarshal_PPJL_V);
				case nameof (_JniMarshal_PPJL_Z):
					return new _JniMarshal_PPJL_Z (Unsafe.As<_JniMarshal_PPJL_Z> (dlg).Wrap_JniMarshal_PPJL_Z);
				case nameof (_JniMarshal_PPJLDL_D):
					return new _JniMarshal_PPJLDL_D (Unsafe.As<_JniMarshal_PPJLDL_D> (dlg).Wrap_JniMarshal_PPJLDL_D);
				case nameof (_JniMarshal_PPJLF_Z):
					return new _JniMarshal_PPJLF_Z (Unsafe.As<_JniMarshal_PPJLF_Z> (dlg).Wrap_JniMarshal_PPJLF_Z);
				case nameof (_JniMarshal_PPJLFFLIZ_L):
					return new _JniMarshal_PPJLFFLIZ_L (Unsafe.As<_JniMarshal_PPJLFFLIZ_L> (dlg).Wrap_JniMarshal_PPJLFFLIZ_L);
				case nameof (_JniMarshal_PPJLI_V):
					return new _JniMarshal_PPJLI_V (Unsafe.As<_JniMarshal_PPJLI_V> (dlg).Wrap_JniMarshal_PPJLI_V);
				case nameof (_JniMarshal_PPJLII_I):
					return new _JniMarshal_PPJLII_I (Unsafe.As<_JniMarshal_PPJLII_I> (dlg).Wrap_JniMarshal_PPJLII_I);
				case nameof (_JniMarshal_PPJLII_V):
					return new _JniMarshal_PPJLII_V (Unsafe.As<_JniMarshal_PPJLII_V> (dlg).Wrap_JniMarshal_PPJLII_V);
				case nameof (_JniMarshal_PPJLII_Z):
					return new _JniMarshal_PPJLII_Z (Unsafe.As<_JniMarshal_PPJLII_Z> (dlg).Wrap_JniMarshal_PPJLII_Z);
				case nameof (_JniMarshal_PPJLIL_I):
					return new _JniMarshal_PPJLIL_I (Unsafe.As<_JniMarshal_PPJLIL_I> (dlg).Wrap_JniMarshal_PPJLIL_I);
				case nameof (_JniMarshal_PPJLJ_V):
					return new _JniMarshal_PPJLJ_V (Unsafe.As<_JniMarshal_PPJLJ_V> (dlg).Wrap_JniMarshal_PPJLJ_V);
				case nameof (_JniMarshal_PPJLJL_J):
					return new _JniMarshal_PPJLJL_J (Unsafe.As<_JniMarshal_PPJLJL_J> (dlg).Wrap_JniMarshal_PPJLJL_J);
				case nameof (_JniMarshal_PPJLL_L):
					return new _JniMarshal_PPJLL_L (Unsafe.As<_JniMarshal_PPJLL_L> (dlg).Wrap_JniMarshal_PPJLL_L);
				case nameof (_JniMarshal_PPJLL_V):
					return new _JniMarshal_PPJLL_V (Unsafe.As<_JniMarshal_PPJLL_V> (dlg).Wrap_JniMarshal_PPJLL_V);
				case nameof (_JniMarshal_PPJLL_Z):
					return new _JniMarshal_PPJLL_Z (Unsafe.As<_JniMarshal_PPJLL_Z> (dlg).Wrap_JniMarshal_PPJLL_Z);
				case nameof (_JniMarshal_PPJLLL_I):
					return new _JniMarshal_PPJLLL_I (Unsafe.As<_JniMarshal_PPJLLL_I> (dlg).Wrap_JniMarshal_PPJLLL_I);
				case nameof (_JniMarshal_PPJLLL_V):
					return new _JniMarshal_PPJLLL_V (Unsafe.As<_JniMarshal_PPJLLL_V> (dlg).Wrap_JniMarshal_PPJLLL_V);
				case nameof (_JniMarshal_PPJLLLL_L):
					return new _JniMarshal_PPJLLLL_L (Unsafe.As<_JniMarshal_PPJLLLL_L> (dlg).Wrap_JniMarshal_PPJLLLL_L);
				case nameof (_JniMarshal_PPJLZZL_V):
					return new _JniMarshal_PPJLZZL_V (Unsafe.As<_JniMarshal_PPJLZZL_V> (dlg).Wrap_JniMarshal_PPJLZZL_V);
				case nameof (_JniMarshal_PPJS_V):
					return new _JniMarshal_PPJS_V (Unsafe.As<_JniMarshal_PPJS_V> (dlg).Wrap_JniMarshal_PPJS_V);
				case nameof (_JniMarshal_PPJZ_J):
					return new _JniMarshal_PPJZ_J (Unsafe.As<_JniMarshal_PPJZ_J> (dlg).Wrap_JniMarshal_PPJZ_J);
				case nameof (_JniMarshal_PPJZ_L):
					return new _JniMarshal_PPJZ_L (Unsafe.As<_JniMarshal_PPJZ_L> (dlg).Wrap_JniMarshal_PPJZ_L);
				case nameof (_JniMarshal_PPJZ_V):
					return new _JniMarshal_PPJZ_V (Unsafe.As<_JniMarshal_PPJZ_V> (dlg).Wrap_JniMarshal_PPJZ_V);
				case nameof (_JniMarshal_PPJZL_L):
					return new _JniMarshal_PPJZL_L (Unsafe.As<_JniMarshal_PPJZL_L> (dlg).Wrap_JniMarshal_PPJZL_L);
				case nameof (_JniMarshal_PPJZL_V):
					return new _JniMarshal_PPJZL_V (Unsafe.As<_JniMarshal_PPJZL_V> (dlg).Wrap_JniMarshal_PPJZL_V);
				case nameof (_JniMarshal_PPJZZ_V):
					return new _JniMarshal_PPJZZ_V (Unsafe.As<_JniMarshal_PPJZZ_V> (dlg).Wrap_JniMarshal_PPJZZ_V);
				case nameof (_JniMarshal_PPJZZL_L):
					return new _JniMarshal_PPJZZL_L (Unsafe.As<_JniMarshal_PPJZZL_L> (dlg).Wrap_JniMarshal_PPJZZL_L);
				case nameof (_JniMarshal_PPL_B):
					return new _JniMarshal_PPL_B (Unsafe.As<_JniMarshal_PPL_B> (dlg).Wrap_JniMarshal_PPL_B);
				case nameof (_JniMarshal_PPL_C):
					return new _JniMarshal_PPL_C (Unsafe.As<_JniMarshal_PPL_C> (dlg).Wrap_JniMarshal_PPL_C);
				case nameof (_JniMarshal_PPL_D):
					return new _JniMarshal_PPL_D (Unsafe.As<_JniMarshal_PPL_D> (dlg).Wrap_JniMarshal_PPL_D);
				case nameof (_JniMarshal_PPL_F):
					return new _JniMarshal_PPL_F (Unsafe.As<_JniMarshal_PPL_F> (dlg).Wrap_JniMarshal_PPL_F);
				case nameof (_JniMarshal_PPL_I):
					return new _JniMarshal_PPL_I (Unsafe.As<_JniMarshal_PPL_I> (dlg).Wrap_JniMarshal_PPL_I);
				case nameof (_JniMarshal_PPL_J):
					return new _JniMarshal_PPL_J (Unsafe.As<_JniMarshal_PPL_J> (dlg).Wrap_JniMarshal_PPL_J);
				case nameof (_JniMarshal_PPL_L):
					return new _JniMarshal_PPL_L (Unsafe.As<_JniMarshal_PPL_L> (dlg).Wrap_JniMarshal_PPL_L);
				case nameof (_JniMarshal_PPL_S):
					return new _JniMarshal_PPL_S (Unsafe.As<_JniMarshal_PPL_S> (dlg).Wrap_JniMarshal_PPL_S);
				case nameof (_JniMarshal_PPL_V):
					return new _JniMarshal_PPL_V (Unsafe.As<_JniMarshal_PPL_V> (dlg).Wrap_JniMarshal_PPL_V);
				case nameof (_JniMarshal_PPL_Z):
					return new _JniMarshal_PPL_Z (Unsafe.As<_JniMarshal_PPL_Z> (dlg).Wrap_JniMarshal_PPL_Z);
				case nameof (_JniMarshal_PPLB_B):
					return new _JniMarshal_PPLB_B (Unsafe.As<_JniMarshal_PPLB_B> (dlg).Wrap_JniMarshal_PPLB_B);
				case nameof (_JniMarshal_PPLB_L):
					return new _JniMarshal_PPLB_L (Unsafe.As<_JniMarshal_PPLB_L> (dlg).Wrap_JniMarshal_PPLB_L);
				case nameof (_JniMarshal_PPLB_V):
					return new _JniMarshal_PPLB_V (Unsafe.As<_JniMarshal_PPLB_V> (dlg).Wrap_JniMarshal_PPLB_V);
				case nameof (_JniMarshal_PPLBB_V):
					return new _JniMarshal_PPLBB_V (Unsafe.As<_JniMarshal_PPLBB_V> (dlg).Wrap_JniMarshal_PPLBB_V);
				case nameof (_JniMarshal_PPLBBI_V):
					return new _JniMarshal_PPLBBI_V (Unsafe.As<_JniMarshal_PPLBBI_V> (dlg).Wrap_JniMarshal_PPLBBI_V);
				case nameof (_JniMarshal_PPLBBL_V):
					return new _JniMarshal_PPLBBL_V (Unsafe.As<_JniMarshal_PPLBBL_V> (dlg).Wrap_JniMarshal_PPLBBL_V);
				case nameof (_JniMarshal_PPLBC_V):
					return new _JniMarshal_PPLBC_V (Unsafe.As<_JniMarshal_PPLBC_V> (dlg).Wrap_JniMarshal_PPLBC_V);
				case nameof (_JniMarshal_PPLBD_V):
					return new _JniMarshal_PPLBD_V (Unsafe.As<_JniMarshal_PPLBD_V> (dlg).Wrap_JniMarshal_PPLBD_V);
				case nameof (_JniMarshal_PPLBF_V):
					return new _JniMarshal_PPLBF_V (Unsafe.As<_JniMarshal_PPLBF_V> (dlg).Wrap_JniMarshal_PPLBF_V);
				case nameof (_JniMarshal_PPLBI_V):
					return new _JniMarshal_PPLBI_V (Unsafe.As<_JniMarshal_PPLBI_V> (dlg).Wrap_JniMarshal_PPLBI_V);
				case nameof (_JniMarshal_PPLBJ_V):
					return new _JniMarshal_PPLBJ_V (Unsafe.As<_JniMarshal_PPLBJ_V> (dlg).Wrap_JniMarshal_PPLBJ_V);
				case nameof (_JniMarshal_PPLBL_V):
					return new _JniMarshal_PPLBL_V (Unsafe.As<_JniMarshal_PPLBL_V> (dlg).Wrap_JniMarshal_PPLBL_V);
				case nameof (_JniMarshal_PPLBS_V):
					return new _JniMarshal_PPLBS_V (Unsafe.As<_JniMarshal_PPLBS_V> (dlg).Wrap_JniMarshal_PPLBS_V);
				case nameof (_JniMarshal_PPLBZ_V):
					return new _JniMarshal_PPLBZ_V (Unsafe.As<_JniMarshal_PPLBZ_V> (dlg).Wrap_JniMarshal_PPLBZ_V);
				case nameof (_JniMarshal_PPLC_C):
					return new _JniMarshal_PPLC_C (Unsafe.As<_JniMarshal_PPLC_C> (dlg).Wrap_JniMarshal_PPLC_C);
				case nameof (_JniMarshal_PPLC_L):
					return new _JniMarshal_PPLC_L (Unsafe.As<_JniMarshal_PPLC_L> (dlg).Wrap_JniMarshal_PPLC_L);
				case nameof (_JniMarshal_PPLC_V):
					return new _JniMarshal_PPLC_V (Unsafe.As<_JniMarshal_PPLC_V> (dlg).Wrap_JniMarshal_PPLC_V);
				case nameof (_JniMarshal_PPLCB_V):
					return new _JniMarshal_PPLCB_V (Unsafe.As<_JniMarshal_PPLCB_V> (dlg).Wrap_JniMarshal_PPLCB_V);
				case nameof (_JniMarshal_PPLCC_V):
					return new _JniMarshal_PPLCC_V (Unsafe.As<_JniMarshal_PPLCC_V> (dlg).Wrap_JniMarshal_PPLCC_V);
				case nameof (_JniMarshal_PPLCD_V):
					return new _JniMarshal_PPLCD_V (Unsafe.As<_JniMarshal_PPLCD_V> (dlg).Wrap_JniMarshal_PPLCD_V);
				case nameof (_JniMarshal_PPLCF_V):
					return new _JniMarshal_PPLCF_V (Unsafe.As<_JniMarshal_PPLCF_V> (dlg).Wrap_JniMarshal_PPLCF_V);
				case nameof (_JniMarshal_PPLCI_V):
					return new _JniMarshal_PPLCI_V (Unsafe.As<_JniMarshal_PPLCI_V> (dlg).Wrap_JniMarshal_PPLCI_V);
				case nameof (_JniMarshal_PPLCJ_V):
					return new _JniMarshal_PPLCJ_V (Unsafe.As<_JniMarshal_PPLCJ_V> (dlg).Wrap_JniMarshal_PPLCJ_V);
				case nameof (_JniMarshal_PPLCL_V):
					return new _JniMarshal_PPLCL_V (Unsafe.As<_JniMarshal_PPLCL_V> (dlg).Wrap_JniMarshal_PPLCL_V);
				case nameof (_JniMarshal_PPLCS_V):
					return new _JniMarshal_PPLCS_V (Unsafe.As<_JniMarshal_PPLCS_V> (dlg).Wrap_JniMarshal_PPLCS_V);
				case nameof (_JniMarshal_PPLCZ_V):
					return new _JniMarshal_PPLCZ_V (Unsafe.As<_JniMarshal_PPLCZ_V> (dlg).Wrap_JniMarshal_PPLCZ_V);
				case nameof (_JniMarshal_PPLD_D):
					return new _JniMarshal_PPLD_D (Unsafe.As<_JniMarshal_PPLD_D> (dlg).Wrap_JniMarshal_PPLD_D);
				case nameof (_JniMarshal_PPLD_L):
					return new _JniMarshal_PPLD_L (Unsafe.As<_JniMarshal_PPLD_L> (dlg).Wrap_JniMarshal_PPLD_L);
				case nameof (_JniMarshal_PPLD_V):
					return new _JniMarshal_PPLD_V (Unsafe.As<_JniMarshal_PPLD_V> (dlg).Wrap_JniMarshal_PPLD_V);
				case nameof (_JniMarshal_PPLD_Z):
					return new _JniMarshal_PPLD_Z (Unsafe.As<_JniMarshal_PPLD_Z> (dlg).Wrap_JniMarshal_PPLD_Z);
				case nameof (_JniMarshal_PPLDB_V):
					return new _JniMarshal_PPLDB_V (Unsafe.As<_JniMarshal_PPLDB_V> (dlg).Wrap_JniMarshal_PPLDB_V);
				case nameof (_JniMarshal_PPLDC_V):
					return new _JniMarshal_PPLDC_V (Unsafe.As<_JniMarshal_PPLDC_V> (dlg).Wrap_JniMarshal_PPLDC_V);
				case nameof (_JniMarshal_PPLDD_V):
					return new _JniMarshal_PPLDD_V (Unsafe.As<_JniMarshal_PPLDD_V> (dlg).Wrap_JniMarshal_PPLDD_V);
				case nameof (_JniMarshal_PPLDF_V):
					return new _JniMarshal_PPLDF_V (Unsafe.As<_JniMarshal_PPLDF_V> (dlg).Wrap_JniMarshal_PPLDF_V);
				case nameof (_JniMarshal_PPLDI_V):
					return new _JniMarshal_PPLDI_V (Unsafe.As<_JniMarshal_PPLDI_V> (dlg).Wrap_JniMarshal_PPLDI_V);
				case nameof (_JniMarshal_PPLDJ_V):
					return new _JniMarshal_PPLDJ_V (Unsafe.As<_JniMarshal_PPLDJ_V> (dlg).Wrap_JniMarshal_PPLDJ_V);
				case nameof (_JniMarshal_PPLDL_L):
					return new _JniMarshal_PPLDL_L (Unsafe.As<_JniMarshal_PPLDL_L> (dlg).Wrap_JniMarshal_PPLDL_L);
				case nameof (_JniMarshal_PPLDL_V):
					return new _JniMarshal_PPLDL_V (Unsafe.As<_JniMarshal_PPLDL_V> (dlg).Wrap_JniMarshal_PPLDL_V);
				case nameof (_JniMarshal_PPLDS_V):
					return new _JniMarshal_PPLDS_V (Unsafe.As<_JniMarshal_PPLDS_V> (dlg).Wrap_JniMarshal_PPLDS_V);
				case nameof (_JniMarshal_PPLDZ_V):
					return new _JniMarshal_PPLDZ_V (Unsafe.As<_JniMarshal_PPLDZ_V> (dlg).Wrap_JniMarshal_PPLDZ_V);
				case nameof (_JniMarshal_PPLF_F):
					return new _JniMarshal_PPLF_F (Unsafe.As<_JniMarshal_PPLF_F> (dlg).Wrap_JniMarshal_PPLF_F);
				case nameof (_JniMarshal_PPLF_L):
					return new _JniMarshal_PPLF_L (Unsafe.As<_JniMarshal_PPLF_L> (dlg).Wrap_JniMarshal_PPLF_L);
				case nameof (_JniMarshal_PPLF_V):
					return new _JniMarshal_PPLF_V (Unsafe.As<_JniMarshal_PPLF_V> (dlg).Wrap_JniMarshal_PPLF_V);
				case nameof (_JniMarshal_PPLFB_V):
					return new _JniMarshal_PPLFB_V (Unsafe.As<_JniMarshal_PPLFB_V> (dlg).Wrap_JniMarshal_PPLFB_V);
				case nameof (_JniMarshal_PPLFC_V):
					return new _JniMarshal_PPLFC_V (Unsafe.As<_JniMarshal_PPLFC_V> (dlg).Wrap_JniMarshal_PPLFC_V);
				case nameof (_JniMarshal_PPLFD_V):
					return new _JniMarshal_PPLFD_V (Unsafe.As<_JniMarshal_PPLFD_V> (dlg).Wrap_JniMarshal_PPLFD_V);
				case nameof (_JniMarshal_PPLFDD_V):
					return new _JniMarshal_PPLFDD_V (Unsafe.As<_JniMarshal_PPLFDD_V> (dlg).Wrap_JniMarshal_PPLFDD_V);
				case nameof (_JniMarshal_PPLFF_L):
					return new _JniMarshal_PPLFF_L (Unsafe.As<_JniMarshal_PPLFF_L> (dlg).Wrap_JniMarshal_PPLFF_L);
				case nameof (_JniMarshal_PPLFF_V):
					return new _JniMarshal_PPLFF_V (Unsafe.As<_JniMarshal_PPLFF_V> (dlg).Wrap_JniMarshal_PPLFF_V);
				case nameof (_JniMarshal_PPLFF_Z):
					return new _JniMarshal_PPLFF_Z (Unsafe.As<_JniMarshal_PPLFF_Z> (dlg).Wrap_JniMarshal_PPLFF_Z);
				case nameof (_JniMarshal_PPLFFF_V):
					return new _JniMarshal_PPLFFF_V (Unsafe.As<_JniMarshal_PPLFFF_V> (dlg).Wrap_JniMarshal_PPLFFF_V);
				case nameof (_JniMarshal_PPLFFFF_V):
					return new _JniMarshal_PPLFFFF_V (Unsafe.As<_JniMarshal_PPLFFFF_V> (dlg).Wrap_JniMarshal_PPLFFFF_V);
				case nameof (_JniMarshal_PPLFFL_V):
					return new _JniMarshal_PPLFFL_V (Unsafe.As<_JniMarshal_PPLFFL_V> (dlg).Wrap_JniMarshal_PPLFFL_V);
				case nameof (_JniMarshal_PPLFFLFFL_V):
					return new _JniMarshal_PPLFFLFFL_V (Unsafe.As<_JniMarshal_PPLFFLFFL_V> (dlg).Wrap_JniMarshal_PPLFFLFFL_V);
				case nameof (_JniMarshal_PPLFFLI_V):
					return new _JniMarshal_PPLFFLI_V (Unsafe.As<_JniMarshal_PPLFFLI_V> (dlg).Wrap_JniMarshal_PPLFFLI_V);
				case nameof (_JniMarshal_PPLFFLL_V):
					return new _JniMarshal_PPLFFLL_V (Unsafe.As<_JniMarshal_PPLFFLL_V> (dlg).Wrap_JniMarshal_PPLFFLL_V);
				case nameof (_JniMarshal_PPLFFZ_V):
					return new _JniMarshal_PPLFFZ_V (Unsafe.As<_JniMarshal_PPLFFZ_V> (dlg).Wrap_JniMarshal_PPLFFZ_V);
				case nameof (_JniMarshal_PPLFFZ_Z):
					return new _JniMarshal_PPLFFZ_Z (Unsafe.As<_JniMarshal_PPLFFZ_Z> (dlg).Wrap_JniMarshal_PPLFFZ_Z);
				case nameof (_JniMarshal_PPLFFZL_V):
					return new _JniMarshal_PPLFFZL_V (Unsafe.As<_JniMarshal_PPLFFZL_V> (dlg).Wrap_JniMarshal_PPLFFZL_V);
				case nameof (_JniMarshal_PPLFI_L):
					return new _JniMarshal_PPLFI_L (Unsafe.As<_JniMarshal_PPLFI_L> (dlg).Wrap_JniMarshal_PPLFI_L);
				case nameof (_JniMarshal_PPLFI_V):
					return new _JniMarshal_PPLFI_V (Unsafe.As<_JniMarshal_PPLFI_V> (dlg).Wrap_JniMarshal_PPLFI_V);
				case nameof (_JniMarshal_PPLFII_L):
					return new _JniMarshal_PPLFII_L (Unsafe.As<_JniMarshal_PPLFII_L> (dlg).Wrap_JniMarshal_PPLFII_L);
				case nameof (_JniMarshal_PPLFJ_V):
					return new _JniMarshal_PPLFJ_V (Unsafe.As<_JniMarshal_PPLFJ_V> (dlg).Wrap_JniMarshal_PPLFJ_V);
				case nameof (_JniMarshal_PPLFJL_Z):
					return new _JniMarshal_PPLFJL_Z (Unsafe.As<_JniMarshal_PPLFJL_Z> (dlg).Wrap_JniMarshal_PPLFJL_Z);
				case nameof (_JniMarshal_PPLFL_V):
					return new _JniMarshal_PPLFL_V (Unsafe.As<_JniMarshal_PPLFL_V> (dlg).Wrap_JniMarshal_PPLFL_V);
				case nameof (_JniMarshal_PPLFL_Z):
					return new _JniMarshal_PPLFL_Z (Unsafe.As<_JniMarshal_PPLFL_Z> (dlg).Wrap_JniMarshal_PPLFL_Z);
				case nameof (_JniMarshal_PPLFLL_V):
					return new _JniMarshal_PPLFLL_V (Unsafe.As<_JniMarshal_PPLFLL_V> (dlg).Wrap_JniMarshal_PPLFLL_V);
				case nameof (_JniMarshal_PPLFLLL_V):
					return new _JniMarshal_PPLFLLL_V (Unsafe.As<_JniMarshal_PPLFLLL_V> (dlg).Wrap_JniMarshal_PPLFLLL_V);
				case nameof (_JniMarshal_PPLFS_V):
					return new _JniMarshal_PPLFS_V (Unsafe.As<_JniMarshal_PPLFS_V> (dlg).Wrap_JniMarshal_PPLFS_V);
				case nameof (_JniMarshal_PPLFZ_V):
					return new _JniMarshal_PPLFZ_V (Unsafe.As<_JniMarshal_PPLFZ_V> (dlg).Wrap_JniMarshal_PPLFZ_V);
				case nameof (_JniMarshal_PPLI_B):
					return new _JniMarshal_PPLI_B (Unsafe.As<_JniMarshal_PPLI_B> (dlg).Wrap_JniMarshal_PPLI_B);
				case nameof (_JniMarshal_PPLI_C):
					return new _JniMarshal_PPLI_C (Unsafe.As<_JniMarshal_PPLI_C> (dlg).Wrap_JniMarshal_PPLI_C);
				case nameof (_JniMarshal_PPLI_D):
					return new _JniMarshal_PPLI_D (Unsafe.As<_JniMarshal_PPLI_D> (dlg).Wrap_JniMarshal_PPLI_D);
				case nameof (_JniMarshal_PPLI_F):
					return new _JniMarshal_PPLI_F (Unsafe.As<_JniMarshal_PPLI_F> (dlg).Wrap_JniMarshal_PPLI_F);
				case nameof (_JniMarshal_PPLI_I):
					return new _JniMarshal_PPLI_I (Unsafe.As<_JniMarshal_PPLI_I> (dlg).Wrap_JniMarshal_PPLI_I);
				case nameof (_JniMarshal_PPLI_J):
					return new _JniMarshal_PPLI_J (Unsafe.As<_JniMarshal_PPLI_J> (dlg).Wrap_JniMarshal_PPLI_J);
				case nameof (_JniMarshal_PPLI_L):
					return new _JniMarshal_PPLI_L (Unsafe.As<_JniMarshal_PPLI_L> (dlg).Wrap_JniMarshal_PPLI_L);
				case nameof (_JniMarshal_PPLI_S):
					return new _JniMarshal_PPLI_S (Unsafe.As<_JniMarshal_PPLI_S> (dlg).Wrap_JniMarshal_PPLI_S);
				case nameof (_JniMarshal_PPLI_V):
					return new _JniMarshal_PPLI_V (Unsafe.As<_JniMarshal_PPLI_V> (dlg).Wrap_JniMarshal_PPLI_V);
				case nameof (_JniMarshal_PPLI_Z):
					return new _JniMarshal_PPLI_Z (Unsafe.As<_JniMarshal_PPLI_Z> (dlg).Wrap_JniMarshal_PPLI_Z);
				case nameof (_JniMarshal_PPLIB_V):
					return new _JniMarshal_PPLIB_V (Unsafe.As<_JniMarshal_PPLIB_V> (dlg).Wrap_JniMarshal_PPLIB_V);
				case nameof (_JniMarshal_PPLIC_V):
					return new _JniMarshal_PPLIC_V (Unsafe.As<_JniMarshal_PPLIC_V> (dlg).Wrap_JniMarshal_PPLIC_V);
				case nameof (_JniMarshal_PPLICIZZLL_I):
					return new _JniMarshal_PPLICIZZLL_I (Unsafe.As<_JniMarshal_PPLICIZZLL_I> (dlg).Wrap_JniMarshal_PPLICIZZLL_I);
				case nameof (_JniMarshal_PPLID_V):
					return new _JniMarshal_PPLID_V (Unsafe.As<_JniMarshal_PPLID_V> (dlg).Wrap_JniMarshal_PPLID_V);
				case nameof (_JniMarshal_PPLIF_V):
					return new _JniMarshal_PPLIF_V (Unsafe.As<_JniMarshal_PPLIF_V> (dlg).Wrap_JniMarshal_PPLIF_V);
				case nameof (_JniMarshal_PPLIFF_F):
					return new _JniMarshal_PPLIFF_F (Unsafe.As<_JniMarshal_PPLIFF_F> (dlg).Wrap_JniMarshal_PPLIFF_F);
				case nameof (_JniMarshal_PPLIFF_J):
					return new _JniMarshal_PPLIFF_J (Unsafe.As<_JniMarshal_PPLIFF_J> (dlg).Wrap_JniMarshal_PPLIFF_J);
				case nameof (_JniMarshal_PPLII_F):
					return new _JniMarshal_PPLII_F (Unsafe.As<_JniMarshal_PPLII_F> (dlg).Wrap_JniMarshal_PPLII_F);
				case nameof (_JniMarshal_PPLII_I):
					return new _JniMarshal_PPLII_I (Unsafe.As<_JniMarshal_PPLII_I> (dlg).Wrap_JniMarshal_PPLII_I);
				case nameof (_JniMarshal_PPLII_J):
					return new _JniMarshal_PPLII_J (Unsafe.As<_JniMarshal_PPLII_J> (dlg).Wrap_JniMarshal_PPLII_J);
				case nameof (_JniMarshal_PPLII_L):
					return new _JniMarshal_PPLII_L (Unsafe.As<_JniMarshal_PPLII_L> (dlg).Wrap_JniMarshal_PPLII_L);
				case nameof (_JniMarshal_PPLII_V):
					return new _JniMarshal_PPLII_V (Unsafe.As<_JniMarshal_PPLII_V> (dlg).Wrap_JniMarshal_PPLII_V);
				case nameof (_JniMarshal_PPLII_Z):
					return new _JniMarshal_PPLII_Z (Unsafe.As<_JniMarshal_PPLII_Z> (dlg).Wrap_JniMarshal_PPLII_Z);
				case nameof (_JniMarshal_PPLIIF_V):
					return new _JniMarshal_PPLIIF_V (Unsafe.As<_JniMarshal_PPLIIF_V> (dlg).Wrap_JniMarshal_PPLIIF_V);
				case nameof (_JniMarshal_PPLIIFF_V):
					return new _JniMarshal_PPLIIFF_V (Unsafe.As<_JniMarshal_PPLIIFF_V> (dlg).Wrap_JniMarshal_PPLIIFF_V);
				case nameof (_JniMarshal_PPLIIFF_Z):
					return new _JniMarshal_PPLIIFF_Z (Unsafe.As<_JniMarshal_PPLIIFF_Z> (dlg).Wrap_JniMarshal_PPLIIFF_Z);
				case nameof (_JniMarshal_PPLIIFFFFFFFFFFFFFFFF_L):
					return new _JniMarshal_PPLIIFFFFFFFFFFFFFFFF_L (Unsafe.As<_JniMarshal_PPLIIFFFFFFFFFFFFFFFF_L> (dlg).Wrap_JniMarshal_PPLIIFFFFFFFFFFFFFFFF_L);
				case nameof (_JniMarshal_PPLIIFFIIZL_V):
					return new _JniMarshal_PPLIIFFIIZL_V (Unsafe.As<_JniMarshal_PPLIIFFIIZL_V> (dlg).Wrap_JniMarshal_PPLIIFFIIZL_V);
				case nameof (_JniMarshal_PPLIIFFL_V):
					return new _JniMarshal_PPLIIFFL_V (Unsafe.As<_JniMarshal_PPLIIFFL_V> (dlg).Wrap_JniMarshal_PPLIIFFL_V);
				case nameof (_JniMarshal_PPLIIFL_I):
					return new _JniMarshal_PPLIIFL_I (Unsafe.As<_JniMarshal_PPLIIFL_I> (dlg).Wrap_JniMarshal_PPLIIFL_I);
				case nameof (_JniMarshal_PPLIII_I):
					return new _JniMarshal_PPLIII_I (Unsafe.As<_JniMarshal_PPLIII_I> (dlg).Wrap_JniMarshal_PPLIII_I);
				case nameof (_JniMarshal_PPLIII_L):
					return new _JniMarshal_PPLIII_L (Unsafe.As<_JniMarshal_PPLIII_L> (dlg).Wrap_JniMarshal_PPLIII_L);
				case nameof (_JniMarshal_PPLIII_V):
					return new _JniMarshal_PPLIII_V (Unsafe.As<_JniMarshal_PPLIII_V> (dlg).Wrap_JniMarshal_PPLIII_V);
				case nameof (_JniMarshal_PPLIII_Z):
					return new _JniMarshal_PPLIII_Z (Unsafe.As<_JniMarshal_PPLIII_Z> (dlg).Wrap_JniMarshal_PPLIII_Z);
				case nameof (_JniMarshal_PPLIIIF_V):
					return new _JniMarshal_PPLIIIF_V (Unsafe.As<_JniMarshal_PPLIIIF_V> (dlg).Wrap_JniMarshal_PPLIIIF_V);
				case nameof (_JniMarshal_PPLIIII_L):
					return new _JniMarshal_PPLIIII_L (Unsafe.As<_JniMarshal_PPLIIII_L> (dlg).Wrap_JniMarshal_PPLIIII_L);
				case nameof (_JniMarshal_PPLIIII_V):
					return new _JniMarshal_PPLIIII_V (Unsafe.As<_JniMarshal_PPLIIII_V> (dlg).Wrap_JniMarshal_PPLIIII_V);
				case nameof (_JniMarshal_PPLIIII_Z):
					return new _JniMarshal_PPLIIII_Z (Unsafe.As<_JniMarshal_PPLIIII_Z> (dlg).Wrap_JniMarshal_PPLIIII_Z);
				case nameof (_JniMarshal_PPLIIIIFFZL_V):
					return new _JniMarshal_PPLIIIIFFZL_V (Unsafe.As<_JniMarshal_PPLIIIIFFZL_V> (dlg).Wrap_JniMarshal_PPLIIIIFFZL_V);
				case nameof (_JniMarshal_PPLIIIII_V):
					return new _JniMarshal_PPLIIIII_V (Unsafe.As<_JniMarshal_PPLIIIII_V> (dlg).Wrap_JniMarshal_PPLIIIII_V);
				case nameof (_JniMarshal_PPLIIIIIIII_V):
					return new _JniMarshal_PPLIIIIIIII_V (Unsafe.As<_JniMarshal_PPLIIIIIIII_V> (dlg).Wrap_JniMarshal_PPLIIIIIIII_V);
				case nameof (_JniMarshal_PPLIIIIIIIII_J):
					return new _JniMarshal_PPLIIIIIIIII_J (Unsafe.As<_JniMarshal_PPLIIIIIIIII_J> (dlg).Wrap_JniMarshal_PPLIIIIIIIII_J);
				case nameof (_JniMarshal_PPLIIIIIIL_J):
					return new _JniMarshal_PPLIIIIIIL_J (Unsafe.As<_JniMarshal_PPLIIIIIIL_J> (dlg).Wrap_JniMarshal_PPLIIIIIIL_J);
				case nameof (_JniMarshal_PPLIIIIIIZL_V):
					return new _JniMarshal_PPLIIIIIIZL_V (Unsafe.As<_JniMarshal_PPLIIIIIIZL_V> (dlg).Wrap_JniMarshal_PPLIIIIIIZL_V);
				case nameof (_JniMarshal_PPLIIIIIL_V):
					return new _JniMarshal_PPLIIIIIL_V (Unsafe.As<_JniMarshal_PPLIIIIIL_V> (dlg).Wrap_JniMarshal_PPLIIIIIL_V);
				case nameof (_JniMarshal_PPLIIIIILI_L):
					return new _JniMarshal_PPLIIIIILI_L (Unsafe.As<_JniMarshal_PPLIIIIILI_L> (dlg).Wrap_JniMarshal_PPLIIIIILI_L);
				case nameof (_JniMarshal_PPLIIIIL_V):
					return new _JniMarshal_PPLIIIIL_V (Unsafe.As<_JniMarshal_PPLIIIIL_V> (dlg).Wrap_JniMarshal_PPLIIIIL_V);
				case nameof (_JniMarshal_PPLIIIILL_V):
					return new _JniMarshal_PPLIIIILL_V (Unsafe.As<_JniMarshal_PPLIIIILL_V> (dlg).Wrap_JniMarshal_PPLIIIILL_V);
				case nameof (_JniMarshal_PPLIIIIZF_I):
					return new _JniMarshal_PPLIIIIZF_I (Unsafe.As<_JniMarshal_PPLIIIIZF_I> (dlg).Wrap_JniMarshal_PPLIIIIZF_I);
				case nameof (_JniMarshal_PPLIIIIZI_F):
					return new _JniMarshal_PPLIIIIZI_F (Unsafe.As<_JniMarshal_PPLIIIIZI_F> (dlg).Wrap_JniMarshal_PPLIIIIZI_F);
				case nameof (_JniMarshal_PPLIIIIZILI_F):
					return new _JniMarshal_PPLIIIIZILI_F (Unsafe.As<_JniMarshal_PPLIIIIZILI_F> (dlg).Wrap_JniMarshal_PPLIIIIZILI_F);
				case nameof (_JniMarshal_PPLIIIIZL_V):
					return new _JniMarshal_PPLIIIIZL_V (Unsafe.As<_JniMarshal_PPLIIIIZL_V> (dlg).Wrap_JniMarshal_PPLIIIIZL_V);
				case nameof (_JniMarshal_PPLIIIIZLI_F):
					return new _JniMarshal_PPLIIIIZLI_F (Unsafe.As<_JniMarshal_PPLIIIIZLI_F> (dlg).Wrap_JniMarshal_PPLIIIIZLI_F);
				case nameof (_JniMarshal_PPLIIIJ_I):
					return new _JniMarshal_PPLIIIJ_I (Unsafe.As<_JniMarshal_PPLIIIJ_I> (dlg).Wrap_JniMarshal_PPLIIIJ_I);
				case nameof (_JniMarshal_PPLIIIL_L):
					return new _JniMarshal_PPLIIIL_L (Unsafe.As<_JniMarshal_PPLIIIL_L> (dlg).Wrap_JniMarshal_PPLIIIL_L);
				case nameof (_JniMarshal_PPLIIIL_V):
					return new _JniMarshal_PPLIIIL_V (Unsafe.As<_JniMarshal_PPLIIIL_V> (dlg).Wrap_JniMarshal_PPLIIIL_V);
				case nameof (_JniMarshal_PPLIIILZ_L):
					return new _JniMarshal_PPLIIILZ_L (Unsafe.As<_JniMarshal_PPLIIILZ_L> (dlg).Wrap_JniMarshal_PPLIIILZ_L);
				case nameof (_JniMarshal_PPLIIIZ_I):
					return new _JniMarshal_PPLIIIZ_I (Unsafe.As<_JniMarshal_PPLIIIZ_I> (dlg).Wrap_JniMarshal_PPLIIIZ_I);
				case nameof (_JniMarshal_PPLIIIZ_L):
					return new _JniMarshal_PPLIIIZ_L (Unsafe.As<_JniMarshal_PPLIIIZ_L> (dlg).Wrap_JniMarshal_PPLIIIZ_L);
				case nameof (_JniMarshal_PPLIIIZZ_L):
					return new _JniMarshal_PPLIIIZZ_L (Unsafe.As<_JniMarshal_PPLIIIZZ_L> (dlg).Wrap_JniMarshal_PPLIIIZZ_L);
				case nameof (_JniMarshal_PPLIIIZZL_L):
					return new _JniMarshal_PPLIIIZZL_L (Unsafe.As<_JniMarshal_PPLIIIZZL_L> (dlg).Wrap_JniMarshal_PPLIIIZZL_L);
				case nameof (_JniMarshal_PPLIIJ_I):
					return new _JniMarshal_PPLIIJ_I (Unsafe.As<_JniMarshal_PPLIIJ_I> (dlg).Wrap_JniMarshal_PPLIIJ_I);
				case nameof (_JniMarshal_PPLIIJ_V):
					return new _JniMarshal_PPLIIJ_V (Unsafe.As<_JniMarshal_PPLIIJ_V> (dlg).Wrap_JniMarshal_PPLIIJ_V);
				case nameof (_JniMarshal_PPLIIJL_L):
					return new _JniMarshal_PPLIIJL_L (Unsafe.As<_JniMarshal_PPLIIJL_L> (dlg).Wrap_JniMarshal_PPLIIJL_L);
				case nameof (_JniMarshal_PPLIIJLLL_V):
					return new _JniMarshal_PPLIIJLLL_V (Unsafe.As<_JniMarshal_PPLIIJLLL_V> (dlg).Wrap_JniMarshal_PPLIIJLLL_V);
				case nameof (_JniMarshal_PPLIIL_I):
					return new _JniMarshal_PPLIIL_I (Unsafe.As<_JniMarshal_PPLIIL_I> (dlg).Wrap_JniMarshal_PPLIIL_I);
				case nameof (_JniMarshal_PPLIIL_L):
					return new _JniMarshal_PPLIIL_L (Unsafe.As<_JniMarshal_PPLIIL_L> (dlg).Wrap_JniMarshal_PPLIIL_L);
				case nameof (_JniMarshal_PPLIIL_V):
					return new _JniMarshal_PPLIIL_V (Unsafe.As<_JniMarshal_PPLIIL_V> (dlg).Wrap_JniMarshal_PPLIIL_V);
				case nameof (_JniMarshal_PPLIIL_Z):
					return new _JniMarshal_PPLIIL_Z (Unsafe.As<_JniMarshal_PPLIIL_Z> (dlg).Wrap_JniMarshal_PPLIIL_Z);
				case nameof (_JniMarshal_PPLIILFFL_V):
					return new _JniMarshal_PPLIILFFL_V (Unsafe.As<_JniMarshal_PPLIILFFL_V> (dlg).Wrap_JniMarshal_PPLIILFFL_V);
				case nameof (_JniMarshal_PPLIILI_I):
					return new _JniMarshal_PPLIILI_I (Unsafe.As<_JniMarshal_PPLIILI_I> (dlg).Wrap_JniMarshal_PPLIILI_I);
				case nameof (_JniMarshal_PPLIILI_V):
					return new _JniMarshal_PPLIILI_V (Unsafe.As<_JniMarshal_PPLIILI_V> (dlg).Wrap_JniMarshal_PPLIILI_V);
				case nameof (_JniMarshal_PPLIILII_L):
					return new _JniMarshal_PPLIILII_L (Unsafe.As<_JniMarshal_PPLIILII_L> (dlg).Wrap_JniMarshal_PPLIILII_L);
				case nameof (_JniMarshal_PPLIILIL_L):
					return new _JniMarshal_PPLIILIL_L (Unsafe.As<_JniMarshal_PPLIILIL_L> (dlg).Wrap_JniMarshal_PPLIILIL_L);
				case nameof (_JniMarshal_PPLIILILIL_V):
					return new _JniMarshal_PPLIILILIL_V (Unsafe.As<_JniMarshal_PPLIILILIL_V> (dlg).Wrap_JniMarshal_PPLIILILIL_V);
				case nameof (_JniMarshal_PPLIILILJJ_V):
					return new _JniMarshal_PPLIILILJJ_V (Unsafe.As<_JniMarshal_PPLIILILJJ_V> (dlg).Wrap_JniMarshal_PPLIILILJJ_V);
				case nameof (_JniMarshal_PPLIILILJJLZ_V):
					return new _JniMarshal_PPLIILILJJLZ_V (Unsafe.As<_JniMarshal_PPLIILILJJLZ_V> (dlg).Wrap_JniMarshal_PPLIILILJJLZ_V);
				case nameof (_JniMarshal_PPLIILL_I):
					return new _JniMarshal_PPLIILL_I (Unsafe.As<_JniMarshal_PPLIILL_I> (dlg).Wrap_JniMarshal_PPLIILL_I);
				case nameof (_JniMarshal_PPLIILL_L):
					return new _JniMarshal_PPLIILL_L (Unsafe.As<_JniMarshal_PPLIILL_L> (dlg).Wrap_JniMarshal_PPLIILL_L);
				case nameof (_JniMarshal_PPLIILL_V):
					return new _JniMarshal_PPLIILL_V (Unsafe.As<_JniMarshal_PPLIILL_V> (dlg).Wrap_JniMarshal_PPLIILL_V);
				case nameof (_JniMarshal_PPLIILLL_V):
					return new _JniMarshal_PPLIILLL_V (Unsafe.As<_JniMarshal_PPLIILLL_V> (dlg).Wrap_JniMarshal_PPLIILLL_V);
				case nameof (_JniMarshal_PPLIIS_V):
					return new _JniMarshal_PPLIIS_V (Unsafe.As<_JniMarshal_PPLIIS_V> (dlg).Wrap_JniMarshal_PPLIIS_V);
				case nameof (_JniMarshal_PPLIIZ_Z):
					return new _JniMarshal_PPLIIZ_Z (Unsafe.As<_JniMarshal_PPLIIZ_Z> (dlg).Wrap_JniMarshal_PPLIIZ_Z);
				case nameof (_JniMarshal_PPLIIZFL_I):
					return new _JniMarshal_PPLIIZFL_I (Unsafe.As<_JniMarshal_PPLIIZFL_I> (dlg).Wrap_JniMarshal_PPLIIZFL_I);
				case nameof (_JniMarshal_PPLIIZII_I):
					return new _JniMarshal_PPLIIZII_I (Unsafe.As<_JniMarshal_PPLIIZII_I> (dlg).Wrap_JniMarshal_PPLIIZII_I);
				case nameof (_JniMarshal_PPLIIZZ_Z):
					return new _JniMarshal_PPLIIZZ_Z (Unsafe.As<_JniMarshal_PPLIIZZ_Z> (dlg).Wrap_JniMarshal_PPLIIZZ_Z);
				case nameof (_JniMarshal_PPLIJ_I):
					return new _JniMarshal_PPLIJ_I (Unsafe.As<_JniMarshal_PPLIJ_I> (dlg).Wrap_JniMarshal_PPLIJ_I);
				case nameof (_JniMarshal_PPLIJ_L):
					return new _JniMarshal_PPLIJ_L (Unsafe.As<_JniMarshal_PPLIJ_L> (dlg).Wrap_JniMarshal_PPLIJ_L);
				case nameof (_JniMarshal_PPLIJ_V):
					return new _JniMarshal_PPLIJ_V (Unsafe.As<_JniMarshal_PPLIJ_V> (dlg).Wrap_JniMarshal_PPLIJ_V);
				case nameof (_JniMarshal_PPLIJ_Z):
					return new _JniMarshal_PPLIJ_Z (Unsafe.As<_JniMarshal_PPLIJ_Z> (dlg).Wrap_JniMarshal_PPLIJ_Z);
				case nameof (_JniMarshal_PPLIJJ_V):
					return new _JniMarshal_PPLIJJ_V (Unsafe.As<_JniMarshal_PPLIJJ_V> (dlg).Wrap_JniMarshal_PPLIJJ_V);
				case nameof (_JniMarshal_PPLIJL_L):
					return new _JniMarshal_PPLIJL_L (Unsafe.As<_JniMarshal_PPLIJL_L> (dlg).Wrap_JniMarshal_PPLIJL_L);
				case nameof (_JniMarshal_PPLIJZ_V):
					return new _JniMarshal_PPLIJZ_V (Unsafe.As<_JniMarshal_PPLIJZ_V> (dlg).Wrap_JniMarshal_PPLIJZ_V);
				case nameof (_JniMarshal_PPLIL_I):
					return new _JniMarshal_PPLIL_I (Unsafe.As<_JniMarshal_PPLIL_I> (dlg).Wrap_JniMarshal_PPLIL_I);
				case nameof (_JniMarshal_PPLIL_J):
					return new _JniMarshal_PPLIL_J (Unsafe.As<_JniMarshal_PPLIL_J> (dlg).Wrap_JniMarshal_PPLIL_J);
				case nameof (_JniMarshal_PPLIL_L):
					return new _JniMarshal_PPLIL_L (Unsafe.As<_JniMarshal_PPLIL_L> (dlg).Wrap_JniMarshal_PPLIL_L);
				case nameof (_JniMarshal_PPLIL_V):
					return new _JniMarshal_PPLIL_V (Unsafe.As<_JniMarshal_PPLIL_V> (dlg).Wrap_JniMarshal_PPLIL_V);
				case nameof (_JniMarshal_PPLIL_Z):
					return new _JniMarshal_PPLIL_Z (Unsafe.As<_JniMarshal_PPLIL_Z> (dlg).Wrap_JniMarshal_PPLIL_Z);
				case nameof (_JniMarshal_PPLILI_I):
					return new _JniMarshal_PPLILI_I (Unsafe.As<_JniMarshal_PPLILI_I> (dlg).Wrap_JniMarshal_PPLILI_I);
				case nameof (_JniMarshal_PPLILI_L):
					return new _JniMarshal_PPLILI_L (Unsafe.As<_JniMarshal_PPLILI_L> (dlg).Wrap_JniMarshal_PPLILI_L);
				case nameof (_JniMarshal_PPLILI_V):
					return new _JniMarshal_PPLILI_V (Unsafe.As<_JniMarshal_PPLILI_V> (dlg).Wrap_JniMarshal_PPLILI_V);
				case nameof (_JniMarshal_PPLILI_Z):
					return new _JniMarshal_PPLILI_Z (Unsafe.As<_JniMarshal_PPLILI_Z> (dlg).Wrap_JniMarshal_PPLILI_Z);
				case nameof (_JniMarshal_PPLILII_V):
					return new _JniMarshal_PPLILII_V (Unsafe.As<_JniMarshal_PPLILII_V> (dlg).Wrap_JniMarshal_PPLILII_V);
				case nameof (_JniMarshal_PPLILII_Z):
					return new _JniMarshal_PPLILII_Z (Unsafe.As<_JniMarshal_PPLILII_Z> (dlg).Wrap_JniMarshal_PPLILII_Z);
				case nameof (_JniMarshal_PPLILIII_I):
					return new _JniMarshal_PPLILIII_I (Unsafe.As<_JniMarshal_PPLILIII_I> (dlg).Wrap_JniMarshal_PPLILIII_I);
				case nameof (_JniMarshal_PPLILIII_V):
					return new _JniMarshal_PPLILIII_V (Unsafe.As<_JniMarshal_PPLILIII_V> (dlg).Wrap_JniMarshal_PPLILIII_V);
				case nameof (_JniMarshal_PPLILIIIL_V):
					return new _JniMarshal_PPLILIIIL_V (Unsafe.As<_JniMarshal_PPLILIIIL_V> (dlg).Wrap_JniMarshal_PPLILIIIL_V);
				case nameof (_JniMarshal_PPLILIIL_V):
					return new _JniMarshal_PPLILIIL_V (Unsafe.As<_JniMarshal_PPLILIIL_V> (dlg).Wrap_JniMarshal_PPLILIIL_V);
				case nameof (_JniMarshal_PPLILIILL_V):
					return new _JniMarshal_PPLILIILL_V (Unsafe.As<_JniMarshal_PPLILIILL_V> (dlg).Wrap_JniMarshal_PPLILIILL_V);
				case nameof (_JniMarshal_PPLILIJ_V):
					return new _JniMarshal_PPLILIJ_V (Unsafe.As<_JniMarshal_PPLILIJ_V> (dlg).Wrap_JniMarshal_PPLILIJ_V);
				case nameof (_JniMarshal_PPLILIL_V):
					return new _JniMarshal_PPLILIL_V (Unsafe.As<_JniMarshal_PPLILIL_V> (dlg).Wrap_JniMarshal_PPLILIL_V);
				case nameof (_JniMarshal_PPLILILILILIIL_V):
					return new _JniMarshal_PPLILILILILIIL_V (Unsafe.As<_JniMarshal_PPLILILILILIIL_V> (dlg).Wrap_JniMarshal_PPLILILILILIIL_V);
				case nameof (_JniMarshal_PPLILIZL_L):
					return new _JniMarshal_PPLILIZL_L (Unsafe.As<_JniMarshal_PPLILIZL_L> (dlg).Wrap_JniMarshal_PPLILIZL_L);
				case nameof (_JniMarshal_PPLILJ_V):
					return new _JniMarshal_PPLILJ_V (Unsafe.As<_JniMarshal_PPLILJ_V> (dlg).Wrap_JniMarshal_PPLILJ_V);
				case nameof (_JniMarshal_PPLILL_I):
					return new _JniMarshal_PPLILL_I (Unsafe.As<_JniMarshal_PPLILL_I> (dlg).Wrap_JniMarshal_PPLILL_I);
				case nameof (_JniMarshal_PPLILL_L):
					return new _JniMarshal_PPLILL_L (Unsafe.As<_JniMarshal_PPLILL_L> (dlg).Wrap_JniMarshal_PPLILL_L);
				case nameof (_JniMarshal_PPLILL_V):
					return new _JniMarshal_PPLILL_V (Unsafe.As<_JniMarshal_PPLILL_V> (dlg).Wrap_JniMarshal_PPLILL_V);
				case nameof (_JniMarshal_PPLILL_Z):
					return new _JniMarshal_PPLILL_Z (Unsafe.As<_JniMarshal_PPLILL_Z> (dlg).Wrap_JniMarshal_PPLILL_Z);
				case nameof (_JniMarshal_PPLILLIZII_V):
					return new _JniMarshal_PPLILLIZII_V (Unsafe.As<_JniMarshal_PPLILLIZII_V> (dlg).Wrap_JniMarshal_PPLILLIZII_V);
				case nameof (_JniMarshal_PPLILLL_I):
					return new _JniMarshal_PPLILLL_I (Unsafe.As<_JniMarshal_PPLILLL_I> (dlg).Wrap_JniMarshal_PPLILLL_I);
				case nameof (_JniMarshal_PPLILLL_V):
					return new _JniMarshal_PPLILLL_V (Unsafe.As<_JniMarshal_PPLILLL_V> (dlg).Wrap_JniMarshal_PPLILLL_V);
				case nameof (_JniMarshal_PPLILLL_Z):
					return new _JniMarshal_PPLILLL_Z (Unsafe.As<_JniMarshal_PPLILLL_Z> (dlg).Wrap_JniMarshal_PPLILLL_Z);
				case nameof (_JniMarshal_PPLILLLL_V):
					return new _JniMarshal_PPLILLLL_V (Unsafe.As<_JniMarshal_PPLILLLL_V> (dlg).Wrap_JniMarshal_PPLILLLL_V);
				case nameof (_JniMarshal_PPLILLLLLLL_V):
					return new _JniMarshal_PPLILLLLLLL_V (Unsafe.As<_JniMarshal_PPLILLLLLLL_V> (dlg).Wrap_JniMarshal_PPLILLLLLLL_V);
				case nameof (_JniMarshal_PPLILZ_V):
					return new _JniMarshal_PPLILZ_V (Unsafe.As<_JniMarshal_PPLILZ_V> (dlg).Wrap_JniMarshal_PPLILZ_V);
				case nameof (_JniMarshal_PPLILZ_Z):
					return new _JniMarshal_PPLILZ_Z (Unsafe.As<_JniMarshal_PPLILZ_Z> (dlg).Wrap_JniMarshal_PPLILZ_Z);
				case nameof (_JniMarshal_PPLILZLLJL_V):
					return new _JniMarshal_PPLILZLLJL_V (Unsafe.As<_JniMarshal_PPLILZLLJL_V> (dlg).Wrap_JniMarshal_PPLILZLLJL_V);
				case nameof (_JniMarshal_PPLILZLLLL_V):
					return new _JniMarshal_PPLILZLLLL_V (Unsafe.As<_JniMarshal_PPLILZLLLL_V> (dlg).Wrap_JniMarshal_PPLILZLLLL_V);
				case nameof (_JniMarshal_PPLILZZIL_V):
					return new _JniMarshal_PPLILZZIL_V (Unsafe.As<_JniMarshal_PPLILZZIL_V> (dlg).Wrap_JniMarshal_PPLILZZIL_V);
				case nameof (_JniMarshal_PPLIS_V):
					return new _JniMarshal_PPLIS_V (Unsafe.As<_JniMarshal_PPLIS_V> (dlg).Wrap_JniMarshal_PPLIS_V);
				case nameof (_JniMarshal_PPLIZ_I):
					return new _JniMarshal_PPLIZ_I (Unsafe.As<_JniMarshal_PPLIZ_I> (dlg).Wrap_JniMarshal_PPLIZ_I);
				case nameof (_JniMarshal_PPLIZ_L):
					return new _JniMarshal_PPLIZ_L (Unsafe.As<_JniMarshal_PPLIZ_L> (dlg).Wrap_JniMarshal_PPLIZ_L);
				case nameof (_JniMarshal_PPLIZ_V):
					return new _JniMarshal_PPLIZ_V (Unsafe.As<_JniMarshal_PPLIZ_V> (dlg).Wrap_JniMarshal_PPLIZ_V);
				case nameof (_JniMarshal_PPLIZF_V):
					return new _JniMarshal_PPLIZF_V (Unsafe.As<_JniMarshal_PPLIZF_V> (dlg).Wrap_JniMarshal_PPLIZF_V);
				case nameof (_JniMarshal_PPLIZI_I):
					return new _JniMarshal_PPLIZI_I (Unsafe.As<_JniMarshal_PPLIZI_I> (dlg).Wrap_JniMarshal_PPLIZI_I);
				case nameof (_JniMarshal_PPLIZI_V):
					return new _JniMarshal_PPLIZI_V (Unsafe.As<_JniMarshal_PPLIZI_V> (dlg).Wrap_JniMarshal_PPLIZI_V);
				case nameof (_JniMarshal_PPLIZL_V):
					return new _JniMarshal_PPLIZL_V (Unsafe.As<_JniMarshal_PPLIZL_V> (dlg).Wrap_JniMarshal_PPLIZL_V);
				case nameof (_JniMarshal_PPLIZZ_I):
					return new _JniMarshal_PPLIZZ_I (Unsafe.As<_JniMarshal_PPLIZZ_I> (dlg).Wrap_JniMarshal_PPLIZZ_I);
				case nameof (_JniMarshal_PPLIZZ_V):
					return new _JniMarshal_PPLIZZ_V (Unsafe.As<_JniMarshal_PPLIZZ_V> (dlg).Wrap_JniMarshal_PPLIZZ_V);
				case nameof (_JniMarshal_PPLJ_I):
					return new _JniMarshal_PPLJ_I (Unsafe.As<_JniMarshal_PPLJ_I> (dlg).Wrap_JniMarshal_PPLJ_I);
				case nameof (_JniMarshal_PPLJ_J):
					return new _JniMarshal_PPLJ_J (Unsafe.As<_JniMarshal_PPLJ_J> (dlg).Wrap_JniMarshal_PPLJ_J);
				case nameof (_JniMarshal_PPLJ_L):
					return new _JniMarshal_PPLJ_L (Unsafe.As<_JniMarshal_PPLJ_L> (dlg).Wrap_JniMarshal_PPLJ_L);
				case nameof (_JniMarshal_PPLJ_V):
					return new _JniMarshal_PPLJ_V (Unsafe.As<_JniMarshal_PPLJ_V> (dlg).Wrap_JniMarshal_PPLJ_V);
				case nameof (_JniMarshal_PPLJ_Z):
					return new _JniMarshal_PPLJ_Z (Unsafe.As<_JniMarshal_PPLJ_Z> (dlg).Wrap_JniMarshal_PPLJ_Z);
				case nameof (_JniMarshal_PPLJB_V):
					return new _JniMarshal_PPLJB_V (Unsafe.As<_JniMarshal_PPLJB_V> (dlg).Wrap_JniMarshal_PPLJB_V);
				case nameof (_JniMarshal_PPLJC_V):
					return new _JniMarshal_PPLJC_V (Unsafe.As<_JniMarshal_PPLJC_V> (dlg).Wrap_JniMarshal_PPLJC_V);
				case nameof (_JniMarshal_PPLJD_V):
					return new _JniMarshal_PPLJD_V (Unsafe.As<_JniMarshal_PPLJD_V> (dlg).Wrap_JniMarshal_PPLJD_V);
				case nameof (_JniMarshal_PPLJF_V):
					return new _JniMarshal_PPLJF_V (Unsafe.As<_JniMarshal_PPLJF_V> (dlg).Wrap_JniMarshal_PPLJF_V);
				case nameof (_JniMarshal_PPLJFL_V):
					return new _JniMarshal_PPLJFL_V (Unsafe.As<_JniMarshal_PPLJFL_V> (dlg).Wrap_JniMarshal_PPLJFL_V);
				case nameof (_JniMarshal_PPLJFLL_V):
					return new _JniMarshal_PPLJFLL_V (Unsafe.As<_JniMarshal_PPLJFLL_V> (dlg).Wrap_JniMarshal_PPLJFLL_V);
				case nameof (_JniMarshal_PPLJI_J):
					return new _JniMarshal_PPLJI_J (Unsafe.As<_JniMarshal_PPLJI_J> (dlg).Wrap_JniMarshal_PPLJI_J);
				case nameof (_JniMarshal_PPLJI_L):
					return new _JniMarshal_PPLJI_L (Unsafe.As<_JniMarshal_PPLJI_L> (dlg).Wrap_JniMarshal_PPLJI_L);
				case nameof (_JniMarshal_PPLJI_V):
					return new _JniMarshal_PPLJI_V (Unsafe.As<_JniMarshal_PPLJI_V> (dlg).Wrap_JniMarshal_PPLJI_V);
				case nameof (_JniMarshal_PPLJI_Z):
					return new _JniMarshal_PPLJI_Z (Unsafe.As<_JniMarshal_PPLJI_Z> (dlg).Wrap_JniMarshal_PPLJI_Z);
				case nameof (_JniMarshal_PPLJIIII_J):
					return new _JniMarshal_PPLJIIII_J (Unsafe.As<_JniMarshal_PPLJIIII_J> (dlg).Wrap_JniMarshal_PPLJIIII_J);
				case nameof (_JniMarshal_PPLJIL_L):
					return new _JniMarshal_PPLJIL_L (Unsafe.As<_JniMarshal_PPLJIL_L> (dlg).Wrap_JniMarshal_PPLJIL_L);
				case nameof (_JniMarshal_PPLJIL_V):
					return new _JniMarshal_PPLJIL_V (Unsafe.As<_JniMarshal_PPLJIL_V> (dlg).Wrap_JniMarshal_PPLJIL_V);
				case nameof (_JniMarshal_PPLJIZ_V):
					return new _JniMarshal_PPLJIZ_V (Unsafe.As<_JniMarshal_PPLJIZ_V> (dlg).Wrap_JniMarshal_PPLJIZ_V);
				case nameof (_JniMarshal_PPLJJ_J):
					return new _JniMarshal_PPLJJ_J (Unsafe.As<_JniMarshal_PPLJJ_J> (dlg).Wrap_JniMarshal_PPLJJ_J);
				case nameof (_JniMarshal_PPLJJ_L):
					return new _JniMarshal_PPLJJ_L (Unsafe.As<_JniMarshal_PPLJJ_L> (dlg).Wrap_JniMarshal_PPLJJ_L);
				case nameof (_JniMarshal_PPLJJ_V):
					return new _JniMarshal_PPLJJ_V (Unsafe.As<_JniMarshal_PPLJJ_V> (dlg).Wrap_JniMarshal_PPLJJ_V);
				case nameof (_JniMarshal_PPLJJ_Z):
					return new _JniMarshal_PPLJJ_Z (Unsafe.As<_JniMarshal_PPLJJ_Z> (dlg).Wrap_JniMarshal_PPLJJ_Z);
				case nameof (_JniMarshal_PPLJJI_I):
					return new _JniMarshal_PPLJJI_I (Unsafe.As<_JniMarshal_PPLJJI_I> (dlg).Wrap_JniMarshal_PPLJJI_I);
				case nameof (_JniMarshal_PPLJJJJ_L):
					return new _JniMarshal_PPLJJJJ_L (Unsafe.As<_JniMarshal_PPLJJJJ_L> (dlg).Wrap_JniMarshal_PPLJJJJ_L);
				case nameof (_JniMarshal_PPLJJJJJLJLLJJ_L):
					return new _JniMarshal_PPLJJJJJLJLLJJ_L (Unsafe.As<_JniMarshal_PPLJJJJJLJLLJJ_L> (dlg).Wrap_JniMarshal_PPLJJJJJLJLLJJ_L);
				case nameof (_JniMarshal_PPLJJJJLJLJJ_L):
					return new _JniMarshal_PPLJJJJLJLJJ_L (Unsafe.As<_JniMarshal_PPLJJJJLJLJJ_L> (dlg).Wrap_JniMarshal_PPLJJJJLJLJJ_L);
				case nameof (_JniMarshal_PPLJJL_L):
					return new _JniMarshal_PPLJJL_L (Unsafe.As<_JniMarshal_PPLJJL_L> (dlg).Wrap_JniMarshal_PPLJJL_L);
				case nameof (_JniMarshal_PPLJJL_V):
					return new _JniMarshal_PPLJJL_V (Unsafe.As<_JniMarshal_PPLJJL_V> (dlg).Wrap_JniMarshal_PPLJJL_V);
				case nameof (_JniMarshal_PPLJJLI_L):
					return new _JniMarshal_PPLJJLI_L (Unsafe.As<_JniMarshal_PPLJJLI_L> (dlg).Wrap_JniMarshal_PPLJJLI_L);
				case nameof (_JniMarshal_PPLJJZ_L):
					return new _JniMarshal_PPLJJZ_L (Unsafe.As<_JniMarshal_PPLJJZ_L> (dlg).Wrap_JniMarshal_PPLJJZ_L);
				case nameof (_JniMarshal_PPLJJZ_V):
					return new _JniMarshal_PPLJJZ_V (Unsafe.As<_JniMarshal_PPLJJZ_V> (dlg).Wrap_JniMarshal_PPLJJZ_V);
				case nameof (_JniMarshal_PPLJL_J):
					return new _JniMarshal_PPLJL_J (Unsafe.As<_JniMarshal_PPLJL_J> (dlg).Wrap_JniMarshal_PPLJL_J);
				case nameof (_JniMarshal_PPLJL_L):
					return new _JniMarshal_PPLJL_L (Unsafe.As<_JniMarshal_PPLJL_L> (dlg).Wrap_JniMarshal_PPLJL_L);
				case nameof (_JniMarshal_PPLJL_V):
					return new _JniMarshal_PPLJL_V (Unsafe.As<_JniMarshal_PPLJL_V> (dlg).Wrap_JniMarshal_PPLJL_V);
				case nameof (_JniMarshal_PPLJL_Z):
					return new _JniMarshal_PPLJL_Z (Unsafe.As<_JniMarshal_PPLJL_Z> (dlg).Wrap_JniMarshal_PPLJL_Z);
				case nameof (_JniMarshal_PPLJLIJJ_V):
					return new _JniMarshal_PPLJLIJJ_V (Unsafe.As<_JniMarshal_PPLJLIJJ_V> (dlg).Wrap_JniMarshal_PPLJLIJJ_V);
				case nameof (_JniMarshal_PPLJLL_I):
					return new _JniMarshal_PPLJLL_I (Unsafe.As<_JniMarshal_PPLJLL_I> (dlg).Wrap_JniMarshal_PPLJLL_I);
				case nameof (_JniMarshal_PPLJLL_L):
					return new _JniMarshal_PPLJLL_L (Unsafe.As<_JniMarshal_PPLJLL_L> (dlg).Wrap_JniMarshal_PPLJLL_L);
				case nameof (_JniMarshal_PPLJLL_V):
					return new _JniMarshal_PPLJLL_V (Unsafe.As<_JniMarshal_PPLJLL_V> (dlg).Wrap_JniMarshal_PPLJLL_V);
				case nameof (_JniMarshal_PPLJLLL_L):
					return new _JniMarshal_PPLJLLL_L (Unsafe.As<_JniMarshal_PPLJLLL_L> (dlg).Wrap_JniMarshal_PPLJLLL_L);
				case nameof (_JniMarshal_PPLJLLL_V):
					return new _JniMarshal_PPLJLLL_V (Unsafe.As<_JniMarshal_PPLJLLL_V> (dlg).Wrap_JniMarshal_PPLJLLL_V);
				case nameof (_JniMarshal_PPLJLLLL_V):
					return new _JniMarshal_PPLJLLLL_V (Unsafe.As<_JniMarshal_PPLJLLLL_V> (dlg).Wrap_JniMarshal_PPLJLLLL_V);
				case nameof (_JniMarshal_PPLJS_V):
					return new _JniMarshal_PPLJS_V (Unsafe.As<_JniMarshal_PPLJS_V> (dlg).Wrap_JniMarshal_PPLJS_V);
				case nameof (_JniMarshal_PPLJZ_J):
					return new _JniMarshal_PPLJZ_J (Unsafe.As<_JniMarshal_PPLJZ_J> (dlg).Wrap_JniMarshal_PPLJZ_J);
				case nameof (_JniMarshal_PPLJZ_V):
					return new _JniMarshal_PPLJZ_V (Unsafe.As<_JniMarshal_PPLJZ_V> (dlg).Wrap_JniMarshal_PPLJZ_V);
				case nameof (_JniMarshal_PPLL_D):
					return new _JniMarshal_PPLL_D (Unsafe.As<_JniMarshal_PPLL_D> (dlg).Wrap_JniMarshal_PPLL_D);
				case nameof (_JniMarshal_PPLL_F):
					return new _JniMarshal_PPLL_F (Unsafe.As<_JniMarshal_PPLL_F> (dlg).Wrap_JniMarshal_PPLL_F);
				case nameof (_JniMarshal_PPLL_I):
					return new _JniMarshal_PPLL_I (Unsafe.As<_JniMarshal_PPLL_I> (dlg).Wrap_JniMarshal_PPLL_I);
				case nameof (_JniMarshal_PPLL_J):
					return new _JniMarshal_PPLL_J (Unsafe.As<_JniMarshal_PPLL_J> (dlg).Wrap_JniMarshal_PPLL_J);
				case nameof (_JniMarshal_PPLL_L):
					return new _JniMarshal_PPLL_L (Unsafe.As<_JniMarshal_PPLL_L> (dlg).Wrap_JniMarshal_PPLL_L);
				case nameof (_JniMarshal_PPLL_V):
					return new _JniMarshal_PPLL_V (Unsafe.As<_JniMarshal_PPLL_V> (dlg).Wrap_JniMarshal_PPLL_V);
				case nameof (_JniMarshal_PPLL_Z):
					return new _JniMarshal_PPLL_Z (Unsafe.As<_JniMarshal_PPLL_Z> (dlg).Wrap_JniMarshal_PPLL_Z);
				case nameof (_JniMarshal_PPLLB_V):
					return new _JniMarshal_PPLLB_V (Unsafe.As<_JniMarshal_PPLLB_V> (dlg).Wrap_JniMarshal_PPLLB_V);
				case nameof (_JniMarshal_PPLLC_V):
					return new _JniMarshal_PPLLC_V (Unsafe.As<_JniMarshal_PPLLC_V> (dlg).Wrap_JniMarshal_PPLLC_V);
				case nameof (_JniMarshal_PPLLD_V):
					return new _JniMarshal_PPLLD_V (Unsafe.As<_JniMarshal_PPLLD_V> (dlg).Wrap_JniMarshal_PPLLD_V);
				case nameof (_JniMarshal_PPLLF_F):
					return new _JniMarshal_PPLLF_F (Unsafe.As<_JniMarshal_PPLLF_F> (dlg).Wrap_JniMarshal_PPLLF_F);
				case nameof (_JniMarshal_PPLLF_V):
					return new _JniMarshal_PPLLF_V (Unsafe.As<_JniMarshal_PPLLF_V> (dlg).Wrap_JniMarshal_PPLLF_V);
				case nameof (_JniMarshal_PPLLFF_Z):
					return new _JniMarshal_PPLLFF_Z (Unsafe.As<_JniMarshal_PPLLFF_Z> (dlg).Wrap_JniMarshal_PPLLFF_Z);
				case nameof (_JniMarshal_PPLLFFF_V):
					return new _JniMarshal_PPLLFFF_V (Unsafe.As<_JniMarshal_PPLLFFF_V> (dlg).Wrap_JniMarshal_PPLLFFF_V);
				case nameof (_JniMarshal_PPLLFFL_V):
					return new _JniMarshal_PPLLFFL_V (Unsafe.As<_JniMarshal_PPLLFFL_V> (dlg).Wrap_JniMarshal_PPLLFFL_V);
				case nameof (_JniMarshal_PPLLFFLL_L):
					return new _JniMarshal_PPLLFFLL_L (Unsafe.As<_JniMarshal_PPLLFFLL_L> (dlg).Wrap_JniMarshal_PPLLFFLL_L);
				case nameof (_JniMarshal_PPLLFI_V):
					return new _JniMarshal_PPLLFI_V (Unsafe.As<_JniMarshal_PPLLFI_V> (dlg).Wrap_JniMarshal_PPLLFI_V);
				case nameof (_JniMarshal_PPLLFJDD_Z):
					return new _JniMarshal_PPLLFJDD_Z (Unsafe.As<_JniMarshal_PPLLFJDD_Z> (dlg).Wrap_JniMarshal_PPLLFJDD_Z);
				case nameof (_JniMarshal_PPLLI_F):
					return new _JniMarshal_PPLLI_F (Unsafe.As<_JniMarshal_PPLLI_F> (dlg).Wrap_JniMarshal_PPLLI_F);
				case nameof (_JniMarshal_PPLLI_I):
					return new _JniMarshal_PPLLI_I (Unsafe.As<_JniMarshal_PPLLI_I> (dlg).Wrap_JniMarshal_PPLLI_I);
				case nameof (_JniMarshal_PPLLI_L):
					return new _JniMarshal_PPLLI_L (Unsafe.As<_JniMarshal_PPLLI_L> (dlg).Wrap_JniMarshal_PPLLI_L);
				case nameof (_JniMarshal_PPLLI_V):
					return new _JniMarshal_PPLLI_V (Unsafe.As<_JniMarshal_PPLLI_V> (dlg).Wrap_JniMarshal_PPLLI_V);
				case nameof (_JniMarshal_PPLLI_Z):
					return new _JniMarshal_PPLLI_Z (Unsafe.As<_JniMarshal_PPLLI_Z> (dlg).Wrap_JniMarshal_PPLLI_Z);
				case nameof (_JniMarshal_PPLLIF_V):
					return new _JniMarshal_PPLLIF_V (Unsafe.As<_JniMarshal_PPLLIF_V> (dlg).Wrap_JniMarshal_PPLLIF_V);
				case nameof (_JniMarshal_PPLLIFLLII_V):
					return new _JniMarshal_PPLLIFLLII_V (Unsafe.As<_JniMarshal_PPLLIFLLII_V> (dlg).Wrap_JniMarshal_PPLLIFLLII_V);
				case nameof (_JniMarshal_PPLLII_I):
					return new _JniMarshal_PPLLII_I (Unsafe.As<_JniMarshal_PPLLII_I> (dlg).Wrap_JniMarshal_PPLLII_I);
				case nameof (_JniMarshal_PPLLII_L):
					return new _JniMarshal_PPLLII_L (Unsafe.As<_JniMarshal_PPLLII_L> (dlg).Wrap_JniMarshal_PPLLII_L);
				case nameof (_JniMarshal_PPLLII_V):
					return new _JniMarshal_PPLLII_V (Unsafe.As<_JniMarshal_PPLLII_V> (dlg).Wrap_JniMarshal_PPLLII_V);
				case nameof (_JniMarshal_PPLLII_Z):
					return new _JniMarshal_PPLLII_Z (Unsafe.As<_JniMarshal_PPLLII_Z> (dlg).Wrap_JniMarshal_PPLLII_Z);
				case nameof (_JniMarshal_PPLLIIFIIIL_V):
					return new _JniMarshal_PPLLIIFIIIL_V (Unsafe.As<_JniMarshal_PPLLIIFIIIL_V> (dlg).Wrap_JniMarshal_PPLLIIFIIIL_V);
				case nameof (_JniMarshal_PPLLIIFIIILLLLLL_L):
					return new _JniMarshal_PPLLIIFIIILLLLLL_L (Unsafe.As<_JniMarshal_PPLLIIFIIILLLLLL_L> (dlg).Wrap_JniMarshal_PPLLIIFIIILLLLLL_L);
				case nameof (_JniMarshal_PPLLIII_I):
					return new _JniMarshal_PPLLIII_I (Unsafe.As<_JniMarshal_PPLLIII_I> (dlg).Wrap_JniMarshal_PPLLIII_I);
				case nameof (_JniMarshal_PPLLIII_L):
					return new _JniMarshal_PPLLIII_L (Unsafe.As<_JniMarshal_PPLLIII_L> (dlg).Wrap_JniMarshal_PPLLIII_L);
				case nameof (_JniMarshal_PPLLIII_V):
					return new _JniMarshal_PPLLIII_V (Unsafe.As<_JniMarshal_PPLLIII_V> (dlg).Wrap_JniMarshal_PPLLIII_V);
				case nameof (_JniMarshal_PPLLIIII_V):
					return new _JniMarshal_PPLLIIII_V (Unsafe.As<_JniMarshal_PPLLIIII_V> (dlg).Wrap_JniMarshal_PPLLIIII_V);
				case nameof (_JniMarshal_PPLLIIII_Z):
					return new _JniMarshal_PPLLIIII_Z (Unsafe.As<_JniMarshal_PPLLIIII_Z> (dlg).Wrap_JniMarshal_PPLLIIII_Z);
				case nameof (_JniMarshal_PPLLIIIIILIII_V):
					return new _JniMarshal_PPLLIIIIILIII_V (Unsafe.As<_JniMarshal_PPLLIIIIILIII_V> (dlg).Wrap_JniMarshal_PPLLIIIIILIII_V);
				case nameof (_JniMarshal_PPLLIIIIILIIZL_V):
					return new _JniMarshal_PPLLIIIIILIIZL_V (Unsafe.As<_JniMarshal_PPLLIIIIILIIZL_V> (dlg).Wrap_JniMarshal_PPLLIIIIILIIZL_V);
				case nameof (_JniMarshal_PPLLIIIILLII_V):
					return new _JniMarshal_PPLLIIIILLII_V (Unsafe.As<_JniMarshal_PPLLIIIILLII_V> (dlg).Wrap_JniMarshal_PPLLIIIILLII_V);
				case nameof (_JniMarshal_PPLLIIIL_V):
					return new _JniMarshal_PPLLIIIL_V (Unsafe.As<_JniMarshal_PPLLIIIL_V> (dlg).Wrap_JniMarshal_PPLLIIIL_V);
				case nameof (_JniMarshal_PPLLIIILL_V):
					return new _JniMarshal_PPLLIIILL_V (Unsafe.As<_JniMarshal_PPLLIIILL_V> (dlg).Wrap_JniMarshal_PPLLIIILL_V);
				case nameof (_JniMarshal_PPLLIIIZ_L):
					return new _JniMarshal_PPLLIIIZ_L (Unsafe.As<_JniMarshal_PPLLIIIZ_L> (dlg).Wrap_JniMarshal_PPLLIIIZ_L);
				case nameof (_JniMarshal_PPLLIIJ_Z):
					return new _JniMarshal_PPLLIIJ_Z (Unsafe.As<_JniMarshal_PPLLIIJ_Z> (dlg).Wrap_JniMarshal_PPLLIIJ_Z);
				case nameof (_JniMarshal_PPLLIIJL_L):
					return new _JniMarshal_PPLLIIJL_L (Unsafe.As<_JniMarshal_PPLLIIJL_L> (dlg).Wrap_JniMarshal_PPLLIIJL_L);
				case nameof (_JniMarshal_PPLLIIL_I):
					return new _JniMarshal_PPLLIIL_I (Unsafe.As<_JniMarshal_PPLLIIL_I> (dlg).Wrap_JniMarshal_PPLLIIL_I);
				case nameof (_JniMarshal_PPLLIIL_L):
					return new _JniMarshal_PPLLIIL_L (Unsafe.As<_JniMarshal_PPLLIIL_L> (dlg).Wrap_JniMarshal_PPLLIIL_L);
				case nameof (_JniMarshal_PPLLIIL_V):
					return new _JniMarshal_PPLLIIL_V (Unsafe.As<_JniMarshal_PPLLIIL_V> (dlg).Wrap_JniMarshal_PPLLIIL_V);
				case nameof (_JniMarshal_PPLLIIL_Z):
					return new _JniMarshal_PPLLIIL_Z (Unsafe.As<_JniMarshal_PPLLIIL_Z> (dlg).Wrap_JniMarshal_PPLLIIL_Z);
				case nameof (_JniMarshal_PPLLIILI_V):
					return new _JniMarshal_PPLLIILI_V (Unsafe.As<_JniMarshal_PPLLIILI_V> (dlg).Wrap_JniMarshal_PPLLIILI_V);
				case nameof (_JniMarshal_PPLLIIZ_Z):
					return new _JniMarshal_PPLLIIZ_Z (Unsafe.As<_JniMarshal_PPLLIIZ_Z> (dlg).Wrap_JniMarshal_PPLLIIZ_Z);
				case nameof (_JniMarshal_PPLLIJ_V):
					return new _JniMarshal_PPLLIJ_V (Unsafe.As<_JniMarshal_PPLLIJ_V> (dlg).Wrap_JniMarshal_PPLLIJ_V);
				case nameof (_JniMarshal_PPLLIJ_Z):
					return new _JniMarshal_PPLLIJ_Z (Unsafe.As<_JniMarshal_PPLLIJ_Z> (dlg).Wrap_JniMarshal_PPLLIJ_Z);
				case nameof (_JniMarshal_PPLLIJL_L):
					return new _JniMarshal_PPLLIJL_L (Unsafe.As<_JniMarshal_PPLLIJL_L> (dlg).Wrap_JniMarshal_PPLLIJL_L);
				case nameof (_JniMarshal_PPLLIL_L):
					return new _JniMarshal_PPLLIL_L (Unsafe.As<_JniMarshal_PPLLIL_L> (dlg).Wrap_JniMarshal_PPLLIL_L);
				case nameof (_JniMarshal_PPLLIL_V):
					return new _JniMarshal_PPLLIL_V (Unsafe.As<_JniMarshal_PPLLIL_V> (dlg).Wrap_JniMarshal_PPLLIL_V);
				case nameof (_JniMarshal_PPLLIL_Z):
					return new _JniMarshal_PPLLIL_Z (Unsafe.As<_JniMarshal_PPLLIL_Z> (dlg).Wrap_JniMarshal_PPLLIL_Z);
				case nameof (_JniMarshal_PPLLILFFLZ_L):
					return new _JniMarshal_PPLLILFFLZ_L (Unsafe.As<_JniMarshal_PPLLILFFLZ_L> (dlg).Wrap_JniMarshal_PPLLILFFLZ_L);
				case nameof (_JniMarshal_PPLLILFFLZLI_L):
					return new _JniMarshal_PPLLILFFLZLI_L (Unsafe.As<_JniMarshal_PPLLILFFLZLI_L> (dlg).Wrap_JniMarshal_PPLLILFFLZLI_L);
				case nameof (_JniMarshal_PPLLILI_L):
					return new _JniMarshal_PPLLILI_L (Unsafe.As<_JniMarshal_PPLLILI_L> (dlg).Wrap_JniMarshal_PPLLILI_L);
				case nameof (_JniMarshal_PPLLILI_V):
					return new _JniMarshal_PPLLILI_V (Unsafe.As<_JniMarshal_PPLLILI_V> (dlg).Wrap_JniMarshal_PPLLILI_V);
				case nameof (_JniMarshal_PPLLILIII_V):
					return new _JniMarshal_PPLLILIII_V (Unsafe.As<_JniMarshal_PPLLILIII_V> (dlg).Wrap_JniMarshal_PPLLILIII_V);
				case nameof (_JniMarshal_PPLLILIIIL_V):
					return new _JniMarshal_PPLLILIIIL_V (Unsafe.As<_JniMarshal_PPLLILIIIL_V> (dlg).Wrap_JniMarshal_PPLLILIIIL_V);
				case nameof (_JniMarshal_PPLLILIL_L):
					return new _JniMarshal_PPLLILIL_L (Unsafe.As<_JniMarshal_PPLLILIL_L> (dlg).Wrap_JniMarshal_PPLLILIL_L);
				case nameof (_JniMarshal_PPLLILIL_V):
					return new _JniMarshal_PPLLILIL_V (Unsafe.As<_JniMarshal_PPLLILIL_V> (dlg).Wrap_JniMarshal_PPLLILIL_V);
				case nameof (_JniMarshal_PPLLILILJIJJL_L):
					return new _JniMarshal_PPLLILILJIJJL_L (Unsafe.As<_JniMarshal_PPLLILILJIJJL_L> (dlg).Wrap_JniMarshal_PPLLILILJIJJL_L);
				case nameof (_JniMarshal_PPLLILL_I):
					return new _JniMarshal_PPLLILL_I (Unsafe.As<_JniMarshal_PPLLILL_I> (dlg).Wrap_JniMarshal_PPLLILL_I);
				case nameof (_JniMarshal_PPLLILL_L):
					return new _JniMarshal_PPLLILL_L (Unsafe.As<_JniMarshal_PPLLILL_L> (dlg).Wrap_JniMarshal_PPLLILL_L);
				case nameof (_JniMarshal_PPLLILL_V):
					return new _JniMarshal_PPLLILL_V (Unsafe.As<_JniMarshal_PPLLILL_V> (dlg).Wrap_JniMarshal_PPLLILL_V);
				case nameof (_JniMarshal_PPLLILLI_V):
					return new _JniMarshal_PPLLILLI_V (Unsafe.As<_JniMarshal_PPLLILLI_V> (dlg).Wrap_JniMarshal_PPLLILLI_V);
				case nameof (_JniMarshal_PPLLILLLIL_V):
					return new _JniMarshal_PPLLILLLIL_V (Unsafe.As<_JniMarshal_PPLLILLLIL_V> (dlg).Wrap_JniMarshal_PPLLILLLIL_V);
				case nameof (_JniMarshal_PPLLILLLILJ_V):
					return new _JniMarshal_PPLLILLLILJ_V (Unsafe.As<_JniMarshal_PPLLILLLILJ_V> (dlg).Wrap_JniMarshal_PPLLILLLILJ_V);
				case nameof (_JniMarshal_PPLLILLZLIZ_L):
					return new _JniMarshal_PPLLILLZLIZ_L (Unsafe.As<_JniMarshal_PPLLILLZLIZ_L> (dlg).Wrap_JniMarshal_PPLLILLZLIZ_L);
				case nameof (_JniMarshal_PPLLIZ_I):
					return new _JniMarshal_PPLLIZ_I (Unsafe.As<_JniMarshal_PPLLIZ_I> (dlg).Wrap_JniMarshal_PPLLIZ_I);
				case nameof (_JniMarshal_PPLLIZ_L):
					return new _JniMarshal_PPLLIZ_L (Unsafe.As<_JniMarshal_PPLLIZ_L> (dlg).Wrap_JniMarshal_PPLLIZ_L);
				case nameof (_JniMarshal_PPLLIZ_V):
					return new _JniMarshal_PPLLIZ_V (Unsafe.As<_JniMarshal_PPLLIZ_V> (dlg).Wrap_JniMarshal_PPLLIZ_V);
				case nameof (_JniMarshal_PPLLJ_L):
					return new _JniMarshal_PPLLJ_L (Unsafe.As<_JniMarshal_PPLLJ_L> (dlg).Wrap_JniMarshal_PPLLJ_L);
				case nameof (_JniMarshal_PPLLJ_V):
					return new _JniMarshal_PPLLJ_V (Unsafe.As<_JniMarshal_PPLLJ_V> (dlg).Wrap_JniMarshal_PPLLJ_V);
				case nameof (_JniMarshal_PPLLJ_Z):
					return new _JniMarshal_PPLLJ_Z (Unsafe.As<_JniMarshal_PPLLJ_Z> (dlg).Wrap_JniMarshal_PPLLJ_Z);
				case nameof (_JniMarshal_PPLLJFZJ_Z):
					return new _JniMarshal_PPLLJFZJ_Z (Unsafe.As<_JniMarshal_PPLLJFZJ_Z> (dlg).Wrap_JniMarshal_PPLLJFZJ_Z);
				case nameof (_JniMarshal_PPLLJI_L):
					return new _JniMarshal_PPLLJI_L (Unsafe.As<_JniMarshal_PPLLJI_L> (dlg).Wrap_JniMarshal_PPLLJI_L);
				case nameof (_JniMarshal_PPLLJI_V):
					return new _JniMarshal_PPLLJI_V (Unsafe.As<_JniMarshal_PPLLJI_V> (dlg).Wrap_JniMarshal_PPLLJI_V);
				case nameof (_JniMarshal_PPLLJJ_L):
					return new _JniMarshal_PPLLJJ_L (Unsafe.As<_JniMarshal_PPLLJJ_L> (dlg).Wrap_JniMarshal_PPLLJJ_L);
				case nameof (_JniMarshal_PPLLJJ_V):
					return new _JniMarshal_PPLLJJ_V (Unsafe.As<_JniMarshal_PPLLJJ_V> (dlg).Wrap_JniMarshal_PPLLJJ_V);
				case nameof (_JniMarshal_PPLLJJJJJ_L):
					return new _JniMarshal_PPLLJJJJJ_L (Unsafe.As<_JniMarshal_PPLLJJJJJ_L> (dlg).Wrap_JniMarshal_PPLLJJJJJ_L);
				case nameof (_JniMarshal_PPLLJJJJZ_L):
					return new _JniMarshal_PPLLJJJJZ_L (Unsafe.As<_JniMarshal_PPLLJJJJZ_L> (dlg).Wrap_JniMarshal_PPLLJJJJZ_L);
				case nameof (_JniMarshal_PPLLJJJL_V):
					return new _JniMarshal_PPLLJJJL_V (Unsafe.As<_JniMarshal_PPLLJJJL_V> (dlg).Wrap_JniMarshal_PPLLJJJL_V);
				case nameof (_JniMarshal_PPLLJJL_L):
					return new _JniMarshal_PPLLJJL_L (Unsafe.As<_JniMarshal_PPLLJJL_L> (dlg).Wrap_JniMarshal_PPLLJJL_L);
				case nameof (_JniMarshal_PPLLJJL_V):
					return new _JniMarshal_PPLLJJL_V (Unsafe.As<_JniMarshal_PPLLJJL_V> (dlg).Wrap_JniMarshal_PPLLJJL_V);
				case nameof (_JniMarshal_PPLLJL_L):
					return new _JniMarshal_PPLLJL_L (Unsafe.As<_JniMarshal_PPLLJL_L> (dlg).Wrap_JniMarshal_PPLLJL_L);
				case nameof (_JniMarshal_PPLLJL_V):
					return new _JniMarshal_PPLLJL_V (Unsafe.As<_JniMarshal_PPLLJL_V> (dlg).Wrap_JniMarshal_PPLLJL_V);
				case nameof (_JniMarshal_PPLLJL_Z):
					return new _JniMarshal_PPLLJL_Z (Unsafe.As<_JniMarshal_PPLLJL_Z> (dlg).Wrap_JniMarshal_PPLLJL_Z);
				case nameof (_JniMarshal_PPLLJLJ_L):
					return new _JniMarshal_PPLLJLJ_L (Unsafe.As<_JniMarshal_PPLLJLJ_L> (dlg).Wrap_JniMarshal_PPLLJLJ_L);
				case nameof (_JniMarshal_PPLLJLL_L):
					return new _JniMarshal_PPLLJLL_L (Unsafe.As<_JniMarshal_PPLLJLL_L> (dlg).Wrap_JniMarshal_PPLLJLL_L);
				case nameof (_JniMarshal_PPLLL_I):
					return new _JniMarshal_PPLLL_I (Unsafe.As<_JniMarshal_PPLLL_I> (dlg).Wrap_JniMarshal_PPLLL_I);
				case nameof (_JniMarshal_PPLLL_J):
					return new _JniMarshal_PPLLL_J (Unsafe.As<_JniMarshal_PPLLL_J> (dlg).Wrap_JniMarshal_PPLLL_J);
				case nameof (_JniMarshal_PPLLL_L):
					return new _JniMarshal_PPLLL_L (Unsafe.As<_JniMarshal_PPLLL_L> (dlg).Wrap_JniMarshal_PPLLL_L);
				case nameof (_JniMarshal_PPLLL_V):
					return new _JniMarshal_PPLLL_V (Unsafe.As<_JniMarshal_PPLLL_V> (dlg).Wrap_JniMarshal_PPLLL_V);
				case nameof (_JniMarshal_PPLLL_Z):
					return new _JniMarshal_PPLLL_Z (Unsafe.As<_JniMarshal_PPLLL_Z> (dlg).Wrap_JniMarshal_PPLLL_Z);
				case nameof (_JniMarshal_PPLLLFF_Z):
					return new _JniMarshal_PPLLLFF_Z (Unsafe.As<_JniMarshal_PPLLLFF_Z> (dlg).Wrap_JniMarshal_PPLLLFF_Z);
				case nameof (_JniMarshal_PPLLLFFIZ_V):
					return new _JniMarshal_PPLLLFFIZ_V (Unsafe.As<_JniMarshal_PPLLLFFIZ_V> (dlg).Wrap_JniMarshal_PPLLLFFIZ_V);
				case nameof (_JniMarshal_PPLLLFFLL_V):
					return new _JniMarshal_PPLLLFFLL_V (Unsafe.As<_JniMarshal_PPLLLFFLL_V> (dlg).Wrap_JniMarshal_PPLLLFFLL_V);
				case nameof (_JniMarshal_PPLLLFFZ_Z):
					return new _JniMarshal_PPLLLFFZ_Z (Unsafe.As<_JniMarshal_PPLLLFFZ_Z> (dlg).Wrap_JniMarshal_PPLLLFFZ_Z);
				case nameof (_JniMarshal_PPLLLI_I):
					return new _JniMarshal_PPLLLI_I (Unsafe.As<_JniMarshal_PPLLLI_I> (dlg).Wrap_JniMarshal_PPLLLI_I);
				case nameof (_JniMarshal_PPLLLI_J):
					return new _JniMarshal_PPLLLI_J (Unsafe.As<_JniMarshal_PPLLLI_J> (dlg).Wrap_JniMarshal_PPLLLI_J);
				case nameof (_JniMarshal_PPLLLI_L):
					return new _JniMarshal_PPLLLI_L (Unsafe.As<_JniMarshal_PPLLLI_L> (dlg).Wrap_JniMarshal_PPLLLI_L);
				case nameof (_JniMarshal_PPLLLI_V):
					return new _JniMarshal_PPLLLI_V (Unsafe.As<_JniMarshal_PPLLLI_V> (dlg).Wrap_JniMarshal_PPLLLI_V);
				case nameof (_JniMarshal_PPLLLI_Z):
					return new _JniMarshal_PPLLLI_Z (Unsafe.As<_JniMarshal_PPLLLI_Z> (dlg).Wrap_JniMarshal_PPLLLI_Z);
				case nameof (_JniMarshal_PPLLLIFF_V):
					return new _JniMarshal_PPLLLIFF_V (Unsafe.As<_JniMarshal_PPLLLIFF_V> (dlg).Wrap_JniMarshal_PPLLLIFF_V);
				case nameof (_JniMarshal_PPLLLIFFL_V):
					return new _JniMarshal_PPLLLIFFL_V (Unsafe.As<_JniMarshal_PPLLLIFFL_V> (dlg).Wrap_JniMarshal_PPLLLIFFL_V);
				case nameof (_JniMarshal_PPLLLII_L):
					return new _JniMarshal_PPLLLII_L (Unsafe.As<_JniMarshal_PPLLLII_L> (dlg).Wrap_JniMarshal_PPLLLII_L);
				case nameof (_JniMarshal_PPLLLII_V):
					return new _JniMarshal_PPLLLII_V (Unsafe.As<_JniMarshal_PPLLLII_V> (dlg).Wrap_JniMarshal_PPLLLII_V);
				case nameof (_JniMarshal_PPLLLIII_I):
					return new _JniMarshal_PPLLLIII_I (Unsafe.As<_JniMarshal_PPLLLIII_I> (dlg).Wrap_JniMarshal_PPLLLIII_I);
				case nameof (_JniMarshal_PPLLLIII_V):
					return new _JniMarshal_PPLLLIII_V (Unsafe.As<_JniMarshal_PPLLLIII_V> (dlg).Wrap_JniMarshal_PPLLLIII_V);
				case nameof (_JniMarshal_PPLLLIIII_V):
					return new _JniMarshal_PPLLLIIII_V (Unsafe.As<_JniMarshal_PPLLLIIII_V> (dlg).Wrap_JniMarshal_PPLLLIIII_V);
				case nameof (_JniMarshal_PPLLLIIIII_V):
					return new _JniMarshal_PPLLLIIIII_V (Unsafe.As<_JniMarshal_PPLLLIIIII_V> (dlg).Wrap_JniMarshal_PPLLLIIIII_V);
				case nameof (_JniMarshal_PPLLLIIIIIL_V):
					return new _JniMarshal_PPLLLIIIIIL_V (Unsafe.As<_JniMarshal_PPLLLIIIIIL_V> (dlg).Wrap_JniMarshal_PPLLLIIIIIL_V);
				case nameof (_JniMarshal_PPLLLIIIL_V):
					return new _JniMarshal_PPLLLIIIL_V (Unsafe.As<_JniMarshal_PPLLLIIIL_V> (dlg).Wrap_JniMarshal_PPLLLIIIL_V);
				case nameof (_JniMarshal_PPLLLIIL_L):
					return new _JniMarshal_PPLLLIIL_L (Unsafe.As<_JniMarshal_PPLLLIIL_L> (dlg).Wrap_JniMarshal_PPLLLIIL_L);
				case nameof (_JniMarshal_PPLLLIIL_V):
					return new _JniMarshal_PPLLLIIL_V (Unsafe.As<_JniMarshal_PPLLLIIL_V> (dlg).Wrap_JniMarshal_PPLLLIIL_V);
				case nameof (_JniMarshal_PPLLLIILI_V):
					return new _JniMarshal_PPLLLIILI_V (Unsafe.As<_JniMarshal_PPLLLIILI_V> (dlg).Wrap_JniMarshal_PPLLLIILI_V);
				case nameof (_JniMarshal_PPLLLIILLLLLZZLZZZZLL_L):
					return new _JniMarshal_PPLLLIILLLLLZZLZZZZLL_L (Unsafe.As<_JniMarshal_PPLLLIILLLLLZZLZZZZLL_L> (dlg).Wrap_JniMarshal_PPLLLIILLLLLZZLZZZZLL_L);
				case nameof (_JniMarshal_PPLLLIJ_L):
					return new _JniMarshal_PPLLLIJ_L (Unsafe.As<_JniMarshal_PPLLLIJ_L> (dlg).Wrap_JniMarshal_PPLLLIJ_L);
				case nameof (_JniMarshal_PPLLLIL_L):
					return new _JniMarshal_PPLLLIL_L (Unsafe.As<_JniMarshal_PPLLLIL_L> (dlg).Wrap_JniMarshal_PPLLLIL_L);
				case nameof (_JniMarshal_PPLLLIL_V):
					return new _JniMarshal_PPLLLIL_V (Unsafe.As<_JniMarshal_PPLLLIL_V> (dlg).Wrap_JniMarshal_PPLLLIL_V);
				case nameof (_JniMarshal_PPLLLIL_Z):
					return new _JniMarshal_PPLLLIL_Z (Unsafe.As<_JniMarshal_PPLLLIL_Z> (dlg).Wrap_JniMarshal_PPLLLIL_Z);
				case nameof (_JniMarshal_PPLLLILL_V):
					return new _JniMarshal_PPLLLILL_V (Unsafe.As<_JniMarshal_PPLLLILL_V> (dlg).Wrap_JniMarshal_PPLLLILL_V);
				case nameof (_JniMarshal_PPLLLILLLL_L):
					return new _JniMarshal_PPLLLILLLL_L (Unsafe.As<_JniMarshal_PPLLLILLLL_L> (dlg).Wrap_JniMarshal_PPLLLILLLL_L);
				case nameof (_JniMarshal_PPLLLILLLLL_L):
					return new _JniMarshal_PPLLLILLLLL_L (Unsafe.As<_JniMarshal_PPLLLILLLLL_L> (dlg).Wrap_JniMarshal_PPLLLILLLLL_L);
				case nameof (_JniMarshal_PPLLLILLLLL_V):
					return new _JniMarshal_PPLLLILLLLL_V (Unsafe.As<_JniMarshal_PPLLLILLLLL_V> (dlg).Wrap_JniMarshal_PPLLLILLLLL_V);
				case nameof (_JniMarshal_PPLLLILZZZZIZILIJ_L):
					return new _JniMarshal_PPLLLILZZZZIZILIJ_L (Unsafe.As<_JniMarshal_PPLLLILZZZZIZILIJ_L> (dlg).Wrap_JniMarshal_PPLLLILZZZZIZILIJ_L);
				case nameof (_JniMarshal_PPLLLIZ_L):
					return new _JniMarshal_PPLLLIZ_L (Unsafe.As<_JniMarshal_PPLLLIZ_L> (dlg).Wrap_JniMarshal_PPLLLIZ_L);
				case nameof (_JniMarshal_PPLLLIZ_V):
					return new _JniMarshal_PPLLLIZ_V (Unsafe.As<_JniMarshal_PPLLLIZ_V> (dlg).Wrap_JniMarshal_PPLLLIZ_V);
				case nameof (_JniMarshal_PPLLLJ_L):
					return new _JniMarshal_PPLLLJ_L (Unsafe.As<_JniMarshal_PPLLLJ_L> (dlg).Wrap_JniMarshal_PPLLLJ_L);
				case nameof (_JniMarshal_PPLLLJ_V):
					return new _JniMarshal_PPLLLJ_V (Unsafe.As<_JniMarshal_PPLLLJ_V> (dlg).Wrap_JniMarshal_PPLLLJ_V);
				case nameof (_JniMarshal_PPLLLJIIZ_L):
					return new _JniMarshal_PPLLLJIIZ_L (Unsafe.As<_JniMarshal_PPLLLJIIZ_L> (dlg).Wrap_JniMarshal_PPLLLJIIZ_L);
				case nameof (_JniMarshal_PPLLLJJIL_V):
					return new _JniMarshal_PPLLLJJIL_V (Unsafe.As<_JniMarshal_PPLLLJJIL_V> (dlg).Wrap_JniMarshal_PPLLLJJIL_V);
				case nameof (_JniMarshal_PPLLLJJJJJ_L):
					return new _JniMarshal_PPLLLJJJJJ_L (Unsafe.As<_JniMarshal_PPLLLJJJJJ_L> (dlg).Wrap_JniMarshal_PPLLLJJJJJ_L);
				case nameof (_JniMarshal_PPLLLJJJJJZ_L):
					return new _JniMarshal_PPLLLJJJJJZ_L (Unsafe.As<_JniMarshal_PPLLLJJJJJZ_L> (dlg).Wrap_JniMarshal_PPLLLJJJJJZ_L);
				case nameof (_JniMarshal_PPLLLJJL_L):
					return new _JniMarshal_PPLLLJJL_L (Unsafe.As<_JniMarshal_PPLLLJJL_L> (dlg).Wrap_JniMarshal_PPLLLJJL_L);
				case nameof (_JniMarshal_PPLLLJJL_V):
					return new _JniMarshal_PPLLLJJL_V (Unsafe.As<_JniMarshal_PPLLLJJL_V> (dlg).Wrap_JniMarshal_PPLLLJJL_V);
				case nameof (_JniMarshal_PPLLLJL_L):
					return new _JniMarshal_PPLLLJL_L (Unsafe.As<_JniMarshal_PPLLLJL_L> (dlg).Wrap_JniMarshal_PPLLLJL_L);
				case nameof (_JniMarshal_PPLLLJLL_V):
					return new _JniMarshal_PPLLLJLL_V (Unsafe.As<_JniMarshal_PPLLLJLL_V> (dlg).Wrap_JniMarshal_PPLLLJLL_V);
				case nameof (_JniMarshal_PPLLLJZ_L):
					return new _JniMarshal_PPLLLJZ_L (Unsafe.As<_JniMarshal_PPLLLJZ_L> (dlg).Wrap_JniMarshal_PPLLLJZ_L);
				case nameof (_JniMarshal_PPLLLJZZ_L):
					return new _JniMarshal_PPLLLJZZ_L (Unsafe.As<_JniMarshal_PPLLLJZZ_L> (dlg).Wrap_JniMarshal_PPLLLJZZ_L);
				case nameof (_JniMarshal_PPLLLJZZJJL_V):
					return new _JniMarshal_PPLLLJZZJJL_V (Unsafe.As<_JniMarshal_PPLLLJZZJJL_V> (dlg).Wrap_JniMarshal_PPLLLJZZJJL_V);
				case nameof (_JniMarshal_PPLLLL_I):
					return new _JniMarshal_PPLLLL_I (Unsafe.As<_JniMarshal_PPLLLL_I> (dlg).Wrap_JniMarshal_PPLLLL_I);
				case nameof (_JniMarshal_PPLLLL_J):
					return new _JniMarshal_PPLLLL_J (Unsafe.As<_JniMarshal_PPLLLL_J> (dlg).Wrap_JniMarshal_PPLLLL_J);
				case nameof (_JniMarshal_PPLLLL_L):
					return new _JniMarshal_PPLLLL_L (Unsafe.As<_JniMarshal_PPLLLL_L> (dlg).Wrap_JniMarshal_PPLLLL_L);
				case nameof (_JniMarshal_PPLLLL_V):
					return new _JniMarshal_PPLLLL_V (Unsafe.As<_JniMarshal_PPLLLL_V> (dlg).Wrap_JniMarshal_PPLLLL_V);
				case nameof (_JniMarshal_PPLLLL_Z):
					return new _JniMarshal_PPLLLL_Z (Unsafe.As<_JniMarshal_PPLLLL_Z> (dlg).Wrap_JniMarshal_PPLLLL_Z);
				case nameof (_JniMarshal_PPLLLLF_L):
					return new _JniMarshal_PPLLLLF_L (Unsafe.As<_JniMarshal_PPLLLLF_L> (dlg).Wrap_JniMarshal_PPLLLLF_L);
				case nameof (_JniMarshal_PPLLLLFI_V):
					return new _JniMarshal_PPLLLLFI_V (Unsafe.As<_JniMarshal_PPLLLLFI_V> (dlg).Wrap_JniMarshal_PPLLLLFI_V);
				case nameof (_JniMarshal_PPLLLLI_I):
					return new _JniMarshal_PPLLLLI_I (Unsafe.As<_JniMarshal_PPLLLLI_I> (dlg).Wrap_JniMarshal_PPLLLLI_I);
				case nameof (_JniMarshal_PPLLLLI_L):
					return new _JniMarshal_PPLLLLI_L (Unsafe.As<_JniMarshal_PPLLLLI_L> (dlg).Wrap_JniMarshal_PPLLLLI_L);
				case nameof (_JniMarshal_PPLLLLI_V):
					return new _JniMarshal_PPLLLLI_V (Unsafe.As<_JniMarshal_PPLLLLI_V> (dlg).Wrap_JniMarshal_PPLLLLI_V);
				case nameof (_JniMarshal_PPLLLLI_Z):
					return new _JniMarshal_PPLLLLI_Z (Unsafe.As<_JniMarshal_PPLLLLI_Z> (dlg).Wrap_JniMarshal_PPLLLLI_Z);
				case nameof (_JniMarshal_PPLLLLII_V):
					return new _JniMarshal_PPLLLLII_V (Unsafe.As<_JniMarshal_PPLLLLII_V> (dlg).Wrap_JniMarshal_PPLLLLII_V);
				case nameof (_JniMarshal_PPLLLLII_Z):
					return new _JniMarshal_PPLLLLII_Z (Unsafe.As<_JniMarshal_PPLLLLII_Z> (dlg).Wrap_JniMarshal_PPLLLLII_Z);
				case nameof (_JniMarshal_PPLLLLIIFIILLLLLLJJJJJZ_L):
					return new _JniMarshal_PPLLLLIIFIILLLLLLJJJJJZ_L (Unsafe.As<_JniMarshal_PPLLLLIIFIILLLLLLJJJJJZ_L> (dlg).Wrap_JniMarshal_PPLLLLIIFIILLLLLLJJJJJZ_L);
				case nameof (_JniMarshal_PPLLLLILL_V):
					return new _JniMarshal_PPLLLLILL_V (Unsafe.As<_JniMarshal_PPLLLLILL_V> (dlg).Wrap_JniMarshal_PPLLLLILL_V);
				case nameof (_JniMarshal_PPLLLLILZJ_V):
					return new _JniMarshal_PPLLLLILZJ_V (Unsafe.As<_JniMarshal_PPLLLLILZJ_V> (dlg).Wrap_JniMarshal_PPLLLLILZJ_V);
				case nameof (_JniMarshal_PPLLLLJ_J):
					return new _JniMarshal_PPLLLLJ_J (Unsafe.As<_JniMarshal_PPLLLLJ_J> (dlg).Wrap_JniMarshal_PPLLLLJ_J);
				case nameof (_JniMarshal_PPLLLLJ_V):
					return new _JniMarshal_PPLLLLJ_V (Unsafe.As<_JniMarshal_PPLLLLJ_V> (dlg).Wrap_JniMarshal_PPLLLLJ_V);
				case nameof (_JniMarshal_PPLLLLJJIL_V):
					return new _JniMarshal_PPLLLLJJIL_V (Unsafe.As<_JniMarshal_PPLLLLJJIL_V> (dlg).Wrap_JniMarshal_PPLLLLJJIL_V);
				case nameof (_JniMarshal_PPLLLLL_L):
					return new _JniMarshal_PPLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLL_L);
				case nameof (_JniMarshal_PPLLLLL_V):
					return new _JniMarshal_PPLLLLL_V (Unsafe.As<_JniMarshal_PPLLLLL_V> (dlg).Wrap_JniMarshal_PPLLLLL_V);
				case nameof (_JniMarshal_PPLLLLL_Z):
					return new _JniMarshal_PPLLLLL_Z (Unsafe.As<_JniMarshal_PPLLLLL_Z> (dlg).Wrap_JniMarshal_PPLLLLL_Z);
				case nameof (_JniMarshal_PPLLLLLI_V):
					return new _JniMarshal_PPLLLLLI_V (Unsafe.As<_JniMarshal_PPLLLLLI_V> (dlg).Wrap_JniMarshal_PPLLLLLI_V);
				case nameof (_JniMarshal_PPLLLLLIL_V):
					return new _JniMarshal_PPLLLLLIL_V (Unsafe.As<_JniMarshal_PPLLLLLIL_V> (dlg).Wrap_JniMarshal_PPLLLLLIL_V);
				case nameof (_JniMarshal_PPLLLLLILL_V):
					return new _JniMarshal_PPLLLLLILL_V (Unsafe.As<_JniMarshal_PPLLLLLILL_V> (dlg).Wrap_JniMarshal_PPLLLLLILL_V);
				case nameof (_JniMarshal_PPLLLLLIZLZIZIJ_L):
					return new _JniMarshal_PPLLLLLIZLZIZIJ_L (Unsafe.As<_JniMarshal_PPLLLLLIZLZIZIJ_L> (dlg).Wrap_JniMarshal_PPLLLLLIZLZIZIJ_L);
				case nameof (_JniMarshal_PPLLLLLJ_Z):
					return new _JniMarshal_PPLLLLLJ_Z (Unsafe.As<_JniMarshal_PPLLLLLJ_Z> (dlg).Wrap_JniMarshal_PPLLLLLJ_Z);
				case nameof (_JniMarshal_PPLLLLLL_L):
					return new _JniMarshal_PPLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLL_V):
					return new _JniMarshal_PPLLLLLL_V (Unsafe.As<_JniMarshal_PPLLLLLL_V> (dlg).Wrap_JniMarshal_PPLLLLLL_V);
				case nameof (_JniMarshal_PPLLLLLL_Z):
					return new _JniMarshal_PPLLLLLL_Z (Unsafe.As<_JniMarshal_PPLLLLLL_Z> (dlg).Wrap_JniMarshal_PPLLLLLL_Z);
				case nameof (_JniMarshal_PPLLLLLLI_L):
					return new _JniMarshal_PPLLLLLLI_L (Unsafe.As<_JniMarshal_PPLLLLLLI_L> (dlg).Wrap_JniMarshal_PPLLLLLLI_L);
				case nameof (_JniMarshal_PPLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLL_V):
					return new _JniMarshal_PPLLLLLLL_V (Unsafe.As<_JniMarshal_PPLLLLLLL_V> (dlg).Wrap_JniMarshal_PPLLLLLLL_V);
				case nameof (_JniMarshal_PPLLLLLLLJ_L):
					return new _JniMarshal_PPLLLLLLLJ_L (Unsafe.As<_JniMarshal_PPLLLLLLLJ_L> (dlg).Wrap_JniMarshal_PPLLLLLLLJ_L);
				case nameof (_JniMarshal_PPLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLL_V):
					return new _JniMarshal_PPLLLLLLLL_V (Unsafe.As<_JniMarshal_PPLLLLLLLL_V> (dlg).Wrap_JniMarshal_PPLLLLLLLL_V);
				case nameof (_JniMarshal_PPLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLL_V):
					return new _JniMarshal_PPLLLLLLLLL_V (Unsafe.As<_JniMarshal_PPLLLLLLLLL_V> (dlg).Wrap_JniMarshal_PPLLLLLLLLL_V);
				case nameof (_JniMarshal_PPLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLL_V):
					return new _JniMarshal_PPLLLLLLLLLL_V (Unsafe.As<_JniMarshal_PPLLLLLLLLLL_V> (dlg).Wrap_JniMarshal_PPLLLLLLLLLL_V);
				case nameof (_JniMarshal_PPLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLL_V):
					return new _JniMarshal_PPLLLLLLLLLLL_V (Unsafe.As<_JniMarshal_PPLLLLLLLLLLL_V> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLL_V);
				case nameof (_JniMarshal_PPLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLL_V):
					return new _JniMarshal_PPLLLLLLLLLLLL_V (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLL_V> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLL_V);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLLLLLLLLLLLLLLLLLL_L):
					return new _JniMarshal_PPLLLLLLLLLLLLLLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLLLLLLLLLLLLLLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLLLLLLLLLLLLLLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLLLLLZ_V):
					return new _JniMarshal_PPLLLLLZ_V (Unsafe.As<_JniMarshal_PPLLLLLZ_V> (dlg).Wrap_JniMarshal_PPLLLLLZ_V);
				case nameof (_JniMarshal_PPLLLLZ_L):
					return new _JniMarshal_PPLLLLZ_L (Unsafe.As<_JniMarshal_PPLLLLZ_L> (dlg).Wrap_JniMarshal_PPLLLLZ_L);
				case nameof (_JniMarshal_PPLLLLZ_V):
					return new _JniMarshal_PPLLLLZ_V (Unsafe.As<_JniMarshal_PPLLLLZ_V> (dlg).Wrap_JniMarshal_PPLLLLZ_V);
				case nameof (_JniMarshal_PPLLLLZ_Z):
					return new _JniMarshal_PPLLLLZ_Z (Unsafe.As<_JniMarshal_PPLLLLZ_Z> (dlg).Wrap_JniMarshal_PPLLLLZ_Z);
				case nameof (_JniMarshal_PPLLLZ_I):
					return new _JniMarshal_PPLLLZ_I (Unsafe.As<_JniMarshal_PPLLLZ_I> (dlg).Wrap_JniMarshal_PPLLLZ_I);
				case nameof (_JniMarshal_PPLLLZ_L):
					return new _JniMarshal_PPLLLZ_L (Unsafe.As<_JniMarshal_PPLLLZ_L> (dlg).Wrap_JniMarshal_PPLLLZ_L);
				case nameof (_JniMarshal_PPLLLZ_V):
					return new _JniMarshal_PPLLLZ_V (Unsafe.As<_JniMarshal_PPLLLZ_V> (dlg).Wrap_JniMarshal_PPLLLZ_V);
				case nameof (_JniMarshal_PPLLLZ_Z):
					return new _JniMarshal_PPLLLZ_Z (Unsafe.As<_JniMarshal_PPLLLZ_Z> (dlg).Wrap_JniMarshal_PPLLLZ_Z);
				case nameof (_JniMarshal_PPLLLZL_L):
					return new _JniMarshal_PPLLLZL_L (Unsafe.As<_JniMarshal_PPLLLZL_L> (dlg).Wrap_JniMarshal_PPLLLZL_L);
				case nameof (_JniMarshal_PPLLLZL_V):
					return new _JniMarshal_PPLLLZL_V (Unsafe.As<_JniMarshal_PPLLLZL_V> (dlg).Wrap_JniMarshal_PPLLLZL_V);
				case nameof (_JniMarshal_PPLLLZLL_L):
					return new _JniMarshal_PPLLLZLL_L (Unsafe.As<_JniMarshal_PPLLLZLL_L> (dlg).Wrap_JniMarshal_PPLLLZLL_L);
				case nameof (_JniMarshal_PPLLLZZ_L):
					return new _JniMarshal_PPLLLZZ_L (Unsafe.As<_JniMarshal_PPLLLZZ_L> (dlg).Wrap_JniMarshal_PPLLLZZ_L);
				case nameof (_JniMarshal_PPLLLZZ_Z):
					return new _JniMarshal_PPLLLZZ_Z (Unsafe.As<_JniMarshal_PPLLLZZ_Z> (dlg).Wrap_JniMarshal_PPLLLZZ_Z);
				case nameof (_JniMarshal_PPLLS_L):
					return new _JniMarshal_PPLLS_L (Unsafe.As<_JniMarshal_PPLLS_L> (dlg).Wrap_JniMarshal_PPLLS_L);
				case nameof (_JniMarshal_PPLLS_V):
					return new _JniMarshal_PPLLS_V (Unsafe.As<_JniMarshal_PPLLS_V> (dlg).Wrap_JniMarshal_PPLLS_V);
				case nameof (_JniMarshal_PPLLZ_I):
					return new _JniMarshal_PPLLZ_I (Unsafe.As<_JniMarshal_PPLLZ_I> (dlg).Wrap_JniMarshal_PPLLZ_I);
				case nameof (_JniMarshal_PPLLZ_L):
					return new _JniMarshal_PPLLZ_L (Unsafe.As<_JniMarshal_PPLLZ_L> (dlg).Wrap_JniMarshal_PPLLZ_L);
				case nameof (_JniMarshal_PPLLZ_V):
					return new _JniMarshal_PPLLZ_V (Unsafe.As<_JniMarshal_PPLLZ_V> (dlg).Wrap_JniMarshal_PPLLZ_V);
				case nameof (_JniMarshal_PPLLZ_Z):
					return new _JniMarshal_PPLLZ_Z (Unsafe.As<_JniMarshal_PPLLZ_Z> (dlg).Wrap_JniMarshal_PPLLZ_Z);
				case nameof (_JniMarshal_PPLLZI_I):
					return new _JniMarshal_PPLLZI_I (Unsafe.As<_JniMarshal_PPLLZI_I> (dlg).Wrap_JniMarshal_PPLLZI_I);
				case nameof (_JniMarshal_PPLLZI_L):
					return new _JniMarshal_PPLLZI_L (Unsafe.As<_JniMarshal_PPLLZI_L> (dlg).Wrap_JniMarshal_PPLLZI_L);
				case nameof (_JniMarshal_PPLLZI_V):
					return new _JniMarshal_PPLLZI_V (Unsafe.As<_JniMarshal_PPLLZI_V> (dlg).Wrap_JniMarshal_PPLLZI_V);
				case nameof (_JniMarshal_PPLLZIL_V):
					return new _JniMarshal_PPLLZIL_V (Unsafe.As<_JniMarshal_PPLLZIL_V> (dlg).Wrap_JniMarshal_PPLLZIL_V);
				case nameof (_JniMarshal_PPLLZJ_L):
					return new _JniMarshal_PPLLZJ_L (Unsafe.As<_JniMarshal_PPLLZJ_L> (dlg).Wrap_JniMarshal_PPLLZJ_L);
				case nameof (_JniMarshal_PPLLZJL_L):
					return new _JniMarshal_PPLLZJL_L (Unsafe.As<_JniMarshal_PPLLZJL_L> (dlg).Wrap_JniMarshal_PPLLZJL_L);
				case nameof (_JniMarshal_PPLLZJLL_L):
					return new _JniMarshal_PPLLZJLL_L (Unsafe.As<_JniMarshal_PPLLZJLL_L> (dlg).Wrap_JniMarshal_PPLLZJLL_L);
				case nameof (_JniMarshal_PPLLZL_L):
					return new _JniMarshal_PPLLZL_L (Unsafe.As<_JniMarshal_PPLLZL_L> (dlg).Wrap_JniMarshal_PPLLZL_L);
				case nameof (_JniMarshal_PPLLZL_V):
					return new _JniMarshal_PPLLZL_V (Unsafe.As<_JniMarshal_PPLLZL_V> (dlg).Wrap_JniMarshal_PPLLZL_V);
				case nameof (_JniMarshal_PPLLZLL_L):
					return new _JniMarshal_PPLLZLL_L (Unsafe.As<_JniMarshal_PPLLZLL_L> (dlg).Wrap_JniMarshal_PPLLZLL_L);
				case nameof (_JniMarshal_PPLLZLLJZ_J):
					return new _JniMarshal_PPLLZLLJZ_J (Unsafe.As<_JniMarshal_PPLLZLLJZ_J> (dlg).Wrap_JniMarshal_PPLLZLLJZ_J);
				case nameof (_JniMarshal_PPLLZLLJZLL_J):
					return new _JniMarshal_PPLLZLLJZLL_J (Unsafe.As<_JniMarshal_PPLLZLLJZLL_J> (dlg).Wrap_JniMarshal_PPLLZLLJZLL_J);
				case nameof (_JniMarshal_PPLLZZ_L):
					return new _JniMarshal_PPLLZZ_L (Unsafe.As<_JniMarshal_PPLLZZ_L> (dlg).Wrap_JniMarshal_PPLLZZ_L);
				case nameof (_JniMarshal_PPLLZZ_Z):
					return new _JniMarshal_PPLLZZ_Z (Unsafe.As<_JniMarshal_PPLLZZ_Z> (dlg).Wrap_JniMarshal_PPLLZZ_Z);
				case nameof (_JniMarshal_PPLLZZI_L):
					return new _JniMarshal_PPLLZZI_L (Unsafe.As<_JniMarshal_PPLLZZI_L> (dlg).Wrap_JniMarshal_PPLLZZI_L);
				case nameof (_JniMarshal_PPLS_L):
					return new _JniMarshal_PPLS_L (Unsafe.As<_JniMarshal_PPLS_L> (dlg).Wrap_JniMarshal_PPLS_L);
				case nameof (_JniMarshal_PPLS_S):
					return new _JniMarshal_PPLS_S (Unsafe.As<_JniMarshal_PPLS_S> (dlg).Wrap_JniMarshal_PPLS_S);
				case nameof (_JniMarshal_PPLS_V):
					return new _JniMarshal_PPLS_V (Unsafe.As<_JniMarshal_PPLS_V> (dlg).Wrap_JniMarshal_PPLS_V);
				case nameof (_JniMarshal_PPLSB_V):
					return new _JniMarshal_PPLSB_V (Unsafe.As<_JniMarshal_PPLSB_V> (dlg).Wrap_JniMarshal_PPLSB_V);
				case nameof (_JniMarshal_PPLSBBBB_V):
					return new _JniMarshal_PPLSBBBB_V (Unsafe.As<_JniMarshal_PPLSBBBB_V> (dlg).Wrap_JniMarshal_PPLSBBBB_V);
				case nameof (_JniMarshal_PPLSC_V):
					return new _JniMarshal_PPLSC_V (Unsafe.As<_JniMarshal_PPLSC_V> (dlg).Wrap_JniMarshal_PPLSC_V);
				case nameof (_JniMarshal_PPLSD_V):
					return new _JniMarshal_PPLSD_V (Unsafe.As<_JniMarshal_PPLSD_V> (dlg).Wrap_JniMarshal_PPLSD_V);
				case nameof (_JniMarshal_PPLSF_V):
					return new _JniMarshal_PPLSF_V (Unsafe.As<_JniMarshal_PPLSF_V> (dlg).Wrap_JniMarshal_PPLSF_V);
				case nameof (_JniMarshal_PPLSI_V):
					return new _JniMarshal_PPLSI_V (Unsafe.As<_JniMarshal_PPLSI_V> (dlg).Wrap_JniMarshal_PPLSI_V);
				case nameof (_JniMarshal_PPLSJ_V):
					return new _JniMarshal_PPLSJ_V (Unsafe.As<_JniMarshal_PPLSJ_V> (dlg).Wrap_JniMarshal_PPLSJ_V);
				case nameof (_JniMarshal_PPLSL_V):
					return new _JniMarshal_PPLSL_V (Unsafe.As<_JniMarshal_PPLSL_V> (dlg).Wrap_JniMarshal_PPLSL_V);
				case nameof (_JniMarshal_PPLSS_V):
					return new _JniMarshal_PPLSS_V (Unsafe.As<_JniMarshal_PPLSS_V> (dlg).Wrap_JniMarshal_PPLSS_V);
				case nameof (_JniMarshal_PPLSZ_V):
					return new _JniMarshal_PPLSZ_V (Unsafe.As<_JniMarshal_PPLSZ_V> (dlg).Wrap_JniMarshal_PPLSZ_V);
				case nameof (_JniMarshal_PPLZ_F):
					return new _JniMarshal_PPLZ_F (Unsafe.As<_JniMarshal_PPLZ_F> (dlg).Wrap_JniMarshal_PPLZ_F);
				case nameof (_JniMarshal_PPLZ_I):
					return new _JniMarshal_PPLZ_I (Unsafe.As<_JniMarshal_PPLZ_I> (dlg).Wrap_JniMarshal_PPLZ_I);
				case nameof (_JniMarshal_PPLZ_L):
					return new _JniMarshal_PPLZ_L (Unsafe.As<_JniMarshal_PPLZ_L> (dlg).Wrap_JniMarshal_PPLZ_L);
				case nameof (_JniMarshal_PPLZ_V):
					return new _JniMarshal_PPLZ_V (Unsafe.As<_JniMarshal_PPLZ_V> (dlg).Wrap_JniMarshal_PPLZ_V);
				case nameof (_JniMarshal_PPLZ_Z):
					return new _JniMarshal_PPLZ_Z (Unsafe.As<_JniMarshal_PPLZ_Z> (dlg).Wrap_JniMarshal_PPLZ_Z);
				case nameof (_JniMarshal_PPLZB_V):
					return new _JniMarshal_PPLZB_V (Unsafe.As<_JniMarshal_PPLZB_V> (dlg).Wrap_JniMarshal_PPLZB_V);
				case nameof (_JniMarshal_PPLZC_V):
					return new _JniMarshal_PPLZC_V (Unsafe.As<_JniMarshal_PPLZC_V> (dlg).Wrap_JniMarshal_PPLZC_V);
				case nameof (_JniMarshal_PPLZD_V):
					return new _JniMarshal_PPLZD_V (Unsafe.As<_JniMarshal_PPLZD_V> (dlg).Wrap_JniMarshal_PPLZD_V);
				case nameof (_JniMarshal_PPLZF_V):
					return new _JniMarshal_PPLZF_V (Unsafe.As<_JniMarshal_PPLZF_V> (dlg).Wrap_JniMarshal_PPLZF_V);
				case nameof (_JniMarshal_PPLZFF_V):
					return new _JniMarshal_PPLZFF_V (Unsafe.As<_JniMarshal_PPLZFF_V> (dlg).Wrap_JniMarshal_PPLZFF_V);
				case nameof (_JniMarshal_PPLZFL_I):
					return new _JniMarshal_PPLZFL_I (Unsafe.As<_JniMarshal_PPLZFL_I> (dlg).Wrap_JniMarshal_PPLZFL_I);
				case nameof (_JniMarshal_PPLZI_L):
					return new _JniMarshal_PPLZI_L (Unsafe.As<_JniMarshal_PPLZI_L> (dlg).Wrap_JniMarshal_PPLZI_L);
				case nameof (_JniMarshal_PPLZI_V):
					return new _JniMarshal_PPLZI_V (Unsafe.As<_JniMarshal_PPLZI_V> (dlg).Wrap_JniMarshal_PPLZI_V);
				case nameof (_JniMarshal_PPLZI_Z):
					return new _JniMarshal_PPLZI_Z (Unsafe.As<_JniMarshal_PPLZI_Z> (dlg).Wrap_JniMarshal_PPLZI_Z);
				case nameof (_JniMarshal_PPLZIII_Z):
					return new _JniMarshal_PPLZIII_Z (Unsafe.As<_JniMarshal_PPLZIII_Z> (dlg).Wrap_JniMarshal_PPLZIII_Z);
				case nameof (_JniMarshal_PPLZIIII_Z):
					return new _JniMarshal_PPLZIIII_Z (Unsafe.As<_JniMarshal_PPLZIIII_Z> (dlg).Wrap_JniMarshal_PPLZIIII_Z);
				case nameof (_JniMarshal_PPLZIIL_V):
					return new _JniMarshal_PPLZIIL_V (Unsafe.As<_JniMarshal_PPLZIIL_V> (dlg).Wrap_JniMarshal_PPLZIIL_V);
				case nameof (_JniMarshal_PPLZILL_V):
					return new _JniMarshal_PPLZILL_V (Unsafe.As<_JniMarshal_PPLZILL_V> (dlg).Wrap_JniMarshal_PPLZILL_V);
				case nameof (_JniMarshal_PPLZJ_L):
					return new _JniMarshal_PPLZJ_L (Unsafe.As<_JniMarshal_PPLZJ_L> (dlg).Wrap_JniMarshal_PPLZJ_L);
				case nameof (_JniMarshal_PPLZJ_V):
					return new _JniMarshal_PPLZJ_V (Unsafe.As<_JniMarshal_PPLZJ_V> (dlg).Wrap_JniMarshal_PPLZJ_V);
				case nameof (_JniMarshal_PPLZJL_L):
					return new _JniMarshal_PPLZJL_L (Unsafe.As<_JniMarshal_PPLZJL_L> (dlg).Wrap_JniMarshal_PPLZJL_L);
				case nameof (_JniMarshal_PPLZJL_V):
					return new _JniMarshal_PPLZJL_V (Unsafe.As<_JniMarshal_PPLZJL_V> (dlg).Wrap_JniMarshal_PPLZJL_V);
				case nameof (_JniMarshal_PPLZJLL_L):
					return new _JniMarshal_PPLZJLL_L (Unsafe.As<_JniMarshal_PPLZJLL_L> (dlg).Wrap_JniMarshal_PPLZJLL_L);
				case nameof (_JniMarshal_PPLZL_L):
					return new _JniMarshal_PPLZL_L (Unsafe.As<_JniMarshal_PPLZL_L> (dlg).Wrap_JniMarshal_PPLZL_L);
				case nameof (_JniMarshal_PPLZL_V):
					return new _JniMarshal_PPLZL_V (Unsafe.As<_JniMarshal_PPLZL_V> (dlg).Wrap_JniMarshal_PPLZL_V);
				case nameof (_JniMarshal_PPLZL_Z):
					return new _JniMarshal_PPLZL_Z (Unsafe.As<_JniMarshal_PPLZL_Z> (dlg).Wrap_JniMarshal_PPLZL_Z);
				case nameof (_JniMarshal_PPLZLL_V):
					return new _JniMarshal_PPLZLL_V (Unsafe.As<_JniMarshal_PPLZLL_V> (dlg).Wrap_JniMarshal_PPLZLL_V);
				case nameof (_JniMarshal_PPLZLL_Z):
					return new _JniMarshal_PPLZLL_Z (Unsafe.As<_JniMarshal_PPLZLL_Z> (dlg).Wrap_JniMarshal_PPLZLL_Z);
				case nameof (_JniMarshal_PPLZLLL_V):
					return new _JniMarshal_PPLZLLL_V (Unsafe.As<_JniMarshal_PPLZLLL_V> (dlg).Wrap_JniMarshal_PPLZLLL_V);
				case nameof (_JniMarshal_PPLZLLLLLLLL_L):
					return new _JniMarshal_PPLZLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLZLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLZLLLLLLLL_L);
				case nameof (_JniMarshal_PPLZLLLLLLLLL_L):
					return new _JniMarshal_PPLZLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPLZLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPLZLLLLLLLLL_L);
				case nameof (_JniMarshal_PPLZLLZ_V):
					return new _JniMarshal_PPLZLLZ_V (Unsafe.As<_JniMarshal_PPLZLLZ_V> (dlg).Wrap_JniMarshal_PPLZLLZ_V);
				case nameof (_JniMarshal_PPLZLZ_L):
					return new _JniMarshal_PPLZLZ_L (Unsafe.As<_JniMarshal_PPLZLZ_L> (dlg).Wrap_JniMarshal_PPLZLZ_L);
				case nameof (_JniMarshal_PPLZLZ_V):
					return new _JniMarshal_PPLZLZ_V (Unsafe.As<_JniMarshal_PPLZLZ_V> (dlg).Wrap_JniMarshal_PPLZLZ_V);
				case nameof (_JniMarshal_PPLZS_V):
					return new _JniMarshal_PPLZS_V (Unsafe.As<_JniMarshal_PPLZS_V> (dlg).Wrap_JniMarshal_PPLZS_V);
				case nameof (_JniMarshal_PPLZZ_L):
					return new _JniMarshal_PPLZZ_L (Unsafe.As<_JniMarshal_PPLZZ_L> (dlg).Wrap_JniMarshal_PPLZZ_L);
				case nameof (_JniMarshal_PPLZZ_V):
					return new _JniMarshal_PPLZZ_V (Unsafe.As<_JniMarshal_PPLZZ_V> (dlg).Wrap_JniMarshal_PPLZZ_V);
				case nameof (_JniMarshal_PPLZZ_Z):
					return new _JniMarshal_PPLZZ_Z (Unsafe.As<_JniMarshal_PPLZZ_Z> (dlg).Wrap_JniMarshal_PPLZZ_Z);
				case nameof (_JniMarshal_PPLZZI_L):
					return new _JniMarshal_PPLZZI_L (Unsafe.As<_JniMarshal_PPLZZI_L> (dlg).Wrap_JniMarshal_PPLZZI_L);
				case nameof (_JniMarshal_PPLZZL_Z):
					return new _JniMarshal_PPLZZL_Z (Unsafe.As<_JniMarshal_PPLZZL_Z> (dlg).Wrap_JniMarshal_PPLZZL_Z);
				case nameof (_JniMarshal_PPLZZZ_V):
					return new _JniMarshal_PPLZZZ_V (Unsafe.As<_JniMarshal_PPLZZZ_V> (dlg).Wrap_JniMarshal_PPLZZZ_V);
				case nameof (_JniMarshal_PPLZZZZZZZII_V):
					return new _JniMarshal_PPLZZZZZZZII_V (Unsafe.As<_JniMarshal_PPLZZZZZZZII_V> (dlg).Wrap_JniMarshal_PPLZZZZZZZII_V);
				case nameof (_JniMarshal_PPS_I):
					return new _JniMarshal_PPS_I (Unsafe.As<_JniMarshal_PPS_I> (dlg).Wrap_JniMarshal_PPS_I);
				case nameof (_JniMarshal_PPS_L):
					return new _JniMarshal_PPS_L (Unsafe.As<_JniMarshal_PPS_L> (dlg).Wrap_JniMarshal_PPS_L);
				case nameof (_JniMarshal_PPS_S):
					return new _JniMarshal_PPS_S (Unsafe.As<_JniMarshal_PPS_S> (dlg).Wrap_JniMarshal_PPS_S);
				case nameof (_JniMarshal_PPS_V):
					return new _JniMarshal_PPS_V (Unsafe.As<_JniMarshal_PPS_V> (dlg).Wrap_JniMarshal_PPS_V);
				case nameof (_JniMarshal_PPSL_L):
					return new _JniMarshal_PPSL_L (Unsafe.As<_JniMarshal_PPSL_L> (dlg).Wrap_JniMarshal_PPSL_L);
				case nameof (_JniMarshal_PPSLLLL_V):
					return new _JniMarshal_PPSLLLL_V (Unsafe.As<_JniMarshal_PPSLLLL_V> (dlg).Wrap_JniMarshal_PPSLLLL_V);
				case nameof (_JniMarshal_PPSS_V):
					return new _JniMarshal_PPSS_V (Unsafe.As<_JniMarshal_PPSS_V> (dlg).Wrap_JniMarshal_PPSS_V);
				case nameof (_JniMarshal_PPSSSSS_V):
					return new _JniMarshal_PPSSSSS_V (Unsafe.As<_JniMarshal_PPSSSSS_V> (dlg).Wrap_JniMarshal_PPSSSSS_V);
				case nameof (_JniMarshal_PPZ_I):
					return new _JniMarshal_PPZ_I (Unsafe.As<_JniMarshal_PPZ_I> (dlg).Wrap_JniMarshal_PPZ_I);
				case nameof (_JniMarshal_PPZ_J):
					return new _JniMarshal_PPZ_J (Unsafe.As<_JniMarshal_PPZ_J> (dlg).Wrap_JniMarshal_PPZ_J);
				case nameof (_JniMarshal_PPZ_L):
					return new _JniMarshal_PPZ_L (Unsafe.As<_JniMarshal_PPZ_L> (dlg).Wrap_JniMarshal_PPZ_L);
				case nameof (_JniMarshal_PPZ_V):
					return new _JniMarshal_PPZ_V (Unsafe.As<_JniMarshal_PPZ_V> (dlg).Wrap_JniMarshal_PPZ_V);
				case nameof (_JniMarshal_PPZ_Z):
					return new _JniMarshal_PPZ_Z (Unsafe.As<_JniMarshal_PPZ_Z> (dlg).Wrap_JniMarshal_PPZ_Z);
				case nameof (_JniMarshal_PPZC_V):
					return new _JniMarshal_PPZC_V (Unsafe.As<_JniMarshal_PPZC_V> (dlg).Wrap_JniMarshal_PPZC_V);
				case nameof (_JniMarshal_PPZDZD_D):
					return new _JniMarshal_PPZDZD_D (Unsafe.As<_JniMarshal_PPZDZD_D> (dlg).Wrap_JniMarshal_PPZDZD_D);
				case nameof (_JniMarshal_PPZFZF_F):
					return new _JniMarshal_PPZFZF_F (Unsafe.As<_JniMarshal_PPZFZF_F> (dlg).Wrap_JniMarshal_PPZFZF_F);
				case nameof (_JniMarshal_PPZI_L):
					return new _JniMarshal_PPZI_L (Unsafe.As<_JniMarshal_PPZI_L> (dlg).Wrap_JniMarshal_PPZI_L);
				case nameof (_JniMarshal_PPZI_V):
					return new _JniMarshal_PPZI_V (Unsafe.As<_JniMarshal_PPZI_V> (dlg).Wrap_JniMarshal_PPZI_V);
				case nameof (_JniMarshal_PPZI_Z):
					return new _JniMarshal_PPZI_Z (Unsafe.As<_JniMarshal_PPZI_Z> (dlg).Wrap_JniMarshal_PPZI_Z);
				case nameof (_JniMarshal_PPZII_L):
					return new _JniMarshal_PPZII_L (Unsafe.As<_JniMarshal_PPZII_L> (dlg).Wrap_JniMarshal_PPZII_L);
				case nameof (_JniMarshal_PPZII_V):
					return new _JniMarshal_PPZII_V (Unsafe.As<_JniMarshal_PPZII_V> (dlg).Wrap_JniMarshal_PPZII_V);
				case nameof (_JniMarshal_PPZIIII_L):
					return new _JniMarshal_PPZIIII_L (Unsafe.As<_JniMarshal_PPZIIII_L> (dlg).Wrap_JniMarshal_PPZIIII_L);
				case nameof (_JniMarshal_PPZIIII_V):
					return new _JniMarshal_PPZIIII_V (Unsafe.As<_JniMarshal_PPZIIII_V> (dlg).Wrap_JniMarshal_PPZIIII_V);
				case nameof (_JniMarshal_PPZIIIIII_L):
					return new _JniMarshal_PPZIIIIII_L (Unsafe.As<_JniMarshal_PPZIIIIII_L> (dlg).Wrap_JniMarshal_PPZIIIIII_L);
				case nameof (_JniMarshal_PPZIL_L):
					return new _JniMarshal_PPZIL_L (Unsafe.As<_JniMarshal_PPZIL_L> (dlg).Wrap_JniMarshal_PPZIL_L);
				case nameof (_JniMarshal_PPZIL_V):
					return new _JniMarshal_PPZIL_V (Unsafe.As<_JniMarshal_PPZIL_V> (dlg).Wrap_JniMarshal_PPZIL_V);
				case nameof (_JniMarshal_PPZILI_V):
					return new _JniMarshal_PPZILI_V (Unsafe.As<_JniMarshal_PPZILI_V> (dlg).Wrap_JniMarshal_PPZILI_V);
				case nameof (_JniMarshal_PPZILII_V):
					return new _JniMarshal_PPZILII_V (Unsafe.As<_JniMarshal_PPZILII_V> (dlg).Wrap_JniMarshal_PPZILII_V);
				case nameof (_JniMarshal_PPZIZ_L):
					return new _JniMarshal_PPZIZ_L (Unsafe.As<_JniMarshal_PPZIZ_L> (dlg).Wrap_JniMarshal_PPZIZ_L);
				case nameof (_JniMarshal_PPZIZI_I):
					return new _JniMarshal_PPZIZI_I (Unsafe.As<_JniMarshal_PPZIZI_I> (dlg).Wrap_JniMarshal_PPZIZI_I);
				case nameof (_JniMarshal_PPZJ_V):
					return new _JniMarshal_PPZJ_V (Unsafe.As<_JniMarshal_PPZJ_V> (dlg).Wrap_JniMarshal_PPZJ_V);
				case nameof (_JniMarshal_PPZJZJ_J):
					return new _JniMarshal_PPZJZJ_J (Unsafe.As<_JniMarshal_PPZJZJ_J> (dlg).Wrap_JniMarshal_PPZJZJ_J);
				case nameof (_JniMarshal_PPZL_L):
					return new _JniMarshal_PPZL_L (Unsafe.As<_JniMarshal_PPZL_L> (dlg).Wrap_JniMarshal_PPZL_L);
				case nameof (_JniMarshal_PPZL_V):
					return new _JniMarshal_PPZL_V (Unsafe.As<_JniMarshal_PPZL_V> (dlg).Wrap_JniMarshal_PPZL_V);
				case nameof (_JniMarshal_PPZL_Z):
					return new _JniMarshal_PPZL_Z (Unsafe.As<_JniMarshal_PPZL_Z> (dlg).Wrap_JniMarshal_PPZL_Z);
				case nameof (_JniMarshal_PPZLI_V):
					return new _JniMarshal_PPZLI_V (Unsafe.As<_JniMarshal_PPZLI_V> (dlg).Wrap_JniMarshal_PPZLI_V);
				case nameof (_JniMarshal_PPZLI_Z):
					return new _JniMarshal_PPZLI_Z (Unsafe.As<_JniMarshal_PPZLI_Z> (dlg).Wrap_JniMarshal_PPZLI_Z);
				case nameof (_JniMarshal_PPZLL_L):
					return new _JniMarshal_PPZLL_L (Unsafe.As<_JniMarshal_PPZLL_L> (dlg).Wrap_JniMarshal_PPZLL_L);
				case nameof (_JniMarshal_PPZLL_V):
					return new _JniMarshal_PPZLL_V (Unsafe.As<_JniMarshal_PPZLL_V> (dlg).Wrap_JniMarshal_PPZLL_V);
				case nameof (_JniMarshal_PPZLL_Z):
					return new _JniMarshal_PPZLL_Z (Unsafe.As<_JniMarshal_PPZLL_Z> (dlg).Wrap_JniMarshal_PPZLL_Z);
				case nameof (_JniMarshal_PPZLLL_V):
					return new _JniMarshal_PPZLLL_V (Unsafe.As<_JniMarshal_PPZLLL_V> (dlg).Wrap_JniMarshal_PPZLLL_V);
				case nameof (_JniMarshal_PPZLLLL_L):
					return new _JniMarshal_PPZLLLL_L (Unsafe.As<_JniMarshal_PPZLLLL_L> (dlg).Wrap_JniMarshal_PPZLLLL_L);
				case nameof (_JniMarshal_PPZLLLL_V):
					return new _JniMarshal_PPZLLLL_V (Unsafe.As<_JniMarshal_PPZLLLL_V> (dlg).Wrap_JniMarshal_PPZLLLL_V);
				case nameof (_JniMarshal_PPZLLLLLL_L):
					return new _JniMarshal_PPZLLLLLL_L (Unsafe.As<_JniMarshal_PPZLLLLLL_L> (dlg).Wrap_JniMarshal_PPZLLLLLL_L);
				case nameof (_JniMarshal_PPZLLLLLLLL_L):
					return new _JniMarshal_PPZLLLLLLLL_L (Unsafe.As<_JniMarshal_PPZLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPZLLLLLLLL_L);
				case nameof (_JniMarshal_PPZLLLLLLLLL_L):
					return new _JniMarshal_PPZLLLLLLLLL_L (Unsafe.As<_JniMarshal_PPZLLLLLLLLL_L> (dlg).Wrap_JniMarshal_PPZLLLLLLLLL_L);
				case nameof (_JniMarshal_PPZLZL_L):
					return new _JniMarshal_PPZLZL_L (Unsafe.As<_JniMarshal_PPZLZL_L> (dlg).Wrap_JniMarshal_PPZLZL_L);
				case nameof (_JniMarshal_PPZZ_V):
					return new _JniMarshal_PPZZ_V (Unsafe.As<_JniMarshal_PPZZ_V> (dlg).Wrap_JniMarshal_PPZZ_V);
				case nameof (_JniMarshal_PPZZ_Z):
					return new _JniMarshal_PPZZ_Z (Unsafe.As<_JniMarshal_PPZZ_Z> (dlg).Wrap_JniMarshal_PPZZ_Z);
				case nameof (_JniMarshal_PPZZIIL_V):
					return new _JniMarshal_PPZZIIL_V (Unsafe.As<_JniMarshal_PPZZIIL_V> (dlg).Wrap_JniMarshal_PPZZIIL_V);
				case nameof (_JniMarshal_PPZZIILL_V):
					return new _JniMarshal_PPZZIILL_V (Unsafe.As<_JniMarshal_PPZZIILL_V> (dlg).Wrap_JniMarshal_PPZZIILL_V);
				case nameof (_JniMarshal_PPZZL_L):
					return new _JniMarshal_PPZZL_L (Unsafe.As<_JniMarshal_PPZZL_L> (dlg).Wrap_JniMarshal_PPZZL_L);
				case nameof (_JniMarshal_PPZZL_V):
					return new _JniMarshal_PPZZL_V (Unsafe.As<_JniMarshal_PPZZL_V> (dlg).Wrap_JniMarshal_PPZZL_V);
				case nameof (_JniMarshal_PPZZZ_V):
					return new _JniMarshal_PPZZZ_V (Unsafe.As<_JniMarshal_PPZZZ_V> (dlg).Wrap_JniMarshal_PPZZZ_V);
				case nameof (_JniMarshal_PPZZZZ_L):
					return new _JniMarshal_PPZZZZ_L (Unsafe.As<_JniMarshal_PPZZZZ_L> (dlg).Wrap_JniMarshal_PPZZZZ_L);
				case nameof (_JniMarshal_PPZZZZ_V):
					return new _JniMarshal_PPZZZZ_V (Unsafe.As<_JniMarshal_PPZZZZ_V> (dlg).Wrap_JniMarshal_PPZZZZ_V);
				case nameof (_JniMarshal_PPZZZZ_Z):
					return new _JniMarshal_PPZZZZ_Z (Unsafe.As<_JniMarshal_PPZZZZ_Z> (dlg).Wrap_JniMarshal_PPZZZZ_Z);
				default:
					return null;
			}
		}
	}
}
