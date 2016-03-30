//
// repl.cs: Support for using the compiler in interactive mode (read-eval-print loop)
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Dual licensed under the terms of the MIT X11 or GNU GPL
//
// Copyright 2001, 2002, 2003 Ximian, Inc (http://www.ximian.com)
// Copyright 2004, 2005, 2006, 2007, 2008 Novell, Inc
// Copyright 2011-2013 Xamarin Inc
//
//
// TODO:
//   Do not print results in Evaluate, do that elsewhere in preparation for Eval refactoring.
//   Driver.PartialReset should not reset the coretypes, nor the optional types, to avoid
//      computing that on every call.
//
using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

using Mono.CSharp;

namespace Mono {

	public class InteractiveBaseShell : InteractiveBase {
		static bool tab_at_start_completes;

		static InteractiveBaseShell ()
		{
			tab_at_start_completes = false;
		}

		internal static Mono.Terminal.LineEditor Editor;

		public static bool TabAtStartCompletes {
			get {
				return tab_at_start_completes;
			}

			set {
				tab_at_start_completes = value;
				if (Editor != null)
					Editor.TabAtStartCompletes = value;
			}
		}

		public static new string help {
			get {
				return InteractiveBase.help +
					"  TabAtStartCompletes      - Whether tab will complete even on empty lines\n";
			}
		}
	}

	public class CSharpShell {
		static bool isatty = true, is_unix = false;
		protected string [] startup_files;

		Mono.Terminal.LineEditor editor;
		bool dumb;
		readonly Evaluator evaluator;

		public CSharpShell (Evaluator evaluator)
		{
			this.evaluator = evaluator;
		}

		protected virtual void ConsoleInterrupt (object sender, ConsoleCancelEventArgs a)
		{
			// Do not about our program
			a.Cancel = true;

			evaluator.Interrupt ();
		}

		void SetupConsole ()
		{
			if (is_unix){
				string term = Environment.GetEnvironmentVariable ("TERM");
				dumb = term == "dumb" || term == null || isatty == false;
			} else
				dumb = false;

			editor = new Mono.Terminal.LineEditor ("csharp", 300);
			InteractiveBaseShell.Editor = editor;

			editor.AutoCompleteEvent += delegate (string s, int pos){
				string prefix = null;

				string complete = s.Substring (0, pos);

				string [] completions = evaluator.GetCompletions (complete, out prefix);

				return new Mono.Terminal.LineEditor.Completion (prefix, completions);
			};

			#if false
			//
			// This is a sample of how completions sould be implemented.
			//
			editor.AutoCompleteEvent += delegate (string s, int pos){

			// Single match: "Substring": Sub-string
			if (s.EndsWith ("Sub")){
			return new string [] { "string" };
			}

			// Multiple matches: "ToString" and "ToLower"
			if (s.EndsWith ("T")){
			return new string [] { "ToString", "ToLower" };
			}
			return null;
			};
			#endif

			Console.CancelKeyPress += ConsoleInterrupt;
		}

		string GetLine (bool primary)
		{
			string prompt = primary ? InteractiveBase.Prompt : InteractiveBase.ContinuationPrompt;

			if (dumb){
				if (isatty)
					Console.Write (prompt);

				return Console.ReadLine ();
			} else {
				return editor.Edit (prompt, "");
			}
		}

		delegate string ReadLiner (bool primary);

		void InitializeUsing ()
		{
			Evaluate ("using System; using System.Linq; using System.Collections.Generic; using System.Collections;");
		}

		void InitTerminal (bool show_banner)
		{
			int p = (int) Environment.OSVersion.Platform;
			is_unix = (p == 4) || (p == 128);

			isatty = !Console.IsInputRedirected && !Console.IsOutputRedirected;

			// Work around, since Console is not accounting for
			// cursor position when writing to Stderr.  It also
			// has the undesirable side effect of making
			// errors plain, with no coloring.
			//			Report.Stderr = Console.Out;
			SetupConsole ();

			if (isatty && show_banner)
				Console.WriteLine ("Mono C# Shell, type \"help;\" for help\n\nEnter statements below.");

		}

		void ExecuteSources (IEnumerable<string> sources, bool ignore_errors)
		{
			foreach (string file in sources){
				try {
					try {
						bool first = true;

						using (System.IO.StreamReader r = System.IO.File.OpenText (file)){
							ReadEvalPrintLoopWith (p => {
								var line = r.ReadLine ();
								if (first){
									if (line.StartsWith ("#!"))
										line = r.ReadLine ();
									first = false;
								}
								return line;
							});
						}
					} catch (FileNotFoundException){
						Console.Error.WriteLine ("cs2001: Source file `{0}' not found", file);
						return;
					}
				} catch {
					if (!ignore_errors)
						throw;
				}
			}
		}

