namespace Android.Content
{
	public partial class ContentProvider
	{
		protected void SetReadPermission (string permission)
		{
			ReadPermission = permission;
		}

		protected void SetWritePermission (string permission)
		{
			WritePermission = permission;
		}
	}
}

