using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace tmt
{
	class Report
	{
		const string FileFieldSeparator      = "\t";

		const string FormattedDuplicateColumnHeader    = "Is Duplicate?";
		const string FormattedGenericColumnHeader      = "Is Generic?";
		const string FormattedJavaTypeColumnHeader     = "Java type name";
		const string FormattedMVIDColumnHeader         = "MVID";
		const string FormattedManagedTypeColumnHeader  = "Managed type name";
		const string FormattedTokenIDColumnHeader      = "Type token ID";

		const string RawJavaTypeNameColumnHeader       = FormattedJavaTypeColumnHeader;
		const string RawManagedModuleIndexColumnHeader = "Managed module index";
		const string RawTypeTokenColumnHeader          = FormattedTokenIDColumnHeader;

		const string TableJavaToManagedTitle           = "Java to Managed";
		const string TableManagedToJavaTitle           = "Managed to Java";

		sealed class Column
		{
			int width = 0;

			public int Width         => width;
			public string Header     { get; }
			public List<string> Rows { get; } = new List<string> ();

			public Column (string header)
			{
				Header = header;
				width = header.Length;
			}

			public void Add (string rowValue)
			{
				if (rowValue.Length > width) {
					width = rowValue.Length;
				}

				Rows.Add (rowValue);
			}

			public void Add (bool rowValue)
			{
				Add (rowValue ? "true" : "false");
			}

			public void Add (uint rowValue)
			{
				Add ($"0x{rowValue:X08} ({rowValue})");
			}

			public void Add (Guid rowValue)
			{
				Add (rowValue.ToString ());
			}
		}

		sealed class Table
		{
			public Column Duplicate   { get; } = new Column (FormattedDuplicateColumnHeader);
			public Column Generic     { get; } = new Column (FormattedGenericColumnHeader);
			public Column JavaType    { get; } = new Column (FormattedJavaTypeColumnHeader);
			public Column MVID        { get; } = new Column (FormattedMVIDColumnHeader);
			public Column ManagedType { get; } = new Column (FormattedManagedTypeColumnHeader);
			public Column TokenID     { get; } = new Column (FormattedTokenIDColumnHeader);

			public string Title       { get; }
			public bool ManagedFirst  { get; }

			public Table (string title, bool managedFirst)
			{
				Title = title;
				ManagedFirst = managedFirst;
			}
		}

		string outputDirectory;
		Regex? filterRegex;
		bool full;
		bool onlyJava;
		bool onlyManaged;
		bool generateFiles;

		public Report (string outputDirectory, string filterRegex, bool full, bool onlyJava, bool onlyManaged, bool generateFiles)
		{
			this.outputDirectory = outputDirectory;
			this.full = full;
			this.onlyJava = onlyJava;
			this.onlyManaged = onlyManaged;
			this.generateFiles = generateFiles;

			if (filterRegex.Length > 0) {
				this.filterRegex = new Regex (filterRegex, RegexOptions.Compiled);
			}
		}

		public void Generate (ITypemap typemap)
		{
			Action<Table, MapEntry, bool> tableGenerator;
			Action<MapEntry, bool> consoleGenerator;
			var tables = new List<Table> ();
			bool filtering = filterRegex != null;
			Table table;

			if (!onlyManaged) {
				typemap.Map.JavaToManaged.Sort ((MapEntry left, MapEntry right) => left.JavaType.Name.CompareTo (right.JavaType.Name));
				table = new Table (TableJavaToManagedTitle, managedFirst: false);
				tables.Add (table);
				if (typemap.Map.Kind == MapKind.Release) {
					tableGenerator = TableGenerateJavaToManagedRelease;
					consoleGenerator = ConsoleGenerateJavaToManagedRelease;
				} else {
					tableGenerator = TableGenerateJavaToManagedDebug;
					consoleGenerator = ConsoleGenerateJavaToManagedDebug;
				}

				Generate ("Java to Managed", table, typemap.Map.JavaToManaged, full, tableGenerator, consoleGenerator);
			}

			if (!onlyJava) {
				typemap.Map.ManagedToJava.Sort ((MapEntry left, MapEntry right) => {
					int result = String.Compare (left.ManagedType.AssemblyName, right.ManagedType.AssemblyName, StringComparison.OrdinalIgnoreCase);
					if (result != 0)
					return result;

					return left.ManagedType.TypeName.CompareTo (right.ManagedType.TypeName);
				});

				table = new Table (TableManagedToJavaTitle, managedFirst: true);
				tables.Add (table);
				if (typemap.Map.Kind == MapKind.Release) {
					tableGenerator = TableGenerateManagedToJavaRelease;
					consoleGenerator = ConsoleGenerateManagedToJavaRelease;
				} else {
					tableGenerator = TableGenerateManagedToJavaDebug;
					consoleGenerator = ConsoleGenerateManagedToJavaDebug;
				}

				Generate ("Managed to Java", table, typemap.Map.ManagedToJava, full, tableGenerator, consoleGenerator);
			}

			string? outputFile = null;
			StreamWriter? sw = null;
			if (generateFiles) {
				outputFile = Utilities.GetOutputFileBaseName (outputDirectory, typemap.FormatVersion, typemap.Map.Kind, typemap.Map.Architecture);
				outputFile = $"{outputFile}.md";
				Utilities.CreateFileDirectory (outputFile);
				sw = new StreamWriter (outputFile, false, new UTF8Encoding (false));
			}

			try {
				if (sw != null) {
					sw.WriteLine ("# Info");
					sw.WriteLine ();
					sw.WriteLine ($"Architecture: **{typemap.MapArchitecture}**");
					sw.WriteLine ($"  Build kind: **{typemap.Map.Kind}**");
					sw.WriteLine ($"      Format: **{typemap.FormatVersion}**");
					sw.WriteLine ($" Description: **{typemap.Description}**");
					sw.WriteLine ();
				}

				foreach (Table t in tables) {
					if (sw != null) {
						WriteTable (sw, t);
						sw.WriteLine ();
					}
				}
			} finally {
				if (sw != null) {
					sw.Flush ();
					sw.Close ();
					sw.Dispose ();
				}
			}
		}

		void WriteTable (StreamWriter sw, Table table)
		{
			sw.WriteLine ($"# {table.Title}");
			sw.WriteLine ();

			// Just non-empty columns
			var columns = new List<Column> ();
			if (table.ManagedFirst) {
				MaybeAddColumn (table.ManagedType);
				MaybeAddColumn (table.JavaType);
			} else {
				MaybeAddColumn (table.JavaType);
				MaybeAddColumn (table.ManagedType);
			}
			MaybeAddColumn (table.TokenID);
			MaybeAddColumn (table.Generic);
			MaybeAddColumn (table.Duplicate);
			MaybeAddColumn (table.MVID);

			if (columns.Count == 0) {
				Log.Warning ("No non-empty columns");
				return;
			}

			// All columns must have equal numbers of rows
			int rows = columns[0].Rows.Count;
			foreach (Column column in columns) {
				if (column.Rows.Count != rows) {
					throw new InvalidOperationException ($"Column {column.Header} has a different number of rows, {column.Rows.Count}, than the expected value of {rows}");
				}
			}

			var sb = new StringBuilder ();
			int width;
			string prefix;
			var divider = new StringBuilder ();
			foreach (Column column in columns) {
				width = GetColumnWidth (column);
				divider.Append ("| ");
				divider.Append ('-', width - 3);
				divider.Append (' ');

				prefix = $"| {column.Header}";
				sw.Write (prefix);
				sw.Write (GetPadding (width - prefix.Length));
			}

			sw.WriteLine ('|');
			sw.Write (divider);
			sw.WriteLine ('|');

			for (int row = 0; row < rows; row++) {
				foreach (Column column in columns) {
					width = GetColumnWidth (column);
					prefix = $"| {column.Rows[row]}";
					sw.Write (prefix);
					sw.Write (GetPadding (width - prefix.Length));
				}
				sw.Write ('|');
				sw.WriteLine ();
			}

			void MaybeAddColumn (Column column)
			{
				if (column.Rows.Count == 0) {
					return;
				}

				columns.Add (column);
			}

			int GetColumnWidth (Column column) => column.Width + 3; // For the '| ' prefix and ' ' suffix

			string GetPadding (int width)
			{
				sb.Clear ();
				return sb.Append (' ', width).ToString ();
			}
		}

		void TableGenerateJavaToManagedDebug (Table table, MapEntry entry, bool full)
		{
			table.JavaType.Add (entry.JavaType.Name);
			table.ManagedType.Add (GetManagedTypeNameDebug (entry));
			table.Duplicate.Add (entry.ManagedType.IsDuplicate);
		}

		void TableGenerateJavaToManagedRelease (Table table, MapEntry entry, bool full)
		{
			string managedTypeName = GetManagedTypeNameRelease (entry);
			string generic = entry.ManagedType.IsGeneric ? IgnoredGeneric : "no";

			table.JavaType.Add (entry.JavaType.Name);
			table.ManagedType.Add (managedTypeName);
			table.Generic.Add (generic);
			if (!full) {
				return;
			}

			table.MVID.Add (entry.ManagedType.MVID);
			table.TokenID.Add (entry.ManagedType.TokenID);
		}

		void TableGenerateManagedToJavaDebug (Table table, MapEntry entry, bool full)
		{
			table.JavaType.Add (entry.JavaType.Name);
			table.ManagedType.Add (GetManagedTypeNameDebug (entry));
			table.Duplicate.Add (entry.ManagedType.IsDuplicate);
		}

		void TableGenerateManagedToJavaRelease (Table table, MapEntry entry, bool full)
		{
			string managedTypeName = GetManagedTypeNameRelease (entry);
			string generic = entry.ManagedType.IsGeneric ? IgnoredGeneric : "no";

			table.ManagedType.Add (managedTypeName);
			table.JavaType.Add (entry.JavaType.Name);
			table.Generic.Add (generic);
			table.Duplicate.Add (entry.ManagedType.IsDuplicate);
			if (!full) {
				return;
			}

			table.MVID.Add (entry.ManagedType.MVID);
			table.TokenID.Add (entry.ManagedType.TokenID);
		}

		void Generate (string label, Table table, List<MapEntry> typemap, bool full, Action<Table, MapEntry, bool> tableGenerator, Action<MapEntry, bool> consoleGenerator)
		{
			bool firstMatch = true;
			foreach (MapEntry entry in typemap) {
				if (generateFiles) {
					tableGenerator (table, entry, full);
				}

				if (filterRegex == null || !EntryMatches (entry, filterRegex)) {
					continue;
				}

				if (firstMatch) {
					Log.Info ();
					Log.Info ($"  Matching entries ({label}):");
					firstMatch = false;
				}

				consoleGenerator (entry, full);
			}
		}

		void GenerateOld (ITypemap typemap)
		{
			string baseOutputFile = generateFiles ? Utilities.GetOutputFileBaseName (outputDirectory, typemap.FormatVersion, typemap.Map.Kind, typemap.Map.Architecture) : String.Empty;
			Action<StreamWriter, MapEntry, bool, bool> fileGenerator;
			Action<MapEntry, bool> consoleGenerator;
			bool filtering = filterRegex != null;

			if (!onlyManaged) {
				typemap.Map.JavaToManaged.Sort ((MapEntry left, MapEntry right) => left.JavaType.Name.CompareTo (right.JavaType.Name));
				if (typemap.Map.Kind == MapKind.Release) {
					fileGenerator = FileGenerateJavaToManagedRelease;
					consoleGenerator = ConsoleGenerateJavaToManagedRelease;
				} else {
					fileGenerator = FileGenerateJavaToManagedDebug;
					consoleGenerator = ConsoleGenerateJavaToManagedDebug;
				}

				Generate (
					filtering ? "Java to Managed" : "Java to Managed output file",
					Utilities.GetJavaOutputFileName (baseOutputFile, "txt"),
					typemap.Map.JavaToManaged,
					full,
					fileGenerator,
					consoleGenerator
				);
			}

			if (!onlyJava) {
				typemap.Map.ManagedToJava.Sort ((MapEntry left, MapEntry right) => {
					int result = String.Compare (left.ManagedType.AssemblyName, right.ManagedType.AssemblyName, StringComparison.OrdinalIgnoreCase);
					if (result != 0)
						return result;

					return left.ManagedType.TypeName.CompareTo (right.ManagedType.TypeName);
				});

				if (typemap.Map.Kind == MapKind.Release) {
					fileGenerator = FileGenerateManagedToJavaRelease;
					consoleGenerator = ConsoleGenerateManagedToJavaRelease;
				} else {
					fileGenerator = FileGenerateManagedToJavaDebug;
					consoleGenerator = ConsoleGenerateManagedToJavaDebug;
				}

				Generate (
					filtering ? "Managed to Java" : "Managed to Java output file",
					Utilities.GetManagedOutputFileName (baseOutputFile, "txt"),
					typemap.Map.ManagedToJava,
					full,
					fileGenerator,
					consoleGenerator
				);
			}
		}

		void Generate (string name, string outputFile, List<MapEntry> typemap, bool full, Action<StreamWriter, MapEntry, bool, bool> fileGenerator, Action<MapEntry, bool> consoleGenerator)
		{
			if (generateFiles) {
				Log.Info ($"  {name}: {outputFile}");
				Utilities.CreateFileDirectory (outputFile);
			}

			StreamWriter? sw = null;
			if (generateFiles) {
				sw = new StreamWriter (outputFile, false, new UTF8Encoding (false));
			}

			bool firstMatch = true;
			bool first = true;
			try {
				foreach (MapEntry entry in typemap) {
					if (generateFiles) {
						fileGenerator (sw!, entry, full, first);
						if (first) {
							first = false;
						}
					}

					if (filterRegex == null || !EntryMatches (entry, filterRegex)) {
						continue;
					}

					if (firstMatch) {
						Log.Info ();
						Log.Info ($"  Matching entries ({name}):");
						firstMatch = false;
					}

					consoleGenerator (entry, full);
				}
			} finally {
				if (sw != null) {
					sw.Flush ();
					sw.Close ();
					sw.Dispose ();
				}
			}
		}

		string GetTokenID (MapEntry entry)
		{
			return $"{entry.ManagedType.TokenID} (0x{entry.ManagedType.TokenID:X08})";
		}

		string GetManagedTypeNameRelease (MapEntry entry)
		{
			return $"{entry.ManagedType.TypeName}, {entry.ManagedType.AssemblyName}";
		}

		string GetManagedTypeNameDebug (MapEntry entry)
		{
			return entry.ManagedType.TypeName;
		}

		const string IgnoredGeneric = "generic, ignored";
		const string Duplicate = "duplicate entry";

		void FileGenerateJavaToManagedDebug (StreamWriter sw, MapEntry entry, bool full, bool firstEntry)
		{
			if (firstEntry) {
				string sep = FileFieldSeparator;
				sw.WriteLine ($"{FormattedJavaTypeColumnHeader}{sep}{FormattedManagedTypeColumnHeader}{sep}{FormattedDuplicateColumnHeader}");
			}

			WriteLineToFile (sw,
			           entry.JavaType.Name,
			           GetManagedTypeNameDebug (entry),
			           entry.ManagedType.IsDuplicate ? Duplicate : String.Empty
			);
		}

		void ConsoleGenerateJavaToManagedDebug (MapEntry entry, bool full)
		{
			Log.Info ($"    {entry.JavaType.Name} -> {entry.ManagedType.TypeName}");
		}

		void FileGenerateJavaToManagedRelease (StreamWriter sw, MapEntry entry, bool full, bool firstEntry)
		{
			string managedTypeName = GetManagedTypeNameRelease (entry);
			string generic = entry.ManagedType.IsGeneric ? IgnoredGeneric : String.Empty;

			if (!full) {
				if (firstEntry) {
					string sep = FileFieldSeparator;
					sw.WriteLine ($"{FormattedJavaTypeColumnHeader}{sep}{FormattedManagedTypeColumnHeader}{sep}{FormattedGenericColumnHeader}");
				}

				WriteLineToFile (
					sw,
					entry.JavaType.Name,
					managedTypeName,
					generic
				);
				return;
			}

			if (firstEntry) {
				string sep = FileFieldSeparator;
				sw.WriteLine ($"{FormattedJavaTypeColumnHeader}{sep}{FormattedManagedTypeColumnHeader}{sep}{FormattedGenericColumnHeader}{sep}{FormattedMVIDColumnHeader}{sep}{FormattedTokenIDColumnHeader}");
			}
			WriteLineToFile (
				sw,
				entry.JavaType.Name,
				managedTypeName,
				generic,
				entry.ManagedType.MVID.ToString (),
				TokenIdToString (entry)
			);
		}

		void ConsoleGenerateJavaToManagedRelease (MapEntry entry, bool full)
		{
			string managedTypeName = GetManagedTypeNameRelease (entry);
			if (!full) {
				string generic;

				if (entry.ManagedType.IsGeneric) {
					generic = $" ({IgnoredGeneric})";
				} else {
					generic = String.Empty;
				}

				Log.Info ($"    {entry.JavaType.Name} -> {managedTypeName}{generic}");
				return;
			}

			Log.Info ($"    {entry.JavaType.Name} -> {managedTypeName}; MVID: {entry.ManagedType.MVID}; Token ID: {TokenIdToString (entry)}");
		}

		string TokenIdToString (MapEntry entry)
		{
			if (entry.ManagedType.IsGeneric) {
				return "0 (0x00000000)";
			} else {
				return GetTokenID (entry);
			}
		}

		void FileGenerateManagedToJavaDebug (StreamWriter sw, MapEntry entry, bool full, bool firstEntry)
		{
			if (firstEntry) {
				string sep = FileFieldSeparator;
				sw.WriteLine ($"{FormattedManagedTypeColumnHeader}{sep}{FormattedJavaTypeColumnHeader}{sep}{FormattedDuplicateColumnHeader}");
			}

			WriteLineToFile (sw,
			           GetManagedTypeNameDebug (entry),
			           entry.JavaType.Name,
			           entry.ManagedType.IsDuplicate ? Duplicate : String.Empty
			);
		}

		void ConsoleGenerateManagedToJavaDebug (MapEntry entry, bool full)
		{
			Log.Info ($"    {GetManagedTypeNameDebug (entry)} -> {entry.JavaType.Name}{GetAdditionalInfo (entry)}");
		}

		void FileGenerateManagedToJavaRelease (StreamWriter sw, MapEntry entry, bool full, bool firstEntry)
		{
			string managedTypeName = GetManagedTypeNameRelease (entry);
			string duplicate = entry.ManagedType.IsDuplicate ? Duplicate : "         ";
			string generic = entry.ManagedType.IsGeneric ? IgnoredGeneric : "         ";

			if (full) {
				if (firstEntry) {
					string sep = FileFieldSeparator;
					sw.WriteLine ($"{FormattedManagedTypeColumnHeader}{sep}{FormattedJavaTypeColumnHeader}{sep}{FormattedGenericColumnHeader}{sep}{FormattedDuplicateColumnHeader}{sep}{FormattedMVIDColumnHeader}{sep}{FormattedTokenIDColumnHeader}");
				}
				WriteLineToFile (
					sw,
					managedTypeName,
					entry.JavaType.Name,
					generic,
					duplicate,
					entry.ManagedType.MVID.ToString (),
					GetTokenID (entry)
				);
			} else {
				if (firstEntry) {
					string sep = FileFieldSeparator;
					sw.WriteLine ($"{FormattedManagedTypeColumnHeader}{sep}{FormattedJavaTypeColumnHeader}{sep}{FormattedGenericColumnHeader}{sep}{FormattedDuplicateColumnHeader}");
				}
				WriteLineToFile (
					sw,
					managedTypeName,
					entry.JavaType.Name,
					generic,
					duplicate
				);
			}
		}

		void ConsoleGenerateManagedToJavaRelease (MapEntry entry, bool full)
		{
			if (!full) {
				Log.Info ($"    {GetManagedTypeNameRelease (entry)} -> {entry.JavaType.Name}{GetAdditionalInfo (entry)}");
			} else {
				Log.Info ($"    {GetManagedTypeNameRelease (entry)}; MVID: {entry.ManagedType.MVID}; Token ID: {TokenIdToString (entry)} -> {entry.JavaType.Name}{GetAdditionalInfo (entry)}");
			}
		}

		string GetAdditionalInfo (MapEntry entry)
		{
			var status = new List<string> ();
			if (entry.ManagedType.IsGeneric) {
				status.Add (IgnoredGeneric);
			}

			if (entry.ManagedType.IsDuplicate) {
				status.Add (Duplicate);
			}

			if (status.Count > 0) {
				return " (" + String.Join ("; ", status) + ")";
			}

			return String.Empty;
		}

		bool EntryMatches (MapEntry entry, Regex regex)
		{
			Match match = regex.Match (entry.JavaType.Name);
			if (match.Success) {
				return true;
			}

			string managedName;
			if (entry.ManagedType.AssemblyName.Length > 0) {
				managedName = $"{entry.ManagedType.TypeName}, {entry.ManagedType.AssemblyName}";
			} else {
				managedName = entry.ManagedType.TypeName;
			}
			match = regex.Match (managedName);

			return match.Success;
		}

		void WriteLineToFile (StreamWriter sw, params string[] fields)
		{
			if (fields.Length == 0) {
				sw.WriteLine ();
				return;
			}

			for (int i = 0; i < fields.Length; i++) {
				if (i > 0)
					sw.Write (FileFieldSeparator);
				sw.Write (fields [i]);
			}
			sw.WriteLine ();
		}
	}
}
