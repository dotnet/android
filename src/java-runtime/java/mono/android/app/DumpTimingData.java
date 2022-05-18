package mono.android.app;

public class DumpTimingData extends android.content.BroadcastReceiver {
	@Override
	public void onReceive (android.content.Context context, android.content.Intent intent) {
		mono.android.Runtime.dumpTimingData ();
	}
}
