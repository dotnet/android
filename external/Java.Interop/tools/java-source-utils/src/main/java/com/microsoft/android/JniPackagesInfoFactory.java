package com.microsoft.android;

import java.io.File;
import java.util.ArrayList;
import java.util.Collection;
import java.util.List;
import java.util.Optional;

import com.github.javaparser.*;
import com.github.javaparser.JavaParser;
import com.github.javaparser.ParserConfiguration;
import com.github.javaparser.StaticJavaParser;
import com.github.javaparser.ast.*;
import com.github.javaparser.ast.comments.*;
import com.github.javaparser.ast.type.*;
import com.github.javaparser.ast.body.*;
import com.github.javaparser.ast.body.BodyDeclaration;
import com.github.javaparser.ast.body.Parameter;
import com.github.javaparser.ast.body.TypeDeclaration;
import com.github.javaparser.ast.comments.JavadocComment;
import com.github.javaparser.ast.expr.Expression;
import com.github.javaparser.ast.nodeTypes.*;
import com.github.javaparser.ast.nodeTypes.NodeWithJavadoc;
import com.github.javaparser.ast.nodeTypes.NodeWithParameters;
import com.github.javaparser.ast.nodeTypes.NodeWithSimpleName;
import com.github.javaparser.resolution.SymbolResolver;
import com.github.javaparser.resolution.declarations.ResolvedReferenceTypeDeclaration;
import com.github.javaparser.resolution.types.ResolvedReferenceType;
import com.github.javaparser.resolution.types.ResolvedType;
import com.github.javaparser.symbolsolver.*;
import com.github.javaparser.symbolsolver.model.resolution.TypeSolver;
import com.github.javaparser.symbolsolver.resolution.typesolvers.*;

import com.github.javaparser.ParseResult;
import com.github.javaparser.ast.CompilationUnit;

import com.microsoft.android.ast.*;

import static com.microsoft.android.util.Parameter.*;

public final class JniPackagesInfoFactory {

	final   JavaParser  parser;

	public JniPackagesInfoFactory(final JavaParser parser) {
		requireNotNull("parser", parser);

		this.parser = parser;
	}

	public JniPackagesInfo parse(final Collection<File> files) throws Throwable {
		requireNotNull("files", files);

		final   JniPackagesInfo packages    = new JniPackagesInfo();

		for (final File file : files) {
			final ParseResult<CompilationUnit> result = parser.parse(file);
			final Optional<CompilationUnit> unit = result.getResult();
			if (!unit.isPresent()) {
				logParseErrors(file, result.getProblems());
				continue;
			}
			parse(packages, unit.get());
		}

		return packages;
	}

	private static void logParseErrors(final File file, final List<Problem> problems) {
		System.err.println(App.APP_NAME + ": could not parse file `" + file.getName() + "`:");
		for (final Problem p : problems) {
			System.err.print("\t");
			Optional<TokenRange> location = p.getLocation();
			if (location.isPresent()) {
				System.err.print(location.get());
				System.err.print(": ");
			}
			System.err.println(p.getVerboseMessage());
			if (JavaSourceUtilsOptions.verboseOutput && p.getCause().isPresent()) {
				p.getCause().get().printStackTrace(System.err);
			}
		}
	}

