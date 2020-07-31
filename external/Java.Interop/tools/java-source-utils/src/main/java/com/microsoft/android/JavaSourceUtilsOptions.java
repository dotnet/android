package com.microsoft.android;

import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Collection;
import java.util.Comparator;
import java.util.Enumeration;
import java.util.HashSet;
import java.util.List;
import java.util.ArrayList;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;

import com.github.javaparser.*;
import com.github.javaparser.JavaParser;
import com.github.javaparser.ParserConfiguration;
import com.github.javaparser.StaticJavaParser;
import com.github.javaparser.ast.*;
import com.github.javaparser.ast.type.*;
import com.github.javaparser.ast.body.*;
import com.github.javaparser.ast.body.BodyDeclaration;
import com.github.javaparser.ast.body.TypeDeclaration;
import com.github.javaparser.ast.expr.Expression;
import com.github.javaparser.ast.body.Parameter;
import com.github.javaparser.ast.nodeTypes.*;
import com.github.javaparser.ast.nodeTypes.NodeWithJavadoc;
import com.github.javaparser.ast.nodeTypes.NodeWithParameters;
import com.github.javaparser.ast.nodeTypes.NodeWithSimpleName;
import com.github.javaparser.resolution.SymbolResolver;
import com.github.javaparser.resolution.types.ResolvedType;
import com.github.javaparser.symbolsolver.*;
import com.github.javaparser.symbolsolver.model.resolution.TypeSolver;
import com.github.javaparser.symbolsolver.resolution.typesolvers.*;


public class JavaSourceUtilsOptions implements AutoCloseable {
	public static final String HELP_STRING = "[-v] [<-a|--aar> AAR]* [<-j|--jar> JAR]* [<-s|--source> DIRS]*\n" +
		"\t[--bootclasspath CLASSPATH]\n" +
		"\t[<-P|--output-params> OUT.params.txt] [<-D|--output-javadoc> OUT.xml] FILES";

	public  static  boolean             verboseOutput;

	public	final   List<File>          aarFiles      = new ArrayList<File>();
	public  final   List<File>          jarFiles      = new ArrayList<File>();

	public  final   Collection<File>    inputFiles    = new ArrayList<File>();

	public  boolean haveBootClassPath;
	public  String  outputParamsTxt;
	public  String  outputJavadocXml;

	private final   Collection<File>    sourceDirectoryFiles  = new ArrayList<File>();
	private         File                extractedTempDir;


	public void close() {
		if (extractedTempDir != null) {
			try {
				Files.walk(extractedTempDir.toPath())
					.sorted(Comparator.reverseOrder())
					.map(Path::toFile)
					.forEach(File::delete);
				extractedTempDir.delete();
			}
			catch (Throwable t) {
				System.err.println(App.APP_NAME + ": error deleting temp directory `" + extractedTempDir.getAbsolutePath() + "`: " + t.getMessage());
				if (verboseOutput) {
					t.printStackTrace(System.err);
				}
			}
		}
		extractedTempDir    = null;
	}

	public ParserConfiguration createConfiguration() throws IOException {
		final   ParserConfiguration config      = new ParserConfiguration()
			// Associate Javadoc comments with AST members
			.setAttributeComments(true)

			// If there are blank lines between Javadoc blocks & declarations,
			// *ignore* those blank lines and associate the Javadoc w/ the decls
			.setDoNotAssignCommentsPrecedingEmptyLines(false)

			// Associate Javadoc comments w/ the declaration, *not* with
			// any annotations on the declaration
			.setIgnoreAnnotationsWhenAttributingComments(true)
			;
		final   TypeSolver          typeSolver  = createTypeSolver(config);
		config.setSymbolResolver(new JavaSymbolSolver(typeSolver));
		return config;
	}

	private final TypeSolver createTypeSolver(ParserConfiguration config) throws IOException {
		final   CombinedTypeSolver  typeSolver  = new CombinedTypeSolver();
		for (File file : aarFiles) {
			typeSolver.add(new AarTypeSolver(file));
		}
		for (File file : jarFiles) {
			typeSolver.add(new JarTypeSolver(file));
		}
		if (!haveBootClassPath) {
			typeSolver.add(new ReflectionTypeSolver());
		}
		for (File srcDir : sourceDirectoryFiles) {
			typeSolver.add(new JavaParserTypeSolver(srcDir, config));
		}
		return typeSolver;
	}

