#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Generates empty native typemap LLVM IR stub files (typemap.{abi}.ll) for the trimmable typemap path.
/// These are compiled by the native toolchain to provide the type_map and related symbols that libmonodroid.so expects.
/// </summary>
public class GenerateEmptyTypemapStub : AndroidTask
{
	public override string TaskPrefix => "GETS";

	[Required]
	public string OutputDirectory { get; set; } = "";

	[Required]
	public ITaskItem [] Abis { get; set; } = [];

	public bool Debug { get; set; }

	[Output]
	public ITaskItem []? Sources { get; set; }

	public override bool RunTask ()
	{
		Directory.CreateDirectory (OutputDirectory);
		var sources = new List<ITaskItem> ();

		foreach (var abi in Abis) {
			string abiName = abi.ItemSpec;
			string stubPath = Path.Combine (OutputDirectory, $"typemap.{abiName}.ll");
			Files.CopyIfStringChanged (GenerateStubLlvmIr (abiName), stubPath);
			var item = new TaskItem (stubPath);
			item.SetMetadata ("abi", abiName);
			sources.Add (item);
		}

		Sources = sources.ToArray ();
		return !Log.HasLoggedErrors;
	}

	string GenerateStubLlvmIr (string abi)
	{
		var (triple, datalayout) = abi switch {
			"arm64-v8a" => ("aarch64-unknown-linux-android21", "e-m:e-i8:8:32-i16:16:32-i64:64-i128:128-n32:64-S128"),
			"x86_64" => ("x86_64-unknown-linux-android21", "e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-f80:128-n8:16:32:64-S128"),
			"armeabi-v7a" => ("armv7-unknown-linux-androideabi21", "e-m:e-p:32:32-Fi8-i64:64-v128:64:128-a:0:32-n32-S64"),
			"x86" => ("i686-unknown-linux-android21", "e-m:e-p:32:32-p270:32:32-p271:32:32-p272:64:64-f64:32:64-f80:32-n8:16:32-S128"),
			_ => throw new NotSupportedException ($"Unsupported ABI: {abi}"),
		};

		string header = $$"""
; ModuleID = 'typemap.{{abi}}.ll'
source_filename = "typemap.{{abi}}.ll"
target datalayout = "{{datalayout}}"
target triple = "{{triple}}"

""";

		if (Debug) {
			return header + """
%struct.TypeMap = type { i32, i32, ptr, ptr }
%struct.TypeMapManagedTypeInfo = type { i64, i32, i32 }
%struct.TypeMapAssembly = type { i64 }

@type_map = dso_local constant %struct.TypeMap zeroinitializer, align 8
@typemap_use_hashes = dso_local constant i8 1, align 1
@type_map_managed_type_info = dso_local constant [0 x %struct.TypeMapManagedTypeInfo] zeroinitializer, align 8
@type_map_unique_assemblies = dso_local constant [0 x %struct.TypeMapAssembly] zeroinitializer, align 8
@type_map_assembly_names = dso_local constant [1 x i8] zeroinitializer, align 1
@type_map_managed_type_names = dso_local constant [1 x i8] zeroinitializer, align 1
@type_map_java_type_names = dso_local constant [1 x i8] zeroinitializer, align 1
""";
		}

		return header + """
@managed_to_java_map_module_count = dso_local constant i32 0, align 4
@managed_to_java_map = dso_local constant [0 x i8] zeroinitializer, align 8
@java_to_managed_map = dso_local constant [0 x i8] zeroinitializer, align 8
@java_to_managed_hashes = dso_local constant [0 x i64] zeroinitializer, align 8
@modules_map_data = dso_local constant [0 x i8] zeroinitializer, align 8
@modules_duplicates_data = dso_local constant [0 x i8] zeroinitializer, align 8
@java_type_count = dso_local constant i32 0, align 4
@java_type_names = dso_local constant [1 x i8] zeroinitializer, align 1
@java_type_names_size = dso_local constant i64 0, align 8
@managed_type_names = dso_local constant [1 x i8] zeroinitializer, align 1
@managed_assembly_names = dso_local constant [1 x i8] zeroinitializer, align 1
""";
	}
}
