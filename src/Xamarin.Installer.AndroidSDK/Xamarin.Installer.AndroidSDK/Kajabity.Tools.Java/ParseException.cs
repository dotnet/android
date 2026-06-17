/*
 * Copyright 2009-15 Williams Technologies Limited.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Kajbity is a trademark of Williams Technologies Limited.
 *
 * http://www.kajabity.com
 */

using System;

namespace Kajabity.Tools.Java
{
	/// <summary>
	/// An exception thrown by <see cref="JavaPropertyReader"/> when parsing
	/// a properties stream.
	/// </summary>
	public class ParseException : System.Exception
	{
		/// <summary>
		/// Construct an exception with an error message.
		/// </summary>
		/// <param name="message">A descriptive message for the exception</param>
		public ParseException( string message ) : base( message )
		{
		}
	}
}
