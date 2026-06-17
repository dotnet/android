using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.Installer.Common
{
	public interface ILogAdapter
	{
		void Action(string format, params object[] parms);
		void Debug (string format, params object[] parms);
		void Debug (string format, Exception ex, params object[] parms);
		void Error (string format, params object[] parms);
		void Exception (string format, Exception ex, params object[] parms);
		void Info (string format, params object[] parms);
		void Warning (string format, params object[] parms);
		void SetOperationStatus (OperationStatus status);
	}

	public interface ILogAdapterExtended: ILogAdapter
	{
		void SaveManifest (string manifestName, string content);
	}

	public interface ILogPathProvider
	{
		string GetLogPath(string fileNameSuffix);
	}
}
