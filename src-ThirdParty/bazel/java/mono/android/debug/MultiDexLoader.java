//
// This is a modified version of Bazel source code. Its copyright lines follow below.
//

// Copyright 2015 Xamarin Inc. All rights reserved.
// Copyright 2017 Microsoft Corporation. All rights reserved.

//
// Copyright 2014 The Bazel Authors. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
package mono.android;

import mono.android.incrementaldeployment.IncrementalClassLoader;

import java.io.File;
import java.lang.reflect.Field;
import java.util.List;
import java.util.Collection;
import android.app.Application;
import android.content.Context;
import android.content.ContextWrapper;
import android.content.res.AssetManager;
import android.content.res.Resources;
import android.util.Log;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.FileReader;
import java.io.FilenameFilter;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.lang.ref.WeakReference;
import java.lang.reflect.Constructor;
import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import android.app.Activity;
import android.content.*;
import android.util.Log;
import android.util.ArrayMap;
import android.os.Build;
import dalvik.system.BaseDexClassLoader;
import android.util.LongSparseArray;
import android.util.SparseArray;
import android.view.ContextThemeWrapper;

public class MultiDexLoader extends ContentProvider {

	static final int KITKAT = 19;

	@Override
	public boolean onCreate ()
	{
		return true;
	}

	@Override
	public void attachInfo (android.content.Context context, android.content.pm.ProviderInfo info)
	{
 		String incrementalDeploymentDir = context.getFilesDir () + "/";

		File codeCacheDir = context.getCacheDir ();
		String nativeLibDir = context.getApplicationInfo ().nativeLibraryDir;
		String dataDir = context.getApplicationInfo ().dataDir;
		String packageName = context.getPackageName ();

		List<String> dexes = getDexList (packageName, incrementalDeploymentDir);
		if (dexes != null && dexes.size () > 0) {
			IncrementalClassLoader.inject (
				MultiDexLoader.class.getClassLoader (),
				packageName,
				codeCacheDir,
				nativeLibDir,
				dexes);
		}
		super.attachInfo (context, info);

	}

	private List<String> getDexList (String packageName, String incrementalDeploymentDir)
	{
		List<String> result = new ArrayList<String> ();
		String dexDirectory = incrementalDeploymentDir + ".__override__/dexes";
		Log.v ("MultiDexLoader", dexDirectory);
		File[] dexes = new File (dexDirectory).listFiles ();
		// It is not illegal state when it was launched to start Seppuku
		if (dexes == null) {
			Log.v("MultiDexLoader", "No dexes!");
			return null;
		} else {
			for (File dex : dexes) {
				if (dex.getName ().endsWith (".dex")) {
					Log.v("MultiDexLoader", "Adding dex " + dex.getPath ());
					result.add (dex.getPath ());
				}
			}
		}

		return result;
	}

	// ---
	@Override
	public android.database.Cursor query (android.net.Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public String getType (android.net.Uri uri)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public android.net.Uri insert (android.net.Uri uri, android.content.ContentValues initialValues)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public int delete (android.net.Uri uri, String where, String[] whereArgs)
	{
		throw new RuntimeException ("This operation is not supported.");
	}

	@Override
	public int update (android.net.Uri uri, android.content.ContentValues values, String where, String[] whereArgs)
	{
		throw new RuntimeException ("This operation is not supported.");
	}
}
