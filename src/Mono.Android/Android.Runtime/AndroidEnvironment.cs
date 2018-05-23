using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
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

namespace Android.Runtime {

	public static class AndroidEnvironment {

		public const string AndroidLogAppName = "Mono.Android";

		static IX509TrustManager sslTrustManager;
		static KeyStore certStore;
		static object lock_ = new object ();

		static IntPtr java_lang_Object_toString;

		static void SetupTrustManager ()
		{
			if (sslTrustManager != null)
				return;

			lock (lock_) {
				TrustManagerFactory factory = TrustManagerFactory.GetInstance (TrustManagerFactory.DefaultAlgorithm);
				factory.Init ((KeyStore) null);
				foreach (ITrustManager tm in factory.GetTrustManagers ()) {
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
					certStore = KeyStore.GetInstance ("AndroidCAStore");
					certStore.Load (null);
				} catch {
					// ignore
					certStore = null;
				}
			}
		}

		[DllImport ("libc")]
		static extern void exit (int status);

		public static void FailFast (string message)
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

		public static event EventHandler<RaiseThrowableEventArgs> UnhandledExceptionRaiser;

		public static void RaiseThrowable (Java.Lang.Throwable throwable)
		{
			if (throwable == null)
				throw new ArgumentNullException ("throwable");
			JNIEnv.Throw (throwable.Handle);
		}

		static IEnumerable<EventHandler<RaiseThrowableEventArgs>> GetUnhandledExceptionRaiserInvocationList ()
		{
			EventHandler<RaiseThrowableEventArgs> h = UnhandledExceptionRaiser;
			if (h == null)
				return new EventHandler<RaiseThrowableEventArgs>[0];
			return h.GetInvocationList ().Cast<EventHandler<RaiseThrowableEventArgs>> ();
		}

		internal static void UnhandledException (Exception e)
		{
			Logger.Log (LogLevel.Info, "MonoDroid", "UNHANDLED EXCEPTION:");
			Logger.Log (LogLevel.Info, "MonoDroid", e.ToString ());

			var info = new RaiseThrowableEventArgs (e);
			bool handled = false;
			foreach (EventHandler<RaiseThrowableEventArgs> handler in GetUnhandledExceptionRaiserInvocationList ()) {
				handler (null, info);
				if (info.Handled) {
					handled = true;
					break;
				}
			}

			if (!handled)
				RaiseThrowable (Java.Lang.Throwable.FromException (e));
		}

		// This is invoked by
		// System.dll!System.AndroidPlatform.TrustEvaluateSsl()
		// DO NOT REMOVE
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
			catch (Exception e) {
				// ignore
			}
			try {
				// Trying to use the collection as a chain failed; see https://bugzilla.xamarin.com/show_bug.cgi?id=6501
				// Try just using the leaf certificate
				sslTrustManager.CheckServerTrusted (new[]{ nativeCerts [0] }, TrustManagerFactory.DefaultAlgorithm);
				return true;
			}
			catch (Exception e) {
				return false;
			}
		}

		// This is invoked by
		// System.dll!!System.AndroidPlatform.CertStoreLookup()
		// DO NOT REMOVE
		static byte[] CertStoreLookup (long hash, bool userStore)
		{
			SetupCertStore ();

			if (certStore == null)
				return null;

			var alias = string.Format ("{0}:{1:x8}.0", userStore ? "user" : "system", hash);
			var certificate = certStore.GetCertificate (alias);
			if (certificate == null)
				return null;

			return certificate.GetEncoded ();
		}

		static Java.Security.Cert.CertificateFactory GetX509CertificateFactory ()
		{
			return Java.Security.Cert.CertificateFactory.GetInstance ("X.509");
		}

		static Java.Security.Cert.X509Certificate ConvertCertificate (Java.Security.Cert.CertificateFactory factory, byte[] certificateData)
		{
			return factory.GenerateCertificate (new System.IO.MemoryStream (certificateData))
				.JavaCast<Java.Security.Cert.X509Certificate>();
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
					Logger.Log (LogLevel.Warn, "MonoAndroid", string.Format ("Ignoring exception from {0}: {1}", clearInfo.Description, e));
				}
			}
		}

		// This is invoked by
		// System.Drawing!System.Drawing.GraphicsAndroid.FromAndroidSurface ()
		// DO NOT REMOVE
		static void GetDisplayDPI (out float x_dpi, out float y_dpi)
		{
			var wm = Application.Context.GetSystemService (Context.WindowService).JavaCast <IWindowManager> ();
			var metrics = new DisplayMetrics ();
#if ANDROID_17
			wm.DefaultDisplay.GetRealMetrics (metrics);
#else
			wm.DefaultDisplay.GetMetrics (metrics);
#endif
			x_dpi = metrics.Xdpi;
			y_dpi = metrics.Ydpi;
		}

		// This is invoked by
		// mscorlib.dll!System.AndroidPlatform.GetDefaultSyncContext()
		// DO NOT REMOVE
		static SynchronizationContext GetDefaultSyncContext ()
		{
			var looper = Android.OS.Looper.MainLooper;
			if (Android.OS.Looper.MyLooper() == looper)
				return Android.App.Application.SynchronizationContext;
			return null;
		}

		// This is invoked by
		// System.dll!System.AndroidPlatform.GetDefaultProxy()
		// DO NOT REMOVE
		static IWebProxy GetDefaultProxy ()
		{
#if ANDROID_14
			return new _Proxy ();
#else
			return null;
#endif
		}

		// This is invoked by
		// System.Net.Http.dll!System.Net.Http.HttpClient.cctor
		// DO NOT REMOVE
		static object GetHttpMessageHandler ()
		{
			string envvar = Environment.GetEnvironmentVariable ("XA_HTTP_CLIENT_HANDLER_TYPE")?.Trim ();
			Type handlerType = null;
			if (!String.IsNullOrEmpty (envvar))
				handlerType = Type.GetType (envvar, false);
			else
				return null;
			if (handlerType == null)
				return null;
			// We don't do any type checking or casting here to avoid dependency on System.Net.Http in Mono.Android.dll
			return Activator.CreateInstance (handlerType);
		}

		class _Proxy : IWebProxy {
			readonly ProxySelector selector = ProxySelector.Default;

			static URI CreateJavaUri (Uri destination)
			{
				return new URI (destination.Scheme, destination.UserInfo, destination.Host, destination.Port, destination.AbsolutePath, destination.Query, destination.Fragment);
			}

			public Uri GetProxy (Uri destination)
			{
				IList<Java.Net.Proxy> list;
				using (var uri = CreateJavaUri (destination))
					list = selector.Select (uri);
				if (list.Count < 1)
					return destination;

				var proxy = list [0];
				if (proxy.Equals (Proxy.NoProxy))
					return destination;

				var address = proxy.Address () as InetSocketAddress;
				if (address == null) // FIXME
					return destination;

#if ANDROID_19
				return new Uri (string.Format ("http://{0}:{1}/", address.HostString, address.Port));
#else
				return new Uri (string.Format ("http://{0}:{1}/", address.HostName, address.Port));
#endif
			}

			public bool IsBypassed (Uri host)
			{
				IList<Java.Net.Proxy> list;
				using (var uri = CreateJavaUri (host))
					list = selector.Select (uri);

				if (list.Count < 1)
					return true;

				return list [0].Equals (Proxy.NoProxy);
			}

			public ICredentials Credentials {
				get;
				set;
			}
		}
	}
}
