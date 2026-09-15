package example;

import android.util.Log;

public class RemapActivity extends android.app.Activity {
    public void onMyCreate (android.os.Bundle bundle) {
        Log.d ("*REMAP-TEST*", "RemapActivity.onMyCreate() invoked!");
        super.onCreate(bundle);
    }

    public int exact (int value) {
        throw new AssertionError ("exact() was not remapped");
    }

    public int exactTarget (int value) {
        return value + 10;
    }

    public int parameterOnly (long value) {
        throw new AssertionError ("parameterOnly() was not remapped");
    }

    public int parameterOnlyTarget (long value) {
        return (int) value + 20;
    }

    public int wildcard () {
        throw new AssertionError ("wildcard() was not remapped");
    }

    public int wildcardTarget () {
        return 30;
    }

    public int sourceValue = -1;
    public int targetValue = 40;
}

class ViewHelper {
    public static void mySetOnClickListener (android.view.View view, android.view.View.OnClickListener listener) {
        Log.d ("*REMAP-TEST*", "ViewHelper.mySetOnClickListener() invoked!");
        view.setOnClickListener (listener);
    }
}
