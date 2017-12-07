using System;

namespace Android.Content {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
	public partial class BroadcastReceiverAttribute : Attribute {

		public BroadcastReceiverAttribute ()
		{
		}

		public bool                   DirectBootAware         {get; set;}
		public bool                   Enabled                 {get; set;}
		public bool                   Exported                {get; set;}
		public string                 Description             {get; set;}
		public string                 Icon                    {get; set;}
		public string                 Label                   {get; set;}
		public string                 Name                    {get; set;}
		public string                 Permission              {get; set;}
		public string                 Process                 {get; set;}
#if ANDROID_25
		public string                 RoundIcon               {get; set;}
#endif
	}
}