	/** parse method */
	private void parse(final JniPackagesInfo packages, final CompilationUnit unit) throws Throwable {
		final   String packageName          = unit.getPackageDeclaration().isPresent()
			? unit.getPackageDeclaration().get().getNameAsString()
			: "";
		final   JniPackageInfo packageInfo  = packages.getPackage(packageName);

		fixJavadocComments(unit, unit.getTypes());

		for (final TypeDeclaration<?> type : unit.getTypes()) {
			if (JavaSourceUtilsOptions.verboseOutput && type.getFullyQualifiedName().isPresent()) {
				System.out.println("Processing: " + type.getFullyQualifiedName().get());
			}
			if (type.isAnnotationDeclaration()) {
				final   AnnotationDeclaration       annoDecl    = type.asAnnotationDeclaration();
				final   JniTypeInfo                 annoInfo    = createAnnotationInfo(packageInfo, annoDecl, null);
				parseType(packageInfo, annoInfo, annoDecl);
				continue;
			}
			if (type.isClassOrInterfaceDeclaration()) {
				final   ClassOrInterfaceDeclaration typeDecl    = type.asClassOrInterfaceDeclaration();
				final   JniTypeInfo                 typeInfo    = createTypeInfo(packageInfo, typeDecl, null);
				parseType(packageInfo, typeInfo, typeDecl);
				continue;
			}
			if (type.isEnumDeclaration()) {
				final   EnumDeclaration             enumDecl    = type.asEnumDeclaration();
				final   JniTypeInfo                 nestedEnum  = createEnumInfo(packageInfo, enumDecl, null);
				parseType(packageInfo, nestedEnum, enumDecl);
				continue;
			}
			System.out.println("# TODO: unknown type decl " + type.getClass().getName());
			System.out.println(type.toString());
		}
	}

	static JniTypeInfo createAnnotationInfo(final JniPackageInfo packageInfo, final AnnotationDeclaration annotationDecl, JniTypeInfo declInfo) {
		final String declName = declInfo == null ? "" : declInfo.getRawName() + ".";
		final JniTypeInfo annotationInfo = new JniInterfaceInfo(packageInfo, declName + annotationDecl.getNameAsString());
		packageInfo.add(annotationInfo);
		fillJavadoc(annotationInfo, annotationDecl);
		if (declInfo != null) {
			for (String typeParameter : declInfo.getTypeParameters()) {
				annotationInfo.addTypeParameter(typeParameter, declInfo.getTypeParameterJniType(typeParameter));
			}
		}
		return annotationInfo;
	}

	static JniTypeInfo createEnumInfo(final JniPackageInfo packageInfo, final EnumDeclaration enumDecl, JniTypeInfo declInfo) {
		final String declName = declInfo == null ? "" : declInfo.getRawName() + ".";
		final JniTypeInfo enumInfo = new JniClassInfo(packageInfo, declName + enumDecl.getNameAsString());
		packageInfo.add(enumInfo);
		fillJavadoc(enumInfo, enumDecl);
		if (declInfo != null) {
			for (String typeParameter : declInfo.getTypeParameters()) {
				enumInfo.addTypeParameter(typeParameter, declInfo.getTypeParameterJniType(typeParameter));
			}
		}
		return enumInfo;
	}

	static JniTypeInfo createTypeInfo(final JniPackageInfo packageInfo, final ClassOrInterfaceDeclaration typeDecl, JniTypeInfo declInfo) {
		final String declName = declInfo == null ? "" : declInfo.getRawName() + ".";
		final JniTypeInfo typeInfo = typeDecl.isInterface()
			? new JniInterfaceInfo(packageInfo, declName + typeDecl.getNameAsString())
			: new JniClassInfo(packageInfo, declName + typeDecl.getNameAsString());
		packageInfo.add(typeInfo);
		fillJavadoc(typeInfo, typeDecl);
		if (declInfo != null) {
			for (String typeParameter : declInfo.getTypeParameters()) {
				typeInfo.addTypeParameter(typeParameter, declInfo.getTypeParameterJniType(typeParameter));
			}
		}
		for (TypeParameter typeParameter : typeDecl.getTypeParameters()) {
			typeInfo.addTypeParameter(
					typeParameter.getNameAsString(),
					getJniType(typeInfo, null, getTypeParameterBound(typeParameter)));
		}
		return typeInfo;
	}

	static ClassOrInterfaceType getTypeParameterBound(TypeParameter typeParameter) {
		for (ClassOrInterfaceType boundType : typeParameter.getTypeBound()) {
			return boundType;
		}
		return null;
	}

