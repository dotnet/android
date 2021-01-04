package com.microsoft.android;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;
import java.io.PrintStream;
import java.io.UnsupportedEncodingException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;

import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;
import javax.xml.parsers.ParserConfigurationException;
import javax.xml.transform.OutputKeys;
import javax.xml.transform.Transformer;
import javax.xml.transform.TransformerException;
import javax.xml.transform.TransformerFactory;
import javax.xml.transform.dom.DOMSource;
import javax.xml.transform.stream.StreamResult;

import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.NodeList;

import com.microsoft.android.ast.*;
import com.microsoft.android.util.Parameter;

public final class JavadocXmlGenerator implements AutoCloseable {

	final   PrintStream output;

	Document    document;
	Element     api;

	public JavadocXmlGenerator(final String output) throws FileNotFoundException, ParserConfigurationException, UnsupportedEncodingException {
		if (output == null)
			this.output = System.out;
		else {
			final File file     = new File(output);
			final File parent   = file.getParentFile();
			if (parent != null) {
				parent.mkdirs();
			}
			this.output = new PrintStream(file, "UTF-8");
		}

		startApi();
	}

	public JavadocXmlGenerator(final PrintStream output) throws ParserConfigurationException {
		Parameter.requireNotNull("output", output);

		this.output = output;

		startApi();
	}

	private void startApi() throws ParserConfigurationException {
		document    = DocumentBuilderFactory.newInstance ()
			.newDocumentBuilder()
			.newDocument();
		api         = document.createElement("api");
		api.setAttribute("api-source", "java-source-utils");
		document.appendChild(api);
	}

	public void close() throws TransformerException {
		Transformer transformer = TransformerFactory.newInstance()
			.newTransformer();
		transformer.setOutputProperty(OutputKeys.INDENT, "yes");
		transformer.setOutputProperty("{http://xml.apache.org/xslt}indent-amount", "2");
		transformer.transform(new DOMSource(document), new StreamResult(output));

		if (output != System.out) {
			output.flush();
			output.close();
		}
	}

	public final void writeCopyrightInfo(final File copyright, final String urlPrefix, final String urlStyle) throws IOException, ParserConfigurationException {
		final   Element    info         = document.createElement("javadoc-metadata");
		if (copyright != null) {
			final   Element    blurb    = document.createElement("copyright");
			final   NodeList   contents = readXmlFile(copyright);
			if (contents == null) {
				final byte[]   data     = Files.readAllBytes(copyright.toPath());
				blurb.appendChild(document.createCDATASection(new String(data, StandardCharsets.UTF_8)));
			} else {
				final int len = contents.getLength();
				for (int i = 0; i < len; ++i)
					blurb.appendChild(document.importNode(contents.item(i),true));
			}
			info.appendChild(blurb);
		}
		if (urlPrefix != null && urlStyle != null) {
			final   Element    link = document.createElement("link");
			link.setAttribute("prefix", urlPrefix);
			link.setAttribute("style", urlStyle);

			info.appendChild(link);
		}

		if (info.hasChildNodes()) {
			api.appendChild(info);
		}
	}

	final NodeList readXmlFile(final File file) throws ParserConfigurationException {
		final   DocumentBuilder     builder     = DocumentBuilderFactory.newInstance ()
			.newDocumentBuilder();
		try {
			final   Document        contents    = builder.parse(file);
			return contents.getChildNodes();
		}
		catch (Throwable t) {
			return null;
		}
	}

	public final void writePackages(final JniPackagesInfo packages) throws ParserConfigurationException, TransformerException {
		Parameter.requireNotNull("packages", packages);

		for (JniPackageInfo packageInfo : packages.getSortedPackages()) {
			writePackage(document, api, packageInfo);
		}
	}

	private static final void writePackage(final Document document, final Element api, final JniPackageInfo packageInfo) {
		final   Element packageXml  = document.createElement("package");
		packageXml.setAttribute("name", packageInfo.getPackageName());
		packageXml.setAttribute("jni-name", packageInfo.getPackageName().replace(".", "/"));
		api.appendChild(packageXml);

		for (JniTypeInfo typeInfo : packageInfo.getSortedTypes()) {
			writeType(document, packageXml, typeInfo);
		}
	}

	private static final void writeType(final Document document, final Element packageXml, final JniTypeInfo typeInfo) {
		final   Element typeXml     = document.createElement(typeInfo.getTypeKind());
		typeXml.setAttribute("name",            typeInfo.getRawName());
		typeXml.setAttribute("jni-signature",   getTypeJniName(typeInfo));
		packageXml.appendChild(typeXml);

		writeJavadoc(document, typeXml, typeInfo.getJavadocComment());

		for (JniMemberInfo memberInfo : typeInfo.getSortedMembers()) {
			writeMember(document, typeXml, memberInfo);
		}
	}

	private static String getTypeJniName(JniTypeInfo typeInfo) {
		final   String          packageName = typeInfo.getDeclaringPackage().getPackageName();
		final   StringBuilder   name        = new StringBuilder();

		name.append("L");
		if (packageName.length() > 0) {
			name.append(packageName.replace(".", "/"));
			name.append("/");
		}
		name.append(typeInfo.getRawName().replace(".", "$"));
		name.append(";");

		return name.toString();
	}

	private static final void writeJavadoc(final Document document, final Element parent, String javadoc) {
		javadoc = Parameter.normalize(javadoc, "");

		if (javadoc.length() == 0) {
			return;
		}

		final   Element javadocXml  = document.createElement("javadoc");
		parent.appendChild(javadocXml);

		javadocXml.appendChild(document.createCDATASection(javadoc));
	}

	private static void writeMember(final Document document, final Element typeXml, final JniMemberInfo memberInfo) {
		JniMethodBaseInfo   paramsInfo  = null;
		int                 paramsCount = 0;
		if (memberInfo.isConstructor() || memberInfo.isMethod()) {
			paramsInfo  = (JniMethodBaseInfo) memberInfo;
			paramsCount = paramsInfo.getParameters().size();
		}
		final   String      javadoc     = Parameter.normalize(memberInfo.getJavadocComment(), "");
		if (paramsCount == 0 && javadoc.length() == 0) {
			return;
		}

		final   Element memberXml   = document.createElement(getMemberXmlElement(memberInfo));
		if (!memberInfo.isConstructor()) {
			memberXml.setAttribute("name",      memberInfo.getName());
		}
		memberXml.setAttribute("jni-signature", memberInfo.getJniSignature());
		typeXml.appendChild(memberXml);

		if (memberInfo.isMethod()) {
			final   JniMethodInfo   methodInfo = (JniMethodInfo) memberInfo;
			memberXml.setAttribute("return", methodInfo.getJavaReturnType());
			memberXml.setAttribute("jni-return", methodInfo.getJniReturnType());
		}

		if (paramsInfo != null) {
			for (JniParameterInfo paramInfo : paramsInfo.getParameters()) {
				final   Element parameter   = document.createElement("parameter");
				parameter.setAttribute("name", paramInfo.name);
				parameter.setAttribute("type", paramInfo.javaType);
				parameter.setAttribute("jni-type", paramInfo.jniType);

				memberXml.appendChild(parameter);
			}
		}

		writeJavadoc(document, memberXml, memberInfo.getJavadocComment());
	}

	private static String getMemberXmlElement(JniMemberInfo member) {
		if (member.isConstructor())
			return "constructor";
		if (member.isMethod())
			return "method";
		if (member.isField())
			return "field";
		throw new Error("Don't know XML element for: " + member.toString());
	}
}
