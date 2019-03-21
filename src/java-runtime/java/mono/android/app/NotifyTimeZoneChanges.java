package mono.android.app;

public class NotifyTimeZoneChanges extends android.content.BroadcastReceiver {
	@Override
	public void onReceive (android.content.Context context, android.content.Intent intent) {
		mono.android.Runtime.notifyTimeZoneChanged ();
	}
}
