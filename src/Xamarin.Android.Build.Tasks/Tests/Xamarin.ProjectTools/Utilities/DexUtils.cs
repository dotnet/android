using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xamarin.Android.Tools;

namespace Xamarin.ProjectTools
{
	public static class DexUtils
	{
		/*
		 Example dexdump output:
		 
			Class #12            -
			  Class descriptor  : 'Landroid/runtime/UncaughtExceptionHandler;'
			  Access flags      : 0x0001 (PUBLIC)
			  Superclass        : 'Ljava/lang/Object;'
			  Interfaces        -
				#0              : 'Ljava/lang/Thread$UncaughtExceptionHandler;'
				#1              : 'Lmono/android/IGCUserPeer;'
			  Static fields     -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '__md_methods'
				  type          : 'Ljava/lang/String;'
				  access        : 0x0019 (PUBLIC STATIC FINAL)
			  Instance fields   -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : 'refList'
				  type          : 'Ljava/util/ArrayList;'
				  access        : 0x0002 (PRIVATE)
			  Direct methods    -
				#0              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '<clinit>'
				  type          : '()V'
				  access        : 0x10008 (STATIC CONSTRUCTOR)
				  code          -
				  registers     : 3
				  ins           : 0
				  outs          : 3
				  insns size    : 10 16-bit code units
				  catches       : (none)
				  positions     : 
					0x0002 line=16
				  locals        : 
				#1              : (in Landroid/runtime/UncaughtExceptionHandler;)
				  name          : '<init>'
				  type          : '()V'
				  access        : 0x10001 (PUBLIC CONSTRUCTOR)
				  code          -
				  registers     : 4
				  ins           : 1
				  outs          : 4
				  insns size    : 22 16-bit code units
				  catches       : (none)
				  positions     : 
					0x0000 line=22
					0x0003 line=23
					0x0010 line=24
				  locals        : 
					0x0000 - 0x0016 reg=3 this Landroid/runtime/UncaughtExceptionHandler; 
		 */

		/// <summary>
		/// Reads the DEX type tables to see if a class exists.
		/// </summary>
		/// <param name="className">A Java class name of the form 'Landroid/app/ActivityTracker;'</param>
		public static bool ContainsClass (string className, string dexFile, string androidSdkDirectory)
		{
			_ = androidSdkDirectory;
			return GetClassDescriptors (dexFile).Any (descriptor => descriptor.Contains (className, StringComparison.Ordinal));
		}

		public static IReadOnlyList<string> GetClassDescriptors (string dexFile)
		{
			using var stream = File.OpenRead (dexFile);
			using var reader = new BinaryReader (stream, Encoding.UTF8, leaveOpen: true);
			if (stream.Length < 112 ||
					reader.ReadByte () != 'd' ||
					reader.ReadByte () != 'e' ||
					reader.ReadByte () != 'x' ||
					reader.ReadByte () != '\n') {
				throw new InvalidDataException ($"'{dexFile}' is not a DEX file.");
			}
			stream.Position = 4;
			var version = Encoding.ASCII.GetString (reader.ReadBytes (3));
			if (version == "041") {
				throw new NotSupportedException ("DEX 041 containers with multiple logical files are not supported.");
			}
			if (version is not ("035" or "037" or "038" or "039" or "040")) {
				throw new InvalidDataException ($"'{dexFile}' uses unsupported DEX version '{version}'.");
			}

			uint endianTag = ReadUInt32 (reader, 40);
			if (endianTag != 0x12345678) {
				throw new InvalidDataException ($"'{dexFile}' uses unsupported DEX endianness 0x{endianTag:x8}.");
			}

			var stringOffsets = ReadUInt32Table (reader, ReadUInt32 (reader, 56), ReadUInt32 (reader, 60));
			var typeDescriptorIndexes = ReadUInt32Table (reader, ReadUInt32 (reader, 64), ReadUInt32 (reader, 68));
			uint classCount = ReadUInt32 (reader, 96);
			uint classOffset = ReadUInt32 (reader, 100);
			ValidateRange (stream, classOffset, checked (classCount * 32), "class definitions");

			var descriptors = new List<string> (checked ((int) classCount));
			for (uint index = 0; index < classCount; index++) {
				uint classIndex = ReadUInt32 (reader, checked (classOffset + index * 32));
				if (classIndex >= typeDescriptorIndexes.Length) {
					throw new InvalidDataException ($"DEX class index {classIndex} is outside the type table.");
				}
				uint descriptorIndex = typeDescriptorIndexes [classIndex];
				if (descriptorIndex >= stringOffsets.Length) {
					throw new InvalidDataException ($"DEX descriptor index {descriptorIndex} is outside the string table.");
				}
				descriptors.Add (ReadModifiedUtf8 (reader, stringOffsets [descriptorIndex]));
			}
			return descriptors;
		}

		static uint [] ReadUInt32Table (BinaryReader reader, uint count, uint offset)
		{
			ValidateRange (reader.BaseStream, offset, checked (count * 4), "table");
			var values = new uint [checked ((int) count)];
			reader.BaseStream.Position = offset;
			for (int index = 0; index < values.Length; index++) {
				values [index] = reader.ReadUInt32 ();
			}
			return values;
		}

