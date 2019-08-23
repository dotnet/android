using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace MonoDroid.Generation
{
	public class Report
	{
		public const int ErrorEnumMap = 0x4000;
		public const int ErrorEnumMapping = 0x4100;
		public const int ErrorParser = 0x4200;
		public const int ErrorApiFixup = 0x4300;
		public const int ErrorCodeGenerator = 0x4400;
		public const int ErrorInvalidArgument = 0x4500;
		public const int WarningClassGen = 0x8100;
		public const int WarningCodeGenerator = 0x8200;
		public const int WarningCtor = 0x8300;
		public const int WarningField = 0x8400;
		public const int WarningFieldNameCollision = 0x8401;
		public const int WarningDuplicateField = 0x8402;
		public const int WarningInterfaceGen = 0x8500;
		public const int WarningParser = 0x8600;
		public const int WarningReturnValue = 0x8700;
		public const int WarningParameter = 0x8800;
		public const int WarningGenBaseSupport = 0x8900;
		public const int WarningApiFixup = 0x8A00;
		public const int WarningGenericParameterDefinition = 0x8B00;
		public const int WarningGenBase = 0x8C00;
		public const int WarningMethodBase = 0x8D00;
		public const int WarningAnnotationsProvider = 0x8E00;

		public static int? Verbosity { get; set; }

		public static void Error (int errorCode, string format, params object[] args)
		{
			Error (errorCode, null, null, -1, -1, format, args);
		}

		public static void Error (int errorCode, string sourceFile, int line, int column, string format, params object [] args)
		{
			Error (errorCode, null, sourceFile, line, column, format, args);
		}

		public static void Error (int errorCode, Exception innerException, string format, params object [] args)
		{
			Error (errorCode, innerException, null, -1, -1, format, args);
		}

		public static void Error (int errorCode, Exception innerException, XNode node, string format, params object [] args)
		{
			Uri uri;
			string file = Uri.TryCreate (node.BaseUri, UriKind.Absolute, out uri) ? uri.LocalPath : null;
			IXmlLineInfo li = node as IXmlLineInfo;
			li = li != null && li.HasLineInfo () ? li : null;
			Error (errorCode, innerException, file, li != null ? li.LineNumber : -1, li != null ? li.LinePosition : -1, format, args);
		}

		public static void Error (int errorCode, Exception innerException, string sourceFile, int line, int column, string format, params object[] args)
		{
			throw new BindingGeneratorException (errorCode, sourceFile, line, column, string.Format (format, args), innerException);
		}
		
		public static void Warning (int verbosity, int errorCode, string format, params object[] args)
		{
			Warning (verbosity, errorCode, null, format, args);
		}

		public static void Warning (int verbosity, int errorCode, Exception innerException, XNode node, string format, params object [] args)
		{
			Uri uri;
			string file = Uri.TryCreate (node.BaseUri, UriKind.Absolute, out uri) ? uri.LocalPath : null;
			IXmlLineInfo li = node as IXmlLineInfo;
			li = li != null && li.HasLineInfo () ? li : null;
			Warning (verbosity, errorCode, innerException, file, li != null ? li.LineNumber : -1, li != null ? li.LinePosition : -1, format, args);
		}

		public static void Warning (int verbosity, int errorCode, string sourceFile, int line, int column, string format, params object[] args)
		{
			Warning (verbosity, errorCode, null, sourceFile, line, column, format, args);
		}

		public static void Warning (int verbosity, int errorCode, Exception innerException, string format, params object [] args)
		{
			Warning (verbosity, errorCode, innerException, null, -1, -1, format, args);
		}
		
		public static void Warning (int verbosity, int errorCode, Exception innerException, string sourceFile, int line, int column, string format, params object[] args)
		{
			if (verbosity > (Verbosity ?? 0))
				return;
			string supp = innerException != null ? "  For details, see verbose output." : null;
			Console.Error.WriteLine (Format (false, errorCode, sourceFile, line, column, format, args) + supp);
			if (innerException != null)
				Console.Error.WriteLine (innerException);
		}
		
		public static void Verbose (int verbosity, string format, params object[] args)
		{
			if (verbosity > (Verbosity ?? 0))
				return;
			Console.Error.WriteLine (format, args);
		}

		public static string Format (bool error, int errorCode, string format, params object [] args)
		{
			return Format (error, errorCode, null, -1, -1, format, args); 
		}
		
		public static string Format (bool error, int errorCode, string sourceFile, int line, int column, string format, params object[] args)
		{
			var origin = sourceFile != null ? sourceFile + (line > 0 ? column > 0 ? $"({line}, {column})" : $"({line})" : null) + ' ' : null;
			return string.Format ("{0}{1} BG{2:X04}: ", origin, error ? "error" : "warning", errorCode) +
				string.Format (format, args);
		}
	}
	
	public class BindingGeneratorException : Exception
	{
		public BindingGeneratorException (int errorCode, string message)
			: this (errorCode, message, null)
		{
		}
		public BindingGeneratorException (int errorCode, string message, Exception innerException)
			: this (errorCode, null, -1, -1, message, innerException)
		{
		}
		public BindingGeneratorException (int errorCode, string sourceFile, int line, int column, string message, Exception innerException)
			: base (Report.Format (true, errorCode, sourceFile, line, column, message), innerException)
		{
		}
	}
}

