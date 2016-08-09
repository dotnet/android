package mono.android;

class GCUserPeer {
	private java.util.ArrayList refList = null;

	void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}

