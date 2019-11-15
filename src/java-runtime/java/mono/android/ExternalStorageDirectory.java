package mono.android;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.Environment;
import android.util.Log;

public class ExternalStorageDirectory
	extends BroadcastReceiver
{
	@Override
	public void onReceive (Context context, Intent intent)
	{
		setResultData (Environment.MEDIA_MOUNTED.equals (Environment.getExternalStorageState ())
				? Environment.getExternalStorageDirectory ().getAbsolutePath ()
				: "");
	}
}
