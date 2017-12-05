using System;

namespace Android.App
{
	sealed partial class ActivityAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {
		public string                 Name                    {get; set;}
	}
}
