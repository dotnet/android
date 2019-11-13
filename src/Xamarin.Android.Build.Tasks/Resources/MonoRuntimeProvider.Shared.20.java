package mono;

//NOTE: we can't use import, see Generator.GetMonoInitSource

public class MonoRuntimeProvider
	extends android.content.ContentProvider
{
	public MonoRuntimeProvider ()
	{
	}

	@Override
	public boolean onCreate ()
	{
		return true;
	}

	@Override
	public void attachInfo (android.content.Context context, android.content.pm.ProviderInfo info)
	{
		// Mono Runtime Initialization {{{
		android.content.pm.ApplicationInfo applicationInfo = context.getApplicationInfo ();
		android.content.pm.PackageManager packageManager = context.getPackageManager ();
		java.util.List<String> apks = new java.util.ArrayList<String> ();
		apks.add (applicationInfo.sourceDir);
		String platformPackage	= mono.MonoPackageManager.getApiPackageName ();
		if (platformPackage != null) {
			try {
				android.content.pm.ApplicationInfo apiInfo = packageManager.getApplicationInfo (platformPackage, 0);
				apks.add (0, apiInfo.sourceDir);
			} catch (android.content.pm.PackageManager.NameNotFoundException e) {
				throw new RuntimeException ("Unable to find application " + platformPackage + "!", e);
			}
		}
		try {
			android.content.pm.ApplicationInfo runtimeInfo = packageManager.getApplicationInfo ("Mono.Android.DebugRuntime", 0);
			apks.add (0, runtimeInfo.sourceDir);
			applicationInfo = runtimeInfo;
		} catch (android.content.pm.PackageManager.NameNotFoundException e) {
			throw new RuntimeException ("Unable to find application Mono.Android.DebugRuntime!", e);
		}
		mono.MonoPackageManager.LoadApplication (context, applicationInfo, apks.toArray(new String[0]));
		// }}}
		super.attachInfo (context, info);
	}

	@Override
	public android.database.Cursor query (android.net.Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public String getType (android.net.Uri uri)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public android.net.Uri insert (android.net.Uri uri, android.content.ContentValues initialValues)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public int delete (android.net.Uri uri, String where, String[] whereArgs)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public int update (android.net.Uri uri, android.content.ContentValues values, String where, String[] whereArgs)
	{
		throw new RuntimeException ("This operation is not supported.");
	}
}

