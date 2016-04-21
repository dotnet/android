using System;

using Android.Views;

namespace Android.Content {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=true, 
			Inherited=false)]
	public partial class GrantUriPermissionAttribute : Attribute {

		public GrantUriPermissionAttribute ()
		{
		}

		public string                 Path                    {get; set;}
		public string                 PathPattern             {get; set;}
		public string                 PathPrefix              {get; set;}
	}
}
