using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

static class NativeAssemblerItemsHelper
{
	public enum KnownMode
	{
		CompressedAssemblies,
		EmbeddedAssemblyStore,
		EmbeddedRuntimeConfig,
		Environment,
		JNIRemap,
		MarshalMethods,
		TypeMap,
	}

	sealed class ModeConfig
	{
		public readonly string FileNameBase;
		public readonly string Extension;

		public ModeConfig (string fileNameBase, string extension)
		{
			FileNameBase = fileNameBase;
			Extension = extension;
		}
	}

	const string LlvmIrExtension = "ll";
	const string NativeAssemblerExtension = "s";

	static readonly Dictionary<KnownMode, ModeConfig> ModeConfigs = new () {
		{ KnownMode.CompressedAssemblies,  new ("compressed_assemblies", LlvmIrExtension) },
		{ KnownMode.EmbeddedAssemblyStore, new ("embed_assembly_store",  NativeAssemblerExtension) },
		{ KnownMode.EmbeddedRuntimeConfig, new ("embed_runtime_config",  NativeAssemblerExtension) },
		{ KnownMode.Environment,           new ("environment",           LlvmIrExtension) },
		{ KnownMode.JNIRemap,              new ("jni_remap",             LlvmIrExtension) },
		{ KnownMode.MarshalMethods,        new ("marshal_methods",       LlvmIrExtension) },
		{ KnownMode.TypeMap,               new ("typemaps",              LlvmIrExtension) },
	};

	public static string? GetSourcePath (TaskLoggingHelper log, KnownMode mode, string nativeSourcesDir, string abi)
	{
		if (!ModeConfigs.TryGetValue (mode, out ModeConfig config)) {
			log.LogError ($"Unknown mode: {mode}");
			return null;
		}

		return Path.Combine (nativeSourcesDir, $"{config.FileNameBase}.{abi.ToLowerInvariant ()}.{config.Extension}");
	}

	public static KnownMode ToKnownMode (string mode)
	{
		if (!Enum.TryParse<KnownMode> (mode, ignoreCase: true, out KnownMode result)) {
			throw new InvalidOperationException ($"Internal exception: uknown native assembler generator mode '{mode}'");
		}

		return result;
	}
}
