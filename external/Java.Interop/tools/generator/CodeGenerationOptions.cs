using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CSharp;
using Xamarin.Android.Binder;
using Xamarin.AndroidTools.AnnotationSupport;

namespace MonoDroid.Generation
{
    public class CodeGenerationOptions
	{
		Stack<GenBase> context_types = new Stack<GenBase> ();
		public Stack<GenBase> ContextTypes {
			get { return context_types; }
		}
		public List<Method> ContextGeneratedMethods { get; set; } = new List<Method> ();
		public GenBase ContextType {
			get { return context_types.Any () ? context_types.Peek () : null; }
		}
		public Field ContextField { get; set; }
		string ContextFieldString { 
			get { return ContextField != null ? "in field " + ContextField.Name + " " : null; }
		}
		public MethodBase ContextMethod { get; set; }
		string ContextMethodString { 
			get { return ContextMethod != null ? "in method " + ContextMethod.Name + " " : null; }
		}
		string ContextTypeString {
			get { return ContextType != null ? "in managed type " + ContextType.FullName : null; }
		}
		public string ContextString {
			get { return ContextFieldString + ContextMethodString + ContextTypeString; }
		}

		CodeGenerationTarget    codeGenerationTarget;
		public      CodeGenerationTarget    CodeGenerationTarget    {
			get { return codeGenerationTarget; }
			set {
				switch (value) {
				case CodeGenerationTarget.XamarinAndroid:
					CodeGenerator   = new XamarinAndroidCodeGenerator ();
					break;
				case CodeGenerationTarget.XAJavaInterop1:
					CodeGenerator   = new XAJavaInteropCodeGenerator ();
					break;
				case CodeGenerationTarget.JavaInterop1:
					CodeGenerator   = new JavaInteropCodeGenerator ();
					break;
				default:
					throw new NotSupportedException ("Don't know what to do for target '" + value + "'.");
				}
				codeGenerationTarget    = value;
			}
		}

		internal    CodeGenerator           CodeGenerator           { get; private set; } = new XamarinAndroidCodeGenerator ();

		public      SymbolTable             SymbolTable             { get; } = new SymbolTable ();

		public bool UseGlobal { get; set; }
		public bool IgnoreNonPublicType { get; set; }
		public string AssemblyName { get; set; }
		public bool UseShortFileNames { get; set; }
		public IList<GenBase> Gens {get;set;}
		public int ProductVersion { get; set; }

		bool? buildingCoreAssembly;
		public bool BuildingCoreAssembly {
			get {
				return buildingCoreAssembly ?? (buildingCoreAssembly = (SymbolTable.Lookup ("java.lang.Object") is XmlClassGen)).Value;
			}
		}

		public string GetOutputName (string s)
		{
			if (s == "System.Void")
				return "void";
			if (s.StartsWith ("params "))
				return "params " + GetOutputName (s.Substring ("params ".Length));
			if (s.StartsWith ("global::"))
				Report.Error (Report.ErrorCodeGenerator + 0, null,  "Unexpected \"global::\" specification. This error happens if it is specified in the Metadata API fixup for example.");
			if (!UseGlobal)
				return s;
			int idx = s.IndexOf ('<');
			if (idx < 0) {
				if (s.IndexOf ('.') < 0)
					return s; // hack, to prevent things like global::int
				return "global::" + s;
			}
			int idx2 = s.LastIndexOf ('>');
			string sub = s.Substring (idx + 1, idx2 - idx - 1);
			var typeParams = new List<string> ();
			while (true) {
				int idx3 = sub.IndexOf ('<');
				int idx4 = sub.IndexOf (',');
				if (idx4 < 0) {
					typeParams.Add (GetOutputName (sub));
					break;
				} else if (idx3 < 0 || idx4 < idx3) { // more than one type params.
					typeParams.Add (GetOutputName (sub.Substring (0, idx4)));
					if (idx4 + 1 == sub.Length)
						break;
					sub = sub.Substring (idx4 + 1).Trim ();
				} else {
					typeParams.Add (GetOutputName (sub));
					break;
				}
			}
			return GetOutputName (s.Substring (0, idx)) + '<' + String.Join (", ", typeParams.ToArray ()) + '>';
		}

		CSharpCodeProvider code_provider = new CSharpCodeProvider ();

		public string GetSafeIdentifier (string name)
		{
			// NOTE: "partial" differs in behavior on macOS vs. Windows, Windows reports "partial" as a valid identifier
			//	This check ensures the same output on both platforms
			if (name == "partial")
				return name;

			// In the ideal world, it should not be applied twice.
			// Sadly that is not true in reality, so we need to exclude non-symbols
			// when replacing the argument name with a valid identifier.
			// (ReturnValue.ToNative() takes an argument which could be either an expression or mere symbol.)
			if (name.LastOrDefault () != ')' && !name.Contains ('.'))
				name = code_provider.IsValidIdentifier (name) ? name : name + '_';
			return name.Replace ('$', '_');
		}

		Dictionary<string,string> short_file_names = new Dictionary<string, string> ();

		public string GetFileName (string fullName)
		{
			if (!UseShortFileNames)
				return fullName;
			string s;
			if (short_file_names.TryGetValue (fullName, out s))
				return s;
			s = short_file_names.Count.ToString ();
			short_file_names [fullName] = s;
			return s;
		}
	}

}

