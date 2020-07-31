package com.microsoft.android.util;

public final class Parameter {
	private Parameter() {
	}

	public static String requireNotEmpty(String parameterName, String value) {
		if (value == null ||
				(value = value.trim()).length() == 0)
			throw new IllegalArgumentException(parameterName);
		return value;
	}

	public static <T> T requireNotNull(String parameterName, T value) {
		if (value == null)
			throw new IllegalArgumentException(parameterName);
		return value;
	}


	public static String normalize(String value, String defaultValue) {
		if (value == null ||
				(value = value.trim()).length() == 0)
			value = defaultValue;
		return value;
	}
}
