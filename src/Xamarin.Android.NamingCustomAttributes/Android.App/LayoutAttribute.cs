#if ANDROID_24
using System;

using Android.Content.PM;
using Android.Views;

namespace Android.App
{

	[Serializable]
	[AttributeUsage (AttributeTargets.Class,
			AllowMultiple = false,
			Inherited = false)]
	public sealed partial class LayoutAttribute : Attribute
	{
		public string DefaultWidth { get; set; }
		public string DefaultHeight { get; set; }
		public string Gravity { get; set; }
		public string MinHeight { get; set; }
		public string MinWidth { get; set; }
	}
}
#endif
