package mono.android.app;

public abstract class IntentService extends android.app.IntentService {
	public IntentService ()
	{
		this (null);
	}

	public IntentService (java.lang.String name)
	{
		super (name);
	}

	java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}