		static uint ReadUInt32 (BinaryReader reader, uint offset)
		{
			ValidateRange (reader.BaseStream, offset, 4, "value");
			reader.BaseStream.Position = offset;
			return reader.ReadUInt32 ();
		}

		static void ValidateRange (Stream stream, uint offset, uint size, string description)
		{
			ulong end = (ulong) offset + size;
			if (end > (ulong) stream.Length) {
				throw new InvalidDataException ($"DEX {description} at 0x{offset:x} extends beyond the file.");
			}
		}

		static string ReadModifiedUtf8 (BinaryReader reader, uint offset)
		{
			ValidateRange (reader.BaseStream, offset, 1, "string");
			reader.BaseStream.Position = offset;
			uint utf16Length = ReadUnsignedLeb128 (reader);
			var value = new StringBuilder (checked ((int) utf16Length));
			while (true) {
				byte first = reader.ReadByte ();
				if (first == 0) {
					break;
				}
				if ((first & 0x80) == 0) {
					value.Append ((char) first);
					continue;
				}
				if ((first & 0xe0) == 0xc0) {
					byte second = ReadContinuationByte (reader);
					value.Append ((char) (((first & 0x1f) << 6) | (second & 0x3f)));
					continue;
				}
				if ((first & 0xf0) == 0xe0) {
					byte second = ReadContinuationByte (reader);
					byte third = ReadContinuationByte (reader);
					value.Append ((char) (((first & 0x0f) << 12) | ((second & 0x3f) << 6) | (third & 0x3f)));
					continue;
				}
				throw new InvalidDataException ($"Invalid DEX modified UTF-8 lead byte 0x{first:x2}.");
			}
			if (value.Length != utf16Length) {
				throw new InvalidDataException ($"DEX string declared {utf16Length} UTF-16 units but decoded {value.Length}.");
			}
			return value.ToString ();
		}

		static byte ReadContinuationByte (BinaryReader reader)
		{
			byte value = reader.ReadByte ();
			if ((value & 0xc0) != 0x80) {
				throw new InvalidDataException ($"Invalid DEX modified UTF-8 continuation byte 0x{value:x2}.");
			}
			return value;
		}

		static uint ReadUnsignedLeb128 (BinaryReader reader)
		{
			uint value = 0;
			for (int index = 0; index < 5; index++) {
				byte next = reader.ReadByte ();
				value |= (uint) (next & 0x7f) << (index * 7);
				if ((next & 0x80) == 0) {
					return value;
				}
			}
			throw new InvalidDataException ("Invalid DEX unsigned LEB128 value.");
		}

		/// <summary>
		/// Runs the dexdump command to see if a class exists in a dex file *and* has a public constructor
		/// </summary>
		/// <param name="className">A Java class name of the form 'Landroid/app/ActivityTracker;'</param>
		/// <param name="method">A Java method name of the form 'foo'</param>
		/// <param name="type">A Java method signature of the form '()V'</param>
		public static bool ContainsClassWithMethod (string className, string method, string type, string dexFile, string androidSdkDirectory)
		{
			bool inClass = false;
			bool hasName = false;
			bool hasType = false;
			DataReceivedEventHandler handler = (s, e) => {
				if (e.Data != null) {
					if (e.Data.Contains ("Class descriptor")) {
						inClass = e.Data.Contains (className);
						hasName = false;
					} else if (inClass && e.Data.Contains ("name") && e.Data.Contains (method)) {
						hasName = true;
					} else if (hasName && e.Data.Contains ("type") && e.Data.Contains (type)) {
						hasType = true;
					}
				}
			};
			DexDump (handler, dexFile, androidSdkDirectory);
			return hasType;
		}

		static void DexDump (DataReceivedEventHandler handler, string dexFile, string androidSdkDirectory)
		{
			var androidSdk = new AndroidSdkInfo ((l, m) => {
				Console.WriteLine ($"{l}: {m}");
				if (l == TraceLevel.Error) {
					throw new Exception (m);
				}
			}, androidSdkDirectory, javaSdkPath: AndroidSdkResolver.GetJavaSdkPath ());
			var buildToolsPath = androidSdk.GetBuildToolsPaths ().FirstOrDefault ();
			if (string.IsNullOrEmpty (buildToolsPath)) {
				throw new Exception ($"Unable to find build-tools in `{androidSdkDirectory}`!");
			}

			var psi = new ProcessStartInfo {
				FileName = Path.Combine (buildToolsPath, "dexdump"),
				Arguments = Path.GetFileName (dexFile),
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				WorkingDirectory = Path.GetDirectoryName (dexFile),
			};
			using (var p = new Process { StartInfo = psi }) {
				p.ErrorDataReceived += handler;
				p.OutputDataReceived += handler;

				p.Start ();
				p.BeginErrorReadLine ();
				p.BeginOutputReadLine ();
				p.WaitForExit ();

				if (p.ExitCode != 0)
					throw new Exception ($"'{psi.FileName} {psi.Arguments}' exited with code: {p.ExitCode}");
			}
		}
	}
}
