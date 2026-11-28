package net.dot.jni.test;

public class UnicodeNames {
	public static class Value\uD800\uDC00 {}

	public int field\uD800\uDC00;
	public static int staticField\uD800\uDC00;
	public Value\uD800\uDC00 value;

	public int method\uD800\uDC00 () { return 7; }
	public static int staticMethod\uD800\uDC00 () { return 11; }
	public static Value\uD800\uDC00 identity (Value\uD800\uDC00 value) { return value; }
	public native int native\uD800\uDC00 (Value\uD800\uDC00 value);
}