	private final void parseType(final JniPackageInfo packageInfo, final JniTypeInfo typeInfo, TypeDeclaration<?> typeDecl) {
		fixJavadocComments(typeDecl, getUndocumentedBodyMembers(typeDecl.getMembers()));
		for (final BodyDeclaration<?> body : typeDecl.getMembers()) {
			if (body.isAnnotationDeclaration()) {
				final   AnnotationDeclaration       annoDecl    = body.asAnnotationDeclaration();
				final   JniTypeInfo                 annoInfo    = createAnnotationInfo(packageInfo, annoDecl, typeInfo);
				parseType(packageInfo, annoInfo, annoDecl);
				continue;
			}
			if (body.isClassOrInterfaceDeclaration()) {
				final   ClassOrInterfaceDeclaration nestedDecl  = body.asClassOrInterfaceDeclaration();
				final   JniTypeInfo                 nestedType  = createTypeInfo(packageInfo, nestedDecl, typeInfo);
				parseType(packageInfo, nestedType, nestedDecl);
				continue;
			}
			if (body.isEnumDeclaration()) {
				final   EnumDeclaration             enumDecl    = body.asEnumDeclaration();
				final   JniTypeInfo                 nestedEnum  = createEnumInfo(packageInfo, enumDecl, typeInfo);
				parseType(packageInfo, nestedEnum, enumDecl);
				continue;
			}
			if (body.isAnnotationMemberDeclaration()) {
				parseAnnotationMemberDecl(typeInfo, body.asAnnotationMemberDeclaration());
				continue;
			}
			if (body.isConstructorDeclaration()) {
				parseConstructorDecl(typeInfo, body.asConstructorDeclaration());
				continue;
			}
			if (body.isFieldDeclaration()) {
				parseFieldDecl(typeInfo, body.asFieldDeclaration());
				continue;
			}
			if (body.isMethodDeclaration()) {
				parseMethodDecl(typeInfo, body.asMethodDeclaration());
				continue;
			}
			if (body.isInitializerDeclaration()) {
				// e.g. `static { CREATOR = null; }
				continue;
			}
			System.out.println("# TODO: unknown body member " + body.getClass().getName());
			System.out.println(body.toString());
		}
	}

	private final void fixJavadocComments(final Node decl, final Iterable<? extends BodyDeclaration<?>> bodyMembers) {
		final List<BodyDeclaration<?>>  members             = getUndocumentedBodyMembers(bodyMembers);
		final List<JavadocComment>      orphanedComments    = getOrphanComments(decl);

		if (members.size() == 0)
			return;

		final BodyDeclaration<?>        firstMember = members.get(0);
		JavadocComment                  comment     = orphanedComments.stream()
			.filter(c -> c.getBegin().get().isBefore(firstMember.getBegin().get()))
			.reduce((a, b) -> b)
			.orElse(null);
		if (comment != null) {
			((NodeWithJavadoc<?>) firstMember).setJavadocComment(comment);
		}

		for (int i = 1; i < members.size(); ++i) {
			BodyDeclaration<?> prevMember   = members.get(i-1);
			BodyDeclaration<?> member       = members.get(i);

			Optional<JavadocComment> commentOpt = orphanedComments.stream()
				.filter(c -> c.getBegin().get().isAfter(prevMember.getEnd().get()) &&
					c.getEnd().get().isBefore(member.getBegin().get()))
				.findFirst();
			if (!commentOpt.isPresent())
				continue;
			((NodeWithJavadoc<?>)member).setJavadocComment(commentOpt.get());
		}
	}

	private final List<BodyDeclaration<?>> getUndocumentedBodyMembers(Iterable<? extends BodyDeclaration<?>> bodyMembers) {
		final List<BodyDeclaration<?>> members = new ArrayList<BodyDeclaration<?>> ();
		for (BodyDeclaration<?> member : bodyMembers) {
			if (!(member instanceof NodeWithJavadoc<?>)) {
				continue;
			}
			final NodeWithJavadoc<?> memberJavadoc = (NodeWithJavadoc<?>) member;
			if (memberJavadoc.getJavadocComment().isPresent())
				continue;
			final Optional<Position> memberBeginOpt = member.getBegin();
			if (!memberBeginOpt.isPresent())
				continue;
			members.add(member);
		}
		members.sort((a, b) -> a.getBegin().get().compareTo(b.getBegin().get()));
		return members;
	}

