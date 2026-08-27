package net.dot.hybrid;

import android.app.Service;
import android.content.Intent;
import android.os.Binder;
import android.os.IBinder;

public final class CoreClrWarmupService extends Service {
	private final IBinder binder = new Binder();

	@Override
	public void onCreate() {
		super.onCreate();
		CoreClrBootstrap.initializeAsync(this);
	}

	@Override
	public IBinder onBind(Intent intent) {
		return binder;
	}
}
