using System;
using System.Diagnostics;

namespace Android.Runtime
{
	public static partial class JNINativeWrapper
	{
		static bool _unhandled_exception (Exception e)
		{
			if (Debugger.IsAttached || !JNIEnv.PropagateExceptions) {
				JNIEnv.mono_unhandled_exception (e);
				return false;
			}
			return true;
		}

		private static Delegate CreateBuiltInDelegate (Delegate dlg, Type delegateType)
		{
			switch (delegateType.Name) {
				case nameof (_JniMarshal_PP_V): {
					_JniMarshal_PP_V callback = (_JniMarshal_PP_V) Delegate.CreateDelegate (typeof (_JniMarshal_PP_V), dlg.Target, dlg.Method);
					_JniMarshal_PP_V result = (jnienv, klazz) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPI_V): {
					_JniMarshal_PPI_V callback = (_JniMarshal_PPI_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPI_V), dlg.Target, dlg.Method);
					_JniMarshal_PPI_V result = (jnienv, klazz, p0) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPL_L): {
					_JniMarshal_PPL_L callback = (_JniMarshal_PPL_L) Delegate.CreateDelegate (typeof (_JniMarshal_PPL_L), dlg.Target, dlg.Method);
					_JniMarshal_PPL_L result = (jnienv, klazz, p0) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							return callback (jnienv, klazz, p0);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							return default;
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPL_V): {
					_JniMarshal_PPL_V callback = (_JniMarshal_PPL_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPL_V), dlg.Target, dlg.Method);
					_JniMarshal_PPL_V result = (jnienv, klazz, p0) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPL_Z): {
					_JniMarshal_PPL_Z callback = (_JniMarshal_PPL_Z) Delegate.CreateDelegate (typeof (_JniMarshal_PPL_Z), dlg.Target, dlg.Method);
					_JniMarshal_PPL_Z result = (jnienv, klazz, p0) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							return callback (jnienv, klazz, p0);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							return default;
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPII_V): {
					_JniMarshal_PPII_V callback = (_JniMarshal_PPII_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPII_V), dlg.Target, dlg.Method);
					_JniMarshal_PPII_V result = (jnienv, klazz, p0, p1) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLI_V): {
					_JniMarshal_PPLI_V callback = (_JniMarshal_PPLI_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPLI_V), dlg.Target, dlg.Method);
					_JniMarshal_PPLI_V result = (jnienv, klazz, p0, p1) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLL_V): {
					_JniMarshal_PPLL_V callback = (_JniMarshal_PPLL_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPLL_V), dlg.Target, dlg.Method);
					_JniMarshal_PPLL_V result = (jnienv, klazz, p0, p1) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLL_Z): {
					_JniMarshal_PPLL_Z callback = (_JniMarshal_PPLL_Z) Delegate.CreateDelegate (typeof (_JniMarshal_PPLL_Z), dlg.Target, dlg.Method);
					_JniMarshal_PPLL_Z result = (jnienv, klazz, p0, p1) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							return callback (jnienv, klazz, p0, p1);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							return default;
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPIIL_V): {
					_JniMarshal_PPIIL_V callback = (_JniMarshal_PPIIL_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPIIL_V), dlg.Target, dlg.Method);
					_JniMarshal_PPIIL_V result = (jnienv, klazz, p0, p1, p2) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1, p2);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPILL_V): {
					_JniMarshal_PPILL_V callback = (_JniMarshal_PPILL_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPILL_V), dlg.Target, dlg.Method);
					_JniMarshal_PPILL_V result = (jnienv, klazz, p0, p1, p2) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1, p2);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLIL_Z): {
					_JniMarshal_PPLIL_Z callback = (_JniMarshal_PPLIL_Z) Delegate.CreateDelegate (typeof (_JniMarshal_PPLIL_Z), dlg.Target, dlg.Method);
					_JniMarshal_PPLIL_Z result = (jnienv, klazz, p0, p1, p2) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							return callback (jnienv, klazz, p0, p1, p2);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							return default;
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLLL_L): {
					_JniMarshal_PPLLL_L callback = (_JniMarshal_PPLLL_L) Delegate.CreateDelegate (typeof (_JniMarshal_PPLLL_L), dlg.Target, dlg.Method);
					_JniMarshal_PPLLL_L result = (jnienv, klazz, p0, p1, p2) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							return callback (jnienv, klazz, p0, p1, p2);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							return default;
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLLL_Z): {
					_JniMarshal_PPLLL_Z callback = (_JniMarshal_PPLLL_Z) Delegate.CreateDelegate (typeof (_JniMarshal_PPLLL_Z), dlg.Target, dlg.Method);
					_JniMarshal_PPLLL_Z result = (jnienv, klazz, p0, p1, p2) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							return callback (jnienv, klazz, p0, p1, p2);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							return default;
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPIIII_V): {
					_JniMarshal_PPIIII_V callback = (_JniMarshal_PPIIII_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPIIII_V), dlg.Target, dlg.Method);
					_JniMarshal_PPIIII_V result = (jnienv, klazz, p0, p1, p2, p3) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1, p2, p3);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLLLL_V): {
					_JniMarshal_PPLLLL_V callback = (_JniMarshal_PPLLLL_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPLLLL_V), dlg.Target, dlg.Method);
					_JniMarshal_PPLLLL_V result = (jnienv, klazz, p0, p1, p2, p3) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1, p2, p3);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLIIII_V): {
					_JniMarshal_PPLIIII_V callback = (_JniMarshal_PPLIIII_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPLIIII_V), dlg.Target, dlg.Method);
					_JniMarshal_PPLIIII_V result = (jnienv, klazz, p0, p1, p2, p3, p4) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1, p2, p3, p4);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPZIIII_V): {
					_JniMarshal_PPZIIII_V callback = (_JniMarshal_PPZIIII_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPZIIII_V), dlg.Target, dlg.Method);
					_JniMarshal_PPZIIII_V result = (jnienv, klazz, p0, p1, p2, p3, p4) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1, p2, p3, p4);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				case nameof (_JniMarshal_PPLIIIIIIII_V): {
					_JniMarshal_PPLIIIIIIII_V callback = (_JniMarshal_PPLIIIIIIII_V) Delegate.CreateDelegate (typeof (_JniMarshal_PPLIIIIIIII_V), dlg.Target, dlg.Method);
					_JniMarshal_PPLIIIIIIII_V result = (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8) => {
						JNIEnv.WaitForBridgeProcessing ();
						try {
							callback (jnienv, klazz, p0, p1, p2, p3, p4, p5, p6, p7, p8);
						} catch (Exception e) when (_unhandled_exception (e)) {
							AndroidEnvironment.UnhandledException (e);
							
						}
					};
					return result;
				}
				default:
					return null;
			}
		}
	}
}
