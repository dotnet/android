package com.microsoft.android;

import java.io.*;
import java.net.URISyntaxException;

import com.microsoft.android.ast.*;

public class JniPackagesInfoTest {

	static JniPackagesInfo createDemoInfo() {
		JniPackagesInfo packages    = new JniPackagesInfo();
		JniPackageInfo  global      = packages.getPackage(null);

		JniTypeInfo     type        = new JniClassInfo(global, "A");
		type.setJavadocComment("jni-sig=LA;");
		global.add(type);

		JniFieldInfo    field       = new JniFieldInfo(type, "field");
		field.setJniType("I");
		field.setJavadocComment("jni-sig=field.I");
		type.add(field);

		JniConstructorInfo  init    = new JniConstructorInfo(type);
		init.addParameter(new JniParameterInfo("one", "int", "I"));
		init.addParameter(new JniParameterInfo("two", "java.lang.String", "Ljava/lang/String;"));
		init.setJavadocComment("jni-sig=<init>.(ILjava/lang/String;)V");
		type.add(init);

		JniMethodInfo       method  = new JniMethodInfo(type, "m");
		method.addTypeParameter("T", "Ljava/lang/Object;");
		method.addParameter(new JniParameterInfo("value", "T", "Ljava/lang/Object;"));
		method.addParameter(new JniParameterInfo("x", "long", "J"));
		method.setReturnType("void", "V");
		method.setJavadocComment("jni-sig=m.(Ljava/lang/Object;J)V");
		type.add(method);

		type    = new JniInterfaceInfo(global, "I");
		type.addTypeParameter("T", "Ljava/lang/Object;");
		type.setJavadocComment("jni-sig=LI;");
		global.add(type);
		method  = new JniMethodInfo(type, "m");
		method.addParameter(new JniParameterInfo("x", "java.util.List<T>", "Ljava/util/List;"));
		method.setReturnType("T", "Ljava/lang/Object;");
		method.setJavadocComment("jni-sig=m.(Ljava/util/List;)Ljava/lang/Object;");
		type.add(method);

		JniPackageInfo  example = packages.getPackage("example");
		type                    = new JniInterfaceInfo(example, "Exampleable");
		type.setJavadocComment("jni-sig=Lexample/Exampleable;");
		example.add(type);

		method                  = new JniMethodInfo(type, "noParameters");
		method.setReturnType("void", "V");
		method.setJavadocComment("jni-sig=noParameters.()V");
		type.add(method);

		method                  = new JniMethodInfo(type, "example");
		method.addParameter(new JniParameterInfo("e", "java.lang.String", "Ljava/lang/String;"));
		method.setReturnType("void", "V");
		method.setJavadocComment("jni-sig=example.(Ljava/lang/String;)V");
		type.add(method);

		packages.getPackage("before.example");

		return packages;
	}

	static String getResourceContents(String resourceName) throws IOException, URISyntaxException {
		final   File            resourceFile    = new File(JniPackagesInfoTest.class.getResource(resourceName).toURI());
		final   StringBuilder   contents        = new StringBuilder();
		final   String          lineEnding      = System.getProperty("line.separator");

		String  line;
		try (final BufferedReader reader = new BufferedReader(new FileReader (resourceFile))) {
			while((line = reader.readLine()) != null) {
				contents.append(line);
				contents.append(lineEnding);
			}
		}
		return contents.toString();
	}
}
