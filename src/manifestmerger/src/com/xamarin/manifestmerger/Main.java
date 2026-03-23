package com.xamarin.manifestmerger;

import java.io.File;
import java.io.FileReader;
import java.io.IOException;
import java.io.BufferedReader;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import com.android.manifmerger.ManifestMerger2;
import com.android.manifmerger.Merger;
import com.android.utils.ILogger;

public class Main {

    // Subclass Merger to apply --lenientUsesSdkInManifestHandling via the API,
    // working around a bug in Google's Merger CLI where the first pass doesn't
    // recognize this as a value-less flag and consumes the next argument.
    static class LenientMerger extends Merger {
        @Override
        protected ManifestMerger2.Invoker createInvoker(File mainManifestFile, ILogger logger) {
            return super.createInvoker(mainManifestFile, logger)
                .withFeatures(ManifestMerger2.Invoker.Feature.USES_SDK_IN_MANIFEST_LENIENT_HANDLING);
        }
    }

    public static void main(String[] args) {
        // parse the args and pick out the following
        // @responseFile
        // each line should be an argument eg.
        //   --main
        //   AndroidManifest.xml
        //   --libs
        //   foo.xml:bar.xml
        //   --out
        //   AndroidManifest.xml.tmp
        try {
            FileReader fr = new FileReader (args [0]);
            BufferedReader br = new BufferedReader (fr);
            String line;
            boolean lenient = false;
            List<String> arguments = new ArrayList<String> ();
            while ((line = br.readLine ()) != null) {
                if ("--lenientUsesSdkInManifestHandling".equals (line.trim ())) {
                    lenient = true;
                } else {
                    arguments.add (line);
                }
            }
            fr.close ();
            String arg [] = arguments.toArray (new String [arguments.size ()]);
            Merger merger = lenient ? new LenientMerger () : new Merger ();
            System.exit (merger.process (arg));
        } catch (IOException ignore) {
            System.err.println("Response file not provided.");
        }
    }
}