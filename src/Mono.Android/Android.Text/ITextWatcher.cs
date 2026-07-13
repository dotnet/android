using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Text {

	/// <summary>
	/// Provides data for the <see cref="Android.Widget.TextView.AfterTextChanged"/> event, raised after the text has been changed.
	/// </summary>
	/// <seealso href="https://developer.android.com/reference/android/text/TextWatcher#afterTextChanged(android.text.Editable)">Android documentation for <c>android.text.TextWatcher.afterTextChanged</c></seealso>
	public class AfterTextChangedEventArgs : EventArgs {

		/// <summary>
		/// Initializes a new instance of the <see cref="AfterTextChangedEventArgs"/> class.
		/// </summary>
		/// <param name="editable">The mutable <see cref="IEditable"/> instance that was changed, or <see langword="null"/>.</param>
		public AfterTextChangedEventArgs (IEditable? editable)
		{
			this.editable = editable;
		}

		IEditable? editable;

		/// <summary>
		/// Gets the mutable <see cref="IEditable"/> instance that was changed, or <see langword="null"/>.
		/// </summary>
		public IEditable? Editable {
			get { return editable; }
		}
	}

	/// <summary>
	/// Provides data for the <see cref="Android.Widget.TextView.TextChanged"/> and <see cref="Android.Widget.TextView.BeforeTextChanged"/> events, describing a change to the text of a <see cref="Android.Widget.TextView"/>.
	/// </summary>
	/// <seealso href="https://developer.android.com/reference/android/text/TextWatcher#onTextChanged(java.lang.CharSequence,%20int,%20int,%20int)">Android documentation for <c>android.text.TextWatcher.onTextChanged</c></seealso>
	/// <seealso href="https://developer.android.com/reference/android/text/TextWatcher#beforeTextChanged(java.lang.CharSequence,%20int,%20int,%20int)">Android documentation for <c>android.text.TextWatcher.beforeTextChanged</c></seealso>
	public class TextChangedEventArgs : EventArgs {

		/// <summary>
		/// Initializes a new instance of the <see cref="TextChangedEventArgs"/> class.
		/// </summary>
		/// <param name="text">The character sequence being reported. For <c>BeforeTextChanged</c> this is the text before the change; for <c>TextChanged</c> this is the text after the change.</param>
		/// <param name="start">The offset in the text where the change began.</param>
		/// <param name="before">The number of characters that are being replaced starting at <paramref name="start"/>.</param>
		/// <param name="after">The number of characters that replace the old text starting at <paramref name="start"/>.</param>
		public TextChangedEventArgs (IEnumerable<char>? text, int start, int before, int after)
		{
			this.text = text;
			this.start = start;
			this.before = before;
			this.after = after;
		}

		IEnumerable<char>? text;

		/// <summary>
		/// Gets the character sequence being reported. For <c>BeforeTextChanged</c> this is the text before the change; for <c>TextChanged</c> this is the text after the change.
		/// </summary>
		public IEnumerable<char>? Text {
			get { return text; }
		}

		int after;

		/// <summary>
		/// Gets the number of characters that replace the old text starting at <see cref="Start"/>.
		/// </summary>
		public int AfterCount {
			get { return after; }
		}

		int before;

		/// <summary>
		/// Gets the number of characters that are being replaced starting at <see cref="Start"/>.
		/// </summary>
		public int BeforeCount {
			get { return before; }
		}

		int start;

		/// <summary>
		/// Gets the offset in the text where the change began.
		/// </summary>
		public int Start {
			get { return start; }
		}

	}

	[Register ("mono/android/text/TextWatcherImplementor")]
	internal sealed class TextWatcherImplementor : Java.Lang.Object, ITextWatcher {

		object inst;
		public EventHandler<AfterTextChangedEventArgs>? AfterTextChanged;
		public EventHandler<TextChangedEventArgs>? BeforeTextChanged;
		public EventHandler<TextChangedEventArgs>? TextChanged;

		public TextWatcherImplementor (object inst, EventHandler<TextChangedEventArgs>? changed_handler, EventHandler<TextChangedEventArgs>? before_handler, EventHandler<AfterTextChangedEventArgs>? after_handler)
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

		void ITextWatcher.AfterTextChanged (Android.Text.IEditable? s)
		{
			var h = AfterTextChanged;
			if (h != null)
				h (inst, new AfterTextChangedEventArgs (s));
		}

		void ITextWatcher.BeforeTextChanged (Java.Lang.ICharSequence? s, int start, int before, int after)
		{
			var h = BeforeTextChanged;
			if (h != null)
				h (inst, new TextChangedEventArgs (s, start, before, after));
		}

		void ITextWatcher.OnTextChanged (Java.Lang.ICharSequence? s, int start, int before, int count)
		{
			var h = TextChanged;
			if (h != null)
				h (inst, new TextChangedEventArgs (s, start, before, count));
		}
	}
}
