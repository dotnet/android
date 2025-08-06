using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using System.Text;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Represents a build item in an MSBuild project, such as source files, resources, references, etc.
	/// This class provides a flexible way to define project items with their build actions, metadata, 
	/// and content for use in test projects.
	/// </summary>
	/// <remarks>
	/// Build items correspond to MSBuild's &lt;ItemGroup&gt; elements and can represent various
	/// types of project artifacts. Use the nested classes for common build item types.
	/// </remarks>
	/// <seealso cref="XamarinProject"/>
	/// <seealso cref="BuildActions"/>
	public class BuildItem
	{
		/// <summary>
		/// Represents a source code file with the Compile build action.
		/// </summary>
		/// <seealso cref="BuildActions.Compile"/>
		public class Source : BuildItem
		{
			/// <summary>
			/// Initializes a new instance of the Source class with the specified include path.
			/// </summary>
			/// <param name="include">The path to the source file.</param>
			public Source (string include)
				: this (() => include)
			{
			}
			
			/// <summary>
			/// Initializes a new instance of the Source class with a function that provides the include path.
			/// </summary>
			/// <param name="include">A function that returns the path to the source file.</param>
			public Source (Func<string> include)
				: base (BuildActions.Compile, include)
			{
			}
		}

		/// <summary>
		/// Represents a file with no build action (None).
		/// </summary>
		/// <seealso cref="BuildActions.None"/>
		public class NoActionResource : BuildItem
		{
			/// <summary>
			/// Initializes a new instance of the NoActionResource class with the specified include path.
			/// </summary>
			/// <param name="include">The path to the file.</param>
			public NoActionResource (string include)
				: this (() => include)
			{
			}
			
			/// <summary>
			/// Initializes a new instance of the NoActionResource class with a function that provides the include path.
			/// </summary>
			/// <param name="include">A function that returns the path to the file.</param>
			public NoActionResource (Func<string> include)
				: base (BuildActions.None, include)
			{
			}
		}

		/// <summary>
		/// Represents a folder in the project with the Folder build action.
		/// </summary>
		/// <seealso cref="BuildActions.Folder"/>
		public class Folder : BuildItem
		{
			/// <summary>
			/// Initializes a new instance of the Folder class with the specified include path.
			/// </summary>
			/// <param name="include">The path to the folder.</param>
			public Folder (string include)
				: this (() => include)
			{
			}
			
			/// <summary>
			/// Initializes a new instance of the Folder class with a function that provides the include path.
			/// </summary>
			/// <param name="include">A function that returns the path to the folder.</param>
			public Folder (Func<string> include)
				: base (BuildActions.Folder, include)
			{
			}
		}

		/// <summary>
		/// Represents a content file with the Content build action.
		/// </summary>
		/// <seealso cref="BuildActions.Content"/>
		/// <summary>
		/// Represents a content file with the Content build action.
		/// </summary>
		/// <seealso cref="BuildActions.Content"/>
		public class Content : BuildItem
		{
			/// <summary>
			/// Initializes a new instance of the Content class with the specified include path.
			/// </summary>
			/// <param name="include">The path to the content file.</param>
			public Content (string include)
				: this (() => include)
			{
			}

			/// <summary>
			/// Initializes a new instance of the Content class with a function that provides the include path.
			/// </summary>
			/// <param name="include">A function that returns the path to the content file.</param>
			public Content (Func<string> include)
				: base (BuildActions.Content, include)
			{
			}
		}

		/// <summary>
		/// Represents an assembly reference with the Reference build action.
		/// </summary>
		/// <seealso cref="BuildActions.Reference"/>
		public class Reference : BuildItem
		{
			/// <summary>
			/// Initializes a new instance of the Reference class with the specified include path.
			/// </summary>
			/// <param name="include">The path or name of the assembly reference.</param>
			public Reference (string include)
				: this (() => include)
			{
			}
			
			/// <summary>
			/// Initializes a new instance of the Reference class with a function that provides the include path.
			/// </summary>
			/// <param name="include">A function that returns the path or name of the assembly reference.</param>
			public Reference (Func<string> include)
				: base (BuildActions.Reference, include)
			{
			}
		}

		/// <summary>
		/// Represents a project reference with the ProjectReference build action.
		/// </summary>
		/// <seealso cref="BuildActions.ProjectReference"/>
		public class ProjectReference : BuildItem
		{
			/// <summary>
			/// Initializes a new instance of the ProjectReference class with the specified include path and metadata.
			/// </summary>
			/// <param name="include">The relative path to the referenced project file.</param>
			/// <param name="name">The name of the referenced project (default: "UnnamedReference").</param>
			/// <param name="guid">The GUID of the referenced project (auto-generated if null).</param>
			public ProjectReference (string include, string name = "UnnamedReference", string guid = null)
				: this (() => include, name, guid)
			{
			}
			
			/// <summary>
			/// Initializes a new instance of the ProjectReference class with a function that provides the include path.
			/// </summary>
			/// <param name="include">A function that returns the relative path to the referenced project file.</param>
			/// <param name="name">The name of the referenced project (default: "UnnamedReference").</param>
			/// <param name="guid">The GUID of the referenced project (auto-generated if null).</param>
			public ProjectReference (Func<string> include, string name = "UnnamedReference", string guid = null)
				: base (BuildActions.ProjectReference, include)
			{
				Metadata.Add ("Project", "{" + (guid ?? Guid.NewGuid ().ToString ()) + "}");
				Metadata.Add ("Name", name);
			}
		}

		/// <summary>
		/// Initializes a new instance of the BuildItem class with the specified build action and include path.
		/// </summary>
		/// <param name="buildAction">The MSBuild action for this item (e.g., "Compile", "Content", "None").</param>
		/// <param name="include">The path or identifier for the item.</param>
		/// <seealso cref="BuildActions"/>
		public BuildItem (string buildAction, string include)
			: this (buildAction, () => include)
		{
		}

		/// <summary>
		/// Initializes a new instance of the BuildItem class with the specified build action and a function that provides the include path.
		/// </summary>
		/// <param name="buildAction">The MSBuild action for this item (e.g., "Compile", "Content", "None").</param>
		/// <param name="include">A function that returns the path or identifier for the item.</param>
		/// <seealso cref="BuildActions"/>
		public BuildItem (string buildAction, Func<string> include = null)
		{
			BuildAction = buildAction;
			Include = include;
			Metadata = new Dictionary<string, string> ();
			Timestamp = DateTimeOffset.UtcNow;
			Encoding = Encoding.UTF8;
			Attributes = FileAttributes.Normal;
			Generator = null;
			Remove = null;
			SubType = null;
			Update = null;
			DependentUpon = null;
			Version = null;
		}

		/// <summary>
		/// Gets or sets the timestamp for the item, used for incremental build decisions.
		/// </summary>
		public DateTimeOffset? Timestamp { get; set; }
		
		/// <summary>
		/// Gets or sets the MSBuild action for this item (e.g., "Compile", "Content", "None").
		/// </summary>
		/// <seealso cref="BuildActions"/>
		public string BuildAction { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the include path or identifier for the item.
		/// </summary>
		public Func<string> Include { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the path for items to be removed from the project.
		/// </summary>
		public Func<string> Remove { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the path for items to be updated in the project.
		/// </summary>
		public Func<string> Update { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the SubType metadata for the item.
		/// </summary>
		public Func<string> SubType { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the custom tool generator for the item.
		/// </summary>
		public Func<string> Generator { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the file this item depends upon.
		/// </summary>
		public Func<string> DependentUpon { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the version metadata for the item.
		/// </summary>
		public Func<string> Version { get; set; }
		
		/// <summary>
		/// Gets the metadata dictionary for this build item.
		/// Contains key-value pairs that become MSBuild metadata on the item.
		/// </summary>
		public IDictionary<string,string> Metadata { get; private set; }
		
		/// <summary>
		/// Gets or sets a function that returns the text content for the file represented by this item.
		/// </summary>
		/// <seealso cref="BinaryContent"/>
		public Func<string> TextContent { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the binary content for the file represented by this item.
		/// </summary>
		/// <seealso cref="TextContent"/>
		public Func<byte[]> BinaryContent { get; set; }
		
		/// <summary>
		/// Gets or sets the text encoding for the file content.
		/// </summary>
		public Encoding Encoding { get; set; }
		
		/// <summary>
		/// Gets or sets a value indicating whether this item should be deleted from the project.
		/// </summary>
		public bool Deleted { get; set; }
		
		/// <summary>
		/// Gets or sets the file attributes for the file represented by this item.
		/// </summary>
		/// <summary>
		/// Gets or sets the file attributes for the file represented by this item.
		/// </summary>
		public FileAttributes Attributes { get; set;}

		/// <summary>
		/// Sets the binary content by downloading from a web URL.
		/// </summary>
		/// <exception cref="NotSupportedException">Thrown when attempting to get the value.</exception>
		/// <seealso cref="WebContentFileNameFromAzure"/>
		/// <seealso cref="DownloadedCache"/>
		public string WebContent {
			get { throw new NotSupportedException (); }
			set {
				BinaryContent = () => {
					var file = new DownloadedCache ().GetAsFile (value);
					return File.ReadAllBytes (file);
				};
			}
		}

		/// <summary>
		/// NOTE: downloads a file from our https://github.com/dellis1972/xamarin-android-unittest-files repo
		/// </summary>
		/// <exception cref="NotSupportedException">Thrown when attempting to get the value.</exception>
		/// <seealso cref="WebContent"/>
		public string WebContentFileNameFromAzure {
			get { throw new NotSupportedException (); }
			set { WebContent = $"https://github.com/dellis1972/xamarin-android-unittest-files/blob/main/{value}?raw=true"; }
		}

		/// <summary>
		/// Gets or sets the metadata values as a semicolon-separated string of key=value pairs.
		/// </summary>
		/// <seealso cref="Metadata"/>
		public string MetadataValues {
			get { return string.Join (";", Metadata.Select (p => p.Key + '=' + p.Value)); }
			set {
				foreach (var p in value.Split (';').Select (i => i.Split ('=')).Select (a => new KeyValuePair<string,string> (a [0], a [1])))
					Metadata.Add (p.Key, p.Value);
			}
		}

		/// <summary>
		/// Returns the include path of this build item.
		/// </summary>
		/// <returns>The include path or an empty string if Include is null.</returns>
		public override string ToString ()
		{
			return Include ();
		}
	}
}
