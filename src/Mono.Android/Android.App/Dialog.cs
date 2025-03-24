using System;
using System.Diagnostics.CodeAnalysis;
using Android.Runtime;

namespace Android.App {

	public partial class Dialog {

		protected Dialog (Android.Content.Context context, bool cancelable, EventHandler cancelHandler) 
			: this (context, cancelable, new Android.Content.IDialogInterfaceOnCancelListenerImplementor () { Handler = cancelHandler }) {}

		public T? FindViewById<
				[DynamicallyAccessedMembers (Constructors)]
				T
		> (int id)
			where T : Android.Views.View
		{
			return this.FindViewById (id).JavaCast<T> ();
		}
	}
}
