using System;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Represents an MSBuild import statement that imports another MSBuild file or targets.
	/// Corresponds to MSBuild &lt;Import&gt; elements and allows including external
	/// build logic, properties, and targets in test projects.
	/// </summary>
	/// <remarks>
	/// Imports are commonly used to include shared build logic, .targets files,
	/// or .props files. This class can represent both file-based imports and
	/// imports with inline content for testing scenarios.
	/// Example: &lt;Import Project="My.targets" /&gt;
	/// </remarks>
	/// <seealso cref="XamarinProject.Imports"/>
	/// <seealso cref="XamarinProject.GetImport(string)"/>
	public class Import
	{
		/// <summary>
		/// Initializes a new instance of the Import class with a static project path.
		/// </summary>
		/// <param name="project">The path to the file or targets to import.</param>
		public Import (string project)
			: this (() => project)
		{
		}

		/// <summary>
		/// Initializes a new instance of the Import class with a dynamic project path.
		/// </summary>
		/// <param name="project">A function that returns the path to the file or targets to import.</param>
		public Import (Func<string> project) {
			Project = project;
			Timestamp = DateTimeOffset.UtcNow;
		}

		/// <summary>
		/// Gets or sets the timestamp for this import, used for incremental build decisions.
		/// </summary>
		public DateTimeOffset? Timestamp { get; set; }

		/// <summary>
		/// Gets or sets a function that returns the project path for the import.
		/// </summary>
		public Func<string> Project { get; set; }
		
		/// <summary>
		/// Gets or sets a function that returns the text content for the imported file.
		/// </summary>
		/// <remarks>
		/// If this is set, the import represents a file with inline content rather than
		/// an external file reference. The content will be written to the file specified
		/// by the Project path.
		/// </remarks>
		public Func<string> TextContent { get; set; }
	}
}

