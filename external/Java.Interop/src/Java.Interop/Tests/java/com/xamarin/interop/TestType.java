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

	public int updateInt32Array (int[] array) {
		if (array == null)
			return -1;
		for (int i = 0; i < array.length; ++i) {
			if (array [i] != (i+1)*1)
				return 1;
			array [i] *= 2;
		}
		return 0;
	}

	public int updateInt32ArrayArray (int[][] array) {
		if (array == null)
			return -1;
		for (int i = 0; i < array.length; ++i) {
			for (int j = 0; j < array [i].length; ++j) {
				if (array [i][j] != ((i+1)*10) + ((j+1)*1))
					return 1;
				array [i][j] *= 2;
			}
		}
		return 0;
	}

	public  int updateInt32ArrayArrayArray (int[][][] array)
	{
		if (array == null)
			return -1;
		for (int i = 0; i < array.length; ++i) {
			for (int j = 0; j < array [i].length; ++j) {
				for (int k = 0; k < array [i][j].length; ++k) {
					if (array [i][j][k] != ((i+1)*100) + ((j+1)*10) + (k+1))
						return 1;
					array [i][j][k] *= 2;
				}
			}
		}
		return 0;
	}

	public int identity (int value) {
	    return value;
	}

	public static int staticIdentity (int value) {
	    return value;
	}

	public  native  boolean equalsThis (Object value);
	public  native  int     getInt32Value ();
	public  native  String  getStringValue (int value);
}
