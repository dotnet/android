using System;
using System.Text;

using Mono.Cecil.Cil;

namespace Java.Interop.Tools.Diagnostics {

	public class XamarinAndroidException : Exception
	{

		public XamarinAndroidException (int code, string message, params object [] args)
			: this (code, null, message, args)
		{
		}

		// http://blogs.msdn.com/b/msbuild/archive/2006/11/03/msbuild-visual-studio-aware-error-messages-and-message-formats.aspx
		static string GetMessage (int code, string message, object [] args)
		{
			var m = new StringBuilder ();
			m.Append ("error ");
			m.AppendFormat ("XA{0:0000}", code);
			m.Append (": ");
			m.AppendFormat (message, args);
			return m.ToString ();
		}

		public XamarinAndroidException (int code, Exception? innerException, string message, params object [] args)
			: base (GetMessage (code, message, args), innerException)
		{
			Code = code;
			MessageWithoutCode = string.Format (message, args);
		}

		public string MessageWithoutCode { get; private set; }

		public int Code { get; private set; }

		public SequencePoint? Location { get; set; }

		public string? SourceFile {
			get { return Location?.Document.Url; }
		}

		public int SourceLine {
			get { return Location == null ? 0 : Location.StartLine; }
		}
	}
}