	private final List<JavadocComment> getOrphanComments(Node decl) {
		final List<JavadocComment> orphanedComments = new ArrayList<JavadocComment>(decl.getOrphanComments().size());
		for (Comment c : decl.getOrphanComments()) {
			if (!c.isJavadocComment())
				continue;
			final Optional<Position> commentBeginOpt = c.getBegin();
			if (!commentBeginOpt.isPresent())
				continue;
			orphanedComments.add(c.asJavadocComment());
		}
		orphanedComments.sort((a, b) -> a.getBegin().get().compareTo(b.getBegin().get()));
		return orphanedComments;
	}

	private final void parseAnnotationMemberDecl(final JniTypeInfo typeInfo, final AnnotationMemberDeclaration memberDecl) {
		final JniMethodInfo methodInfo = new JniMethodInfo(typeInfo, memberDecl.getNameAsString());
		typeInfo.add(methodInfo);

		methodInfo.setReturnType(
				getJavaType(typeInfo, methodInfo, memberDecl.getType()),
				getJniType(typeInfo, methodInfo, memberDecl.getType()));

		fillJavadoc(methodInfo, memberDecl);
	}

	private final void parseFieldDecl(final JniTypeInfo typeInfo, final FieldDeclaration fieldDecl) {
		for (VariableDeclarator f : fieldDecl.getVariables()) {
			final   JniFieldInfo    fieldInfo   = new JniFieldInfo(typeInfo, f.getNameAsString());
			fieldInfo.setJniType(getJniType(typeInfo, null, f.getType()));

			typeInfo.add(fieldInfo);

			fillJavadoc(fieldInfo, fieldDecl);
		}
	}

	private final void parseConstructorDecl(final JniTypeInfo typeInfo, final ConstructorDeclaration ctorDecl) {
		final JniConstructorInfo ctorInfo = new JniConstructorInfo(typeInfo);
		typeInfo.add(ctorInfo);

		fillMethodBase(ctorInfo, ctorDecl);
		fillJavadoc(ctorInfo, ctorDecl);
	}

	private final void parseMethodDecl(final JniTypeInfo typeInfo, final MethodDeclaration methodDecl) {
		final JniMethodInfo methodInfo = new JniMethodInfo(typeInfo, methodDecl.getNameAsString());
		typeInfo.add(methodInfo);

		for (TypeParameter typeParameter : methodDecl.getTypeParameters()) {
			methodInfo.addTypeParameter(
					typeParameter.getNameAsString(),
					getJniType(typeInfo, methodInfo, getTypeParameterBound(typeParameter)));
		}
		methodInfo.setReturnType(
				getJavaType(typeInfo, methodInfo, methodDecl.getType()),
				getJniType(typeInfo, methodInfo, methodDecl.getType()));

		fillMethodBase(methodInfo, methodDecl);
		fillJavadoc(methodInfo, methodDecl);
	}

	private static final void fillJavadoc(final HasJavadocComment member, NodeWithJavadoc<?> nodeWithJavadoc) {
		JavadocComment javadoc = null;
		if (nodeWithJavadoc.getJavadocComment().isPresent()) {
			javadoc = nodeWithJavadoc.getJavadocComment().get();
		}

		if (javadoc != null) {
			member.setJavadocComment(javadoc.parse().toText());
		}
	}

