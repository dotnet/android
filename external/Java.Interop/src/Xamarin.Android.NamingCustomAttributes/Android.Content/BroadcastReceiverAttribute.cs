using System;

namespace Android.Content {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	partial class BroadcastReceiverAttribute : Attribute {

		public BroadcastReceiverAttribute ()
		{
		}

		public bool                   Enabled                 {get; set;}
		public bool                   Exported                {get; set;}
		public string                 Icon                    {get; set;}
		public string                 Label                   {get; set;}
		public string                 Name                    {get; set;}
		public string                 Permission              {get; set;}
		public string                 Process                 {get; set;}
	}
}
