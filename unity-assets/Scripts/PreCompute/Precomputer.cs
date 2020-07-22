using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

/**
 * The high level function to take care of packing
 * Takes the command line arguments and generates 
 * pack accordingly
 * -packFolder is folder inside StreamingAssets
 * -packFile, if empty would precompute for all file
 *     inside the folder, else only that file
 */ 

public class Precomputer : MonoBehaviour
{   

    Precompute precompute;

    // Arguments with the default values
    string packFolder = "pack_tr";
    string packFile = "";
    List<string> packFileNames;
    bool editorMode = false;
    int iteration = 0;

    // Use this for initialization
    void Start ()
    {
        precompute = GetComponent<Precompute>();
        if(!editorMode)
        {
            // gettting the command line arguments
            var args = System.Environment.GetCommandLineArgs();
            for(int i = 0; i < args.Length; i++)
            {
                if(args[i].ToString() == "-packFolder" && args.Length > i + 1)
                {
                    packFolder = args [i+1].ToString ();
                }

                if(args[i].ToString() == "-packFile" && args.Length > i + 1)
                {
                    packFile = args [i+1].ToString ();
                }
            }
        }

        packFileNames = new List<string>();
	if(packFile == "")
	{
            string path = Application.streamingAssetsPath + "/" + packFolder;
            DirectoryInfo dir = new DirectoryInfo(path);
            FileInfo[] info = dir.GetFiles("*.*");
            foreach (FileInfo f in info)
            {   
                if((!f.Name.ToString().Contains("log")) && (!f.Name.ToString().Contains("meta")))
                {
                    packFileNames.Add(packFolder + "/" + f.Name.ToString());
                }
            }
	}
	else
	{
	    packFileNames.Add(packFolder + "/" + packFile);
	}

        if (!Directory.Exists(
            Application.streamingAssetsPath 
            + "/" + packFolder + "_precompute_unity"))
        {
            Directory.CreateDirectory(
                Application.streamingAssetsPath 
                + "/" + packFolder + "_precompute_unity");
        }

        if (!Directory.Exists(
            Application.streamingAssetsPath 
            + "/" + packFolder + "_precompute_python"))
        {
            Directory.CreateDirectory(
                Application.streamingAssetsPath 
                + "/" + packFolder + "_precompute_python");
        }
    }

    void Update()
    {
        precompute.PrecomptePacks(packFileNames.ElementAt(iteration));
        iteration++;
        if(iteration >=  packFileNames.Count)
        {   
	    #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
