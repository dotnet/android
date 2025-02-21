using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public abstract class JavaToolTask : AndroidToolTask
	{
		/*
		Example Javac output for errors. Regex Matches on the first line, we then need to
		process the second line to get the column number so the IDE can correctly
		mark where the error is.

		TestMe.java:1: error: class, interface, or enum expected
		public classo TestMe { }
		^
		TestMe2.java:1: error: ';' expected
		public class TestMe2 {public vod Test ()}
		^
		2 errors
		*/
		const string CodeErrorRegExString = @"(?<file>.+\.java):(?<line>\d+):(?<error>.+)";
		/*

		Sample OutOfMemoryError raised by java. RegEx matches the java.lang.* line of the error
		and splits it into an exception and an error

		UNEXPECTED TOP-LEVEL ERROR:
		java.lang.OutOfMemoryError: GC overhead limit exceeded
			at java.util.Arrays.copyOf(Arrays.java:2219)
			at java.util.ArrayList.grow(ArrayList.java:242)
			at java.util.ArrayList.ensureExplicitCapacity(ArrayList.java:216)
			at java.util.ArrayList.ensureCapacityInternal(ArrayList.java:208)
			at java.util.ArrayList.add(ArrayList.java:440)
			at com.android.dx.ssa.Dominators$DfsWalker.visitBlock(Dominators.java:263)
			at com.android.dx.ssa.SsaMethod.forEachBlockDepthFirst(SsaMethod.java:783)
			at com.android.dx.ssa.Dominators.run(Dominators.java:185)
			at com.android.dx.ssa.Dominators.make(Dominators.java:90)
			at com.android.dx.ssa.DomFront.run(DomFront.java:86)
			at com.android.dx.ssa.SsaConverter.placePhiFunctions(SsaConverter.java:297)
			at com.android.dx.ssa.SsaConverter.convertToSsaMethod(SsaConverter.java:51)
			at com.android.dx.ssa.Optimizer.optimize(Optimizer.java:98)
			at com.android.dx.ssa.Optimizer.optimize(Optimizer.java:72)
			at com.android.dx.dex.cf.CfTranslator.processMethods(CfTranslator.java:297)
			at com.android.dx.dex.cf.CfTranslator.translate0(CfTranslator.java:137)
			at com.android.dx.dex.cf.CfTranslator.translate(CfTranslator.java:93)
			at com.android.dx.command.dexer.Main.processClass(Main.java:729)
			at com.android.dx.command.dexer.Main.processFileBytes(Main.java:673)
			at com.android.dx.command.dexer.Main.access$300(Main.java:83)
			at com.android.dx.command.dexer.Main$1.processFileBytes(Main.java:602)
			at com.android.dx.cf.direct.ClassPathOpener.processArchive(ClassPathOpener.java:284)
			at com.android.dx.cf.direct.ClassPathOpener.processOne(ClassPathOpener.java:166)
			at com.android.dx.cf.direct.ClassPathOpener.process(ClassPathOpener.java:144)
			at com.android.dx.command.dexer.Main.processOne(Main.java:632)
			at com.android.dx.command.dexer.Main.processAllFiles(Main.java:505)
			at com.android.dx.command.dexer.Main.runMultiDex(Main.java:334)
			at com.android.dx.command.dexer.Main.run(Main.java:244)
			at com.android.dx.command.dexer.Main.main(Main.java:215)
			at com.android.dx.command.Main.main(Main.java:106)
		*/
		const string ExceptionRegExString = @"(?<exception>java.lang.+):(?<error>.+)";
		const string LPDirectoryRegExString = @"(lp)([/\\]+)(?<identifier>[0-9]+)([/\\]+)";
		static readonly Regex codeErrorRegEx = new Regex (CodeErrorRegExString, RegexOptions.Compiled);
		static readonly Regex exceptionRegEx = new Regex (ExceptionRegExString, RegexOptions.Compiled);
		static readonly Regex lpRegex = new Regex (LPDirectoryRegExString, RegexOptions.Compiled);
		bool foundError = false;
		List<string> errorLines = new List<string> ();
		StringBuilder errorText = new StringBuilder ();
		HashSet<string> mappingText = new HashSet<string> ();
		string file;
		int line, column;

		public string JavaOptions { get; set; }

		public string JavaMaximumHeapSize { get; set; }

		public virtual string DefaultErrorCode => "JAVA0000";

		public string AssemblyIdentityMapFile { get; set; }

		public string IntermediateOutputPath { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "java.exe" : "java"; }
		}

		protected virtual Regex CodeErrorRegEx => codeErrorRegEx;

		protected virtual Regex ExceptionRegEx => exceptionRegEx;

		protected override bool HandleTaskExecutionErrors ()
		{
			if (foundError) {
				AssemblyIdentityMap assemblyMap = new AssemblyIdentityMap ();
				assemblyMap.Load (AssemblyIdentityMapFile);
				errorText.Clear ();
				mappingText.Clear ();
				foreach (var line in errorLines) {
					if (!ProcessOutput (line, assemblyMap))
						break;
				}
				if (foundError && errorText.Length > 0) {
					Log.LogError (ToolName, DefaultErrorCode, null, file, line - 1, column + 1, 0, 0, errorText.ToString () + String.Join (Environment.NewLine, mappingText));
				}
				return !Log.HasLoggedErrors;
			}
			return base.HandleTaskExecutionErrors ();
		}

		protected override string GetWorkingDirectory ()
		{
			if (!string.IsNullOrEmpty (WorkingDirectory))
				return WorkingDirectory;
			return base.GetWorkingDirectory ();
		}

		protected override string GenerateFullPathToTool ()
		{
			return Path.Combine (ToolPath, ToolExe);
		}

		protected bool LogFromException (string exception, string error) {
			switch (exception) {
				case "java.lang.OutOfMemoryError":
					Log.LogCodedError ("XA5213", Properties.Resources.XA5213, ToolName, GenerateCommandLineCommands ());
					return false;
				default:
					return true;
			}
		}

		internal virtual bool ProcessOutput (string singleLine, AssemblyIdentityMap assemblyMap)
		{
			var match = CodeErrorRegEx.Match (singleLine);
			var exceptionMatch = ExceptionRegEx.Match (singleLine);
			foreach (Match lp in lpRegex.Matches (singleLine)) {
				var id = lp.Groups["identifier"].Value;
				var asmName = assemblyMap.GetAssemblyNameForImportDirectory (id);
				if (!string.IsNullOrEmpty (asmName)) {
					var path = Path.Combine(IntermediateOutputPath ?? string.Empty, "lp", id);
					mappingText.Add (string.Format (Properties.Resources.XA_Directory_Is_From, path, asmName));
				}
			}

			if (match.Success) {
				if (!string.IsNullOrEmpty (file)) {
					Log.LogError (ToolName, DefaultErrorCode, null, file, line - 1, column + 1, 0, 0, errorText.ToString () + String.Join (Environment.NewLine, mappingText));
					errorText.Clear ();
					mappingText.Clear ();
				}
				file = match.Groups ["file"].Value;
				var error = match.Groups ["error"].Value;
				GetLineNumber (match.Groups ["line"].Value, out line, out column);
				errorText.AppendLine (error);
				return true;
			} else if (exceptionMatch.Success) {
				var error = exceptionMatch.Groups ["error"].Value;
				var exception = exceptionMatch.Groups ["exception"].Value;
				line = 1;
				file = "";
				column = 0;
				errorText.AppendLine (exception);
				errorText.AppendLine (error);
				return LogFromException (exception, error);
			} else if (foundError) {
				if (singleLine.Trim () == "^") {
					column = singleLine.IndexOf ("^", StringComparison.Ordinal);
					return true;
				}

				if (singleLine.StartsWith ("Note:", StringComparison.Ordinal) || singleLine.Trim ().EndsWith ("errors", StringComparison.Ordinal)) {
					// See if we have one last error to print out
					Log.LogError (ToolName, DefaultErrorCode, null, file, line - 1, column + 1, 0, 0, errorText.ToString () + String.Join (Environment.NewLine, mappingText));
					errorText.Clear ();
					mappingText.Clear ();
					foundError = false;
					return true;
				}
				errorText.AppendLine (singleLine);
			}
			return true;
		}

		protected virtual void GetLineNumber (string match, out int line, out int column)
		{
			line = int.Parse (match) + 1;
			column = 0;
		}

		protected virtual IEnumerable<Regex> GetCustomExpressions ()
		{
			return Enumerable.Empty<Regex> ();
		}

		protected void SetFileLineAndColumn (string file, int line = 0, int column = 0)
		{
			this.file = file;
			this.line = line;
			this.column = column;
		}

		protected void AppendTextToErrorText (string text)
		{
			errorText.AppendLine (text);
		}

		protected virtual void CheckForError (string singleLine)
		{
			errorLines.Add (singleLine);
			
			if (foundError) {
				return;
			}
			var match = CodeErrorRegEx.Match (singleLine);
			var exceptionMatch = ExceptionRegEx.Match (singleLine);
			var customMatch = false;
			foreach (var customRegex in GetCustomExpressions ()) {
				customMatch |= customRegex.Match (singleLine).Success;
			}
			foundError = foundError || match.Success || exceptionMatch.Success || customMatch;
		}

		protected override void LogEventsFromTextOutput (string singleLine, MessageImportance messageImportance)
		{
			CheckForError (singleLine);
			base.LogEventsFromTextOutput (singleLine, messageImportance);
		}
	}
}

