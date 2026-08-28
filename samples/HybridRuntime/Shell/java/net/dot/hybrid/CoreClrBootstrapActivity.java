package net.dot.hybrid;

import android.app.Application;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.Process;
import android.util.Log;
import android.view.Gravity;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
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

		getWindow().setStatusBarColor(Color.rgb(23, 23, 26));
		getWindow().setNavigationBarColor(Color.rgb(23, 23, 26));
		LinearLayout loading = new LinearLayout(this);
		loading.setBackgroundColor(Color.rgb(23, 23, 26));
		loading.setGravity(Gravity.CENTER);
		loading.setOrientation(LinearLayout.VERTICAL);
		loading.setPadding(dp(36), dp(48), dp(36), dp(48));

		ProgressBar progress = new ProgressBar(this);
		progress.setIndeterminateTintList(ColorStateList.valueOf(Color.rgb(172, 153, 234)));
		loading.addView(progress, new LinearLayout.LayoutParams(dp(52), dp(52)));

		TextView title = new TextView(this);
		title.setGravity(Gravity.CENTER);
		title.setText("Opening your workspace");
		title.setTextColor(Color.WHITE);
		title.setTextSize(26);
		title.setTypeface(title.getTypeface(), android.graphics.Typeface.BOLD);
		LinearLayout.LayoutParams titleLayout = matchWidth();
		titleLayout.topMargin = dp(28);
		loading.addView(title, titleLayout);

		status = new TextView(this);
		status.setGravity(Gravity.CENTER);
		status.setText("Loading your projects and tasks...");
		status.setTextColor(Color.rgb(195, 195, 195));
		status.setTextSize(16);
		LinearLayout.LayoutParams statusLayout = matchWidth();
		statusLayout.topMargin = dp(10);
		loading.addView(status, statusLayout);
		setContentView(loading);
		CoreClrBootstrap.initializeAsync(this);
		updateStatus();
	}

	@Override
	public void onBackPressed() {
		finish();
	}

	private void updateStatus() {
		switch (CoreClrBootstrap.getState()) {
		case NOT_STARTED:
			status.setText("Preparing your workspace...");
			break;
		case INITIALIZING:
			status.setText("Loading your projects and tasks...");
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

	private LinearLayout.LayoutParams matchWidth() {
		return new LinearLayout.LayoutParams(
			ViewGroup.LayoutParams.MATCH_PARENT,
			ViewGroup.LayoutParams.WRAP_CONTENT
		);
	}

	private int dp(int value) {
		return Math.round(value * getResources().getDisplayMetrics().density);
	}
}
