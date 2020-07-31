package com.microsoft.android.ast;

public class JniInterfaceInfo extends JniTypeInfo {
	public JniInterfaceInfo(JniPackageInfo declaringPackage, String name) {
		super(declaringPackage, name);
	}

	@Override
	public String getTypeKind() {
		return "interface";
	}
}
