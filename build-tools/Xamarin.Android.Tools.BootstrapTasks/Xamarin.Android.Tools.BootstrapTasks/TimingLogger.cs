using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

using Microsoft.Build;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class TimingLogger : Logger
	{
		class LogEvent
		{
			public XmlElement Element;
			public DateTime Start;
		}

		const string DefaultOutputFileName = "xa_timing.xml";

		string outputFileName;
		string buildID;
		string buildDescription;
		string buildCommitHash;
		bool overwriteOutputFile;

		XmlDocument doc;
		Stack<LogEvent> events;
		bool haveSolutionDirectory;
		XmlElement toolsets;
		XmlElement files;
		int fileID = 0;

		Dictionary<string, int> fileCache = new Dictionary<string, int> (StringComparer.OrdinalIgnoreCase);

		public override void Initialize (IEventSource eventSource)
		{
			ParseParameters (Parameters?.Split (';'));

			if (String.IsNullOrEmpty (outputFileName))
				outputFileName = DefaultOutputFileName;

			if (String.IsNullOrEmpty (buildID))
				buildID = MakeDefaultBuildId ();

			eventSource.BuildStarted += OnBuildStarted;
			eventSource.BuildFinished += OnBuildFinished;
			eventSource.ProjectStarted += OnProjectStarted;
			eventSource.ProjectFinished += OnProjectFinished;
			eventSource.TargetStarted += OnTargetStarted;
			eventSource.TargetFinished += OnTargetFinished;
			eventSource.TaskStarted += OnTaskStarted;
			eventSource.TaskFinished += OnTaskFinished;

			events = new Stack<LogEvent> ();
			doc = new XmlDocument ();
			XmlElement element = doc.CreateElement ("builds");
			doc.AppendChild (element);

			toolsets = doc.CreateElement ("toolsets");
			foreach (Toolset toolset in ProjectCollection.GlobalProjectCollection.Toolsets) {
				XmlElement toolsetElement = doc.CreateElement ("toolset");
				AddAttribute (toolsetElement, "version", toolset.ToolsVersion);
				AddAttribute (toolsetElement, "path", toolset.ToolsPath);
				toolsets.AppendChild (toolsetElement);
			}

			files = doc.CreateElement ("files");
		}

		void SaveLog ()
		{
			bool appendToExistingFile = false;
			if (File.Exists (outputFileName)) {
				if (overwriteOutputFile)
					File.Delete (outputFileName);
				else
					appendToExistingFile = true;
			}

			var settings = new XmlWriterSettings {
				CheckCharacters = true,
				Encoding = Encoding.UTF8,
				Indent = true,
				IndentChars = "\t",
				NewLineHandling = NewLineHandling.Entitize,
				NewLineOnAttributes = false,
				OmitXmlDeclaration = false,
			};

			if (appendToExistingFile) {
				Console.WriteLine ("Appending to existing file");
				// TODO: load the file and append all the children of our doc.DocumentElement to the
				// loaded document's root
				var oldDoc = new XmlDocument ();
				oldDoc.Load (outputFileName);
				foreach (XmlNode node in doc.DocumentElement.ChildNodes) {
					XmlNode newNode = oldDoc.ImportNode (node, true);
					oldDoc.DocumentElement.AppendChild (newNode);
				}
				doc = oldDoc;
			}

			using (var stream = File.Open (outputFileName, FileMode.OpenOrCreate)) {
				using (XmlWriter xw = XmlWriter.Create (stream, settings)) {
					doc.Save (xw);
				}
			}
		}

		void OnBuildStarted (object sender, BuildStartedEventArgs args)
		{
			XmlElement e = StartElement ("build", args);
			AddAttribute (e, "id", buildID);
			AddAttribute (e, "commit", buildCommitHash ?? String.Empty);
			AddAttribute (e, "description", buildDescription ?? String.Empty);
			e.AppendChild (toolsets);
			e.AppendChild (files);
		}

		void OnBuildFinished (object sender, BuildFinishedEventArgs args)
		{
			XmlElement e = EndElement ("build", args);
			AddAttribute (e, "succeeded", args.Succeeded.ToString ());

			foreach (var kvp in fileCache) {
				XmlElement fe = doc.CreateElement ("file");
				AddAttribute (fe, "id", kvp.Value.ToString ());
				AddAttribute (fe, "path", kvp.Key);

				files.AppendChild (fe);
			}
			SaveLog ();
		}

		void OnProjectStarted (object sender, ProjectStartedEventArgs args)
		{
			string solutionDir = null;
			if (!haveSolutionDirectory) {
				if (String.Compare (".sln", Path.GetExtension (args.ProjectFile), StringComparison.OrdinalIgnoreCase) == 0) {
					solutionDir = Path.GetDirectoryName (args.ProjectFile) + Path.DirectorySeparatorChar;
					haveSolutionDirectory = true;
				}
			}

			XmlElement e = StartElement ("project", args);
			if (!String.IsNullOrEmpty (solutionDir))
				AddAttribute (e, "solution-dir", solutionDir);
			AddAttribute (e, "file-id", ShortenFilePath (args.ProjectFile));
			//NOTE: MissingMethodException on xbuild
			//AddAttribute (e, "id", args.ProjectId.ToString ());
		}

		void OnProjectFinished (object sender, ProjectFinishedEventArgs args)
		{
			XmlElement e = EndElement ("project", args);
			AddAttribute (e, "succeeded", args.Succeeded.ToString ());
		}

		void OnTargetStarted (object sender, TargetStartedEventArgs args)
		{
			XmlElement e = StartElement ("target", args);
			AddAttribute (e, "name", args.TargetName);
			AddAttribute (e, "file-id", ShortenFilePath (args.TargetFile));
		}

		void OnTargetFinished (object sender, TargetFinishedEventArgs args)
		{
			XmlElement e = EndElement ("target", args);
			AddAttribute (e, "succeeded", args.Succeeded.ToString ());
		}

		void OnTaskStarted (object sender, TaskStartedEventArgs args)
		{
			XmlElement e = StartElement ("task", args);
			AddAttribute (e, "name", args.TaskName);
			AddAttribute (e, "file-id", ShortenFilePath (args.TaskFile));
		}

		void OnTaskFinished (object sender, TaskFinishedEventArgs args)
		{
			XmlElement e = EndElement ("task", args);
			if (e == null)
				return;
			AddAttribute (e, "succeeded", args.Succeeded.ToString ());
		}

		XmlElement StartElement (string name, BuildEventArgs args)
		{
			XmlElement element = doc.CreateElement (name);
			var logEvent = new LogEvent {
				Element = element,
				Start = args.Timestamp
			};
			AppendCommonStartAttributes (logEvent, args);
			AddElementToTree (element);
			events.Push (logEvent);
			return element;
		}

		XmlElement EndElement (string name, BuildEventArgs args)
		{
			if (events.Count == 0)
				return null; // No need to break the build because we screwed up. TODO: add a warning or
							 // something
			LogEvent logEvent = events.Pop ();
			XmlElement element = logEvent.Element;
			if (element == null || String.Compare (name, element.Name, StringComparison.Ordinal) != 0)
				return null; // TODO: log a warning somehow
			AppendCommonEndAttributes (logEvent, args);
			return element;
		}

		XmlElement AddElementToTree (XmlElement element)
		{
			if (element == null)
				return null;

			XmlElement parent = null;
			if (events.Count > 0)
				parent = events.Peek ()?.Element;
			if (parent == null)
				parent = doc.DocumentElement;

			parent.AppendChild (element);
			return element;
		}

		void AppendCommonStartAttributes (LogEvent logEvent, BuildEventArgs args)
		{
			if (logEvent == null)
				return;

			XmlElement element = logEvent.Element;
			if (element == null)
				return;
			AddAttribute (element, GetTimeAttribute ("start-ticks", args));
			//AddAttribute (element, "start-message", args.Message ?? String.Empty);
			AddAttribute (element, "start-threadid", args.ThreadId.ToString ());
		}

		void AppendCommonEndAttributes (LogEvent logEvent, BuildEventArgs args)
		{
			if (logEvent == null)
				return;

			XmlElement element = logEvent.Element;
			if (element == null)
				return;
			AddAttribute (element, GetTimeAttribute ("end-ticks", args));
			//AddAttribute (element, "end-message", args.Message ?? String.Empty);
			AddAttribute (element, "end-threadid", args.ThreadId.ToString ());
			AddAttribute (element, "elapsed", (args.Timestamp - logEvent.Start).Duration ().ToString ());
		}

		void AddAttribute (XmlElement element, XmlAttribute attr)
		{
			if (element == null || attr == null)
				return;
			element.Attributes.Append (attr);
		}

		void AddAttribute (XmlElement element, string name, string value, string nameSpacePrefix = null)
		{
			AddAttribute (element, CreateAttribute (name, value, nameSpacePrefix));
		}

		XmlAttribute GetTimeAttribute (string name, BuildEventArgs args)
		{
			return CreateAttribute (name, $"{args.Timestamp.Ticks}");
		}

		XmlAttribute CreateAttribute (string name, string value, string nameSpacePrefix = null)
		{
			XmlAttribute attr = doc.CreateAttribute (name);
			attr.Value = value;
			if (!String.IsNullOrEmpty (nameSpacePrefix))
				attr.Prefix = nameSpacePrefix;
			return attr;
		}

		string MakeDefaultBuildId ()
		{
			// TODO: a date for now, think of some other stuff (host name, bot name etc etc)
			return DateTime.UtcNow.ToString (CultureInfo.InvariantCulture);
		}

		string ShortenFilePath (string filePath)
		{
			if (String.IsNullOrEmpty (filePath))
				return "0";

			if (fileCache.TryGetValue (filePath, out int id))
				return id.ToString ();
			fileID++;
			fileCache.Add (filePath, fileID);
			return fileID.ToString ();
		}

		void ParseParameters (string [] parameters)
		{
			if (parameters == null || parameters.Length == 0)
				return;

			foreach (string p in parameters) {
				string param = p?.Trim ();
				if (String.IsNullOrEmpty (param))
					continue;

				if (IsParameter ("OverwriteOutput", param)) {
					overwriteOutputFile = true;
					continue;
				}

				if (IsParameter ("OutputPath", param, ref outputFileName))
					continue;

				if (IsParameter ("Description", param, ref buildDescription))
					continue;

				if (IsParameter ("ID", param, ref buildID))
					continue;

				if (IsParameter ("Commit", param, ref buildCommitHash))
					continue;
			}
		}

		bool IsParameter (string name, string param)
		{
			return String.Compare (name, param, StringComparison.OrdinalIgnoreCase) == 0;
		}

		bool IsParameter (string name, string param, ref string value)
		{
			string [] parts = param.Split ('=');
			if (parts.Length < 2)
				throw new InvalidOperationException ($"Parameter '{name}' requires a value");

			if (!IsParameter (name, parts [0]))
				return false;

			if (parts.Length == 2)
				value = parts [1].Trim ();
			else
				value = String.Join ("=", parts, 1, parts.Length - 1).Trim ();

			if (String.IsNullOrEmpty (value))
				throw new InvalidOperationException ($"Parameter '{name}' requires a non-empty value");

			return true;
		}
	}
}
