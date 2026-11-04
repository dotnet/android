using System;

namespace Java.Interop {

	public static class EventHelper {
		
		public static void AddEventHandler<TInterface, TImplementor> (
				ref WeakReference implementor,
				Func<TImplementor> creator,
				Action<TInterface> setListener,
				Action<TImplementor> add)
			where TImplementor : Java.Lang.Object, TInterface
		{
			TImplementor impl = null;
			if (implementor == null || (impl = (TImplementor) implementor.Target) == null) {
				impl = creator ();
				implementor = new WeakReference (impl, true);
				setListener (impl);
			}
			add (impl);
		}

		public static void RemoveEventHandler<TInterface, TImplementor> (
				ref WeakReference implementor,
				Func<TImplementor, bool> empty,
				Action<TInterface> unsetListener,
				Action<TImplementor> remove)
			where TImplementor : Java.Lang.Object, TInterface
		{
			TImplementor impl = null;
			if (implementor == null || (impl = (TImplementor) implementor.Target) == null)
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

