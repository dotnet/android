using System;

using Android.Content;
using Android.Runtime;

namespace Android.Views {

	public partial class LayoutInflater {

		[Register ("from", "(Landroid/content/Context;)Landroid/view/LayoutInflater;", "")]
		public static LayoutInflater? From (Context? context)
		{
			ArgumentNullException.ThrowIfNull (context);

			return FromContext (context);
		}

		public static LayoutInflater? FromContext (Context context)
		{
			var service = context.GetSystemService (Context.LayoutInflaterService!);

			if (service is LayoutInflater inflater)
				return inflater;

			if (service?.Handle != IntPtr.Zero)
				return Java.Lang.Object.GetObject<LayoutInflater> (service.Handle, JniHandleOwnership.DoNotTransfer);

			return null;
		}
	}
}
