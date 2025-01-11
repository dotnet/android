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

import org.junit.Assume;
import org.junit.Test;
import static org.junit.Assert.*;

import com.github.javaparser.JavaParser;
import com.microsoft.android.ast.*;

public final class JavadocXmlGeneratorTest {
	@Test(expected = FileNotFoundException.class)
	public void init_invalidFileThrows() throws FileNotFoundException, ParserConfigurationException, TransformerException, UnsupportedEncodingException {
		String invalidFilePath = "/this/file/does/not/exist";
		String osName = System.getProperty("os.name");
		if (osName.startsWith("Windows")) {
			invalidFilePath = System.getenv("ProgramFiles") + "\\this\\file\\does\\not\\exist";
			// Ignore if running on an Azure Pipelines Microsoft hosted agent by only running when %AGENT_NAME% is not set.
			Assume.assumeTrue(System.getenv("AGENT_NAME") == null);
		}
		try (JavadocXmlGenerator g = new JavadocXmlGenerator(invalidFilePath)) {
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
		generator.close();

		final   String  expected = (
			"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n" +
			"<api api-source=\"java-source-utils\"/>\n"
		).replace("\n", System.lineSeparator());

		assertEquals("no packages", expected, bytes.toString());
	}


	@Test
	public void testWritePackages_demo() throws Throwable {
		final   JniPackagesInfo         packages    = JniPackagesInfoTest.createDemoInfo();

		testWritePackages (packages, "global package + example packages", "DemoInfo.xml");
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

		int                             lastSlash       = resourceJava.lastIndexOf('/');
		final   String                  assertDesc      = resourceJava.substring(lastSlash+1) + " Javadoc XML";

		testWritePackages(packagesInfo, assertDesc, resourceXml);
	}

	private static void testWritePackages(final JniPackagesInfo packagesInfo, final String assertDescription, final String expectedResourceXml) throws Throwable {
		final   ByteArrayOutputStream   bytes           = new ByteArrayOutputStream();
		final   JavadocXmlGenerator     generator       = new JavadocXmlGenerator(new PrintStream(bytes));

		final   String                  expected        = JniPackagesInfoTest.getResourceContents(expectedResourceXml);

		generator.writePackages(packagesInfo);
		generator.close();

		final   File                    actual          = new File(assertDescription + "-jonp.xml");
		try (FileOutputStream o = new FileOutputStream(actual)) {
			bytes.writeTo(o);
		}
		assertEquals(assertDescription, expected, bytes.toString());
		actual.delete();
	}
}
