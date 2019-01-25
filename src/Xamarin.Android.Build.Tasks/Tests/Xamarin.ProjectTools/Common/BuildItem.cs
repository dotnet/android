using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using System.Text;

namespace Xamarin.ProjectTools
{
	public class BuildItem
	{
		public class Source : BuildItem
		{
			public Source (string include)
				: this (() => include)
			{
			}
			public Source (Func<string> include)
				: base (BuildActions.Compile, include)
			{
			}
		}

		public class NoActionResource : BuildItem
		{
			public NoActionResource (string include)
				: this (() => include)
			{
			}
			public NoActionResource (Func<string> include)
				: base (BuildActions.None, include)
			{
			}
		}

		public class Folder : BuildItem
		{
			public Folder (string include)
				: this (() => include)
			{
			}
			public Folder (Func<string> include)
				: base (BuildActions.Folder, include)
			{
			}
		}

		public class Content : BuildItem
		{
			public Content (string include)
				: this (() => include)
			{
			}

			public Content (Func<string> include)
				: base (BuildActions.Content, include)
			{
			}
		}

		public class Reference : BuildItem
		{
			public Reference (string include)
				: this (() => include)
			{
			}
			public Reference (Func<string> include)
				: base (BuildActions.Reference, include)
			{
			}
		}

		public class ProjectReference : BuildItem
		{
			public ProjectReference (string include, string name = "UnnamedReference", string guid = null)
				: this (() => include, name, guid)
			{
			}
			public ProjectReference (Func<string> include, string name = "UnnamedReference", string guid = null)
				: base (BuildActions.ProjectReference, include)
			{
				Metadata.Add ("Project", "{" + (guid ?? Guid.NewGuid ().ToString ()) + "}");
				Metadata.Add ("Name", name);
			}
		}

		public BuildItem (string buildAction, string include)
			: this (buildAction, () => include)
		{
		}

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

		public DateTimeOffset? Timestamp { get; set; }
		public string BuildAction { get; set; }
		public Func<string> Include { get; set; }
		public Func<string> Remove { get; set; }
		public Func<string> Update { get; set; }
		public Func<string> SubType { get; set; }
		public Func<string> Generator { get; set; }
		public Func<string> DependentUpon { get; set; }
		public Func<string> Version { get; set; }
		public IDictionary<string,string> Metadata { get; private set; }
		public Func<string> TextContent { get; set; }
		public Func<byte[]> BinaryContent { get; set; }
		public Encoding Encoding { get; set; }
		public bool Deleted { get; set; }
		public FileAttributes Attributes { get; set;}

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
		/// NOTE: downloads a file from our https://xamjenkinsartifact.azureedge.net/ Azure Storage Account
		/// </summary>
		public string WebContentFileNameFromAzure {
			get { throw new NotSupportedException (); }
			set { WebContent = $"https://xamjenkinsartifact.azureedge.net/mono-jenkins/xamarin-android-test/{value}"; }
		}

		public string MetadataValues {
			get { return string.Join (";", Metadata.Select (p => p.Key + '=' + p.Value)); }
			set {
				foreach (var p in value.Split (';').Select (i => i.Split ('=')).Select (a => new KeyValuePair<string,string> (a [0], a [1])))
					Metadata.Add (p.Key, p.Value);
			}
		}

		public override string ToString ()
		{
			return Include ();
		}
	}
}
