package net.dot.android.app;

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
	public void dotnetAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void dotnetClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}

