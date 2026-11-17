package com.microsoft.android;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.PrintStream;
import java.io.UnsupportedEncodingException;
import java.util.Collection;
import java.util.List;
import java.util.stream.Collectors;

import com.microsoft.android.ast.*;
import com.microsoft.android.util.Parameter;

public class ParameterNameGenerator implements AutoCloseable {

	final PrintStream output;

	public ParameterNameGenerator(final String output) throws FileNotFoundException, UnsupportedEncodingException {
		if (output == null)
			this.output = System.out;
		else {
			final File file = new File(output);
			final File parent = file.getParentFile();
			if (parent != null) {
				parent.mkdirs();
			}
			this.output = new PrintStream(file, "UTF-8");
		}
	}

	public ParameterNameGenerator(final PrintStream output) {
		Parameter.requireNotNull("output", output);

		this.output = output;
	}

	public void close() {
		if (output != System.out) {
			output.flush();
			output.close();
		}
	}

	public final void writePackages(final JniPackagesInfo packages) {
		Parameter.requireNotNull("packages", packages);

		boolean first = true;
		for (JniPackageInfo packageInfo : packages.getSortedPackages()) {
			if (!first)
				output.println();
			first = false;
			writePackage(packageInfo);
		}
	}

	private final void writePackage(final JniPackageInfo packageInfo) {
		if (packageInfo.getPackageName().length() > 0) {
			output.println("package " + packageInfo.getPackageName());
		}
		output.println(";---------------------------------------");

		for (JniTypeInfo type : packageInfo.getSortedTypes()) {
			writeType(type);
		}
	}

	private final void writeType(JniTypeInfo type) {
		output.println("  " + type.getTypeKind() + " " + type.getName());
		final List<JniMethodBaseInfo> sortedMethods = type.getSortedMembers()
			.stream()
			.filter(member -> member.isMethod() || member.isConstructor())
			.map(member -> (JniMethodBaseInfo) member)
			.filter(method -> method.getParameters().size() > 0)
			.collect(Collectors.toList());

		for (JniMethodBaseInfo method : sortedMethods) {
			output.print("    ");
			if (method.isMethod()) {
				JniMethodInfo m = (JniMethodInfo) method;
				Collection<String> typeParameters = m.getTypeParameters();
				if (typeParameters.size() > 0) {
					output.print("<");
					output.print(String.join(", ", typeParameters));
					output.print("> ");
				}
			}
			output.print(method.getName());
			output.print("(");
			boolean first = true;
			for (JniParameterInfo parameter : method.getParameters()) {
				if (!first) {
					output.print(", ");
				}
				first = false;
				output.print(parameter.javaType);
				output.print(" ");
				output.print(parameter.name);
			}
			output.print(")");
			output.println();
		}
	}
}