		protected virtual void LoadStartupFiles ()
		{
			string dir = Path.Combine (
				Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
				"csharp");
			if (!Directory.Exists (dir))
				return;

			List<string> sources = new List<string> ();
			List<string> libraries = new List<string> ();

			foreach (string file in System.IO.Directory.GetFiles (dir)){
				string l = file.ToLower ();

				if (l.EndsWith (".cs"))
					sources.Add (file);
				else if (l.EndsWith (".dll"))
					libraries.Add (file);
			}

			foreach (string file in libraries)
				evaluator.LoadAssembly (file);

			ExecuteSources (sources, true);
		}

		void ReadEvalPrintLoopWith (ReadLiner readline)
		{
			string expr = null;
			while (!InteractiveBase.QuitRequested){
				string input = readline (expr == null);
				if (input == null)
					return;

				if (input == "")
					continue;

				expr = expr == null ? input : expr + "\n" + input;

				expr = Evaluate (expr);
			}
		}

		public int ReadEvalPrintLoop ()
		{
			if (startup_files != null && startup_files.Length == 0)
				InitTerminal (startup_files.Length == 0);

			InitializeUsing ();

			LoadStartupFiles ();

			if (startup_files != null && startup_files.Length != 0) {
				ExecuteSources (startup_files, false);
			} else {
				ReadEvalPrintLoopWith (GetLine);

				editor.SaveHistory ();
			}

			Console.CancelKeyPress -= ConsoleInterrupt;

			return 0;
		}

		protected virtual string Evaluate (string input)
		{
			bool result_set;
			object result;

			try {
				input = evaluator.Evaluate (input, out result, out result_set);

				if (result_set){
					PrettyPrint (Console.Out, result);
					Console.WriteLine ();
				}
			} catch (Exception e){
				Console.WriteLine (e);
				return null;
			}

			return input;
		}

		static void p (TextWriter output, string s)
		{
			output.Write (s);
		}

		static string EscapeString (string s)
		{
			return s.Replace ("\"", "\\\"");
		}

		static void EscapeChar (TextWriter output, char c)
		{
			if (c == '\''){
				output.Write ("'\\''");
				return;
			}
			if (c > 32){
				output.Write ("'{0}'", c);
				return;
			}
			switch (c){
			case '\a':
				output.Write ("'\\a'");
				break;

			case '\b':
				output.Write ("'\\b'");
				break;

			case '\n':
				output.Write ("'\\n'");
				break;

			case '\v':
				output.Write ("'\\v'");
				break;

			case '\r':
				output.Write ("'\\r'");
				break;

			case '\f':
				output.Write ("'\\f'");
				break;

			case '\t':
				output.Write ("'\\t");
				break;

			default:
				output.Write ("'\\x{0:x}", (int) c);
				break;
			}
		}

		// Some types (System.Json.JsonPrimitive) implement
		// IEnumerator and yet, throw an exception when we
		// try to use them, helper function to check for that
		// condition
		static internal bool WorksAsEnumerable (object obj)
		{
			IEnumerable enumerable = obj as IEnumerable;
			if (enumerable != null){
				try {
					enumerable.GetEnumerator ();
					return true;
				} catch {
					// nothing, we return false below
				}
			}
			return false;
		}

		internal static void PrettyPrint (TextWriter output, object result)
		{
			if (result == null){
				p (output, "null");
				return;
			}

			if (result is Array){
				Array a = (Array) result;

				p (output, "{ ");
				int top = a.GetUpperBound (0);
				for (int i = a.GetLowerBound (0); i <= top; i++){
					PrettyPrint (output, a.GetValue (i));
					if (i != top)
						p (output, ", ");
				}
				p (output, " }");
			} else if (result is bool){
				if ((bool) result)
					p (output, "true");
				else
					p (output, "false");
			} else if (result is string){
				p (output, String.Format ("\"{0}\"", EscapeString ((string)result)));
			} else if (result is IDictionary){
				IDictionary dict = (IDictionary) result;
				int top = dict.Count, count = 0;

				p (output, "{");
				foreach (DictionaryEntry entry in dict){
					count++;
					p (output, "{ ");
					PrettyPrint (output, entry.Key);
					p (output, ", ");
					PrettyPrint (output, entry.Value);
					if (count != top)
						p (output, " }, ");
					else
						p (output, " }");
				}
				p (output, "}");
			} else if (WorksAsEnumerable (result)) {
				int i = 0;
				p (output, "{ ");
				foreach (object item in (IEnumerable) result) {
					if (i++ != 0)
						p (output, ", ");

					PrettyPrint (output, item);
				}
				p (output, " }");
			} else if (result is char) {
				EscapeChar (output, (char) result);
			} else {
				p (output, result.ToString ());
			}
		}

		public virtual int Run (string [] startup_files)
		{
			this.startup_files = startup_files;
			return ReadEvalPrintLoop ();
		}

	}
}

