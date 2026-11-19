package net.dot.android;

class GCUserPeer implements IGCUserPeer {
	private java.util.ArrayList refList = null;

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