	private final void fillMethodBase(final JniMethodBaseInfo methodBaseInfo, final CallableDeclaration<?> callableDecl) {
		JniMethodInfo   methodInfo  = null;
		if (methodBaseInfo instanceof JniMethodInfo) {
			methodInfo  = (JniMethodInfo) methodBaseInfo;
		}
		NodeWithParameters<?> params = callableDecl;
		for (final Parameter p : params.getParameters()) {
			String name = p.getNameAsString();
			String javaType = getJavaType(methodBaseInfo.getDeclaringType(), methodInfo, p.getType());
			String jniType  = getJniType(methodBaseInfo.getDeclaringType(), methodInfo, p.getType());
			methodBaseInfo.addParameter(new JniParameterInfo(name, javaType, jniType));
		}
	}

	static String getJavaType(JniTypeInfo typeInfo, JniMethodInfo methodInfo, Type type) {
		String typeName = type.asString();
		if (methodInfo != null && methodInfo.getTypeParameters().contains(typeName))
			return typeName;
		if (typeInfo.getTypeParameters().contains(typeName))
			return typeName;
		try {
			final ResolvedType rt = type.resolve();
			return rt.describe();
		} catch (final Throwable thr) {
			return ".*" + type.asString();
		}
	}

	static String getJniType(JniTypeInfo typeInfo, JniMethodInfo methodInfo, Type type) {
		if (type == null) {
			return "Ljava/lang/Object;";
		}

		if (type.isArrayType()) {
			return getJniType(typeInfo, methodInfo, type.asArrayType());
		}
		if (type.isPrimitiveType()) {
			return getPrimitiveJniType(type.asString());
		}

		if (methodInfo != null && methodInfo.getTypeParameters().contains(type.asString())) {
			return methodInfo.getTypeParameterJniType(type.asString());
		}
		if (typeInfo.getTypeParameters().contains(type.asString())) {
			return typeInfo.getTypeParameterJniType(type.asString());
		}

		try {
			return getJniType(type.resolve());
		}
		catch (final Exception thr) {
		}
		return ".*" + type.asString();
	}

	static String getJniType(JniTypeInfo typeInfo, JniMethodInfo methodInfo, ArrayType type) {
		final   int           level = type.getArrayLevel();
		final   StringBuilder depth = new StringBuilder();
		for (int i = 0; i < level; ++i)
			depth.append("[");
		return depth.toString() + getJniType(typeInfo, methodInfo, type.getElementType());
	}

	static String getPrimitiveJniType(String javaType) {
		switch (javaType) {
			case "boolean": return "Z";
			case "byte":    return "B";
			case "char":    return "C";
			case "double":  return "D";
			case "float":   return "F";
			case "int":     return "I";
			case "long":    return "J";
			case "short":   return "S";
			case "void":    return "V";
		}
		throw new Error("Don't know JNI type for `" + javaType + "`!");
	}

	static String getJniType(ResolvedType type) {
		if (type.isPrimitive()) {
			return getPrimitiveJniType(type.asPrimitive().describe());
		}
		if (type.isReferenceType()) {
			return getJniType(type.asReferenceType());
		}
		if (type.isVoid()) {
			return "V";
		}
		return "-" + type.getClass().getName() + "-";
	}

	static String getJniType(ResolvedReferenceType type) {
		final Optional<ResolvedReferenceTypeDeclaration> typeDeclOpt = type.getTypeDeclaration();
		if (!typeDeclOpt.isPresent())
			throw new Error("Can't get `ResolvedReferenceTypeDeclaration` for type `" + type.toString() + "`!");

		final ResolvedReferenceTypeDeclaration typeDecl = typeDeclOpt.get();
		if (!type.hasName())
			throw new Error("Type `" + type.toString() + "` has no name!");

		StringBuilder name = new StringBuilder();
		name.append("L");
		name.append(typeDecl.getPackageName());
		int len = name.length();
		for (int i = 0; i < len; ++i) {
			if (name.charAt (i) == '.') {
				name.setCharAt(i, '/');
			}
		}
		if (len > 1) {
			name.append("/");
		}
		name.append(typeDecl.getName().replace(".", "$"));
		name.append(";");
		return name.toString();
	}
}
