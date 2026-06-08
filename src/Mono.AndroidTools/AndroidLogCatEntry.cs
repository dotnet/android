using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mono.AndroidTools
{
	public class AndroidLogCatEntry
	{
		public string Date { get; set; }
		public LogEntryType Type { get; set; }
		public string Tag { get; set; }
		public int Pid { get; set; }
		public string Message { get; set; }
		public string Raw { get; set; }

		public override string ToString ()
		{
			return Raw;
		}
	}

	public enum LogEntryType
	{
		Debug,
		Info,
		Warning,
		Error,
		Verbose
	}
}
