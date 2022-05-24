using System;
using System.Threading;

using Android.Content;
using Android.Runtime;

namespace Android.App {

	partial class Application {
		// NOTE: an explicit .cctor solves startup ordering with JNIEnv.Initialize()
		static Application () { }

		static Context? _context;
		public static Context Context {
			get {
				if (_context != null)
					return _context;

				IntPtr klass = JNIEnv.FindClass ("mono/MonoPackageManager");
				try {
					IntPtr field  = JNIEnv.GetStaticFieldID (klass, "Context", "Landroid/content/Context;");
					IntPtr lref   = JNIEnv.GetStaticObjectField (klass, field);
					return _context = Java.Lang.Object.GetObject<Context> (lref, JniHandleOwnership.TransferLocalRef)!;
				} finally {
					JNIEnv.DeleteGlobalRef (klass);
				}
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

