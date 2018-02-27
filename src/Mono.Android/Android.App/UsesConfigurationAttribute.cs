using System;

using Android.Content.PM;
using Android.Views;

namespace Android.App {

	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly, 
			AllowMultiple=true, 
			Inherited=false)]
	public sealed partial class UsesConfigurationAttribute : Attribute {

		public UsesConfigurationAttribute ()
		{
		}

		public bool ReqFiveWayNav { get; set; }
		public bool ReqHardKeyboard { get; set; }
		public string ReqKeyboardType { get; set; }
		public string ReqNavigation { get; set; }
		public string ReqTouchScreen { get; set; }
	}
}

