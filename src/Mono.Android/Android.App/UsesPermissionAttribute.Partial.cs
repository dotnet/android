namespace Android.App;

public sealed partial class UsesPermissionAttribute
{
	public UsesPermissionAttribute (string name)
	{
		Name = name;
	}

	public UsesPermissionAttribute (string name, string usesPermissionFlags)
	{
		Name = name;
		UsesPermissionFlags = usesPermissionFlags;
	}
}
