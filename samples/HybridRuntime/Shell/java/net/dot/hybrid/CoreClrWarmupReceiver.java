package net.dot.hybrid;

import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.IBinder;
import android.util.Log;

public final class CoreClrWarmupReceiver extends BroadcastReceiver {
	private static final String TAG = "HybridRuntime";
	private static boolean bound;
	private static final ServiceConnection connection = new ServiceConnection() {
		@Override
		public void onServiceConnected(ComponentName name, IBinder service) {
			Log.i(TAG, "NativeAOT process bound to CoreCLR warmup service");
		}

		@Override
		public void onServiceDisconnected(ComponentName name) {
			Log.w(TAG, "CoreCLR warmup service disconnected; Android will rebind it");
		}
	};

	@Override
	public synchronized void onReceive(Context context, Intent intent) {
		if (bound) {
			return;
		}

		Context applicationContext = context.getApplicationContext();
		Intent serviceIntent = new Intent(applicationContext, CoreClrWarmupService.class);
		bound = applicationContext.bindService(
			serviceIntent,
			connection,
			Context.BIND_AUTO_CREATE | Context.BIND_IMPORTANT
		);
		if (!bound) {
			throw new IllegalStateException("Could not bind the CoreCLR warmup service.");
		}
	}
}
