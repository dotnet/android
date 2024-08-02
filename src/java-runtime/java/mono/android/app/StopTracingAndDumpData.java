package mono.android.app;

public class StopTracingAndDumpData extends android.content.BroadcastReceiver {
	@Override
	public void onReceive (android.content.Context context, android.content.Intent intent) {
		mono.android.Runtime.stopTracingAndDumpData ();
	}
}
