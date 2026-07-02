package com.microsoft.android.ast;

import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

import com.microsoft.android.util.Parameter;

public abstract class JniTypeInfo implements HasJavadocComment {
	private final   String                      name;
	private final   JniPackageInfo              declaringPackage;

	private final   Collection<JniMemberInfo>   members         = new ArrayList<JniMemberInfo>();
	private final   Collection<String>          typeParameters  = new ArrayList<String>();
	private final   Map<String, String>         jniTypes        = new HashMap<String, String>();

	String javaDocComment = "";

	JniTypeInfo(final JniPackageInfo declaringPackage, String name) {
		Parameter.requireNotNull("declaringPackage", declaringPackage);

		name    = Parameter.requireNotEmpty("name", name);

		this.declaringPackage   = declaringPackage;
		this.name               = name;
	}

	public abstract String getTypeKind();

	public final JniPackageInfo getDeclaringPackage() {
		return declaringPackage;
	}

	public final String getName() {
		if (typeParameters.isEmpty())
			return name;
		return name + "<" + String.join(",", typeParameters) + ">";
	}

	public final String getRawName() {
		return name;
	}

	public final void addTypeParameter(String typeParameter, String jniType) {
		typeParameter   = Parameter.requireNotEmpty("typeParameter", typeParameter);
		jniType         = Parameter.requireNotEmpty("jniType", jniType);

		if (typeParameters.contains(typeParameter)) {
			jniTypes.replace(typeParameter, jniType);
			return;
		}
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

	public final void add(JniMemberInfo member) {
		Parameter.requireNotNull("member", member);

		members.add(member);
	}

	public final Collection<JniMemberInfo> getMembers() {
		return members;
	}

	public final String getJavadocComment() {
		return javaDocComment;
	}

	public final void setJavadocComment(String javaDocComment) {
		this.javaDocComment = Parameter.normalize(javaDocComment, "");
	}

	public final Collection<JniMemberInfo> getSortedMembers() {
		final List<JniMemberInfo> sortedMembers = members
			.stream()
			.sorted((m1, m2) -> (m1.getName() + "." + m1.getJniSignature()).compareTo((m2.getName() + "." + m2.getJniSignature())))
			.collect(Collectors.toList());
		return sortedMembers;
	}
}
