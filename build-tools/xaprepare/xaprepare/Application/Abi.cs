using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Prepare
{
	partial class Abi
	{
		static readonly List<Abi> KnownAbis = new List<Abi> {
			new Abi { Name = AbiNames.TargetJit.AndroidArmV7a, Type = AbiType.TargetJit, Is64Bit = false, IsCross = false, IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.TargetJit.AndroidArmV8a, Type = AbiType.TargetJit, Is64Bit = true,  IsCross = false, IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.TargetJit.AndroidX86,    Type = AbiType.TargetJit, Is64Bit = false, IsCross = false, IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.TargetJit.AndroidX86_64, Type = AbiType.TargetJit, Is64Bit = true,  IsCross = false, IsHost = false, IsLlvm = false, IsWindows = false },

			new Abi { Name = AbiNames.TargetAot.ArmV7a,        Type = AbiType.TargetAot, Is64Bit = false, IsCross = false, IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.TargetAot.WinArmV7a,     Type = AbiType.TargetAot, Is64Bit = false, IsCross = false, IsHost = false, IsLlvm = false, IsWindows = true },
			new Abi { Name = AbiNames.TargetAot.ArmV8a,        Type = AbiType.TargetAot, Is64Bit = true,  IsCross = false, IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.TargetAot.WinArmV8a,     Type = AbiType.TargetAot, Is64Bit = true,  IsCross = false, IsHost = false, IsLlvm = false, IsWindows = true },
			new Abi { Name = AbiNames.TargetAot.X86,           Type = AbiType.TargetAot, Is64Bit = false, IsCross = false, IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.TargetAot.WinX86,        Type = AbiType.TargetAot, Is64Bit = false, IsCross = false, IsHost = false, IsLlvm = false, IsWindows = true },
			new Abi { Name = AbiNames.TargetAot.X86_64,        Type = AbiType.TargetAot, Is64Bit = true,  IsCross = false, IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.TargetAot.WinX86_64,     Type = AbiType.TargetAot, Is64Bit = true,  IsCross = false, IsHost = false, IsLlvm = false, IsWindows = true },

			new Abi { Name = AbiNames.HostJit.Linux,           Type = AbiType.HostJit,   Is64Bit = false, IsCross = false, IsHost = true,  IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.HostJit.Darwin,          Type = AbiType.HostJit,   Is64Bit = false, IsCross = false, IsHost = true,  IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.HostJit.Win32,           Type = AbiType.HostJit,   Is64Bit = false, IsCross = false, IsHost = true,  IsLlvm = false, IsWindows = true },
			new Abi { Name = AbiNames.HostJit.Win64,           Type = AbiType.HostJit,   Is64Bit = true,  IsCross = false, IsHost = true,  IsLlvm = false, IsWindows = true },

			new Abi { Name = AbiNames.CrossAot.ArmV7a,         Type = AbiType.CrossAot,  Is64Bit = false, IsCross = true,  IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.CrossAot.WinArmV7a,      Type = AbiType.CrossAot,  Is64Bit = false, IsCross = true,  IsHost = false, IsLlvm = false, IsWindows = true },
			new Abi { Name = AbiNames.CrossAot.ArmV8a,         Type = AbiType.CrossAot,  Is64Bit = true,  IsCross = true,  IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.CrossAot.WinArmV8a,      Type = AbiType.CrossAot,  Is64Bit = true,  IsCross = true,  IsHost = false, IsLlvm = false, IsWindows = true },
			new Abi { Name = AbiNames.CrossAot.X86,            Type = AbiType.CrossAot,  Is64Bit = false, IsCross = true,  IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.CrossAot.WinX86,         Type = AbiType.CrossAot,  Is64Bit = false, IsCross = true,  IsHost = false, IsLlvm = false, IsWindows = true },
			new Abi { Name = AbiNames.CrossAot.X86_64,         Type = AbiType.CrossAot,  Is64Bit = true,  IsCross = true,  IsHost = false, IsLlvm = false, IsWindows = false },
			new Abi { Name = AbiNames.CrossAot.WinX86_64,      Type = AbiType.CrossAot,  Is64Bit = true,  IsCross = true,  IsHost = false, IsLlvm = false, IsWindows = true },

			new Abi { Name = AbiNames.Llvm.Host32Bit,          Type = AbiType.Llvm,      Is64Bit = false, IsCross = false, IsHost = false, IsLlvm = true,  IsWindows = false },
			new Abi { Name = AbiNames.Llvm.Host64Bit,          Type = AbiType.Llvm,      Is64Bit = true,  IsCross = false, IsHost = false, IsLlvm = true,  IsWindows = false },
			new Abi { Name = AbiNames.Llvm.Windows32Bit,       Type = AbiType.Llvm,      Is64Bit = false, IsCross = false, IsHost = false, IsLlvm = true,  IsWindows = true },
			new Abi { Name = AbiNames.Llvm.Windows64Bit,       Type = AbiType.Llvm,      Is64Bit = true,  IsCross = false, IsHost = false, IsLlvm = true,  IsWindows = true },
		};

		public bool Is64Bit   { get; private set; }
		public bool IsCross   { get; private set; }
		public bool IsHost    { get; private set; }
		public bool IsLlvm    { get; private set; }
		public bool IsWindows { get; private set; }
		public string Name    { get; private set; }
		public AbiType Type   { get; private set; }

		public static HashSet<string> GetHostAbis (OS osType = OS.Any, Bitness bitness = Bitness.Any, bool includeAllHostOSes = true)
		{
			IEnumerable<string> abis = KnownAbis.Where
				(a =>
				 a.Type == AbiType.HostJit &&
				 OSMatches(a, osType) &&
				 BitnessMatches (a, bitness) &&
				 (includeAllHostOSes || IsCurrentHostOS (a))).Select (a => a.Name);

			return MakeHashSet (abis);
		}

		public static HashSet<string> GetLlvmAbis (OS osType = OS.Any, Bitness bitness = Bitness.Any)
		{
			return MakeHashSet (KnownAbis.Where (a => a.Type == AbiType.Llvm && OSMatches (a, osType) && BitnessMatches (a, bitness)).Select (a => a.Name));
		}

		public static HashSet<string> GetTargetJitAbis (Bitness bitness = Bitness.Any)
		{
			return MakeHashSet (KnownAbis.Where (a => a.Type == AbiType.TargetJit && BitnessMatches (a, bitness)).Select (a => a.Name));
		}

		public static HashSet<string> GetHostAotAbis (OS osType = OS.Any, Bitness bitness = Bitness.Any)
		{
			return MakeHashSet (KnownAbis.Where (a => a.Type == AbiType.TargetAot && OSMatches (a, osType) && BitnessMatches (a, bitness)).Select (a => a.Name));
		}

		public static HashSet<string> GetCrossAbis (OS osType = OS.Any, Bitness bitness = Bitness.Any)
		{
			return MakeHashSet (KnownAbis.Where (a => a.Type == AbiType.CrossAot && OSMatches (a, osType) && BitnessMatches (a, bitness)).Select (a => a.Name));
		}

		static bool IsCurrentHostOS (Abi abi)
		{
			if (abi.IsWindows)
				return true; // Cross-build ABIs should be included in this case

			return String.Compare (abi.Name, Context.Instance.OS.Type, StringComparison.Ordinal) == 0;
		}

		static bool OSMatches (Abi abi, OS osType)
		{
			if (osType == OS.Any)
				return true;

			if (osType == OS.Windows)
				return abi.IsWindows;

			return !abi.IsWindows;
		}

		static bool BitnessMatches (Abi abi, Bitness bitness)
		{
			if (bitness == Bitness.Any)
				return true;

			if (bitness == Bitness.ThirtyTwo)
				return !abi.Is64Bit;

			return abi.Is64Bit;
		}

		static HashSet<string> MakeHashSet (IEnumerable<string> entries)
		{
			return new HashSet<string> (entries, StringComparer.Ordinal);
		}
	}
}
