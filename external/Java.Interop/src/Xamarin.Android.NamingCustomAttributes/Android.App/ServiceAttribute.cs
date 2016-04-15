using System;

#if !JCW_ONLY_TYPE_NAMES
using Android.Content.PM;
using Android.Views;
#endif  // !JCW_ONLY_TYPE_NAMES

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	sealed partial class ServiceAttribute : Attribute {

		public ServiceAttribute ()
		{
		}

		public string                 Name                    {get; set;}

#if !JCW_ONLY_TYPE_NAMES
		public bool                   Enabled                 {get; set;}
		public bool                   Exported                {get; set;}
		public string                 Icon                    {get; set;}
#if ANDROID_16
		public bool                   IsolatedProcess         {get; set;}
#endif
		public string                 Label                   {get; set;}
		public string                 Permission              {get; set;}
		public string                 Process                 {get; set;}
#endif  // !JCW_ONLY_TYPE_NAMES
	}
}
