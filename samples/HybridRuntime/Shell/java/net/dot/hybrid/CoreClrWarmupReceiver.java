package net.dot.hybrid;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

public final class CoreClrWarmupReceiver extends BroadcastReceiver {
	@Override
	public void onReceive(Context context, Intent intent) {
		CoreClrBootstrap.initializeAsync(context);
	}
}
