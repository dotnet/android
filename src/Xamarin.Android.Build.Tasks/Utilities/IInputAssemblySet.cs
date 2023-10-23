using System;
using System.IO;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

abstract class InputAssemblySet
{
	public abstract void AddJavaTypeAssembly (ITaskItem assemblyItem);
	public abstract void AddUserAssembly (ITaskItem assemblyItem);
	public abstract bool IsUserAssembly (string name);

	protected static readonly StringComparer AssemblyNameStringComparer = StringComparer.OrdinalIgnoreCase;

	protected string GetUserAssemblyKey (ITaskItem assemblyItem) => Path.GetFileNameWithoutExtension (assemblyItem.ItemSpec);
}
