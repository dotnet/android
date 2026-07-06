#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Java.Interop.Tools.Cecil;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDroid.Tuner;

namespace Xamarin.Android.Tasks;

/// <summary>
/// Rewrites references to the <c>_Microsoft.Android.Resource.Designer</c> assembly into inline
/// constant resource ids, using the final (post-aapt2) <c>R.txt</c>. Each
/// <c>call int _Microsoft.Android.Resource.Designer.Resource/&lt;Type&gt;::get_&lt;Identifier&gt;()</c>
/// becomes an <c>ldc.i4 &lt;id&gt;</c> (and each <c>int[]</c> styleable getter becomes the equivalent
/// inline array construction).
///
/// The design goal: once every designer reference is a literal, the designer assembly is
/// unreferenced and the trimmer / ILC drops it entirely — no reflection, no shipped resource
/// designer assembly, nothing to root. This is the AOT/trim-ideal replacement for keeping the
/// designer assembly alive.
///
/// This runs late (after aapt2 has assigned the final ids) over the resolved managed assemblies,
/// before ILLink/ILC. Modified assemblies are written to <see cref="OutputDirectory"/> (not
/// in-place) to avoid mutating files in the shared NuGet cache or shared intermediate output paths.
///
/// The main application assembly is skipped: it is compiled after aapt2, so its own resource ids
/// are already baked in as constants (via the internal ResourceConstant class). Framework
/// assemblies are skipped as well.
/// </summary>
public class InlineResourceDesignerConstants : AndroidTask
{
	public override string TaskPrefix => "IRDC";

	[Required]
	public ITaskItem [] Assemblies { get; set; } = [];

	[Required]
	public string RTxtFile { get; set; } = "";

	[Required]
	public string TargetName { get; set; } = "";

	[Required]
	public string OutputDirectory { get; set; } = "";

	public string? CaseMapFile { get; set; }

	public bool Deterministic { get; set; }

	[Output]
	public ITaskItem []? ModifiedAssemblies { get; set; }

