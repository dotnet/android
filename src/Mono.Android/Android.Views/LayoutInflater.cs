using System;

using Android.Content;

namespace Android.Views {

	public partial class LayoutInflater {

		public static LayoutInflater FromContext (Context context)
		{
			return context.GetSystemService (Context.LayoutInflaterService) as LayoutInflater;
		}
	}
}


