package com.microsoft.android;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileNotFoundException;
import java.io.PrintStream;
import java.io.FileOutputStream;
import java.io.UnsupportedEncodingException;
import java.util.Arrays;
import javax.xml.parsers.ParserConfigurationException;
import javax.xml.transform.TransformerException;

import org.junit.Test;
import static org.junit.Assert.*;

import com.github.javaparser.JavaParser;
import com.microsoft.android.ast.*;

public final class JavadocXmlGeneratorTest {
	@Test(expected = FileNotFoundException.class)
	public void init_invalidFileThrows() throws FileNotFoundException, UnsupportedEncodingException {
		try (JavadocXmlGenerator g = new JavadocXmlGenerator("/this/file/does/not/exist")) {
		}
	}

	@Test(expected = IllegalArgumentException.class)
	public void testWritePackages_nullPackages() throws ParserConfigurationException, TransformerException {
		ByteArrayOutputStream   bytes       = new ByteArrayOutputStream();
		JavadocXmlGenerator     generator   = new JavadocXmlGenerator(new PrintStream(bytes));

		generator.writePackages(null);
	}

	@Test
	public void testWritePackages_noPackages() throws ParserConfigurationException, TransformerException {
		ByteArrayOutputStream   bytes       = new ByteArrayOutputStream();
		JavadocXmlGenerator     generator   = new JavadocXmlGenerator(new PrintStream(bytes));

		JniPackagesInfo packages = new JniPackagesInfo();
		generator.writePackages(packages);

		final   String  expected =
			"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n" +
			"<api api-source=\"java-source-utils\"/>\n";
		assertEquals("no packages", expected, bytes.toString());
	}


	@Test
	public void testWritePackages_demo() throws ParserConfigurationException, TransformerException {
		final   ByteArrayOutputStream   bytes       = new ByteArrayOutputStream();
		final   JavadocXmlGenerator     generator   = new JavadocXmlGenerator(new PrintStream(bytes));
		final   JniPackagesInfo         packages    = JniPackagesInfoTest.createDemoInfo();

		final   String                  expected    = 
			"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n" +
			"<api api-source=\"java-source-utils\">\n" +
			"  <package jni-name=\"\" name=\"\">\n" +
			"    <class jni-signature=\"LA;\" name=\"A\">\n" +
			"      <javadoc><![CDATA[jni-sig=LA;]]></javadoc>\n" +
			"      <constructor jni-signature=\"(ILjava/lang/String;)V\">\n" +
			"        <parameter jni-type=\"I\" name=\"one\" type=\"int\"/>\n" +
			"        <parameter jni-type=\"Ljava/lang/String;\" name=\"two\" type=\"java.lang.String\"/>\n" +
			"        <javadoc><![CDATA[jni-sig=<init>.(ILjava/lang/String;)V]]></javadoc>\n" +
			"      </constructor>\n" +
			"      <field jni-signature=\"I\" name=\"field\">\n" +
			"        <javadoc><![CDATA[jni-sig=field.I]]></javadoc>\n" +
			"      </field>\n" +
			"      <method jni-return=\"V\" jni-signature=\"(Ljava/lang/Object;J)V\" name=\"m\" return=\"void\">\n" +
			"        <parameter jni-type=\"Ljava/lang/Object;\" name=\"value\" type=\"T\"/>\n" +
			"        <parameter jni-type=\"J\" name=\"x\" type=\"long\"/>\n" +
			"        <javadoc><![CDATA[jni-sig=m.(Ljava/lang/Object;J)V]]></javadoc>\n" +
			"      </method>\n" +
			"    </class>\n" +
			"    <interface jni-signature=\"LI;\" name=\"I\">\n" +
			"      <javadoc><![CDATA[jni-sig=LI;]]></javadoc>\n" +
			"      <method jni-return=\"Ljava/lang/Object;\" jni-signature=\"(Ljava/util/List;)Ljava/lang/Object;\" name=\"m\" return=\"T\">\n" +
			"        <parameter jni-type=\"Ljava/util/List;\" name=\"x\" type=\"java.util.List&lt;T&gt;\"/>\n" +
			"        <javadoc><![CDATA[jni-sig=m.(Ljava/util/List;)Ljava/lang/Object;]]></javadoc>\n" +
			"      </method>\n" +
			"    </interface>\n" +
			"  </package>\n" +
			"  <package jni-name=\"before/example\" name=\"before.example\"/>\n" +
			"  <package jni-name=\"example\" name=\"example\">\n" +
			"    <interface jni-signature=\"Lexample/Exampleable;\" name=\"Exampleable\">\n" +
			"      <javadoc><![CDATA[jni-sig=Lexample/Exampleable;]]></javadoc>\n" +
			"      <method jni-return=\"V\" jni-signature=\"(Ljava/lang/String;)V\" name=\"example\" return=\"void\">\n" +
			"        <parameter jni-type=\"Ljava/lang/String;\" name=\"e\" type=\"java.lang.String\"/>\n" +
			"        <javadoc><![CDATA[jni-sig=example.(Ljava/lang/String;)V]]></javadoc>\n" +
			"      </method>\n" +
			"      <method jni-return=\"V\" jni-signature=\"()V\" name=\"noParameters\" return=\"void\">\n" +
			"        <javadoc><![CDATA[jni-sig=noParameters.()V]]></javadoc>\n" +
			"      </method>\n" +
			"    </interface>\n" +
			"  </package>\n" +
			"</api>\n";

		generator.writePackages(packages);
		assertEquals("global package + example packages", expected, bytes.toString());
	}

	@Test
	public void testWritePackages_Outer_java() throws Throwable {
		testWritePackages("Outer.java", "Outer.xml");
	}

	@Test
	public void testWritePackages_JavaType_java() throws Throwable {
		testWritePackages("../../../com/xamarin/JavaType.java", "JavaType.xml");
	}

	@Test
	public void testWritePackages_UnresolvedTypes_txt() throws Throwable {
		testWritePackages("../../../UnresolvedTypes.txt", "../../../UnresolvedTypes.xml");
	}

	private static void testWritePackages(final String resourceJava, final String resourceXml) throws Throwable {
		final   JavaParser              parser          = JniPackagesInfoFactoryTest.createParser();
		final   JniPackagesInfoFactory  factory         = new JniPackagesInfoFactory(parser);
		final   File                    demoSource      = new File(JniPackagesInfoFactoryTest.class.getResource(resourceJava).toURI());
		final   JniPackagesInfo         packagesInfo    = factory.parse(Arrays.asList(new File[]{demoSource}));

		final   ByteArrayOutputStream   bytes           = new ByteArrayOutputStream();
		final   JavadocXmlGenerator     generator       = new JavadocXmlGenerator(new PrintStream(bytes));

		final   String                  expected        = JniPackagesInfoTest.getResourceContents(resourceXml);

		generator.writePackages(packagesInfo);
		// try (FileOutputStream o = new FileOutputStream(resourceXml + "-jonp.xml")) {
		// 	bytes.writeTo(o);
		// }
		assertEquals(resourceJava + " Javadoc XML", expected, bytes.toString());
	}
}
