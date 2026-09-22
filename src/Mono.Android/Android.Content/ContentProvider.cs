namespace Android.Content
{
	public partial class ContentProvider
	{
		protected void SetReadPermission (string permission)
		{
#pragma warning disable CS0618 // This method is the supported wrapper for the legacy property setter.
			ReadPermission = permission;
#pragma warning restore CS0618
		}

		protected void SetWritePermission (string permission)
		{
#pragma warning disable CS0618 // This method is the supported wrapper for the legacy property setter.
			WritePermission = permission;
#pragma warning restore CS0618
		}
	}
}
