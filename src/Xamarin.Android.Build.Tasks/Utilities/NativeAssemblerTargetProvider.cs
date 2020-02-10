using System;
using System.IO;

namespace Xamarin.Android.Tasks
{
	abstract class NativeAssemblerTargetProvider
	{
		public abstract bool Is64Bit { get; }
		public abstract string PointerFieldType { get; }
		public abstract string TypePrefix { get; }
		public abstract string AbiName { get; }
		public abstract uint MapModulesAlignBits { get; }
		public abstract uint MapJavaAlignBits { get; }

		public virtual string MapType <T> ()
		{
			if (typeof(T) == typeof(byte))
				return ".byte";

			if (typeof(T) == typeof(bool))
				return ".byte";

			if (typeof(T) == typeof(string))
				return ".asciz";

			throw new InvalidOperationException ($"Unable to map managed type {typeof(T)} to native assembly type");
		}

		public virtual uint GetTypeSize <T> (T field)
		{
			return GetTypeSize <T> ();
		}

		public virtual uint GetTypeSize <T> ()
		{
			return GetTypeSize (typeof(T));
		}

		public virtual uint GetTypeSize (Type type)
		{
			if (type == typeof(byte))
				return 1u;

			if (type == typeof(bool))
				return 1u;

			if (type == typeof(string))
				return GetPointerSize();

			if (type == typeof(Int32) || type == typeof(UInt32))
				return 4u;

			if (type == typeof(Int64) || type == typeof(UInt64))
				return 8u;

			throw new InvalidOperationException ($"Unable to map managed type {type} to native assembly type");
		}

		public virtual uint GetStructureAlignment (bool hasPointers)
		{
			return (!hasPointers || !Is64Bit) ? 2u : 3u;
		}

		public virtual uint GetPointerSize ()
		{
			return Is64Bit ? 8u : 4u;
		}

		public virtual void WriteFileHeader (StreamWriter output, string indent)
		{
		}
	}
}
