package com.xamarin.interop;

public class TestType {

	public  void    runTests () {
		int n = getInt32Value ();
		if (getInt32Value() != 42)
			throw new Error("Expected getInt32Value()==42; got " + n + "!");

		String s = getStringValue(64);
		if (s == null || !s.equals("64"))
			throw new Error("Expected getStringValue(64)==\"64\"; got " + s + "!");

		if (!equalsThis(this))
			throw new Error("Expected equalsThis(this)==true!");
	}

	public  native  boolean equalsThis (Object value);
	public  native  int     getInt32Value ();
	public  native  String  getStringValue (int value);
}
