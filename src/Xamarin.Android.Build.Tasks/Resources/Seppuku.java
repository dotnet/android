package mono.android;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

public class Seppuku extends BroadcastReceiver {
	@Override
	public void onReceive (Context context, Intent intent)
	{
		Intent startMain = new Intent (Intent.ACTION_MAIN);
		startMain.addCategory (Intent.CATEGORY_HOME);
		startMain.setFlags (Intent.FLAG_ACTIVITY_NEW_TASK);
		context.startActivity (startMain);

		java.lang.Runtime.getRuntime ().exit (-1);
	}
}
