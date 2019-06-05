using System.Collections.Generic;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// A project class for use when a project already exists on disk
	/// </summary>
	public class ExistingProject : XamarinProject
	{
		public override string ProjectTypeGuid => string.Empty;

		public override List<ProjectResource> Save (bool saveProject = true) => new List<ProjectResource> ();

		public override void UpdateProjectFiles (string directory, IEnumerable<ProjectResource> projectFiles, bool doNotCleanup = false) { }
	}
}
