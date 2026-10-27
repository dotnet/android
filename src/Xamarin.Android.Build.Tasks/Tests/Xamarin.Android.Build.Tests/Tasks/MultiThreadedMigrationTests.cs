#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Mono.Cecil;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class MultiThreadedMigrationTests
	{
		[Test]
		public void MigratedTasksHaveDirectAttribute ()
		{
			string [] assemblies = [
				"Microsoft.Android.Build.BaseTasks.dll",
				"Xamarin.Android.Build.Tasks.dll",
				"Xamarin.Android.Build.Debugging.Tasks.dll",
				"Xamarin.Installer.Build.Tasks.dll",
				Path.Combine ("net", "Microsoft.Android.Build.Tasks.dll"),
			];
			using var resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (TestEnvironment.AndroidMSBuildDirectory);
			resolver.AddSearchDirectory (Path.Combine (TestEnvironment.AndroidMSBuildDirectory, "net"));
			resolver.AddSearchDirectory (TestContext.CurrentContext.TestDirectory);
			resolver.AddSearchDirectory (RuntimeEnvironment.GetRuntimeDirectory ());
			int migratedTasks = 0;
			foreach (var name in assemblies) {
				using var assembly = AssemblyDefinition.ReadAssembly (
					Path.Combine (TestEnvironment.AndroidMSBuildDirectory, name),
					new ReaderParameters { AssemblyResolver = resolver });
				foreach (var type in assembly.MainModule.GetTypes ()) {
					bool marked = type.CustomAttributes.Any (a => a.AttributeType.FullName == typeof (MSBuildMultiThreadableTaskAttribute).FullName);
					if (type.IsAbstract) {
						Assert.IsFalse (marked, $"{type.FullName} is abstract and must not declare MSBuildMultiThreadableTaskAttribute.");
						continue;
					}
					var baseTypes = GetBaseTypes (type).ToArray ();
					if (baseTypes.Contains (typeof (MultithreadedAndroidTask).FullName)) {
						migratedTasks++;
						Assert.IsTrue (marked, $"{type.FullName} must declare MSBuildMultiThreadableTaskAttribute directly.");
					}
					if (marked) {
						Assert.IsFalse (baseTypes.Contains (typeof (AndroidTask).FullName) ||
							baseTypes.Contains (typeof (AsyncTask).FullName) ||
							baseTypes.Contains (typeof (AndroidToolTask).FullName),
							$"{type.FullName} must not use a base that captures the process working directory.");
					}
				}
			}
			Assert.Greater (migratedTasks, 0, "No migrated tasks were found in the SDK task assemblies.");
		}

		static IEnumerable<string> GetBaseTypes (TypeDefinition type)
		{
			for (TypeReference? baseType = type.BaseType; baseType != null;) {
				yield return baseType.FullName;
				if (baseType.FullName == "System.Object")
					yield break;
				var definition = baseType.Resolve () ?? throw new InvalidOperationException ($"Unable to resolve {baseType.FullName}.");
				baseType = definition.BaseType;
			}
		}
	}
}
