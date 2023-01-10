package com.xamarin.android;

public abstract class CallMethodFromCtor {

	public CallMethodFromCtor () {
		int value = calledFromCtor ();
		if (value != 42)
			throw new Error ("Value mismatch! should be 42, was " + value + ".");
	}

	public abstract int calledFromCtor ();

	public static CallMethodFromCtor newInstance (Class<? extends CallMethodFromCtor> c)
		throws Throwable
	{
		return (CallMethodFromCtor) c.newInstance ();
	}
}

