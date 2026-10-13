using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ELFSharp.ELF;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests
{
	class EnvironmentHelper
	{
		public sealed class ApplicationConfig
		{
			public bool ignore_split_configs;
			public uint number_of_runtime_properties;
			public uint package_naming_policy;
			public uint environment_variable_count;
			public uint system_property_count;
			public uint number_of_assemblies_in_apk;
			public uint bundled_assembly_name_width;
			public uint number_of_dso_cache_entries;
			public uint number_of_shared_libraries;
			public string android_package_name = "";
			public bool have_assembly_store;
		}

		public sealed class JniPreloadsEntry
		{
			public uint Index;
			public string LibraryName;
		}

		public sealed class JniPreloads
		{
			public uint IndexStride;
			public List<JniPreloadsEntry> Entries;
			public string SourceFile;
		}

		public static (ApplicationConfig Config, JniPreloads Preloads, Dictionary<string, string> EnvironmentVariables) ReadCoreClrBootstrap (string intermediateDirectory)
		{
			string [] files = Directory.GetFiles (intermediateDirectory, "coreclr-bootstrap.bin", SearchOption.AllDirectories);
			Assert.AreEqual (1, files.Length, "Expected one shared CoreCLR bootstrap intermediate.");
			string path = files [0];
			byte [] data = File.ReadAllBytes (path);
			uint Read (uint offset) => BinaryPrimitives.ReadUInt32LittleEndian (data.AsSpan (checked ((int)offset), 4));

			Assert.GreaterOrEqual (data.Length, 84);
			Assert.AreEqual (0x47464358u, Read (0), "Expected XCFG magic.");
			Assert.AreEqual (1, BinaryPrimitives.ReadUInt16LittleEndian (data.AsSpan (4, 2)));
			Assert.AreEqual (data.Length, Read (8));
			uint environmentCount = Read (12);
			uint dsoCount = Read (24);
			uint preloadCount = Read (32);
			uint preloadStride = Read (36);
			uint environmentOffset = Read (40);
			uint dsoOffset = Read (52);
			uint preloadOffset = Read (56);
			uint stringsOffset = Read (60);

			string ReadString (uint offset, bool allowEmpty = false)
			{
				if (offset == 0 && allowEmpty) {
					return "";
				}
				Assert.Greater (offset, stringsOffset);
				Assert.Less (offset, (uint)data.Length);
				int end = Array.IndexOf (data, (byte)0, checked ((int)offset));
				Assert.Greater (end, (int)offset);
				return Encoding.UTF8.GetString (data, checked ((int)offset), end - checked ((int)offset));
			}

			var config = new ApplicationConfig {
				ignore_split_configs = (data [6] & 1) != 0,
				number_of_runtime_properties = Read (20),
				package_naming_policy = Read (68),
				environment_variable_count = environmentCount,
				system_property_count = Read (16),
				number_of_dso_cache_entries = dsoCount,
				number_of_assemblies_in_apk = Read (72),
				bundled_assembly_name_width = Read (76),
				number_of_shared_libraries = Read (28),
				android_package_name = ReadString (Read (80)),
				have_assembly_store = (data [6] & 2) != 0,
			};
			var preloads = new JniPreloads {
				IndexStride = preloadStride,
				Entries = new List<JniPreloadsEntry> (),
				SourceFile = path,
			};
			for (uint i = 0; i < preloadCount; i++) {
				uint index = Read (preloadOffset + i * 4);
				Assert.Less (index, dsoCount);
				preloads.Entries.Add (new JniPreloadsEntry {
					Index = index,
					LibraryName = ReadString (Read (dsoOffset + index * 12 + 8)),
				});
			}
			var environment = new Dictionary<string, string> (StringComparer.Ordinal);
			for (uint i = 0; i < environmentCount; i++) {
				uint offset = checked (environmentOffset + i * 8);
				string name = ReadString (Read (offset));
				Assert.IsNotEmpty (name, "Environment variable names must not be empty.");
				environment.Add (name, ReadString (Read (offset + 4), allowEmpty: true));
			}
			return (config, preloads, environment);
		}

		public static Dictionary<string, string> ReadNativeAotEnvironmentVariables (string outputDirectoryRoot, bool required = true, bool systemProperties = false)
		{
			var ret = new Dictionary<string, string> (StringComparer.Ordinal);
			string [] objects = Directory.GetFiles (outputDirectoryRoot, "nativeaot-bootstrap.o", SearchOption.AllDirectories);
			if (required) {
				Assert.IsNotEmpty (objects, $"NativeAOT bootstrap data objects do not exist: {outputDirectoryRoot}");
			} else if (objects.Length == 0) {
				return ret;
			}
			using IELF elf = ELFReader.Load (objects [0]);
			Assert.AreEqual (FileType.Relocatable, elf.Type);
			byte [] section = elf.GetSection (".rodata").GetContents ();
			Assert.AreEqual (0x42424c42u, BinaryPrimitives.ReadUInt32LittleEndian (section));
			Assert.AreEqual (0, BinaryPrimitives.ReadUInt16LittleEndian (section.AsSpan (6, 2)),
				"This helper reads the default uncompressed bootstrap.");
			uint length = BinaryPrimitives.ReadUInt32LittleEndian (section.AsSpan (12, 4));
			byte [] body = section.AsSpan (16, checked ((int)length)).ToArray ();
			uint Read (int offset) => BinaryPrimitives.ReadUInt32LittleEndian (body.AsSpan (offset, 4));
			Assert.AreEqual (0x4746434eu, Read (0));
			Assert.AreEqual (body.Length, Read (8));
			uint count = Read (systemProperties ? 16 : 12);
			uint table = systemProperties ? Read (20) : 32;
			string ReadString (uint offset)
			{
				Assert.GreaterOrEqual (offset, Read (24));
				Assert.Less (offset, length);
				int end = Array.IndexOf (body, (byte)0, checked ((int)offset));
				Assert.GreaterOrEqual (end, (int)offset);
				return Encoding.UTF8.GetString (body, checked ((int)offset), end - checked ((int)offset));
			}
			for (uint i = 0; i < count; i++) {
				int offset = checked ((int)(table + i * 8));
				ret.Add (ReadString (Read (offset)), ReadString (Read (offset + 4)));
			}
			return ret;
		}
	}
}
