package com.microsoft.android.ast;

import com.microsoft.android.util.Parameter;

public abstract class JniMemberInfo implements HasJavadocComment {
	private final   String          name;
	private final   JniTypeInfo     declaringType;

	String javadocComment = "";

    JniMemberInfo(final JniTypeInfo declaringType, String name) {
		Parameter.requireNotNull("declaringType", declaringType);

		name    = Parameter.requireNotEmpty("name", name);

		this.declaringType  = declaringType;
		this.name           = name;
	}

	public final JniTypeInfo getDeclaringType() {
		return declaringType;
	}

	public abstract String getJniSignature();

	public final String getName() {
		return name;
	}

	public boolean isField() {
		return false;
	}

	public boolean isMethod() {
		return false;
	}

	public boolean isConstructor() {
		return false;
	}

	public final String getJavadocComment() {
		return javadocComment;
	}

	public final void setJavadocComment(String javaDocComment) {
		this.javadocComment = Parameter.normalize(javaDocComment, "");
	}
}
