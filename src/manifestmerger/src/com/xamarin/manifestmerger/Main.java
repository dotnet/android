package com.xamarin.manifestmerger;

import java.io.File;
import java.io.FileReader;
import java.io.IOException;
import java.io.BufferedReader;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import com.android.manifmerger.Merger;

public class Main {
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
            List<String> arguments = new ArrayList<String> ();
            while((line = br.readLine ())!=null) {
                arguments.add (line);
            }
            fr.close ();
            String arg []=arguments.toArray (new String[arguments.size ()]);
            Merger.main (arg);
        } catch (IOException ignore) {
            System.err.println("Response file not provided.");
        }
    }
}