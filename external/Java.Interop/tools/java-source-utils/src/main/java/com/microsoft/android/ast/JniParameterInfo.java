package com.microsoft.android.ast;

public final class JniParameterInfo {
	public final String name, jniType, javaType;

	public JniParameterInfo(String name, String javaType, String jniType) {
		if (name == null ||
				(name = name.trim()).length() == 0)
			throw new IllegalArgumentException("name");
		if (javaType == null ||
				(javaType = javaType.trim()).length() == 0)
			throw new IllegalArgumentException("javaType");
		if (jniType == null ||
				(jniType = jniType.trim()).length() == 0)
			throw new IllegalArgumentException("jniType");

		this.name       = name;
		this.javaType   = javaType;
		this.jniType    = jniType;
	}
}
