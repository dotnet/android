package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class RenameClassDerived
	extends RenameClassBase2        // Note: does NOT match C# binding!  This is "post Bytecode rewriting"
	implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.InteropTests.RenameClassDerived, Java.Interop-Tests";

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public RenameClassDerived () {
		System.out.println("RenameClassDerived.<init>()");
		if (RenameClassDerived.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					""
			);
		}
	}

	// Note: while *at runtime* `RenameClassBase1` is replaced with `RenameClassBase2`,
	// Java Callable Wrapper generator doesn't know about that (yet?), and thus the
	// *original* method name will be present.
	// Not sure if this is actually a problem; perhaps Bytecode rewriting happens *after*
	// Java Callable Wrapper generation?
	public int hashCode () {
		System.out.println("RenameClassDerived.hashCode()");
		return 64;
	}

	public void jiAddManagedReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		managedReferences.clear ();
	}
}
