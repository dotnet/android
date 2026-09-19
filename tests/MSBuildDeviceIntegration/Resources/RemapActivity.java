package example;

import android.util.Log;

public class RemapActivity extends android.app.Activity {
    public void onMyCreate (android.os.Bundle bundle) {
        Log.d ("*REMAP-TEST*", "RemapActivity.onMyCreate() invoked!");
        super.onCreate(bundle);
    }

    public void méthodeCible () {
        Log.d ("*REMAP-TEST*", "RemapActivity.méthodeCible() invoked!");
    }

    public void boundary15 () {
        Log.d ("*REMAP-TEST*", "RemapActivity.boundary15() invoked!");
    }

    public void boundary16 () {
        Log.d ("*REMAP-TEST*", "RemapActivity.boundary16() invoked!");
    }

    public void boundary17 () {
        Log.d ("*REMAP-TEST*", "RemapActivity.boundary17() invoked!");
    }

    public void secondChunkA () {
        Log.d ("*REMAP-TEST*", "RemapActivity.secondChunkA() invoked!");
    }

    public void secondChunkB () {
        Log.d ("*REMAP-TEST*", "RemapActivity.secondChunkB() invoked!");
    }
}

class ViewHelper {
    public static void mySetOnClickListener (android.view.View view, android.view.View.OnClickListener listener) {
        Log.d ("*REMAP-TEST*", "ViewHelper.mySetOnClickListener() invoked!");
        view.setOnClickListener (listener);
    }
}
