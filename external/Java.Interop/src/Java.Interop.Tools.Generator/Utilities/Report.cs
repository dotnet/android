using System;
using System.Xml;
using System.Xml.Linq;

namespace Java.Interop.Tools.Generator
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

		public static LocalizedMessage ErrorFailedToRemoveConstants => new LocalizedMessage (0x4000, Localization.Resources.Generator_BG4000);
		public static LocalizedMessage ErrorFailedToProcessEnumMap => new LocalizedMessage (0x4100, Localization.Resources.Generator_BG4100);
		public static LocalizedMessage ErrorFailedToProcessMetadata => new LocalizedMessage (0x4200, Localization.Resources.Generator_BG4200);
		public static LocalizedMessage ErrorRemoveNodeInvalidXPath => new LocalizedMessage (0x4301, Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorAddNodeInvalidXPath => new LocalizedMessage (0x4302, Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorChangeNodeInvalidXPath => new LocalizedMessage (0x4303, Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorAttrInvalidXPath => new LocalizedMessage (0x4304, Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorMoveNodeInvalidXPath => new LocalizedMessage (0x4305, Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorRemoveAttrInvalidXPath => new LocalizedMessage (0x4306, Localization.Resources.Generator_BG4300);
		public static LocalizedMessage ErrorMissingAttrName => new LocalizedMessage (0x4307, Localization.Resources.Generator_BG4307);
		public static LocalizedMessage ErrorUnexpectedGlobal => new LocalizedMessage (0x4400, Localization.Resources.Generator_BG4400);
		public static LocalizedMessage ErrorInvalidDIMArgument => new LocalizedMessage (0x4500, Localization.Resources.Generator_BG4500);

		public static LocalizedMessage WarningUnexpectedChild => new LocalizedMessage (0x8101, Localization.Resources.Generator_BG8101);
		public static LocalizedMessage WarningUnknownBaseType => new LocalizedMessage (0x8102, Localization.Resources.Generator_BG8102);
		public static LocalizedMessage WarningInvalidBaseType => new LocalizedMessage (0x8103, Localization.Resources.Generator_BG8103);
		public static LocalizedMessage WarningAssemblyParseFailure => new LocalizedMessage (0x8200, Localization.Resources.Generator_BG8200);
		public static LocalizedMessage WarningMissingClassForConstructor => new LocalizedMessage (0x8300, Localization.Resources.Generator_BG8300);
		public static LocalizedMessage WarningUnexpectedFieldType => new LocalizedMessage (0x8400, Localization.Resources.Generator_BG8400);
		public static LocalizedMessage WarningFieldNameCollision_Property => new LocalizedMessage (0x8401, Localization.Resources.Generator_BG8401_Property);
		public static LocalizedMessage WarningFieldNameCollision_Method => new LocalizedMessage (0x8401, Localization.Resources.Generator_BG8401_Method);
		public static LocalizedMessage WarningFieldNameCollision_NestedType => new LocalizedMessage (0x8401, Localization.Resources.Generator_BG8401_NestedType);
		public static LocalizedMessage WarningDuplicateField => new LocalizedMessage (0x8402, Localization.Resources.Generator_BG8402);
		public static LocalizedMessage WarningUnexpectedInterfaceChild => new LocalizedMessage (0x8500, Localization.Resources.Generator_BG8500);
		public static LocalizedMessage WarningEmptyEventName => new LocalizedMessage (0x8501, Localization.Resources.Generator_BG8501);
		public static LocalizedMessage WarningInvalidDueToInterfaces => new LocalizedMessage (0x8502, Localization.Resources.Generator_BG8502);
		public static LocalizedMessage WarningInvalidDueToMethods => new LocalizedMessage (0x8503, Localization.Resources.Generator_BG8503);
		public static LocalizedMessage WarningInvalidEventName => new LocalizedMessage (0x8504, Localization.Resources.Generator_BG8504);
		public static LocalizedMessage WarningInvalidEventName2 => new LocalizedMessage (0x8505, Localization.Resources.Generator_BG8504);
		public static LocalizedMessage WarningInvalidEventPropertyName => new LocalizedMessage (0x8506, Localization.Resources.Generator_BG8506);
		public static LocalizedMessage WarningInvalidXmlFile => new LocalizedMessage (0x8600, Localization.Resources.Generator_BG8600);
		public static LocalizedMessage WarningNoPackageElements => new LocalizedMessage (0x8601, Localization.Resources.Generator_BG8601);
		public static LocalizedMessage WarningUnexpectedRootChildNode => new LocalizedMessage (0x8602, Localization.Resources.Generator_BG8602);
		public static LocalizedMessage WarningUnexpectedPackageChildNode => new LocalizedMessage (0x8603, Localization.Resources.Generator_BG8603);
		public static LocalizedMessage WarningNestedTypeAncestorNotFound => new LocalizedMessage (0x8604, Localization.Resources.Generator_BG8604);
		public static LocalizedMessage WarningJavaTypeNotResolved => new LocalizedMessage (0x8605, Localization.Resources.Generator_BG8605);
		public static LocalizedMessage WarningTypesNotBoundDueToMissingJavaTypes => new LocalizedMessage (0x8606, Localization.Resources.Generator_BG8606);
		public static LocalizedMessage WarningUnknownReturnType => new LocalizedMessage (0x8700, Localization.Resources.Generator_BG8700);
		public static LocalizedMessage WarningInvalidReturnType => new LocalizedMessage (0x8701, Localization.Resources.Generator_BG8701);
		public static LocalizedMessage WarningUnknownParameterType => new LocalizedMessage (0x8800, Localization.Resources.Generator_BG8800);
		public static LocalizedMessage WarningInvalidParameterType => new LocalizedMessage (0x8801, Localization.Resources.Generator_BG8801);
		public static LocalizedMessage WarningRemoveNodeMatchedNoNodes => new LocalizedMessage (0x8A00, Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningAddNodeMatchedNoNodes => new LocalizedMessage (0x8A01, Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningChangeNodeTypeMatchedNoNodes => new LocalizedMessage (0x8A03, Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningAttrMatchedNoNodes => new LocalizedMessage (0x8A04, Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningMoveNodeMatchedNoNodes => new LocalizedMessage (0x8A05, Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningRemoveAttrMatchedNoNodes => new LocalizedMessage (0x8A06, Localization.Resources.Generator_BG8A00);
		public static LocalizedMessage WarningInvalidNamespaceTransform => new LocalizedMessage (0x8A07, Localization.Resources.Generator_BG8A07);
		public static LocalizedMessage WarningUnknownGenericConstraint => new LocalizedMessage (0x8B00, Localization.Resources.Generator_BG8B00);
		public static LocalizedMessage WarningBaseInterfaceNotFound => new LocalizedMessage (0x8C00, Localization.Resources.Generator_BG8C00);
		public static LocalizedMessage WarningBaseInterfaceInvalid => new LocalizedMessage (0x8C01, Localization.Resources.Generator_BG8C01);

		public static void LogCodedErrorAndExit (LocalizedMessage message, params string [] args)
			=> LogCodedErrorAndExit (message, null, null, args);

		public static void LogCodedErrorAndExit (LocalizedMessage message, Exception? innerException, params string [] args)
			=> LogCodedErrorAndExit (message, innerException, null, args);

		public static void LogCodedErrorAndExit (LocalizedMessage message, Exception? innerException, XNode? node, params string? [] args)
		{
			LogCodedError (message, node, args);

			// Throwing a BindingGeneratorException will cause generator to terminate
			throw new BindingGeneratorException (message.Code, string.Format (message.Value, args), innerException);
		}

		public static void LogCodedError (LocalizedMessage message, XNode? node, params string? [] args)
		{
			var (file, line, col) = GetLineInfo (node);

			LogCodedError (message, file, line, col, args);
		}

		public static void LogCodedError (LocalizedMessage message, string? sourceFile, int line, int column, params string? [] args)
		{
			Console.Error.WriteLine (Format (true, message.Code, sourceFile, line, column, message.Value, args));
		}

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, params string? [] args)
			=> LogCodedWarning (verbosity, message, null, null, -1, -1, args);

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, ISourceLineInfo sourceInfo, params string? [] args)
			=> LogCodedWarning (verbosity, message, null, sourceInfo.SourceFile, sourceInfo.LineNumber, sourceInfo.LinePosition, args);

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, Exception? innerException, params string? [] args)
			=> LogCodedWarning (verbosity, message, innerException, null, -1, -1, args);

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, Exception? innerException, XNode node, params string? [] args)
		{
			var (file, line, col) = GetLineInfo (node);

			LogCodedWarning (verbosity, message, innerException, file, line, col, args);
		}

		public static void LogCodedWarning (int verbosity, LocalizedMessage message, Exception? innerException, string? sourceFile, int line, int column, params string? [] args)
		{
			if (verbosity > (Verbosity ?? 0))
				return;

			var supp = innerException != null ? "  For details, see verbose output." : null;
			Console.Error.WriteLine (Format (false, message.Code, sourceFile, line, column, message.Value, args) + supp);

			if (innerException != null)
				Console.Error.WriteLine (innerException);
		}
		
		public static void Verbose (int verbosity, string format, params object?[] args)
		{
			if (verbosity > (Verbosity ?? 0))
				return;
			Console.Error.WriteLine (format, args);
		}

		public static string FormatCodedMessage (bool error, LocalizedMessage message, params object? [] args)
			=> Format (error, message.Code, null, -1, -1, message.Value, args);
		
		public static string Format (bool error, int errorCode, string? sourceFile, int line, int column, string format, params object?[] args)
		{
			var origin = FormatOrigin (sourceFile, line, column);

			return $"{origin}{(error ? "error" : "warning")} BG{errorCode:X04}: " + string.Format (format, args);
		}

		static string? FormatOrigin (string? sourceFile, int line, int column)
		{
			if (string.IsNullOrWhiteSpace (sourceFile))
				return null;

			var ret = sourceFile;

			if (line == 0)
				return ret + ": ";

			if (column > 0)
				return ret + $"({line},{column}): ";

			return ret + $"({line}): ";
		}

		static (string? file, int line, int col) GetLineInfo (XNode? node)
		{
			if (node is null)
				return (null, -1, -1);

			var file = Uri.TryCreate (node.BaseUri, UriKind.Absolute, out var uri) ? uri.LocalPath : null;
			var pos = (node as IXmlLineInfo)?.HasLineInfo () == true ? node as IXmlLineInfo : null;

			return (file, pos?.LineNumber ?? -1, pos?.LinePosition ?? -1);
		}
	}

	/// <summary>
	/// Throwing this exception will cause generator to exit gracefully.
	/// </summary>
	public class BindingGeneratorException : Exception
	{
		public BindingGeneratorException (int errorCode, string message)
			: this (errorCode, message, null)
		{
		}
		public BindingGeneratorException (int errorCode, string message, Exception? innerException)
			: this (errorCode, null, -1, -1, message, innerException)
		{
		}
		public BindingGeneratorException (int errorCode, string? sourceFile, int line, int column, string message, Exception? innerException)
			: base (Report.Format (true, errorCode, sourceFile, line, column, message), innerException)
		{
		}
	}
}
