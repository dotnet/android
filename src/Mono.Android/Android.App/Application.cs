using System;
using System.Threading;

using Android.Content;
using Android.Runtime;

namespace Android.App {

	partial class Application {

		static Context? _context;
		public static Context Context {
			get {
				if (_context != null)
					return _context;

				var lref = JNIEnvInit.applicationContext;
				if (lref == IntPtr.Zero)
					throw new InvalidOperationException ("JNIEnvInit.applicationContext is not set!");
					
				return _context = Java.Lang.Object.GetObject<Context> (lref, JniHandleOwnership.TransferLocalRef)!;
			}
			internal set => _context = value;
		}

		static SyncContext? _sync;
		public static SynchronizationContext SynchronizationContext {
			get {
				if (_sync == null)
					_sync = new SyncContext ();
				return _sync;
			}
		}
	}
}

