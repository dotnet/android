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

				var gref = JNIEnvInit.applicationContext;
				if (gref == IntPtr.Zero)
					throw new InvalidOperationException ("JNIEnvInit.applicationContext is not set!");
					
				return _context = Java.Lang.Object.GetObject<Context> (gref, JniHandleOwnership.TransferGlobalRef)!;
			}
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

