package net.dot.jni.test;

public class FieldRemapRenamedBase
{
	public boolean hiddenInstanceField;
	public boolean remappedInstanceField;
	public boolean remappedInheritedInstanceField;
	public static String hiddenStaticField = "base";
	public static String remappedStaticField = "remapped";
	public static String remappedInheritedStaticField = "inherited";

	public int hiddenInstanceMethod () { return 10; }
	public int remappedInstanceMethod () { return 11; }
	public int remappedInheritedInstanceMethod () { return 12; }
	public static int hiddenStaticMethod () { return 20; }
	public static int remappedStaticMethod () { return 21; }
	public static int remappedInheritedStaticMethod () { return 22; }

	public static int specificityExact (int value) { return value + 100; }
	public static int specificityValue;
	public static void specificityParameters (int value) { specificityValue = value + 200; }
	public static int specificityWildcard (long value) { return (int)value + 300; }
}
