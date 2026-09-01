using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public static class DexUtils
	{
		/*
		 Example dexdump output:
		 
			Class #12            -
			  Class descriptor  : 'Landroid/runtime/UncaughtExceptionHandler;'
			  Access flags      : 0x0001 (PUBLIC)
			  Superclass        : 'Ljava/lang/Object;'
			  Interfaces        -
				#0              : 'Ljava/lang/Thread$UncaughtExceptionHandler;'
				#1              : 'Lmono/android/IGCUserPeer;'
			  Static fields     -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '__md_methods'
				  type          : 'Ljava/lang/String;'
				  access        : 0x0019 (PUBLIC STATIC FINAL)
			  Instance fields   -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : 'refList'
				  type          : 'Ljava/util/ArrayList;'
				  access        : 0x0002 (PRIVATE)
			  Direct methods    -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '<clinit>'
				  type          : '()V'
				  access        : 0x10008 (STATIC CONSTRUCTOR)
				  code          -
				  registers     : 3
				  ins           : 0
				  outs          : 3
				  insns size    : 10 16-bit code units
				  catches       : (none)
				  positions     : 
					0x0002 line=16
				  locals        : 
				#1              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '<init>'
				  type          : '()V'
				  access        : 0x10001 (PUBLIC CONSTRUCTOR)
				  code          -
				  registers     : 4
				  ins           : 1
				  outs          : 4
				  insns size    : 22 16-bit code units
				  catches       : (none)
				  positions     : 
					0x0000 line=22
					0x0003 line=23
					0x0010 line=24
				  locals        : 
					0x0000 - 0x0016 reg=3 this Landroid/runtime/UncaughtExceptionHandler; 
		 */

		/// <summary>
		/// Runs the dexdump command to see if a class exists in a dex file
		/// </summary>
		/// <param name="className">A Java class name of the form 'Landroid/app/ActivityTracker;'</param>
		public static bool ContainsClass (string className, string dexFile, string androidSdkDirectory)
		{
			bool containsClass = false;
			DataReceivedEventHandler handler = (s, e) => {
				if (e.Data != null && e.Data.Contains ("Class descriptor") && e.Data.Contains (className))
					containsClass = true;
			};
			DexDump (handler, dexFile, androidSdkDirectory);			
			return containsClass;
		}

		/// <summary>
		/// Runs the dexdump command to see if a class exists in a dex file *and* has a public constructor
		/// </summary>
		/// <param name="className">A Java class name of the form 'Landroid/app/ActivityTracker;'</param>
		/// <param name="method">A Java method name of the form 'foo'</param>
		/// <param name="type">A Java method signature of the form '()V'</param>
		public static bool ContainsClassWithMethod (string className, string method, string type, string dexFile, string androidSdkDirectory)
		{
			bool inClass = false;
			bool hasName = false;
			bool hasType = false;
			DataReceivedEventHandler handler = (s, e) => {
				if (e.Data != null) {
					if (e.Data.Contains ("Class descriptor")) {
						inClass = e.Data.Contains (className);
						hasName = false;
					} else if (inClass && e.Data.Contains ("name") && e.Data.Contains (method)) {
						hasName = true;
					} else if (hasName && e.Data.Contains ("type") && e.Data.Contains (type)) {
						hasType = true;
					}
				}
			};
			DexDump (handler, dexFile, androidSdkDirectory);
			return hasType;
		}

		/// <summary>
		/// Runs the dexdump command to see if a method has a runtime-visible annotation
		/// </summary>
		/// <param name="className">A Java class name of the form 'Landroid/app/ActivityTracker;'</param>
		/// <param name="method">A Java method name of the form 'foo'. If overloaded, any overload may match because dexdump annotation headers omit signatures.</param>
		/// <param name="annotationType">A Java annotation type of the form 'Landroid/webkit/JavascriptInterface;'</param>
		public static bool ContainsRuntimeMethodAnnotation (string className, string method, string annotationType, string dexFile, string androidSdkDirectory)
		{
			var parser = new RuntimeMethodAnnotationParser (className, method, annotationType);
			DataReceivedEventHandler handler = (s, e) => parser.ProcessLine (e.Data);
			DexDump (handler, dexFile, androidSdkDirectory, showAnnotations: true);
			return parser.ContainsAnnotation;
		}

		/// <summary>
		/// Checks supplied dexdump -a output for a runtime-visible method annotation
		/// </summary>
		public static bool ContainsRuntimeMethodAnnotation (IEnumerable<string> dexDumpOutput, string className, string method, string annotationType)
		{
			ArgumentNullException.ThrowIfNull (dexDumpOutput);
			var parser = new RuntimeMethodAnnotationParser (className, method, annotationType);
			foreach (string line in dexDumpOutput) {
				parser.ProcessLine (line);
			}
			return parser.ContainsAnnotation;
		}

		sealed class RuntimeMethodAnnotationParser
		{
			const string classDescriptorPrefix = "Class descriptor  : '";
			const string classPrefix = "Class #";
			const string classAnnotationsSuffix = " annotations:";
			const string methodAnnotationPrefix = "Annotations on method ";
			readonly string classDescriptor;
			readonly string methodAnnotationSuffix;
			readonly string runtimeAnnotation;
			readonly HashSet<int> annotatedClasses = new HashSet<int> ();
			readonly HashSet<int> targetClasses = new HashSet<int> ();
			int? annotationClassIndex;
			int? descriptorClassIndex;
			bool inMethodAnnotations;

			public bool ContainsAnnotation { get; private set; }

			public RuntimeMethodAnnotationParser (string className, string method, string annotationType)
			{
				classDescriptor = $"{classDescriptorPrefix}{className}'";
				methodAnnotationSuffix = $"'{method}'";
				runtimeAnnotation = $"VISIBILITY_RUNTIME {annotationType}";
			}

			public void ProcessLine (string? data)
			{
				if (data == null) {
					return;
				}
				string line = data.Trim ();
				if (line.StartsWith (classPrefix, StringComparison.Ordinal)) {
					if (!TryGetClassIndex (line, out int classIndex)) {
						ResetPendingClass ();
						return;
					}
					if (line.EndsWith (classAnnotationsSuffix, StringComparison.Ordinal)) {
						ResetPendingClass ();
						annotationClassIndex = classIndex;
					} else if (line.EndsWith ("-", StringComparison.Ordinal)) {
						descriptorClassIndex = classIndex;
					} else {
						ResetPendingClass ();
					}
				} else if (line.StartsWith ("Annotations on ", StringComparison.Ordinal)) {
					inMethodAnnotations = annotationClassIndex.HasValue &&
						line.StartsWith (methodAnnotationPrefix, StringComparison.Ordinal) &&
						line.EndsWith (methodAnnotationSuffix, StringComparison.Ordinal);
				} else if (annotationClassIndex is int annotatedClassIndex &&
						inMethodAnnotations &&
						line.Equals (runtimeAnnotation, StringComparison.Ordinal)) {
					annotatedClasses.Add (annotatedClassIndex);
					ContainsAnnotation |= targetClasses.Contains (annotatedClassIndex);
				} else if (line.StartsWith (classDescriptorPrefix, StringComparison.Ordinal)) {
					if (descriptorClassIndex is int targetClassIndex &&
							line.Equals (classDescriptor, StringComparison.Ordinal)) {
						targetClasses.Add (targetClassIndex);
						ContainsAnnotation |= annotatedClasses.Contains (targetClassIndex);
					}
					ResetPendingClass ();
				}
			}

			static bool TryGetClassIndex (string line, out int classIndex)
			{
				int indexEnd = line.IndexOf (' ', classPrefix.Length);
				classIndex = 0;
				return indexEnd > classPrefix.Length &&
					int.TryParse (line.Substring (classPrefix.Length, indexEnd - classPrefix.Length),
						NumberStyles.None, CultureInfo.InvariantCulture, out classIndex);
			}

			void ResetPendingClass ()
			{
				annotationClassIndex = null;
				descriptorClassIndex = null;
				inMethodAnnotations = false;
			}
		}

		static void DexDump (DataReceivedEventHandler handler, string dexFile, string androidSdkDirectory, bool showAnnotations = false)
		{
			var androidSdk = new AndroidSdkInfo ((l, m) => {
				Console.WriteLine ($"{l}: {m}");
				if (l == TraceLevel.Error) {
					throw new Exception (m);
				}
			}, androidSdkDirectory, javaSdkPath: AndroidSdkResolver.GetJavaSdkPath ());
			var buildToolsPath = androidSdk.GetBuildToolsPaths ().FirstOrDefault ();
			if (string.IsNullOrEmpty (buildToolsPath)) {
				throw new Exception ($"Unable to find build-tools in `{androidSdkDirectory}`!");
			}

			var psi = new ProcessStartInfo {
				FileName = Path.Combine (buildToolsPath, "dexdump"),
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				WorkingDirectory = Path.GetDirectoryName (dexFile),
			};
			var errors = new StringBuilder ();
			if (showAnnotations) {
				psi.ArgumentList.Add ("-a");
			}
			psi.ArgumentList.Add (Path.GetFileName (dexFile));
			using (var p = new Process { StartInfo = psi }) {
				p.ErrorDataReceived += (s, e) => {
					if (e.Data != null) {
						errors.AppendLine (e.Data);
					}
				};
				p.OutputDataReceived += handler;

				p.Start ();
				p.BeginErrorReadLine ();
				p.BeginOutputReadLine ();
				p.WaitForExit ();

				if (p.ExitCode != 0) {
					throw new Exception ($"'{psi.FileName} {string.Join (" ", psi.ArgumentList)}' exited with code: {p.ExitCode}{Environment.NewLine}{errors.ToString ().TrimEnd ()}");
				}
			}
		}
	}
}
