using System;
using System.Collections.Generic;

using ELFSharp.ELF.Sections;
using Xamarin.Android.Application.Utilities;

namespace Xamarin.Android.Application;

sealed class MarshalMethodsManagedClass
{}

sealed class MarshalMethodName
{}

abstract class MarshalMethods
{
	protected const string LogTag = "Marshal methods:";

	public abstract string FormatVersion { get; }

	protected ILogger Log { get; }
	public ulong? NumberOfClasses        { get; protected set; }
	public bool? XamarinAppInitFuncValid { get; protected set; }

	protected MarshalMethods (ILogger log)
	{
		Log = log;
	}

	public static MarshalMethods? Create (ILogger log, AnELF elf, ulong format_tag)
	{
		switch (format_tag) {
			case Constants.FormatTag_V1:
			case Constants.FormatTag_V2:
				return new MarshalMethods_V2 (log, elf, format_tag);

			default:
				WarnNotSupported (elf, format_tag);
				return null;
		}
	}

	public static bool Supported (AnELF elf, ulong format_tag)
	{
		Util.Log?.DebugLine ($"{LogTag} checking whether {elf.FilePath} contains supported marshal methods structures");
		// V1 is the same format as V2
		switch (format_tag) {
			case Constants.FormatTag_V1:
			case Constants.FormatTag_V2:
				return LogAndReturn (MarshalMethods_V2.CompatibleBinary (elf, format_tag));

			default:
				WarnNotSupported (elf, format_tag);
				break;
		}

		return LogAndReturn (false);

		bool LogAndReturn (bool retval)
		{
			Util.Log?.DebugLine ($"Marshal methods {Util.AreOrNot (retval)} supported in {elf.FilePath}");
			return retval;
		}
	}

	protected static bool HasRequiredSymbols (AnELF elf, List<string> symbolNames, string formatName)
	{
		Util.Log?.DebugLine ($"{LogTag} checking for required {formatName} symbols in {elf.FilePath}");
		bool allGood = true;
		foreach (string symbol in symbolNames) {
			Util.Log?.Debug ($"  '{symbol}'");
			if (!elf.HasSymbol (symbol)) {
				Util.Log?.DebugLine (" [missing]");
				allGood = false;
			} else {
				Util.Log?.DebugLine (" [found]");
			}
		}

		return allGood;
	}

	static void WarnNotSupported (AnELF elf, ulong format_tag)
	{
		Util.Log?.WarningLine ($"{LogTag} reader does not support libxamarin-app.so format version 0x{format_tag:x}");
	}
}

class MarshalMethods_V2 : MarshalMethods
{
	const string formatVersion = "V2";

	static readonly List<string> RequiredSymbols_V2 = new List<string> {
		Constants.SymbolNames.MarshalMethodsClassCache,
		Constants.SymbolNames.MarshalMethodsClassNames,
		Constants.SymbolNames.MarshalMethodsMethodNames,
		Constants.SymbolNames.MarshalMethodsNumberOfClasses,
		Constants.SymbolNames.MarshalMethodsXamarinAppInit,
	};

	public override string FormatVersion => formatVersion;

	public MarshalMethods_V2 (ILogger log, AnELF elf, ulong format_tag)
		: base (log)
	{
		NumberOfClasses = elf.GetUInt32 (Constants.SymbolNames.MarshalMethodsNumberOfClasses);

		ISymbolEntry? xamarinAppInit = elf.GetSymbol (Constants.SymbolNames.MarshalMethodsXamarinAppInit);
		if (xamarinAppInit != null) {
			XamarinAppInitFuncValid = true;
			if (xamarinAppInit.Type != SymbolType.Function) {
				Log.WarningLine ($"{LogTag} symbol {Constants.SymbolNames.MarshalMethodsXamarinAppInit} is not a function");
				XamarinAppInitFuncValid = false;
			}

			if (xamarinAppInit.Visibility != SymbolVisibility.Default) {
				Log.WarningLine ($"{LogTag} symbol {Constants.SymbolNames.MarshalMethodsXamarinAppInit} is not exported");
				XamarinAppInitFuncValid = false;
			}
		}
	}

	public static bool CompatibleBinary (AnELF elf, ulong format_tag)
	{
		return HasRequiredSymbols (elf, RequiredSymbols_V2, formatVersion);
	}
}
