package mono;

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
		android.content.pm.ApplicationInfo apiInfo = null;

		String platformPackage	= mono.MonoPackageManager.getApiPackageName ();
		if (platformPackage != null) {
			Throwable t = null;
			try {
				apiInfo = context.getPackageManager ().getApplicationInfo (platformPackage, 0);
			} catch (android.content.pm.PackageManager.NameNotFoundException e) {
				// ignore
			}
			if (apiInfo == null) {
				try {
					apiInfo = context.getPackageManager ().getApplicationInfo ("Xamarin.Android.Platform", 0);
				} catch (android.content.pm.PackageManager.NameNotFoundException e) {
					t = e;
				}
			}
			if (apiInfo == null)
				throw new RuntimeException ("Unable to find application " + platformPackage + " or Xamarin.Android.Platform!", t);
		}
		try {
			android.content.pm.ApplicationInfo runtimeInfo = context.getPackageManager ().getApplicationInfo ("Mono.Android.DebugRuntime", 0);
			mono.MonoPackageManager.LoadApplication (context, runtimeInfo,
					apiInfo != null
					? new String[]{runtimeInfo.sourceDir, apiInfo.sourceDir, context.getApplicationInfo ().sourceDir}
					: new String[]{runtimeInfo.sourceDir, context.getApplicationInfo ().sourceDir});
		} catch (android.content.pm.PackageManager.NameNotFoundException e) {
			throw new RuntimeException ("Unable to find application Mono.Android.DebugRuntime!", e);
		}
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

