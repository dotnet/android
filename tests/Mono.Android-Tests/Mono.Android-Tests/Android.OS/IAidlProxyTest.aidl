package android.apptests.aidl;

import android.os;

interface IAidlProxyTest {
	oneway void block (in IBinder gate);
	void ping ();
}
