using System;

namespace Java.Interop {

	public static class EventHelper {
		
		public static void AddEventHandler<TInterface, TImplementor> (
				ref WeakReference? implementor,
				Func<TImplementor> creator,
				Action<TInterface> setListener,
				Action<TImplementor> add)
			where TImplementor : Java.Lang.Object, TInterface
		{
			Android.Util.Log.Info ("EventHelper", $"AddEventHandler<{typeof(TInterface).Name}, {typeof(TImplementor).Name}> called");
			TImplementor? impl = null;
			if (implementor == null || (impl = (TImplementor?) implementor.Target) == null) {
				Android.Util.Log.Info ("EventHelper", "Creating new implementor...");
				impl = creator ();
				Android.Util.Log.Info ("EventHelper", $"Created implementor: {impl?.GetType().FullName}, Handle=0x{impl?.Handle ?? IntPtr.Zero:X}");
				implementor = new WeakReference (impl, true);
				Android.Util.Log.Info ("EventHelper", "Calling setListener...");
				setListener (impl);
				Android.Util.Log.Info ("EventHelper", "setListener completed");
			} else {
				Android.Util.Log.Info ("EventHelper", $"Reusing existing implementor: Handle=0x{impl?.Handle ?? IntPtr.Zero:X}");
			}
			Android.Util.Log.Info ("EventHelper", "Calling add (registering handler)...");
			add (impl);
			Android.Util.Log.Info ("EventHelper", "AddEventHandler completed successfully");
		}

		public static void RemoveEventHandler<TInterface, TImplementor> (
				ref WeakReference? implementor,
				Func<TImplementor, bool> empty,
				Action<TInterface> unsetListener,
				Action<TImplementor> remove)
			where TImplementor : Java.Lang.Object, TInterface
		{
			TImplementor? impl = null;
			if (implementor == null || (impl = (TImplementor?) implementor.Target) == null)
				return;
			remove (impl);
			if (empty (impl)) {
				unsetListener (impl);
				impl = null;
				implementor.Target = null;
				implementor = null;
			}
		}
	}
}

