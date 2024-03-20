using System.Collections.Generic;

using Xamarin.Android.Tools;

namespace Xamarin.Android.AssemblyStore;

abstract class AssemblyStoreItem
{
	public string Name                  { get; }
	public IList<ulong> Hashes          { get; }
	public bool Is64Bit                 { get; }
	public uint DataOffset              { get; protected set; }
	public uint DataSize                { get; protected set; }
	public uint DebugOffset             { get; protected set; }
	public uint DebugSize               { get; protected set; }
	public uint ConfigOffset            { get; protected set; }
	public uint ConfigSize              { get; protected set; }
	public AndroidTargetArch TargetArch { get; protected set; }

	protected AssemblyStoreItem (string name, bool is64Bit, List<ulong> hashes)
	{
		Name = name;
		Hashes = hashes.AsReadOnly ();
		Is64Bit = is64Bit;
	}
}
