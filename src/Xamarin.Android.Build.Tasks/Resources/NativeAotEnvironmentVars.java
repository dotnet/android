package net.dot.jni.nativeaot;

import android.system.ErrnoException;
import android.system.Os;
import android.util.Log;
import java.lang.String;

public class NativeAotEnvironmentVars
{
	private static final String TAG = "NativeAotEnvironmentVars";

	static String[] envNames = new String[] {
@ENVIRONMENT_VAR_NAMES@
	};

	static String[] envValues = new String[] {
@ENVIRONMENT_VAR_VALUES@
	};

	public static void Initialize ()
	{
		Log.d (TAG, "Initializing environment variables");

		if (envNames.length != envValues.length) {
			Log.w (TAG, "Unable to initialize environment variables, name and value arrays have different sizes");
			return;
		}

		try {
			for (int i = 0; i < envNames.length; i++) {
				Log.d (TAG, "Setting env var: '" + envNames[i] + "'='" + envValues[i] + "'");
				Os.setenv (envNames[i], envValues[i], true /* overwrite */);
			}
		} catch (ErrnoException e) {
			Log.e (TAG, "Failed to set environment variables", e);
		}
	}
}
