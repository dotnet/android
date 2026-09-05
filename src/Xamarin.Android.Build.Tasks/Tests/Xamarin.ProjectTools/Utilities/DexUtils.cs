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
			if (stream.Length < 112 || stream.Length > uint.MaxValue) {
				throw new InvalidDataException ($"'{dexFile}' is not a DEX file.");
			}
			var magic = reader.ReadBytes (8);
			if (magic.Length != 8 ||
					magic [0] != 'd' ||
					magic [1] != 'e' ||
					magic [2] != 'x' ||
					magic [3] != '\n' ||
					magic [7] != 0) {
				throw new InvalidDataException ($"'{dexFile}' has invalid DEX magic.");
			}
			var version = Encoding.ASCII.GetString (magic, 4, 3);
			if (version == "041") {
				throw new NotSupportedException ("DEX 041 containers with multiple logical files are not supported.");
			}
			if (version is not ("035" or "037" or "038" or "039" or "040")) {
				throw new InvalidDataException ($"'{dexFile}' uses unsupported DEX version '{version}'.");
			}

			uint fileSize = ReadUInt32 (reader, 32);
			if (fileSize != stream.Length) {
				throw new InvalidDataException ($"'{dexFile}' declares size {fileSize} but contains {stream.Length} bytes.");
			}
			uint headerSize = ReadUInt32 (reader, 36);
			if (headerSize != 112) {
				throw new InvalidDataException ($"'{dexFile}' declares unsupported DEX header size {headerSize}.");
			}
			uint endianTag = ReadUInt32 (reader, 40);
			if (endianTag != 0x12345678) {
				throw new InvalidDataException ($"'{dexFile}' uses unsupported DEX endianness 0x{endianTag:x8}.");
			}

			ValidateSection (stream, ReadUInt32 (reader, 44), ReadUInt32 (reader, 48), 1, "link");
			uint dataSize = ReadUInt32 (reader, 104);
			uint dataOffset = ReadUInt32 (reader, 108);
			ValidateSection (stream, dataSize, dataOffset, 1, "data");
			uint mapOffset = ReadUInt32 (reader, 52);
			if (mapOffset == 0) {
				throw new InvalidDataException ("DEX file has no map list.");
			}
			ValidateRange (stream, mapOffset, 4, "map");
			uint mapCount = ReadUInt32 (reader, mapOffset);
			if (mapCount == 0) {
				throw new InvalidDataException ("DEX map list is empty.");
			}
			ulong mapSize = 4 + (ulong) mapCount * 12;
			ValidateRange (stream, mapOffset, mapSize, "map");
			ulong dataEnd = (ulong) dataOffset + dataSize;
			if (mapOffset < dataOffset || (ulong) mapOffset + mapSize > dataEnd) {
				throw new InvalidDataException ("DEX map list is outside the data section.");
			}

			uint stringCount = ReadUInt32 (reader, 56);
			uint stringOffset = ReadUInt32 (reader, 60);
			uint typeCount = ReadUInt32 (reader, 64);
			uint typeOffset = ReadUInt32 (reader, 68);
			ValidateSection (stream, stringCount, stringOffset, 4, "string identifiers");
			ValidateSection (stream, typeCount, typeOffset, 4, "type identifiers");
			ValidateSection (stream, ReadUInt32 (reader, 72), ReadUInt32 (reader, 76), 12, "prototype identifiers");
			ValidateSection (stream, ReadUInt32 (reader, 80), ReadUInt32 (reader, 84), 8, "field identifiers");
			ValidateSection (stream, ReadUInt32 (reader, 88), ReadUInt32 (reader, 92), 8, "method identifiers");
			uint classCount = ReadUInt32 (reader, 96);
			uint classOffset = ReadUInt32 (reader, 100);
			ValidateSection (stream, classCount, classOffset, 32, "class definitions");

			var stringOffsets = ReadUInt32Table (reader, stringCount, stringOffset);
			var typeDescriptorIndexes = ReadUInt32Table (reader, typeCount, typeOffset);
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
			if (count > int.MaxValue) {
				throw new InvalidDataException ($"DEX table contains too many entries: {count}.");
			}
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

		static void ValidateSection (Stream stream, uint count, uint offset, uint itemSize, string description)
		{
			if (count == 0) {
				return;
			}
			if (offset == 0) {
				throw new InvalidDataException ($"DEX {description} has entries but no offset.");
			}
			ValidateRange (stream, offset, (ulong) count * itemSize, description);
		}

		static void ValidateRange (Stream stream, uint offset, ulong size, string description)
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
			long remainingBytes = reader.BaseStream.Length - reader.BaseStream.Position;
			if (remainingBytes < 1 || utf16Length > (ulong) (remainingBytes - 1)) {
				throw new InvalidDataException (
					$"DEX string declares {utf16Length} UTF-16 units with only {remainingBytes} bytes remaining.");
			}
			var value = new StringBuilder ();
			bool pendingHighSurrogate = false;
			while (true) {
				byte first = ReadStringByte (reader);
				if (first == 0) {
					break;
				}
				if ((first & 0x80) == 0) {
					AppendCodeUnit ((char) first);
					continue;
				}
				if ((first & 0xe0) == 0xc0) {
					byte second = ReadContinuationByte (reader);
					char decoded = (char) (((first & 0x1f) << 6) | (second & 0x3f));
					if (decoded < '\u0080' && (first != 0xc0 || second != 0x80)) {
						throw new InvalidDataException ("DEX modified UTF-8 contains an invalid two-byte overlong encoding.");
					}
					AppendCodeUnit (decoded);
					continue;
				}
				if ((first & 0xf0) == 0xe0) {
					byte second = ReadContinuationByte (reader);
					byte third = ReadContinuationByte (reader);
					char decoded = (char) (((first & 0x0f) << 12) | ((second & 0x3f) << 6) | (third & 0x3f));
					if (decoded < '\u0800') {
						throw new InvalidDataException ("DEX modified UTF-8 contains an invalid three-byte overlong encoding.");
					}
					AppendCodeUnit (decoded);
					continue;
				}
				throw new InvalidDataException ($"Invalid DEX modified UTF-8 lead byte 0x{first:x2}.");
			}
			if (pendingHighSurrogate) {
				throw new InvalidDataException ("DEX modified UTF-8 string ends with an unmatched high surrogate.");
			}
			if (value.Length != utf16Length) {
				throw new InvalidDataException ($"DEX string declared {utf16Length} UTF-16 units but decoded {value.Length}.");
			}
			return value.ToString ();

			void AppendCodeUnit (char decoded)
			{
				if (pendingHighSurrogate) {
					if (!char.IsLowSurrogate (decoded)) {
						throw new InvalidDataException ("DEX modified UTF-8 high surrogate is not followed by a low surrogate.");
					}
					pendingHighSurrogate = false;
				} else if (char.IsLowSurrogate (decoded)) {
					throw new InvalidDataException ("DEX modified UTF-8 contains an unmatched low surrogate.");
				} else if (char.IsHighSurrogate (decoded)) {
					pendingHighSurrogate = true;
				}
				if (value.Length >= utf16Length) {
					throw new InvalidDataException ($"DEX string decodes to more than its declared {utf16Length} UTF-16 units.");
				}
				value.Append (decoded);
			}
		}

		static byte ReadContinuationByte (BinaryReader reader)
		{
			byte value = ReadStringByte (reader);
			if ((value & 0xc0) != 0x80) {
				throw new InvalidDataException ($"Invalid DEX modified UTF-8 continuation byte 0x{value:x2}.");
			}
			return value;
		}

		static byte ReadStringByte (BinaryReader reader)
		{
			if (reader.BaseStream.Position >= reader.BaseStream.Length) {
				throw new InvalidDataException ("DEX modified UTF-8 string is not null-terminated.");
			}
			return reader.ReadByte ();
		}

		static uint ReadUnsignedLeb128 (BinaryReader reader)
		{
			uint value = 0;
			for (int index = 0; index < 5; index++) {
				byte next = ReadStringByte (reader);
				if (index == 4 && (next & 0xf0) != 0) {
					throw new InvalidDataException ("DEX unsigned LEB128 value exceeds 32 bits.");
				}
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
