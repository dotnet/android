package com.microsoft.android;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileNotFoundException;
import java.io.PrintStream;
import java.io.UnsupportedEncodingException;
import java.util.Arrays;

import org.junit.Assume;
import org.junit.Test;
import static org.junit.Assert.*;

import com.github.javaparser.JavaParser;
import com.microsoft.android.ast.*;

public class ParameterNameGeneratorTest {

	@Test(expected = FileNotFoundException.class)
	public void init_invalidFileThrows() throws FileNotFoundException, UnsupportedEncodingException {
		String invalidFilePath = "/this/file/does/not/exist";
		String osName = System.getProperty("os.name");
		if (osName.startsWith("Windows")) {
			invalidFilePath = System.getenv("ProgramFiles") + "\\this\\file\\does\\not\\exist";
			// Ignore if running on an Azure Pipelines Microsoft hosted agent by only running when %AGENT_NAME% is not set.
			Assume.assumeTrue(System.getenv("AGENT_NAME") == null);
		}
		new ParameterNameGenerator(invalidFilePath);
	}

	@Test(expected = IllegalArgumentException.class)
	public void testWritePackages_nullPackages() {
		ByteArrayOutputStream   bytes       = new ByteArrayOutputStream();
		ParameterNameGenerator  generator   = new ParameterNameGenerator(new PrintStream(bytes));

		generator.writePackages(null);
	}

	@Test
	public void testWritePackages_noPackages() {
		ByteArrayOutputStream   bytes       = new ByteArrayOutputStream();
		ParameterNameGenerator  generator   = new ParameterNameGenerator(new PrintStream(bytes));

		JniPackagesInfo packages = new JniPackagesInfo();
		generator.writePackages(packages);
		assertEquals("no packages", "", bytes.toString());
	}


	@Test
	public void testWritePackages_demo() {
		ByteArrayOutputStream   bytes       = new ByteArrayOutputStream();
		ParameterNameGenerator  generator   = new ParameterNameGenerator(new PrintStream(bytes));
		JniPackagesInfo         packages    = JniPackagesInfoTest.createDemoInfo();

		final String expected = (
			";---------------------------------------\n" +
			"  class A\n" +
			"    #ctor(int one, java.lang.String two)\n" +
			"    <T> m(T value, long x)\n" +
			"  interface I<T>\n" +
			"    m(java.util.List<T> x)\n" +
			"\n" +
			"package before.example\n" +
			";---------------------------------------\n" +
			"\n" +
			"package example\n" +
			";---------------------------------------\n" +
			"  interface Exampleable\n" +
			"    example(java.lang.String e)\n" +
			""
		).replace("\n", System.lineSeparator());

		generator.writePackages(packages);
		assertEquals("global package + example packages", expected, bytes.toString());
	}

	@Test
	public void testWritePackages_Outer_java() throws Throwable {
		testWritePackages("Outer.java", "Outer.params.txt");
	}

	@Test
	public void testWritePackages_JavaType_java() throws Throwable {
		testWritePackages("../../../com/xamarin/JavaType.java", "JavaType.params.txt");
	}

	private static void testWritePackages(final String resourceJava, final String resourceParamsTxt) throws Throwable {
		final   JavaParser              parser          = JniPackagesInfoFactoryTest.createParser();
		final   JniPackagesInfoFactory  factory         = new JniPackagesInfoFactory(parser);
		final   File                    demoSource      = new File(JniPackagesInfoFactoryTest.class.getResource(resourceJava).toURI());
		final   JniPackagesInfo         packagesInfo    = factory.parse(Arrays.asList(new File[]{demoSource}));

		final   ByteArrayOutputStream   bytes           = new ByteArrayOutputStream();
		final   ParameterNameGenerator  generator       = new ParameterNameGenerator(new PrintStream(bytes));

		final   String                  expected        = JniPackagesInfoTest.getResourceContents(resourceParamsTxt);

		generator.writePackages(packagesInfo);
		assertEquals(resourceJava + " parameter names", expected, bytes.toString());
	}
}
