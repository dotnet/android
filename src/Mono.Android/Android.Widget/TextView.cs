using System;
using System.Collections.Generic;
using Android.Runtime;
using Android.Text;

namespace Android.Widget {

	public partial class TextView {

		static IntPtr id_addTextChangedListener;
		void AddTextChangedListener (TextWatcherImplementor watcher)
		{
			if (id_addTextChangedListener == IntPtr.Zero)
				id_addTextChangedListener = JNIEnv.GetMethodID (class_ref, "addTextChangedListener", "(Landroid/text/TextWatcher;)V");
			JNIEnv.CallVoidMethod (Handle, id_addTextChangedListener, new JValue (watcher));
		}

		WeakReference implementor_TextWatcher;

		public event EventHandler<AfterTextChangedEventArgs> AfterTextChanged {
			add {
				if (implementor_TextWatcher == null || !implementor_TextWatcher.IsAlive) {
					implementor_TextWatcher = new WeakReference (new TextWatcherImplementor (this, null, null, value));
					AddTextChangedListener ((TextWatcherImplementor) implementor_TextWatcher.Target);
				} else {
					var impl = (TextWatcherImplementor) implementor_TextWatcher.Target;
					impl.AfterTextChanged = (EventHandler<AfterTextChangedEventArgs>) Delegate.Combine (impl.AfterTextChanged, value);
				}
			}
			remove {
				var impl = implementor_TextWatcher != null ? (TextWatcherImplementor) implementor_TextWatcher.Target : null;
				if (impl != null)
					impl.AfterTextChanged = (EventHandler<AfterTextChangedEventArgs>) Delegate.Remove (impl.AfterTextChanged, value);
			}
		}

		public event EventHandler<TextChangedEventArgs> BeforeTextChanged {
			add {
				if (implementor_TextWatcher == null || !implementor_TextWatcher.IsAlive) {
					implementor_TextWatcher = new WeakReference (new TextWatcherImplementor (this, null, value, null));
					AddTextChangedListener ((TextWatcherImplementor) implementor_TextWatcher.Target);
				} else {
					var impl = (TextWatcherImplementor) implementor_TextWatcher.Target;
					impl.BeforeTextChanged = (EventHandler<TextChangedEventArgs>) Delegate.Combine (impl.BeforeTextChanged, value);
				}
			}
			remove {
				var impl = implementor_TextWatcher != null ? (TextWatcherImplementor) implementor_TextWatcher.Target : null;
				if (impl != null)
					impl.BeforeTextChanged = (EventHandler<TextChangedEventArgs>) Delegate.Remove (impl.BeforeTextChanged, value);
			}
		}

		public event EventHandler<TextChangedEventArgs> TextChanged {
			add {
				if (implementor_TextWatcher == null || !implementor_TextWatcher.IsAlive) {
					implementor_TextWatcher = new WeakReference (new TextWatcherImplementor (this, value, null, null));
					AddTextChangedListener ((TextWatcherImplementor) implementor_TextWatcher.Target);
				} else {
					var impl = (TextWatcherImplementor) implementor_TextWatcher.Target;
					impl.TextChanged = (EventHandler<TextChangedEventArgs>) Delegate.Combine (impl.TextChanged, value);
				}
			}
			remove {
				var impl = implementor_TextWatcher != null ? (TextWatcherImplementor) implementor_TextWatcher.Target : null;
				if (impl != null)
					impl.TextChanged = (EventHandler<TextChangedEventArgs>) Delegate.Remove (impl.TextChanged, value);
			}
		}
	}
}

