using System;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Util;
using Android.Views;
using Java.Net;
using Java.Security;
using Javax.Net.Ssl;
using Microsoft.Android.Runtime;

namespace Android.Runtime {

	public static class AndroidEnvironment {

		public const string AndroidLogAppName = "Mono.Android";

		static IX509TrustManager? sslTrustManager;
		static KeyStore? certStore;
		static object lock_ = new object ();
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
		static Type? httpMessageHandlerType;

		static void SetupTrustManager ()
		{
			if (sslTrustManager != null)
				return;

			lock (lock_) {
				TrustManagerFactory factory = TrustManagerFactory.GetInstance (TrustManagerFactory.DefaultAlgorithm)!;
				factory.Init ((KeyStore?) null);
				foreach (ITrustManager tm in factory.GetTrustManagers ()!) {
					try {
						sslTrustManager = tm.JavaCast<IX509TrustManager>();
					}
					catch {
						// ignore
					}
					if (sslTrustManager != null)
						break;
				}
			}
		}

		static void SetupCertStore ()
		{
			if (certStore != null)
				return;

			lock (lock_) {
				try {
					certStore = KeyStore.GetInstance ("AndroidCAStore")!;
					certStore.Load (null);
				} catch {
					// ignore
					certStore = null;
				}
			}
		}

		[DllImport ("libc")]
		static extern void exit (int status);

		public static void FailFast (string? message)
		{
			Logger.Log (LogLevel.Fatal, AndroidLogAppName, message);
			exit (-1);
		}

#if !JAVA_INTEROP
		internal static Exception GetExceptionForLastThrowable ()
		{
			IntPtr e = JNIEnv.ExceptionOccurred ();
			if (e == IntPtr.Zero)
				return null;
			JNIEnv.ExceptionClear ();
			return HandleToException (e);
		}

		static System.Exception HandleToException (IntPtr handle)
		{
			try {
				System.Exception result = (System.Exception) Java.Lang.Object.GetObject (handle, JniHandleOwnership.DoNotTransfer, typeof (Java.Lang.Throwable));
				var p = result as JavaProxyThrowable;
				if (p != null)
					return p.InnerException;
				return result;
			} finally {
				JNIEnv.DeleteLocalRef (handle);
			}
		}
#endif  // !JAVA_INTEROP

		public static event EventHandler<RaiseThrowableEventArgs>? UnhandledExceptionRaiser;

		public static void RaiseThrowable (Java.Lang.Throwable throwable)
		{
			if (throwable == null)
				throw new ArgumentNullException ("throwable");
			JNIEnv.Throw (throwable.Handle);
		}

		internal static void UnhandledException (Exception e)
		{
			var raisers         = UnhandledExceptionRaiser;
			if (raisers != null) {
				var info    = new RaiseThrowableEventArgs (e);
				foreach (EventHandler<RaiseThrowableEventArgs> handler in raisers.GetInvocationList ()) {
					handler (null, info);
					if (info.Handled)
						return;
				}
			}

			RaiseThrowable (Java.Lang.Throwable.FromException (e));
		}

		// This is invoked by
		// System.dll!System.AndroidPlatform.TrustEvaluateSsl()
		// DO NOT REMOVE
		//
		// Exception audit:
		//
		//  Verdict
		//     No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//     This method is called by System.AndroidPlatform.TrustEvaluateSsl which is, eventually, called by
		//     System/Mono.Net.Security/SystemCertificateValidator(). All exceptions are caught and handled
		//     by the caller.
		//
		static bool TrustEvaluateSsl (List <byte[]> certsRawData)
		{
			SetupTrustManager ();

			if (sslTrustManager == null) {
				return false;
			}

			var factory     = GetX509CertificateFactory ();
			var nativeCerts = new Java.Security.Cert.X509Certificate [certsRawData.Count];
			for (int i = 0; i < nativeCerts.Length; ++i) {
				// wha? api.xml doesn't contain:  http://developer.android.com/reference/javax/security/cert/X509Certificate.html#getInstance(byte[])
				// nativeCerts [i] = Java.Security.Cert.X509Certificate.GetInstance (certs [i].RawData);
				nativeCerts [i] = ConvertCertificate (factory, certsRawData [i]);
			}
			try {
				sslTrustManager.CheckServerTrusted (nativeCerts, TrustManagerFactory.DefaultAlgorithm);
				return true;
			}
			catch (Exception) {
				// ignore
			}
			try {
				// Trying to use the collection as a chain failed; see https://bugzilla.xamarin.com/show_bug.cgi?id=6501
				// Try just using the leaf certificate
				sslTrustManager.CheckServerTrusted (new[]{ nativeCerts [0] }, TrustManagerFactory.DefaultAlgorithm);
				return true;
			}
			catch (Exception) {
				return false;
			}
		}

		// This is invoked by
		// System.dll!!System.AndroidPlatform.CertStoreLookup()
		// DO NOT REMOVE
		//
		// Exception audit:
		//
		//  Verdict
		//     No need to wrap thrown exceptions in a BCL class
		//
		//  Rationale
		//     This method is called by System.AndroidPlatform.CertStoreLookup which is, eventually, called by
		//     System.Mono.Btls.MonoBtlsX509LookupMono.OnGetBySubject(). All exceptions are caught and handled
		//     by the caller.
		//
		static byte[]? CertStoreLookup (long hash, bool userStore)
		{
			SetupCertStore ();

			if (certStore == null)
				return null;

			var store = userStore ? "user" : "system";
			var alias = FormattableString.Invariant ($"{store}:{hash:x8}.0");
			var certificate = certStore.GetCertificate (alias);
			if (certificate == null)
				return null;

			return certificate.GetEncoded ();
		}

