package net.dot.hybrid;

import android.app.Application;
import android.graphics.Color;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.Process;
import android.util.Log;
import android.widget.TextView;
import androidx.appcompat.app.AppCompatActivity;

public final class CoreClrBootstrapActivity extends AppCompatActivity {
	private static final String TAG = "HybridRuntime";
	private final Handler mainHandler = new Handler(Looper.getMainLooper());
	private TextView status;
	private boolean todoAppAttached;

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		int mauiTheme = getResources().getIdentifier(
			"Maui.MainTheme.NoActionBar",
			"style",
			getPackageName()
		);
		if (mauiTheme == 0) {
			throw new IllegalStateException("The merged MAUI theme resource was not found.");
		}
		setTheme(mauiTheme);
		super.onCreate(savedInstanceState);

		status = new TextView(this);
		status.setPadding(48, 96, 48, 48);
		status.setTextColor(Color.WHITE);
		status.setTextSize(20);
		setContentView(status);
		CoreClrBootstrap.initializeAsync(this);
		updateStatus();
	}

	private void updateStatus() {
		switch (CoreClrBootstrap.getState()) {
		case NOT_STARTED:
			status.setText("CoreCLR warmup has not started.");
			break;
		case INITIALIZING:
			status.setText("CoreCLR is initializing on its runtime thread...");
			mainHandler.postDelayed(this::updateStatus, 100);
			break;
		case READY:
			if (!todoAppAttached) {
				todoAppAttached = true;
				try {
					CoreClrBootstrap.showTodoApp(this);
				} catch (Throwable error) {
					Log.e(TAG, "Could not attach the CoreCLR MAUI UI", error);
					status.setText("Could not attach the CoreCLR MAUI UI in process " +
						Application.getProcessName() + " (PID " + Process.myPid() + "):\n\n" +
						Log.getStackTraceString(error));
				}
			}
			break;
		case FAILED:
			Throwable error = CoreClrBootstrap.getFailure();
			Log.e(TAG, "CoreCLR initialization failed", error);
			status.setText("CoreCLR initialization failed:\n\n" + Log.getStackTraceString(error));
			break;
		}
	}
}
