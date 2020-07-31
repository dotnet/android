package com.microsoft.android;

import java.io.ByteArrayOutputStream;
import java.io.FileNotFoundException;
import java.io.PrintStream;
import java.util.Arrays;
import java.io.File;

import org.junit.Test;
import static org.junit.Assert.*;

import com.github.javaparser.JavaParser;
import com.github.javaparser.ParserConfiguration;
import com.github.javaparser.symbolsolver.*;
import com.github.javaparser.symbolsolver.resolution.typesolvers.CombinedTypeSolver;
import com.github.javaparser.symbolsolver.resolution.typesolvers.*;


import com.microsoft.android.ast.*;

public class JniPackagesInfoFactoryTest {

	@Test(expected = IllegalArgumentException.class)
	public void testInit_nullParser() {
		new JniPackagesInfoFactory(null);
	}

	@Test(expected = IllegalArgumentException.class)
	public void testParse_nullFiles() throws Throwable {
		final JavaParser              parser  = new JavaParser();
		final JniPackagesInfoFactory  factory = new JniPackagesInfoFactory(parser);
		factory.parse(null);
	}

	@Test
	public void testParse_demo() throws Throwable {
		final   JavaParser              parser          = createParser();
		final   JniPackagesInfoFactory  factory         = new JniPackagesInfoFactory(parser);
		final   File                    demoSource      = new File(JniPackagesInfoFactoryTest.class.getResource("Outer.java").toURI());
		final   JniPackagesInfo         packagesInfo    = factory.parse(Arrays.asList(new File[]{demoSource}));

		assertEquals("Only one package processed", 1, packagesInfo.getPackages().size());
		final   JniPackageInfo          p   = packagesInfo.getPackage("example");
		assertNotNull("Should have found `example` package", p);
		assertEquals("Outer & Outer.Inner & Outer.Inner.NestedInner & Outer.MyAnnotation types found", 4, p.getTypes().size());

		JniTypeInfo info = p.getType("Outer");
		assertNotNull(info);
		assertEquals("Outer<T,U>", info.getName());
	}

	static JavaParser createParser() {
		final CombinedTypeSolver typeSolver = new CombinedTypeSolver();
		typeSolver.add(new ReflectionTypeSolver());
		final ParserConfiguration config = new ParserConfiguration();
		config.setSymbolResolver(new JavaSymbolSolver(typeSolver));
		return new JavaParser(config);
	}
}
