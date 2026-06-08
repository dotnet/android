// 
// AndroidDeviceProperties.cs
//  
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
// 
// Copyright (c) 2011 Xamarin Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;

namespace Mono.AndroidTools
{
	//TODO: more properties
	public class AndroidDeviceProperties
	{
		Dictionary<string,string> values;
		
		internal AndroidDeviceProperties (Dictionary<string,string> values)
		{
			this.values = values;
		}
		
		public string Get (string key)
		{
			string val;
			if (values.TryGetValue (key, out val))
				return val;
			return null;
		}

		internal Dictionary<string, string> ToDictionary ()
		{
			return new Dictionary<string, string> (values);
		}

		int TryGetInt (string key)
		{
			int val;
			string s;
			if (values.TryGetValue (key, out s) && int.TryParse (s, out val))
				return val;
			return -1;
		}
		
		public bool Secure {
			get {
				return Get ("ro.secure") == "1";
			}
		}
		
		public bool Debuggable {
			get {
				return Get ("ro.debuggable") == "1";
			}
		}
		
		/// <summary>
		/// Build Id e.g. "FROYO".
		/// </summary>
		public string BuildId {
			get {
				return Get ("ro.build.version.release");
			}
		}
		
		/// <summary>
		/// Release version e.g. "2.2".
		/// </summary>
		public string BuildVersionRelease {
			get {
				return Get ("ro.build.version.release");
			}
		}
		
		/// <summary>
		/// SDK version, e.g. "8".
		/// </summary>
		public int BuildVersionSdk {
			get {
				return TryGetInt ("ro.build.version.sdk");
			}
		}
		
		public string ProductModel {
			get {
				return Get ("ro.product.model");
			}
		}
		
		public string ProductBrand {
			get {
				return Get ("ro.product.brand");
			}
		}
		
		public string ProductName {
			get {
				return Get ("ro.product.name");
			}
		}
		
		public string ProductDevice {
			get {
				return Get ("ro.product.device");
			}
		}
		
		public string ProductBoard {
			get {
				return Get ("ro.product.board");
			}
		}
		
		public string ProductManufacturer {
			get {
				return Get ("ro.product.manufacturer");
			}
		}
		
		/// <summary>
		/// CPU ABI, e.g. "armeabi-v7a".
		/// </summary>
		public string ProductCpuAbi {
			get {
				return Get ("ro.product.cpu.abi");
			}
		}
		
		/// <summary>
		/// Alternate CPU ABI, e.g. "armeabi".
		/// </summary>
		public string ProductCpuAbi2 {
			get {
				return Get ("ro.product.cpu.abi2");
			}
		}

		/// <summary>
		/// List of CPU ABIs supported by the device.
		/// Generally reflects it supports both 32bits and 64bits ABIs.
		/// </summary>
		/// <value>The product cpu abi list.</value>
		public string[] ProductCpuAbiList {
			get {
				var result = Get ("ro.product.cpu.abilist");
				return result == null ? new string[0] : result.Split (',');
			}
		}
		
		/// <summary>
		/// Product locale language code, e.g. "en".
		/// </summary>
		public string ProductLocaleLanguage {
			get {
				return Get ("ro.product.locale.language");
			}
		}
		
		/// <summary>
		/// Product locale region code, e.g. "US".
		/// </summary>
		public string ProductLocaleRegion {
			get {
				return Get ("ro.product.locale.region");
			}
		}

		public string MonoLog {
			get {
				return Get ("debug.mono.log");
			}
		}
	}
}