using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class Generator
	{
		public static bool CreateJavaSources (TaskLoggingHelper log, IEnumerable<TypeDefinition> javaTypes, string outputPath, 
			string applicationJavaClass, string androidSdkPlatform, bool useSharedRuntime, bool generateOnCreateOverrides, bool hasExportReference)
		{
			string monoInit = GetMonoInitSource (androidSdkPlatform, useSharedRuntime);

			bool ok = true;
			using (var memoryStream = new MemoryStream ())
			using (var writer = new StreamWriter (memoryStream)) {
				foreach (var t in javaTypes) {
					//Reset for reuse
					memoryStream.SetLength (0);

					try {
						var jti = new JavaCallableWrapperGenerator (t, log.LogWarning) {
							GenerateOnCreateOverrides = generateOnCreateOverrides,
							ApplicationJavaClass = applicationJavaClass,
							MonoRuntimeInitialization = monoInit,
						};

						jti.Generate (writer);
						writer.Flush ();

						var path = jti.GetDestinationPath (outputPath);
						MonoAndroidHelper.CopyIfStreamChanged (memoryStream, path);
						if (jti.HasExport && !hasExportReference)
							Diagnostic.Error (4210, Properties.Resources.XA4210);
					} catch (XamarinAndroidException xae) {
						ok = false;
						log.LogError (
								subcategory: "",
								errorCode: "XA" + xae.Code,
								helpKeyword: string.Empty,
								file: xae.SourceFile,
								lineNumber: xae.SourceLine,
								columnNumber: 0,
								endLineNumber: 0,
								endColumnNumber: 0,
								message: xae.MessageWithoutCode,
								messageArgs: new object [0]
						);
					} catch (DirectoryNotFoundException ex) {
						ok = false;
						if (OS.IsWindows) {
							Diagnostic.Error (5301, Properties.Resources.XA5301, t.FullName, ex);
						} else {
							Diagnostic.Error (4209, Properties.Resources.XA4209, t.FullName, ex);
						}
					} catch (Exception ex) {
						ok = false;
						Diagnostic.Error (4209, Properties.Resources.XA4209, t.FullName, ex);
					}
				}
			}
			return ok;
		}

		static string GetMonoInitSource (string androidSdkPlatform, bool useSharedRuntime)
		{
			// Lookup the mono init section from MonoRuntimeProvider:
			// Mono Runtime Initialization {{{
			// }}}
			var builder = new StringBuilder ();
			var runtime = useSharedRuntime ? "Shared" : "Bundled";
			var api = "";
			if (int.TryParse (androidSdkPlatform, out int apiLevel) && apiLevel < 21) {
				api = ".20";
			}
			var assembly = Assembly.GetExecutingAssembly ();
			using (var s = assembly.GetManifestResourceStream ($"MonoRuntimeProvider.{runtime}{api}.java"))
			using (var reader = new StreamReader (s)) {
				bool copy = false;
				string line;
				while ((line = reader.ReadLine ()) != null) {
					if (string.CompareOrdinal ("\t\t// Mono Runtime Initialization {{{", line) == 0)
						copy = true;
					if (copy)
						builder.AppendLine (line);
					if (string.CompareOrdinal ("\t\t// }}}", line) == 0)
						break;
				}
			}
			return builder.ToString ();
		}
	}
}
