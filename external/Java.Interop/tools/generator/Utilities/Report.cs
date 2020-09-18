using System;
using System.Xml;
using System.Xml.Linq;

namespace MonoDroid.Generation
{
	public class Report
	{
		public static int? Verbosity { get; set; }

		public class LocalizedMessage
		{
			public int Code { get; set; }
			public string Value { get; set; }

			public LocalizedMessage (int code, string value)
			{
				Code = code;
				Value = value;
			}
		}

		public static LocalizedMessage ErrorFailedToRemoveConstants => new LocalizedMessage (0x4000, Java.Interop.Localization.Resources.Generator_BG4000);
		public static LocalizedMessage ErrorFailedToProcessEnumMap => new LocalizedMessage (0x4100, Java.Interop.Localization.Resources.Generator_BG4100);
		public static LocalizedMessage ErrorFailedToProcessMetadata => new LocalizedMessage (0x4200, Java.Interop.Localization.Resources.Generator_BG4200);
		public static LocalizedMessage ErrorRemoveNodeInvalidXPath => new LocalizedMessage (0x4301, Java.Interop.Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorAddNodeInvalidXPath => new LocalizedMessage (0x4302, Java.Interop.Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorChangeNodeInvalidXPath => new LocalizedMessage (0x4303, Java.Interop.Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorAttrInvalidXPath => new LocalizedMessage (0x4304, Java.Interop.Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorMoveNodeInvalidXPath => new LocalizedMessage (0x4305, Java.Interop.Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorRemoveAttrInvalidXPath => new LocalizedMessage (0x4306, Java.Interop.Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorMissingAttrName => new LocalizedMessage (0x4307, Java.Interop.Localization.Resources.Generator_BG4307);
		public static LocalizedMessage ErrorUnexpectedGlobal => new LocalizedMessage (0x4400, Java.Interop.Localization.Resources.Generator_BG4400);
		public static LocalizedMessage ErrorInvalidDIMArgument => new LocalizedMessage (0x4500, Java.Interop.Localization.Resources.Generator_BG4500);

		public static LocalizedMessage WarningUnexpectedChild => new LocalizedMessage (0x8101, Java.Interop.Localization.Resources.Generator_BG8101);
		public static LocalizedMessage WarningUnknownBaseType => new LocalizedMessage (0x8102, Java.Interop.Localization.Resources.Generator_BG8102);
		public static LocalizedMessage WarningInvalidBaseType => new LocalizedMessage (0x8103, Java.Interop.Localization.Resources.Generator_BG8103);
		public static LocalizedMessage WarningAssemblyParseFailure => new LocalizedMessage (0x8200, Java.Interop.Localization.Resources.Generator_BG8200);
		public static LocalizedMessage WarningMissingClassForConstructor => new LocalizedMessage (0x8300, Java.Interop.Localization.Resources.Generator_BG8300);
		public static LocalizedMessage WarningUnexpectedFieldType => new LocalizedMessage (0x8400, Java.Interop.Localization.Resources.Generator_BG8400);
		public static LocalizedMessage WarningFieldNameCollision_Property => new LocalizedMessage (0x8401, Java.Interop.Localization.Resources.Generator_BG8401_Property);
		public static LocalizedMessage WarningFieldNameCollision_Method => new LocalizedMessage (0x8401, Java.Interop.Localization.Resources.Generator_BG8401_Method);
		public static LocalizedMessage WarningFieldNameCollision_NestedType => new LocalizedMessage (0x8401, Java.Interop.Localization.Resources.Generator_BG8401_NestedType);
		public static LocalizedMessage WarningDuplicateField => new LocalizedMessage (0x8402, Java.Interop.Localization.Resources.Generator_BG8402);
		public static LocalizedMessage WarningUnexpectedInterfaceChild => new LocalizedMessage (0x8500, Java.Interop.Localization.Resources.Generator_BG8500);
		public static LocalizedMessage WarningEmptyEventName => new LocalizedMessage (0x8501, Java.Interop.Localization.Resources.Generator_BG8501);
		public static LocalizedMessage WarningInvalidDueToInterfaces => new LocalizedMessage (0x8502, Java.Interop.Localization.Resources.Generator_BG8502);
		public static LocalizedMessage WarningInvalidDueToMethods => new LocalizedMessage (0x8503, Java.Interop.Localization.Resources.Generator_BG8503);
		public static LocalizedMessage WarningInvalidEventName => new LocalizedMessage (0x8504, Java.Interop.Localization.Resources.Generator_BG8504);
		public static LocalizedMessage WarningInvalidEventName2 => new LocalizedMessage (0x8505, Java.Interop.Localization.Resources.Generator_BG8504);
		public static LocalizedMessage WarningInvalidEventPropertyName => new LocalizedMessage (0x8506, Java.Interop.Localization.Resources.Generator_BG8506);
		public static LocalizedMessage WarningInvalidXmlFile => new LocalizedMessage (0x8600, Java.Interop.Localization.Resources.Generator_BG8600);
		public static LocalizedMessage WarningNoPackageElements => new LocalizedMessage (0x8601, Java.Interop.Localization.Resources.Generator_BG8601);
		public static LocalizedMessage WarningUnexpectedRootChildNode => new LocalizedMessage (0x8602, Java.Interop.Localization.Resources.Generator_BG8602);
		public static LocalizedMessage WarningUnexpectedPackageChildNode => new LocalizedMessage (0x8603, Java.Interop.Localization.Resources.Generator_BG8603);
		public static LocalizedMessage WarningNestedTypeAncestorNotFound => new LocalizedMessage (0x8604, Java.Interop.Localization.Resources.Generator_BG8604);
		public static LocalizedMessage WarningUnknownReturnType => new LocalizedMessage (0x8700, Java.Interop.Localization.Resources.Generator_BG8700);
		public static LocalizedMessage WarningInvalidReturnType => new LocalizedMessage (0x8701, Java.Interop.Localization.Resources.Generator_BG8701);
		public static LocalizedMessage WarningUnknownParameterType => new LocalizedMessage (0x8800, Java.Interop.Localization.Resources.Generator_BG8800);
		public static LocalizedMessage WarningInvalidParameterType => new LocalizedMessage (0x8801, Java.Interop.Localization.Resources.Generator_BG8801);
		public static LocalizedMessage WarningRemoveNodeMatchedNoNodes => new LocalizedMessage (0x8A00, Java.Interop.Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningAddNodeMatchedNoNodes => new LocalizedMessage (0x8A01, Java.Interop.Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningChangeNodeTypeMatchedNoNodes => new LocalizedMessage (0x8A03, Java.Interop.Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningAttrMatchedNoNodes => new LocalizedMessage (0x8A04, Java.Interop.Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningMoveNodeMatchedNoNodes => new LocalizedMessage (0x8A05, Java.Interop.Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningRemoveAttrMatchedNoNodes => new LocalizedMessage (0x8A06, Java.Interop.Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningUnknownGenericConstraint => new LocalizedMessage (0x8B00, Java.Interop.Localization.Resources.Generator_BG8B00);
		public static LocalizedMessage WarningBaseInterfaceNotFound => new LocalizedMessage (0x8C00, Java.Interop.Localization.Resources.Generator_BG8C00);
		public static LocalizedMessage WarningBaseInterfaceInvalid => new LocalizedMessage (0x8C01, Java.Interop.Localization.Resources.Generator_BG8C01);

		public static void LogCodedError (LocalizedMessage message, params string [] args)
			=> LogCodedError (message, null, null, -1, -1, args);

		public static void LogCodedError (LocalizedMessage message, Exception innerException, params string [] args)
			=> LogCodedError (message, innerException, null, -1, -1, args);

		public static void LogCodedError (LocalizedMessage message, Exception innerException, XNode node, params string [] args)
		{
			var file = Uri.TryCreate (node.BaseUri, UriKind.Absolute, out var uri) ? uri.LocalPath : null;
			var line_info = (node as IXmlLineInfo)?.HasLineInfo () == true ? node as IXmlLineInfo : null;

			LogCodedError (message, innerException, file, line_info?.LineNumber ?? -1, line_info?.LinePosition ?? -1, args);
		}

		public static void LogCodedError (LocalizedMessage message, Exception innerException, string sourceFile, int line, int column, params string [] args)
		{
			throw new BindingGeneratorException (message.Code, sourceFile, line, column, string.Format (message.Value, args), innerException);
		}

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, params string [] args)
			=> LogCodedWarning (verbosity, message, null, null, -1, -1, args);

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, ISourceLineInfo sourceInfo, params string [] args)
			=> LogCodedWarning (verbosity, message, null, sourceInfo.SourceFile, sourceInfo.LineNumber, sourceInfo.LinePosition, args);

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, Exception innerException, params string [] args)
			=> LogCodedWarning (verbosity, message, innerException, null, -1, -1, args);

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, Exception innerException, XNode node, params string [] args)
		{
			var file = Uri.TryCreate (node.BaseUri, UriKind.Absolute, out var uri) ? uri.LocalPath : null;
			var line_info = (node as IXmlLineInfo)?.HasLineInfo () == true ? node as IXmlLineInfo : null;

			LogCodedWarning (verbosity, message, innerException, file, line_info?.LineNumber ?? -1, line_info?.LinePosition ?? -1, args);
		}

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, Exception innerException, string sourceFile, int line, int column, params string [] args)
		{
			if (verbosity > (Verbosity ?? 0))
				return;

			var supp = innerException != null ? "  For details, see verbose output." : null;
			Console.Error.WriteLine (Format (false, message.Code, sourceFile, line, column, message.Value, args) + supp);

			if (innerException != null)
				Console.Error.WriteLine (innerException);
		}
		
		public static void Verbose (int verbosity, string format, params object[] args)
		{
			if (verbosity > (Verbosity ?? 0))
				return;
			Console.Error.WriteLine (format, args);
		}

		public static string FormatCodedMessage (bool error, LocalizedMessage message, params object [] args)
			=> Format (error, message.Code, null, -1, -1, message.Value, args);
		
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

