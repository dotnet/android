package com.microsoft.android.ast;

import com.microsoft.android.util.Parameter;

public final class JniFieldInfo extends JniMemberInfo {

	private String jniType;

	public JniFieldInfo(JniTypeInfo declaringType, String name) {
		super(declaringType, name);
	}

	@Override
	public String getJniSignature() {
		return jniType;
	}

	@Override
	public boolean isField() {
		return true;
	}

	public void setJniType(String jniType) {
		this.jniType    = Parameter.requireNotEmpty("jniType", jniType);
	}
}
