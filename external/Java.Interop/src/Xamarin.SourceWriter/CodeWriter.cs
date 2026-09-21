using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Xamarin.SourceWriter
{
	public class CodeWriter : IDisposable
	{
		TextWriter stream;
		bool owns_stream;
		int indent;
		bool need_indent = true;
		string base_indent;
		readonly Dictionary<string, int> suppression_depth = new Dictionary<string, int> ();

		public CodeWriter (string filename)
		{
			stream = File.CreateText (filename);
			owns_stream = true;
		}

		public CodeWriter (TextWriter streamWriter, string baseIndent = "")
		{
			stream = streamWriter;
			base_indent = baseIndent;
		}

		public void Write (string value)
		{
			WriteIndent ();
			stream.Write (value);
		}
		// `#pragma warning restore` ends a suppression however many `disable` directives
		// preceded it, so a nested `disable`/`restore` pair for the same code would stop
		// suppressing the enclosing scope early. Track how deep each code is nested and let
		// only the outermost pair be written.
		public bool BeginWarningSuppression (string code)
		{
			if (code == null)
				throw new ArgumentNullException (nameof (code));

			suppression_depth.TryGetValue (code, out var depth);
			suppression_depth [code] = depth + 1;

			return depth == 0;
		}

		public bool EndWarningSuppression (string code)
		{
			if (code == null)
				throw new ArgumentNullException (nameof (code));

			if (!suppression_depth.TryGetValue (code, out var depth) || depth == 0)
				throw new InvalidOperationException ($"No warning suppression is open for '{code}'.");

			suppression_depth [code] = depth - 1;

			return depth == 1;
		}

		public bool IsWarningSuppressed (string code) =>
			suppression_depth.TryGetValue (code, out var depth) && depth > 0;


		public void WriteLine ()
		{
			stream.WriteLine ();
			need_indent = true;
		}

		public void WriteLine (string value)
		{
			if (value?.Length > 0)
				WriteIndent ();

			stream.WriteLine (value);
			need_indent = true;
		}

		public void WriteLine (string format, params object[] args)
		{
			if (format?.Length > 0)
				WriteIndent ();

			stream.WriteLine (format, args);
			need_indent = true;
		}

		public void WriteLineNoIndent (string value)
		{
			stream.WriteLine (value);
			need_indent = true;
		}

		public void Indent (int count = 1) => indent += count;
		public void Unindent (int count = 1) => indent -= count;

		private void WriteIndent ()
		{
			if (!need_indent)
				return;

			stream.Write (base_indent + new string ('\t', indent));

			need_indent = false;
		}

		public void Dispose ()
		{
			if (owns_stream)
				stream?.Dispose ();
		}
	}
}