		static Java.Security.Cert.CertificateFactory GetX509CertificateFactory ()
		{
			return Java.Security.Cert.CertificateFactory.GetInstance ("X.509")!;
		}

		static Java.Security.Cert.X509Certificate ConvertCertificate (Java.Security.Cert.CertificateFactory factory, byte[] certificateData)
		{
			return factory.GenerateCertificate (new System.IO.MemoryStream (certificateData))
				.JavaCast<Java.Security.Cert.X509Certificate>()!;
		}

		// This is invoked by libmonodroid.so.
		// DO NOT REMOVE
		static void NotifyTimeZoneChanged ()
		{
			var thread            = Thread.CurrentThread;
			var timeZoneClearInfo = new[]{
				new { Description = "Thread.CurrentCulture.ClearCachedData()",    Method = (Action) thread.CurrentCulture.ClearCachedData },
				new { Description = "Thread.CurrentUICulture.ClearCachedData()",  Method = (Action) thread.CurrentUICulture.ClearCachedData },
			};
			foreach (var clearInfo in timeZoneClearInfo) {
				try {
					clearInfo.Method ();
				} catch (Exception e) {
					Logger.Log (LogLevel.Warn, "MonoAndroid", FormattableString.Invariant ($"Ignoring exception from {clearInfo.Description}: {e}"));
				}
			}
		}

		static void DetectCPUAndArchitecture (out ushort builtForCPU, out ushort runningOnCPU, out bool is64bit)
		{
			ushort built_for_cpu = 0;
			ushort running_on_cpu = 0;
			byte _is64bit = 0;

			RuntimeNativeMethods._monodroid_detect_cpu_and_architecture (ref built_for_cpu, ref running_on_cpu, ref _is64bit);
			builtForCPU = built_for_cpu;
			runningOnCPU = running_on_cpu;
			is64bit = _is64bit != 0;
		}

		// This is invoked by
		// System.Net.Http.dll!System.Net.Http.HttpClient.cctor
		// DO NOT REMOVE
		[DynamicDependency (DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof (Xamarin.Android.Net.AndroidMessageHandler))]
		static HttpMessageHandler GetHttpMessageHandler ()
		{
			if (!RuntimeFeature.XaHttpClientHandlerType) {
				return new Xamarin.Android.Net.AndroidMessageHandler ();
			}

			return GetHttpMessageHandlerFromEnvironment ();
		}

		[RequiresUnreferencedCode ("The handler type specified in XA_HTTP_CLIENT_HANDLER_TYPE might be removed by the trimmer.")]
		static HttpMessageHandler GetHttpMessageHandlerFromEnvironment ()
		{
			[UnconditionalSuppressMessage ("Trimming", "IL2057", Justification = "Preserved by the MarkJavaObjects trimmer step.")]
			[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
			static Type? TypeGetType (string typeName) =>
				Type.GetType (typeName, throwOnError: false);

			if (httpMessageHandlerType is null) {
				var handlerTypeName = Environment.GetEnvironmentVariable ("XA_HTTP_CLIENT_HANDLER_TYPE")?.Trim ();
				Type? handlerType = null;
				if (!String.IsNullOrEmpty (handlerTypeName))
					handlerType = TypeGetType (handlerTypeName);

				if (handlerType is null || !IsAcceptableHttpMessageHandlerType (handlerType)) {
					handlerType = GetFallbackHttpMessageHandlerType ();
				}

				httpMessageHandlerType = handlerType;
			}

			return (HttpMessageHandler) Activator.CreateInstance (httpMessageHandlerType)
				?? throw new InvalidOperationException ($"Could not create an instance of HTTP message handler type {httpMessageHandlerType.AssemblyQualifiedName}");
		}

		static bool IsAcceptableHttpMessageHandlerType (Type handlerType)
		{
			if (Extends (handlerType, "System.Net.Http.HttpClientHandler, System.Net.Http")) {
				// It's not possible to construct HttpClientHandler in this method because it would cause infinite recursion
				// as HttpClientHandler's constructor calls the GetHttpMessageHandler function
				Logger.Log (LogLevel.Warn, "MonoAndroid", $"The type {handlerType.AssemblyQualifiedName} cannot be used as the native HTTP handler because it is derived from System.Net.Htt.HttpClientHandler. Use a type that extends System.Net.Http.HttpMessageHandler instead.");
				return false;
			}
			if (!Extends (handlerType, "System.Net.Http.HttpMessageHandler, System.Net.Http")) {
				Logger.Log (LogLevel.Warn, "MonoAndroid", $"The type {handlerType.AssemblyQualifiedName} set as the default HTTP handler is invalid. Use a type that extends System.Net.Http.HttpMessageHandler.");
				return false;
			}

			return true;
		}

		static bool Extends (
				Type handlerType,
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
				string baseTypeName)
		{
			var baseType = Type.GetType (baseTypeName, throwOnError: false);
			return baseType?.IsAssignableFrom (handlerType) ?? false;
		}

		[return: DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
		static Type GetFallbackHttpMessageHandlerType ()
		{
			const string typeName = "Xamarin.Android.Net.AndroidMessageHandler, Mono.Android";
			var handlerType = Type.GetType (typeName, throwOnError: false)
				?? throw new InvalidOperationException ($"The {typeName} was not found. The type was probably linked away.");

			Logger.Log (LogLevel.Info, "MonoAndroid", $"Using {typeName} as the native HTTP message handler.");
			return handlerType;
		}

	}
}
