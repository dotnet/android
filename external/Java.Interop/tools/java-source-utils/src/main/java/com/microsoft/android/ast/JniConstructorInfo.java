package com.microsoft.android.ast;

public final class JniConstructorInfo extends JniMethodBaseInfo {
	public JniConstructorInfo(JniTypeInfo declaringType) {
		super(declaringType, "#ctor");
	}

	@Override
	public boolean isConstructor() {
		return true;
	}

	@Override
	public String getJniSignature() {
		return super.getJniSignature() + "V";
	}
}
