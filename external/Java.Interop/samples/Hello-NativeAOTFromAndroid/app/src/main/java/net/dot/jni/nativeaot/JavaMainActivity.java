package net.dot.jni.nativeaot;

import android.app.Activity;
import android.os.Bundle;
import android.util.Log;

import net.dot.jni.helloandroid.R;

public class JavaMainActivity extends Activity {
    private static final String TAG = "NativeAot:JavaMainActivity";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        Log.i(TAG, "JavaMainActivity.onCreate()");
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
    }
}
