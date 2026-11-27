using System;
using System.Collections.Generic;
using Android.Runtime;
using Android.Text;

namespace Android.Widget {

	public partial class TextView {

		static IntPtr id_addTextChangedListener;
		void AddTextChangedListener (TextWatcherImplementor watcher)
		{
			unsafe {
				// SAFETY: addTextChangedListener takes one TextWatcher; the cached ID matches that ABI and existing KeepAlive retains the watcher and receiver.
				if (id_addTextChangedListener == IntPtr.Zero)
					id_addTextChangedListener = JNIEnv.GetMethodID (_members.GetPeerTypeHandle (), "addTextChangedListener", "(Landroid/text/TextWatcher;)V");
				JNIEnv.CallVoidMethod (Handle, id_addTextChangedListener, new JValue (watcher));
				GC.KeepAlive (watcher);
				GC.KeepAlive (this);
			}
		}

		WeakReference? implementor_TextWatcher;

		public event EventHandler<AfterTextChangedEventArgs> AfterTextChanged {
			add {
				if (implementor_TextWatcher?.Target is TextWatcherImplementor impl) {
					impl.AfterTextChanged = (EventHandler<AfterTextChangedEventArgs>) Delegate.Combine (impl.AfterTextChanged, value);
				} else {
					impl = new TextWatcherImplementor (this, null, null, value);
					implementor_TextWatcher = new WeakReference (impl);
					AddTextChangedListener (impl);
				}
			}
			remove {
				if (implementor_TextWatcher?.Target is TextWatcherImplementor impl)
					impl.AfterTextChanged = (EventHandler<AfterTextChangedEventArgs>?) Delegate.Remove (impl.AfterTextChanged, value);
			}
		}

		public event EventHandler<TextChangedEventArgs> BeforeTextChanged {
			add {
				if (implementor_TextWatcher?.Target is TextWatcherImplementor impl) {
					impl.BeforeTextChanged = (EventHandler<TextChangedEventArgs>) Delegate.Combine (impl.BeforeTextChanged, value);
				} else {
					impl = new TextWatcherImplementor (this, null, value, null);
					implementor_TextWatcher = new WeakReference (impl);
					AddTextChangedListener (impl);
				}
			}
			remove {
				if (implementor_TextWatcher?.Target is TextWatcherImplementor impl)
					impl.BeforeTextChanged = (EventHandler<TextChangedEventArgs>?) Delegate.Remove (impl.BeforeTextChanged, value);
			}
		}

		public event EventHandler<TextChangedEventArgs> TextChanged {
			add {
				if (implementor_TextWatcher?.Target is TextWatcherImplementor impl) {
					impl.TextChanged = (EventHandler<TextChangedEventArgs>) Delegate.Combine (impl.TextChanged, value);
				} else {
					impl = new TextWatcherImplementor (this, value, null, null);
					implementor_TextWatcher = new WeakReference (impl);
					AddTextChangedListener (impl);
				}
			}
			remove {
				if (implementor_TextWatcher?.Target is TextWatcherImplementor impl)
					impl.TextChanged = (EventHandler<TextChangedEventArgs>?) Delegate.Remove (impl.TextChanged, value);
			}
		}
	}
}
