using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public sealed class CodeGenDiff
	{
		public static List<string> GenerateMissingItems (string codeGenPath, string contractAssembly, string implementationAssembly)
		{
			var contract = GenerateObjectDescription (codeGenPath, contractAssembly);
			var implementation = GenerateObjectDescription (codeGenPath, implementationAssembly);

			return Diff (contract, implementation);
		}

		static List<string> Diff (ObjectDescription contract, ObjectDescription implementation)
		{
			System.Diagnostics.Trace.Assert (contract.Item == implementation.Item, "Comparing object should be the same.");

			var missingItems = new List<string> ();
			var internalMissingItems = new List<string> ();

			foreach (var obj in contract.InnerObjects) {
				var implObj = implementation.InnerObjects.SingleOrDefault (i => i.Item == obj.Item);
				if (implObj == null) {
					internalMissingItems.Add ($"-{obj.Item}");
					continue;
				}

				internalMissingItems.AddRange (Diff (obj, implObj));
			}

			foreach (var att in contract.Attributes) {
				if (!implementation.Attributes.Contains (att)) {
					missingItems.Add ($"-{att}");
				}
			}

			if (contract.Item == "root") {
				return internalMissingItems;
			}

			if (internalMissingItems.Any () || missingItems.Any ()) {
				missingItems.Add ($"{contract.Item}");
				if (internalMissingItems.Any ()) {
					missingItems.Add ("{");
					missingItems.AddRange (internalMissingItems);
					missingItems.Add ("}");
				}
			}

			return missingItems;
		}

		static ObjectDescription GenerateObjectDescription (string codeGenPath, string assembly)
		{
			ObjectDescription currentObject = new ObjectDescription () { Item = "root" };
			var objectStack = new Stack<ObjectDescription> ();
			objectStack.Push (currentObject);

			using (var genApiProcess = new Process ()) {

				if (Environment.Version.Major >= 5) {
					var apiCompat = new FileInfo (Path.Combine (codeGenPath, "..", "netcoreapp3.1", "Microsoft.DotNet.GenAPI.dll"));
					genApiProcess.StartInfo.FileName = "dotnet";
					genApiProcess.StartInfo.Arguments = $"\"{apiCompat}\" ";
				} else {
					var apiCompat = new FileInfo (Path.Combine (codeGenPath, "Microsoft.DotNet.GenAPI.exe"));
					genApiProcess.StartInfo.FileName = apiCompat.FullName;
				}

				genApiProcess.StartInfo.Arguments += $"\"{assembly}\"";

				genApiProcess.StartInfo.UseShellExecute = false;
				genApiProcess.StartInfo.CreateNoWindow = true;
				genApiProcess.StartInfo.RedirectStandardOutput = true;
				genApiProcess.StartInfo.RedirectStandardError = true;
				genApiProcess.EnableRaisingEvents = true;

				var line = 0;

				void dataReceived (object sender, DataReceivedEventArgs args)
				{
					line++;
					var content = args.Data?.Trim ();

					if (string.IsNullOrWhiteSpace (content) || content.StartsWith ("//", StringComparison.OrdinalIgnoreCase) || content.StartsWith ("Unable to resolve assembly", StringComparison.OrdinalIgnoreCase)) {
						return;
					}

					if (content.StartsWith ("[", StringComparison.OrdinalIgnoreCase)) {
						if (!string.IsNullOrWhiteSpace (currentObject.Item)) {
							var newObject = new ObjectDescription ();
							currentObject.InnerObjects.Add (newObject);
							objectStack.Push (newObject);
							currentObject = newObject;
						}

						currentObject.Attributes.Add (content);
						return;
					}

					if (content.StartsWith ("{", StringComparison.OrdinalIgnoreCase)) {
						currentObject.InternalCounter++;
						return;
					}

					if (content.StartsWith ("}", StringComparison.OrdinalIgnoreCase)) {
						currentObject.InternalCounter--;
						System.Diagnostics.Debug.Assert (currentObject.InternalCounter >= 0);
						if (currentObject.InternalCounter == 0) {
							objectStack.Pop ();
							if (objectStack.Count > 0) {
								currentObject = objectStack.Peek ();
							}
						}

						return;
					}

					if (content.StartsWith ("namespace ", StringComparison.Ordinal) || content.IndexOf (" interface ", StringComparison.Ordinal) != -1 || content.IndexOf (" class ", StringComparison.Ordinal) != -1 || content.IndexOf (" partial struct ", StringComparison.Ordinal) != -1 || content.IndexOf (" enum ", StringComparison.Ordinal) != -1) {
						if (string.IsNullOrWhiteSpace (currentObject.Item)) {
							currentObject.Item = content;
						} else {
							var newObject = new ObjectDescription () { Item = content };
							currentObject.InnerObjects.Add (newObject);
							objectStack.Push (newObject);
							currentObject = newObject;
						}

						return;
					}


					if (string.IsNullOrWhiteSpace (currentObject.Item)) {
						currentObject.Item = content;
						objectStack.Pop ();
						currentObject = objectStack.Peek ();
					} else {
						var newObject = new ObjectDescription () { Item = content };
						currentObject.InnerObjects.Add (newObject);
					}
				}


				genApiProcess.OutputDataReceived += dataReceived;
				genApiProcess.ErrorDataReceived += dataReceived;

				genApiProcess.Start ();
				genApiProcess.BeginOutputReadLine ();
				genApiProcess.BeginErrorReadLine ();

				genApiProcess.WaitForExit ();

				genApiProcess.CancelOutputRead ();
				genApiProcess.CancelErrorRead ();

			}

			return currentObject;
		}

		class ObjectDescription
		{
			public string Item;

			public HashSet<string> Attributes = new HashSet<string> ();

			public List<ObjectDescription> InnerObjects = new List<ObjectDescription> ();

			public int InternalCounter;
		}
	}
}
