package com.microsoft.android.ast;

import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;
import java.util.Map;


import com.microsoft.android.util.Parameter;

public final class JniMethodInfo extends JniMethodBaseInfo {

	private String javaReturnType;
	private String jniReturnType;

	private final   Collection<String>  typeParameters  = new ArrayList<String>();
	private final   Map<String, String> jniTypes        = new HashMap<String, String>();

	public JniMethodInfo(JniTypeInfo declaringType, String name) {
		super(declaringType, name);
	}

	@Override
	public boolean isMethod() {
		return true;
	}

	public final void setReturnType(String javaType, String jniType) {
		this.javaReturnType = Parameter.normalize(javaType, "void");
		this.jniReturnType  = Parameter.normalize(jniType, "V");
	}

	public final void addTypeParameter(String typeParameter, String jniType) {
		typeParameter   = Parameter.requireNotEmpty("typeParameter", typeParameter);
		jniType         = Parameter.requireNotEmpty("jniType", jniType);

		if (typeParameters.contains(typeParameter))
			throw new IllegalArgumentException("Already added Type Parameter `" +typeParameter + "`");
		typeParameters.add(typeParameter);
		jniTypes.put(typeParameter, jniType);
	}

	public final Collection<String> getTypeParameters() {
		return typeParameters;
	}

	public final String getTypeParameterJniType(String typeParameter) {
		typeParameter   = Parameter.requireNotEmpty("typeParameter", typeParameter);
		return jniTypes.get(typeParameter);
	}

	@Override
	public String getJniSignature() {
		return super.getJniSignature() + jniReturnType;
	}

	public final String getJavaReturnType() {
		return javaReturnType;
	}

	public final String getJniReturnType() {
		return jniReturnType;
	}
}
