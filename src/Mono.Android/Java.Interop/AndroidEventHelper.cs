using System;

namespace Java.Interop {

	[Obsolete ("Use EventHelper")]
	public static class AndroidEventHelper {
		
		public static void AddEventHandler<TInterface, TImplementor> (
				ref WeakReference implementor,
				Func<TImplementor> creator,
				Action<TInterface> setListener,
				Action<TImplementor> add)
			where TImplementor : TInterface, Java.Lang.Object
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
				Action<TInterface> setListener,
				Action<TImplementor> remove)
			where TImplementor : TInterface, Java.Lang.Object
		{
			TImplementor impl = null;
			if (implementor == null || (impl = (TImplementor) implementor.Target) == null)
				return;
			remove (impl);
			if (empty (impl)) {
				impl.Dispose ();
				impl = null;
				implementor = null;
				setListener (impl);
			}
		}
	}
}

