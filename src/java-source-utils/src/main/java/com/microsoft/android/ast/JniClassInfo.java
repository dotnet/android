package com.microsoft.android.ast;

public class JniClassInfo extends JniTypeInfo {
	public JniClassInfo(JniPackageInfo declaringPackage, String name) {
		super(declaringPackage, name);
	}

	@Override
	public String getTypeKind() {
		return "class";
	}
}
