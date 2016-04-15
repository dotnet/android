using System;

namespace Android.Content {

	[Serializable]
	[AttributeUsage (AttributeTargets.Class, 
			AllowMultiple=false, 
			Inherited=false)]
#if !JCW_ONLY_TYPE_NAMES
	public
#endif  // !JCW_ONLY_TYPE_NAMES
	partial class ContentProviderAttribute : Attribute {

		public ContentProviderAttribute (string[] authorities)
		{
			if (authorities == null)
				throw new ArgumentNullException ("authorities");
			if (authorities.Length < 1)
				throw new ArgumentException ("At least one authority must be specified.", "authorities");
			Authorities = authorities;
		}

		public string[]               Authorities             {get; private set;}
		public bool                   Enabled                 {get; set;}
		public bool                   Exported                {get; set;}
		public bool                   GrantUriPermissions     {get; set;}
		public string                 Icon                    {get; set;}
		public int                    InitOrder               {get; set;}
		public string                 Label                   {get; set;}
		public bool                   MultiProcess            {get; set;}
		public string                 Name                    {get; set;}
		public string                 Permission              {get; set;}
		public string                 Process                 {get; set;}
		public string                 ReadPermission          {get; set;}
		public bool                   Syncable                {get; set;}
		public string                 WritePermission         {get; set;}
	}
}
