using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.XPath;

using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	// TODO: add doc comments to the generated properties
	//
	public partial class GenerateLayoutBindings : AndroidAsyncTask
	{
		public override string TaskPrefix => "GLB";

		sealed class PartialClass
		{
			public string Name;
			public string Namespace;
			public string OutputFilePath;
			public StreamWriter Writer;
			public FileStream Stream;
			public BindingGenerator.State State;
		}

		sealed class LayoutGroup
		{
			public List<ITaskItem> Items;
			public List<PartialClass> Classes;
			public HashSet<string> ClassNames;
		}

		internal sealed class BindingGeneratorLanguage
		{
			public readonly string Name;
			public readonly string Extension;
			public Func<BindingGenerator> Creator { get; }

			public BindingGeneratorLanguage (string name, string extension, Func<BindingGenerator> creator)
			{
				if (String.IsNullOrEmpty (name))
					throw new ArgumentException (nameof (name));
				if (String.IsNullOrEmpty (extension))
					throw new ArgumentException (nameof (extension));
				Creator = creator ?? throw new ArgumentNullException (nameof (creator));
				Name = name;
				Extension = extension;
			}
		}

		internal static readonly BindingGeneratorLanguage DefaultOutputGenerator = new BindingGeneratorLanguage ("C#", ".cs", () => new CSharpBindingGenerator ());
		internal static readonly Dictionary <string, BindingGeneratorLanguage> KnownBindingGenerators = new Dictionary <string, BindingGeneratorLanguage> (StringComparer.OrdinalIgnoreCase) {
			{"C#", DefaultOutputGenerator},
		};

		public string OutputLanguage { get; set; }

		[Required]
		public string MonoAndroidCodeBehindDir { get; set; }

		[Required]
		public string AndroidFragmentType { get; set; }

		public string AppNamespace { get; set; }

		[Required]
		public ITaskItem [] ResourceFiles { get; set; }

		public ITaskItem [] PartialClassFiles { get; set; }

		[Output]
		public ITaskItem [] GeneratedFiles { get; set; }

		BindingGenerator GetBindingGenerator (string language)
		{
			BindingGeneratorLanguage gen;
			if (!KnownBindingGenerators.TryGetValue (language, out gen) || gen == null)
				return null;

			return gen.Creator ();
		}

		public async override System.Threading.Tasks.Task RunTaskAsync ()
		{
			if (String.IsNullOrWhiteSpace (OutputLanguage))
				OutputLanguage = DefaultOutputGenerator.Name;

			BindingGenerator generator = GetBindingGenerator (OutputLanguage);

			if (generator == null) {
				LogMessage ($"Unknown binding output language '{OutputLanguage}', will use {DefaultOutputGenerator.Name} instead");
				generator = DefaultOutputGenerator.Creator ();
			}

			if (generator == null) {
				// Should "never" happen
				LogCodedError ("XA4219", Properties.Resources.XA4219, OutputLanguage, DefaultOutputGenerator.Name);
				return;
			}

			generator.SetCodeBehindDir (MonoAndroidCodeBehindDir);

			LogDebugMessage ($"Generating {generator.LanguageName} binding sources");

			var layoutGroups = new Dictionary <string, LayoutGroup> (StringComparer.Ordinal);
			string layoutGroupName;
			LayoutGroup group;

			foreach (ITaskItem item in ResourceFiles) {
				if (item == null)
					continue;

				if (!GetRequiredMetadata (item, CalculateLayoutCodeBehind.LayoutGroupMetadata, out layoutGroupName))
					return;

				if (!layoutGroups.TryGetValue (layoutGroupName, out group) || group == null) {
					group = new LayoutGroup {
						Items = new List<ITaskItem> ()
					};
					layoutGroups [layoutGroupName] = group;
				}
				group.Items.Add (item);
			}

			if (PartialClassFiles != null && PartialClassFiles.Length > 0) {
				foreach (ITaskItem item in PartialClassFiles) {
					if (!GetRequiredMetadata (item, CalculateLayoutCodeBehind.LayoutGroupMetadata, out layoutGroupName))
						return;
					if (!layoutGroups.TryGetValue (layoutGroupName, out group) || group == null) {
						LogCodedError ("XA4220", Properties.Resources.XA4220, item.ItemSpec, layoutGroupName);
						return;
					}

					string partialClassName;
					if (!GetRequiredMetadata (item, CalculateLayoutCodeBehind.PartialCodeBehindClassNameMetadata, out partialClassName))
						return;

					string outputFileName;
					if (!GetRequiredMetadata (item, CalculateLayoutCodeBehind.LayoutPartialClassFileNameMetadata, out outputFileName))
						return;

					if (group.Classes == null)
						group.Classes = new List<PartialClass> ();
					if (group.ClassNames == null)
						group.ClassNames = new HashSet<string> ();

					if (group.ClassNames.Contains (partialClassName))
						continue;

					string shortName;
					string namespaceName;
					int idx = partialClassName.LastIndexOf ('.');
					if (idx >= 0) {
						shortName = partialClassName.Substring (idx + 1);
						namespaceName = partialClassName.Substring (0, idx);
					} else {
						shortName = partialClassName;
						namespaceName = null;
					}

					group.Classes.Add (new PartialClass {
						Name = shortName,
						Namespace = namespaceName,
						OutputFilePath = Path.Combine (MonoAndroidCodeBehindDir, outputFileName)
					});
					group.ClassNames.Add (partialClassName);
				}
			}

			IEnumerable<string> generatedFilePaths = null;
			if (ResourceFiles.Length >= CalculateLayoutCodeBehind.ParallelGenerationThreshold) {
				// NOTE: Update the tests in $TOP_DIR/tests/CodeBehind/UnitTests/BuildTests.cs if this message
				// is changed!
				LogDebugMessage ($"Generating binding code in parallel (threshold of {CalculateLayoutCodeBehind.ParallelGenerationThreshold} layouts met)");
				var fileSet = new ConcurrentBag <string> ();
				await this.WhenAll (layoutGroups, kvp => GenerateSourceForLayoutGroup (generator, kvp.Value, rpath => fileSet.Add (rpath)));
				generatedFilePaths = fileSet;
			} else {
				var fileSet = new List<string> ();
				foreach (var kvp in layoutGroups)
					GenerateSourceForLayoutGroup (generator, kvp.Value, rpath => fileSet.Add (rpath));
				generatedFilePaths = fileSet;
			}

			GeneratedFiles = generatedFilePaths.Where (gfp => !String.IsNullOrEmpty (gfp)).Select (gfp => new TaskItem (gfp)).ToArray ();
			if (GeneratedFiles.Length == 0)
				LogCodedWarning ("XA4221", Properties.Resources.XA4221);
			LogDebugTaskItems ("  GeneratedFiles:", GeneratedFiles);
		}

		void GenerateSourceForLayoutGroup (BindingGenerator generator, LayoutGroup group, Action <string> pathAdder)
		{
			List<ITaskItem> resourceItems = group?.Items;
			if (resourceItems == null || resourceItems.Count == 0)
				return;

			ITaskItem item = resourceItems.FirstOrDefault ();
			if (item == null)
				return;

			string collectionKey;
			if (!GetRequiredMetadata (item, CalculateLayoutCodeBehind.WidgetCollectionKeyMetadata, out collectionKey))
				return;

			ICollection<LayoutWidget> widgets = BuildEngine4.GetRegisteredTaskObjectAssemblyLocal<ICollection<LayoutWidget>> (ProjectSpecificTaskObjectKey (collectionKey), RegisteredTaskObjectLifetime.Build);
			if ((widgets?.Count ?? 0) == 0) {
				string inputPaths = String.Join ("; ", resourceItems.Select (i => i.ItemSpec));
				LogCodedWarning ("XA4222", Properties.Resources.XA4222, inputPaths);
				return;
			}

			string outputFileName;
			if (!GetRequiredMetadata (item, CalculateLayoutCodeBehind.LayoutBindingFileNameMetadata, out outputFileName))
				return;

			string fullClassName;
			if (!GetRequiredMetadata (item, CalculateLayoutCodeBehind.ClassNameMetadata, out fullClassName))
				return;

			string outputFilePath = Path.Combine (MonoAndroidCodeBehindDir, outputFileName);
			string classNamespace;
			string className;
			int idx = fullClassName.LastIndexOf ('.');
			if (idx < 0) {
				classNamespace = String.Empty;
				className = fullClassName;
			} else {
				bool fail = false;
				classNamespace = fullClassName.Substring (0, idx);
				if (String.IsNullOrEmpty (classNamespace)) {
					LogCodedError ("XA4223", Properties.Resources.XA4223, fullClassName);
					fail = true;
				}

				className = fullClassName.Substring (idx + 1);
				if (String.IsNullOrEmpty (className)) {
					LogCodedError ("XA4224", Properties.Resources.XA4224, fullClassName);
					fail = true;
				}

				if (fail)
					return;
			}

			if (!GenerateSource (generator, outputFilePath, widgets, classNamespace, className, group.Classes))
				return;

			pathAdder (outputFilePath);
		}

		bool GenerateSource (BindingGenerator generator, string outputFilePath, ICollection <LayoutWidget> widgets, string classNamespace, string className, List<PartialClass> partialClasses)
		{
			bool result = false;
			var tempFile = Path.GetTempFileName ();
			try {
				if (partialClasses != null && partialClasses.Count > 0) {
					foreach (var pc in partialClasses) {
						if (pc == null)
							continue;

						pc.Stream = File.Open (pc.OutputFilePath, FileMode.Create);
						pc.Writer = new StreamWriter (pc.Stream, Encoding.UTF8);
					}
				}

				using (var fs = File.Open (tempFile, FileMode.Create)) {
					using (var sw = new StreamWriter (fs, Encoding.UTF8)) {
						result = GenerateSource (sw, generator, widgets, classNamespace, className, partialClasses);
					}
				}
				if (result)
					Files.CopyIfChanged (tempFile, outputFilePath);
			} finally {
				if (File.Exists (tempFile))
					File.Delete (tempFile);
				if (partialClasses != null && partialClasses.Count > 0) {
					foreach (var pc in partialClasses) {
						if (pc == null)
							continue;

						if (pc.Writer != null) {
							pc.Writer.Close ();
							pc.Writer.Dispose ();
						}

						if (pc.Stream == null)
							continue;

						pc.Stream.Close ();
						pc.Stream.Dispose ();
					}
				}
			}
			return result;
		}

		bool GenerateSource (StreamWriter writer, BindingGenerator generator, ICollection <LayoutWidget> widgets, string classNamespace, string className, List<PartialClass> partialClasses)
		{
			bool havePartialClasses = partialClasses != null && partialClasses.Count > 0;
			string ns = AppNamespace == null ? String.Empty : $"{AppNamespace}.";

			if (havePartialClasses) {
				string fullBindingClassName = $"{classNamespace}.{className}";
				WriteToPartialClasses (pc => pc.State = generator.BeginPartialClassFile (pc.Writer, fullBindingClassName, pc.Namespace, pc.Name, AndroidFragmentType));
			}

			var state = generator.BeginBindingFile (writer, $"global::{ns}Resource.Layout.{className}", classNamespace, className, AndroidFragmentType);
			foreach (LayoutWidget widget in widgets) {
				DetermineWidgetType (widget, widget.Type == null);
				if (widget.Type == null) {
					widget.TypeFixups = null; // Not needed - we'll use decayed type
					string decayedType = null;
					switch (widget.WidgetType) {
						case LayoutWidgetType.View:
							decayedType = "global::Android.Views.View";
							break;

						case LayoutWidgetType.Fragment:
							decayedType = $"global::{AndroidFragmentType}";
							break;

						case LayoutWidgetType.Mixed:
							decayedType = "object";
							break;

						default:
							throw new InvalidOperationException ($"Widget {widget.Name} is of unknown type {widget.WidgetType}");
					}
					widget.Type = decayedType;
					LogCodedWarning ("XA4225", Properties.Resources.XA4225, widget.Name, className, decayedType);
				}

				if (String.IsNullOrWhiteSpace (widget.Type))
					throw new InvalidOperationException ($"Widget {widget.Name} does not have a type");

				generator.WriteBindingProperty (state, widget, ns);
				WriteToPartialClasses (pc => generator.WritePartialClassProperty (pc.State, widget));
			}
			generator.EndBindingFile (state);
			WriteToPartialClasses (pc => generator.EndPartialClassFile (pc.State));

			return true;

			void WriteToPartialClasses (Action<PartialClass> code)
			{
				if (!havePartialClasses)
					return;
				foreach (var pc in partialClasses) {
					if (pc == null)
						continue;
					code (pc);
				}
			}
		}

		void DetermineWidgetType (LayoutWidget widget, bool needsFullCheck)
		{
			if (!needsFullCheck && widget.WidgetType != LayoutWidgetType.Unknown)
				return;

			if (widget.AllTypes.All (wt => wt == LayoutWidgetType.View))
				widget.WidgetType = LayoutWidgetType.View;
			else if (widget.AllTypes.All (wt => wt == LayoutWidgetType.Fragment))
				widget.WidgetType = LayoutWidgetType.Fragment;
			else
				widget.WidgetType = LayoutWidgetType.Mixed;
		}

		bool GetRequiredMetadata (ITaskItem resourceItem, string metadataName, out string metadataValue)
		{
			metadataValue = resourceItem.GetMetadata (metadataName);
			if (String.IsNullOrEmpty (metadataValue)) {
				LogCodedError ("XA4226", Properties.Resources.XA4226, resourceItem.ItemSpec, metadataName);
				return false;
			}

			return true;
		}
	}
}
