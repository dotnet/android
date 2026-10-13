using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

		public static Dictionary<string, string> ReadNativeAotEnvironmentVariables (string outputDirectoryRoot, bool required = true)
		{
			var ret = new Dictionary<string, string> (StringComparer.Ordinal);
			string javaSourcePath = Path.Combine (outputDirectoryRoot, "android", "src", "net", "dot", "jni", "nativeaot", "NativeAotEnvironmentVars.java");
			bool exists = File.Exists (javaSourcePath);
			if (required) {
				Assert.IsTrue (exists, $"NativeAOT Java source with environment variables does not exist: {javaSourcePath}");
			} else if (!exists) {
				return ret;
			}

			var names = new List<string> ();
			var values = new List<string> ();
			bool collectingNames = false;
			bool collectingValues = false;
			int lineNum = 0;
			foreach (string l in File.ReadAllLines (javaSourcePath)) {
				lineNum++;
				string line = l.Trim ();
				switch (line) {
					case "static String[] envNames = new String[] {":
						collectingNames = true;
						collectingValues = false;
						continue;
					case "static String[] envValues = new String[] {":
						collectingValues = true;
						collectingNames = false;
						continue;
					case "};":
						collectingValues = false;
						collectingNames = false;
						continue;
					case "":
						continue;
				}
				if (!collectingValues && !collectingNames) {
					continue;
				}
				Assert.IsTrue (line [0] == '"', $"Line {lineNum} in '{javaSourcePath}' doesn't start with a double quote: '{line}'");
				Assert.IsTrue (line [line.Length - 1] == ',', $"Line {lineNum} in '{javaSourcePath}' doesn't end with a comma: '{line}'");
				Assert.IsTrue (line [line.Length - 2] == '"', $"Line {lineNum} in '{javaSourcePath}' doesn't close the quoted string properly: '{line}'");

				string data = line.Substring (1, line.Length - 3);
				if (collectingNames) {
					names.Add (data);
				} else if (collectingValues) {
					values.Add (data);
				}
			}
			Assert.AreEqual (names.Count, values.Count, $"Environment variable name and value arrays aren't of the same size in '{javaSourcePath}'");
			for (int i = 0; i < names.Count; i++) {
				ret.Add (names [i], values [i]);
			}
			return ret;
		}
	}
}