	public override bool RunTask ()
	{
		if (!File.Exists (RTxtFile)) {
			// Nothing to inline against (e.g. a project with no resources). No-op.
			Log.LogDebugMessage ($"  {RTxtFile} does not exist; skipping resource designer constant inlining.");
			ModifiedAssemblies = [];
			return true;
		}

		Directory.CreateDirectory (OutputDirectory);

		// Build the id lookup from the final R.txt, keyed by "<ResourceTypeName>::<Identifier>".
		var caseMap = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		if (!CaseMapFile.IsNullOrEmpty () && File.Exists (CaseMapFile)) {
			foreach (var line in File.ReadLines (CaseMapFile)) {
				var parts = line.Split (new [] { ';' }, 2);
				if (parts.Length == 2) {
					caseMap [parts [0]] = parts [1];
				}
			}
		}

		var scalarIds = new Dictionary<string, int> (StringComparer.Ordinal);
		var arrayIds = new Dictionary<string, int []> (StringComparer.Ordinal);
		foreach (var r in new RtxtParser ().Parse (RTxtFile, Log, caseMap)) {
			if (r.ResourceTypeName == null || r.Identifier == null) {
				continue;
			}
			string key = $"{r.ResourceTypeName}::{r.Identifier}";
			if (r.Type == RType.Array) {
				if (r.Ids != null) {
					arrayIds [key] = r.Ids;
				}
			} else {
				scalarIds [key] = r.Id;
			}
		}

		string designerResourceFullName = $"{FixLegacyResourceDesignerStep.DesignerAssemblyNamespace}.Resource";

		using var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true);
		foreach (var assembly in Assemblies) {
			var dir = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec) ?? "");
			if (!resolver.SearchDirectories.Contains (dir)) {
				resolver.SearchDirectories.Add (dir);
			}
		}

		var modified = new List<ITaskItem> ();
		foreach (var item in Assemblies) {
			// The main assembly already has its ids baked in at compile time (post-aapt2), and
			// framework assemblies never reference the designer.
			if (Path.GetFileNameWithoutExtension (item.ItemSpec) == TargetName) {
				continue;
			}
			if (MonoAndroidHelper.IsFrameworkAssembly (item)) {
				continue;
			}

			var assembly = resolver.GetAssembly (item.ItemSpec);
			bool changed = false;
			foreach (var type in assembly.MainModule.GetTypes ()) {
				foreach (var method in type.Methods) {
					if (!method.HasBody) {
						continue;
					}
					changed |= RewriteMethodBody (method.Body, designerResourceFullName, scalarIds, arrayIds);
				}
			}

			if (changed) {
				var outputPath = Path.Combine (OutputDirectory, Path.GetFileName (item.ItemSpec));
				Log.LogDebugMessage ($"  Inlined resource designer constants in {item.ItemSpec}; writing {outputPath}");
				assembly.Write (outputPath, new WriterParameters {
					WriteSymbols = assembly.MainModule.HasSymbols,
					DeterministicMvid = Deterministic,
				});

				var outputItem = new TaskItem (outputPath);
				item.CopyMetadataTo (outputItem);
				outputItem.SetMetadata ("OriginalPath", item.ItemSpec);
				modified.Add (outputItem);
			}
		}

		ModifiedAssemblies = modified.ToArray ();
		return !Log.HasLoggedErrors;
	}

	internal static bool RewriteMethodBody (MethodBody body, string designerResourceFullName, Dictionary<string, int> scalarIds, Dictionary<string, int []> arrayIds)
	{
		var scalarRewrites = new List<(Instruction Call, int Value)> ();
		var arrayRewrites = new List<(Instruction Call, int [] Values)> ();

		foreach (var instruction in body.Instructions) {
			if (instruction.OpCode != OpCodes.Call || instruction.Operand is not MethodReference method) {
				continue;
			}
			// We only care about static getters on a type nested under
			// _Microsoft.Android.Resource.Designer.Resource, e.g. Resource/Drawable::get_tile().
			var declaringType = method.DeclaringType;
			if (declaringType?.DeclaringType == null ||
					!string.Equals (declaringType.DeclaringType.FullName, designerResourceFullName, StringComparison.Ordinal)) {
				continue;
			}
			if (!method.Name.StartsWith ("get_", StringComparison.Ordinal)) {
				continue;
			}

			string key = $"{declaringType.Name}::{method.Name.Substring (4)}";
			if (scalarIds.TryGetValue (key, out int value)) {
				scalarRewrites.Add ((instruction, value));
			} else if (arrayIds.TryGetValue (key, out int [] values)) {
				arrayRewrites.Add ((instruction, values));
			}
		}

		if (scalarRewrites.Count == 0 && arrayRewrites.Count == 0) {
			return false;
		}

		var il = body.GetILProcessor ();
		var intType = body.Method.Module.TypeSystem.Int32;

		// `call get_X()` takes no arguments and pushes a single value, so replacing it with the
		// literal load (or inline array construction) is stack-neutral. ILProcessor.Replace also
		// retargets any branches/exception handlers that pointed at the original call.
		foreach (var (call, value) in scalarRewrites) {
			il.Replace (call, il.Create (OpCodes.Ldc_I4, value));
		}

		foreach (var (call, values) in arrayRewrites) {
			var first = il.Create (OpCodes.Ldc_I4, values.Length);
			il.Replace (call, first);
			Instruction anchor = first;
			anchor = InsertAfter (il, anchor, il.Create (OpCodes.Newarr, intType));
			for (int i = 0; i < values.Length; i++) {
				anchor = InsertAfter (il, anchor, il.Create (OpCodes.Dup));
				anchor = InsertAfter (il, anchor, il.Create (OpCodes.Ldc_I4, i));
				anchor = InsertAfter (il, anchor, il.Create (OpCodes.Ldc_I4, values [i]));
				anchor = InsertAfter (il, anchor, il.Create (OpCodes.Stelem_I4));
			}
		}

		return true;
	}

	static Instruction InsertAfter (ILProcessor il, Instruction anchor, Instruction instruction)
	{
		il.InsertAfter (anchor, instruction);
		return instruction;
	}
}
