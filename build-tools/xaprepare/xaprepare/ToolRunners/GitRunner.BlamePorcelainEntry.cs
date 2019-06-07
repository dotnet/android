using System;
using System.Collections.Generic;

namespace Xamarin.Android.Prepare
{
	partial class GitRunner
	{
		sealed class BlameParserState
		{
			public BlamePorcelainEntry CurrentEntry;
			public List<BlamePorcelainEntry> Entries;
		}

		// Encompasses the `git blame -p` output as documented in https://www.git-scm.com/docs/git-blame#_the_porcelain_format
		public class BlamePorcelainEntry
		{
			static readonly char[] FieldSeparator = new [] { ' ' };

			// Standard/known headers
			public string Author                            { get; private set; }
			public string AuthorMail                        { get; private set; }
			public uint AuthorTime                          { get; private set; }
			public string AuthorTZ                          { get; private set; }

			public string Committer                         { get; private set; }
			public string CommitterMail                     { get; private set; }
			public uint CommitterTime                       { get; private set; }
			public string CommitterTZ                       { get; private set; }

			public string Summary                           { get; private set; }
			public string PreviousCommit                    { get; private set; }
			public string PreviousCommitFile                { get; private set; }
			public string Filename                          { get; private set; }

			// Unknown headers
			public IDictionary<string, string> OtherHeaders { get; private set; }

			public string Commit                            { get; private set; }
			public int OriginalFileLine                     { get; private set; }
			public int FinalFileLine                        { get; private set; }
			public int NumberOfLinesInGroup                 { get; private set; }

			// Contents of the changed line
			public string Line                              { get; private set; }

			public bool ProcessLine (string line)
			{
				if (String.IsNullOrEmpty (line))
					return false;

				if (String.IsNullOrEmpty (Commit)) {
					ParseCommit (line);
					return false;
				}

				if (line [0] == '\t') {
					Line = line.Substring (1);
					// Means we've parsed the last line in the record and our job is done
					return true;
				}

				StoreHeader (line);
				return false;
			}

			void StoreHeader (string line)
			{
				string[] parts = line.Split (FieldSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length != 2)
					throw new InvalidOperationException ($"Unexpected commit header format (wrong number of fields): {line}");

				if (IsHeaderName ("author")) {
					Author = parts [1];
					return;
				}

				if (IsHeaderName ("author-mail")) {
					AuthorMail = parts [1];
					return;
				}

				if (IsHeaderName ("author-time")) {
					AuthorTime = UInt32.Parse (parts [1]);
					return;
				}

				if (IsHeaderName ("author-tz")) {
					AuthorTZ = parts [1];
					return;
				}

				if (IsHeaderName ("committer")) {
					Committer = parts [1];
					return;
				}

				if (IsHeaderName ("committer-mail")) {
					CommitterMail = parts [1];
					return;
				}

				if (IsHeaderName ("committer-time")) {
					CommitterTime = UInt32.Parse (parts [1]);
					return;
				}

				if (IsHeaderName ("committer-tz")) {
					CommitterTZ = parts [1];
					return;
				}

				if (IsHeaderName ("summary")) {
					Summary = parts [1];
					return;
				}

				if (IsHeaderName ("previous")) {
					string[] previous_parts = parts [1].Split (FieldSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
					if (previous_parts.Length != 2)
						throw new InvalidOperationException ($"Unexpected previous commmit header format (not enough fields): {parts[1]}");
					PreviousCommit = previous_parts [0];
					PreviousCommitFile = previous_parts [1];
					return;
				}

				if (IsHeaderName ("filename")) {
					Filename = parts [1];
					return;
				}

				if (OtherHeaders == null)
					OtherHeaders = new SortedDictionary <string, string> (StringComparer.Ordinal);
				OtherHeaders [parts [0]] = parts [1];

				bool IsHeaderName (string name)
				{
					return String.Compare (name, parts [0], StringComparison.Ordinal) == 0;
				}
			}

			void ParseCommit (string line)
			{
				string[] parts = line.Split (FieldSeparator, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length < 3)
					throw new InvalidOperationException ($"Unexpected commit line format (not enough fields): {line}");

				Commit = parts [0];
				OriginalFileLine = Int32.Parse (parts [1]);
				FinalFileLine = Int32.Parse (parts [2]);
				if (parts.Length > 3)
					NumberOfLinesInGroup = Int32.Parse (parts [3]);
				else
					NumberOfLinesInGroup = 0;
			}
		}
	}
}
