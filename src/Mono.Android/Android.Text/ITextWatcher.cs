using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Text {

	public class AfterTextChangedEventArgs : EventArgs {

		public AfterTextChangedEventArgs (IEditable editable)
		{
			this.editable = editable;
		}

		IEditable editable;
		public IEditable Editable {
			get { return editable; }
		}
	}

	public class TextChangedEventArgs : EventArgs {

		public TextChangedEventArgs (IEnumerable<char> text, int start, int before, int after)
		{
			this.text = text;
			this.start = start;
			this.before = before;
			this.after = after;
		}

		IEnumerable<char> text;
		public IEnumerable<char> Text {
			get { return text; }
		}

		int after;
		public int AfterCount {
			get { return after; }
		}

		int before;
		public int BeforeCount {
			get { return before; }
		}

		int start;
		public int Start {
			get { return start; }
		}

	}

	[Register ("mono/android/text/TextWatcherImplementor")]
	internal sealed class TextWatcherImplementor : Java.Lang.Object, ITextWatcher {

		object inst;
		public EventHandler<AfterTextChangedEventArgs> AfterTextChanged;
		public EventHandler<TextChangedEventArgs> BeforeTextChanged;
		public EventHandler<TextChangedEventArgs> TextChanged;

		public TextWatcherImplementor (object inst, EventHandler<TextChangedEventArgs> changed_handler, EventHandler<TextChangedEventArgs> before_handler, EventHandler<AfterTextChangedEventArgs> after_handler)
			: base (
					JNIEnv.StartCreateInstance ("mono/android/text/TextWatcherImplementor", "()V"),
					JniHandleOwnership.TransferLocalRef)
		{
			JNIEnv.FinishCreateInstance (Handle, "()V");

			this.inst = inst;
			AfterTextChanged = after_handler;
			BeforeTextChanged = before_handler;
			TextChanged = changed_handler;
		}

		void ITextWatcher.AfterTextChanged (Android.Text.IEditable s)
		{
			var h = AfterTextChanged;
			if (h != null)
				h (inst, new AfterTextChangedEventArgs (s));
		}

		void ITextWatcher.BeforeTextChanged (Java.Lang.ICharSequence s, int start, int before, int after)
		{
			var h = BeforeTextChanged;
			if (h != null)
				h (inst, new TextChangedEventArgs (s, start, before, after));
		}

		void ITextWatcher.OnTextChanged (Java.Lang.ICharSequence s, int start, int before, int count)
		{
			var h = TextChanged;
			if (h != null)
				h (inst, new TextChangedEventArgs (s, start, before, count));
		}
	}
}

