package example;

import android.util.Log;

public class RemapActivity extends android.app.Activity {
    public void onMyCreate (android.os.Bundle bundle) {
        Log.d ("*REMAP-TEST*", "RemapActivity.onMyCreate() invoked!");
        super.onCreate(bundle);
    }
}

class ViewHelper {
    public static void mySetOnClickListener (android.view.View view, android.view.View.OnClickListener listener) {
        Log.d ("*REMAP-TEST*", "ViewHelper.mySetOnClickListener() invoked!");
        view.setOnClickListener (listener);
    }
}
