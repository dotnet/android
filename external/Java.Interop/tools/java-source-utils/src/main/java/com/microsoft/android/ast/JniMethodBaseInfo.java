package com.microsoft.android.ast;

import java.util.ArrayList;
import java.util.Collection;

import com.microsoft.android.util.Parameter;

public abstract class JniMethodBaseInfo extends JniMemberInfo {

	private final  Collection<JniParameterInfo> parameters  = new ArrayList<JniParameterInfo> ();

	JniMethodBaseInfo(final JniTypeInfo declaringType, final String name) {
		super(declaringType, name);
	}

	public final void addParameter(final JniParameterInfo parameter) {
		Parameter.requireNotNull("parameter", parameter);

		parameters.add(parameter);
	}

	public Collection<JniParameterInfo> getParameters() {
		return parameters;
	}

	@Override
	public String getJniSignature() {
		final StringBuilder sig = new StringBuilder();
		sig.append("(");
		for (JniParameterInfo p : parameters) {
			sig.append(p.jniType);
		}
		sig.append(")");
		return sig.toString();
	}
}