	public static JavaSourceUtilsOptions parse(final String[] args) throws IOException {
		final   JavaSourceUtilsOptions  options = new JavaSourceUtilsOptions();

		for (int i = 0; i < args.length; ++i) {
			final   String  arg     = args[i];
			switch (arg) {
				case "-bootclasspath": {
					final   String          bootClassPath   = getOptionValue(args, ++i, arg);
					final   ArrayList<File> files           = new ArrayList<File>();
					for (final String cp : bootClassPath.split(File.pathSeparator)) {
						final   File    file    = new File(cp);
						if (!file.exists()) {
							System.err.println(App.APP_NAME + ": warning: invalid file path for option `-bootclasspath`: " + cp);
							continue;
						}
						files.add(file);
					}
					for (int j = files.size(); j > 0; --j) {
						options.jarFiles.add(0, files.get(j-1));
					}
					options.haveBootClassPath   = true;
					break;
				}
				case "-a":
				case "--aar": {
					final   File    file    = getOptionFile(args, ++i, arg);
					if (file == null) {
						break;
					}
					options.aarFiles.add(file);
					break;
				}
				case "-j":
				case "--jar": {
					final   File    file    = getOptionFile(args, ++i, arg);
					if (file == null) {
						break;
					}
					options.jarFiles.add(file);
					break;
				}
				case "-s":
				case "--source": {
					final   File    dir     = getOptionFile(args, ++i, arg);
					if (dir == null) {
						break;
					}
					options.sourceDirectoryFiles.add(dir);
					break;
				}
				case "-D":
				case "--output-javadoc": {
					options.outputJavadocXml    = getOptionValue(args, ++i, arg);
					break;
				}
				case "-P":
				case "--output-params": {
					options.outputParamsTxt     = getOptionValue(args, ++i, arg);
					break;
				}
				case "-v": {
					verboseOutput   = true;
					break;
				}
				case "-h":
				case "--help": {
					return null;
				}
				default: {
					final   File    file    = getOptionFile(args, i, "FILES");
					if (file == null)
						break;

					if (file.isDirectory()) {
						options.sourceDirectoryFiles.add(file);
						Files.walk(file.toPath())
							.filter(f -> Files.isRegularFile(f) && f.getFileName().toString().endsWith(".java"))
							.map(Path::toFile)
							.forEach(f -> options.inputFiles.add(f));
						break;
					}
					if (file.getName().endsWith(".java")) {
						options.inputFiles.add(file);
						break;
					}
					if (!file.getName().endsWith(".jar") && !file.getName().endsWith(".zip")) {
						System.err.println(App.APP_NAME + ": warning: ignoring input file `" + file.getAbsolutePath() +"`.");
						break;
					}
					if (options.extractedTempDir == null) {
						options.extractedTempDir    = Files.createTempDirectory("ji-jst").toFile();
					}
					File toDir  = new File(options.extractedTempDir, file.getName());
					options.sourceDirectoryFiles.add(toDir);
					extractTo(file, toDir, options.inputFiles);
					break;
				}
			}
		}
		return options;
	}

	private static void extractTo(final File zipFilePath, final File toDir, final Collection<File> inputFiles) throws IOException {
		try (final ZipFile zipFile  = new ZipFile(zipFilePath)) {
			Enumeration<? extends ZipEntry> e   = zipFile.entries();
			while (e.hasMoreElements()) {
				final   ZipEntry    entry       = e.nextElement();
				if (entry.isDirectory())
					continue;
				if (!entry.getName().endsWith(".java"))
					continue;
				final   File        target      = new File(toDir, entry.getName());
				if (verboseOutput) {
					System.out.println ("# creating file: " + target.getAbsolutePath());
				}
				target.getParentFile().mkdirs();
				final   InputStream zipContents = zipFile.getInputStream(entry);
				Files.copy(zipContents, target.toPath());
				zipContents.close();
				inputFiles.add(target);
			}
		}
	}

	static String getOptionValue(final String[] args, final int index, final String option) {
		if (index >= args.length)
			throw new IllegalArgumentException(
					"Expected required value for option `" + option + "` at index " + index + ".");
		return args[index];
	}

	static File getOptionFile(final String[] args, final int index, final String option) {
		if (index >= args.length)
			throw new IllegalArgumentException(
					"Expected required value for option `" + option + "` at index " + index + ".");
		final   String  fileName    = args[index];
		final   File    file        = new File(fileName);
		if (!file.exists()) {
			System.err.println(App.APP_NAME + ": warning: invalid file path for option `" + option + "`: " + fileName);
			return null;
		}
		return file;
	}
}
