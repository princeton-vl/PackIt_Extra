using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

/**
 * The high level function to take care of packing
 * This is a cleaned version of the move.cs script
 * Takes the command line arguments and generates 
 * pack accordingly
 * for the -runAblation true case, only the -fileName 
 * -shapeNet, -numPacks, -resolution and -seed matter
 */ 

public class Packer : MonoBehaviour
{ 	

	PackEvolver pack;
	bool packStart;
	System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

	// Arguments with the default values
	int numPacks = 1;
	int currPack = 1;
	int numChromoGen = 100;
	int numGen = 1000;
	int numGenBreak = 100;
	string _useEmptySpace = "true";
	bool useEmptySpace = true;
	string _initLargestShape = "false";
	bool initLargestShape = false;
	float resolution = 0.0125f;
	string fileName = "test";
	string shapeNet = "/Users/ankgoyal/MEGA/Research/Codes/egp/Resources/Shapes_tr";
	int seed = (int)System.DateTime.Now.Ticks;
	bool editorMode = false;
	string _runAblation = "false";
	bool runAblation = false;

	// Use this for initialization
	void Start ()
	{
		pack = GetComponent<PackEvolver> ();
		if(!editorMode)
		{
			// gettting the command line arguments
			var args = System.Environment.GetCommandLineArgs();
			for(int i = 0; i < args.Length; i++)
			{
				if(args[i].ToString() == "-fileName" && args.Length > i + 1)
				{
					fileName = args [i+1].ToString ();
				}

				if(args[i].ToString() == "-shapeNet" && args.Length > i + 1)
				{
					shapeNet = args[i+1].ToString ();
				}

				if(args[i].ToString() == "-numPacks" && args.Length > i + 1)
				{
					numPacks = int.Parse(args[i+1].ToString());
				}

				if(args[i].ToString() == "-numChromoGen" && args.Length > i + 1)
				{
					numChromoGen = int.Parse(args[i+1].ToString());
				}

				if(args[i].ToString() == "-numGen" && args.Length > i + 1)
				{
					numGen = int.Parse(args[i+1].ToString());
				}

				if(args[i].ToString() == "-numGenBreak" && args.Length > i + 1)
				{
					numGenBreak = int.Parse(args[i+1].ToString());
				}

				if(args[i].ToString() == "-useEmptySpace" && args.Length > i + 1)
				{
					_useEmptySpace = args[i+1].ToString();
				}

				if(args[i].ToString() == "-initLargestShape" && args.Length > i + 1)
				{
					_initLargestShape = args[i+1].ToString();
				}

				if(args[i].ToString() == "-resolution" && args.Length > i + 1)
				{
					resolution = float.Parse(args[i+1].ToString());
				}

				if(args[i].ToString() == "-seed" && args.Length > i + 1)
				{
					seed = int.Parse(args[i+1].ToString());
				}

				if(args[i].ToString() == "-runAblation" && args.Length > i + 1)
				{
					_runAblation = args[i+1].ToString();
				}
			}
		}

		pack.NUM_CHROMO_GEN = numChromoGen;
		pack.NUM_GEN = numGen;
		pack.NUM_GEN_BREAK = numGenBreak;
		pack.RESOLUTION = resolution;
		packStart = true;
		Random.InitState(seed);

		if(_useEmptySpace == "true")
		{
			useEmptySpace = true;
		}
		else
		{
			useEmptySpace = false;
		}

		if(_initLargestShape == "true")
		{
			initLargestShape = true;
		}
		else
		{
			initLargestShape = false;
		}

		if(_runAblation == "true")
		{
			runAblation = true;
		}
		else
		{
			runAblation = false;
		}

		if(!runAblation)
		{
			string path = Application.streamingAssetsPath + "/" + fileName + "_log";
			string oldOutput = "";
			if(File.Exists(path))
			{
				oldOutput = File.ReadAllText(path);
			}
			string output = "Experiment Parameters: " + "\n"
							+ "NUM_CHROMO_GEN: " + pack.NUM_CHROMO_GEN + "\n"
							+ "NUM_GEN: " + pack.NUM_GEN + "\n"
							+ "NUM NUM_GEN_BREAK: " + pack.NUM_GEN_BREAK + "\n"
							+ "RESOLUTION: " + pack.RESOLUTION + "\n"
							+ "useEmptySpace: " + useEmptySpace + "\n"
							+ "initLargestShape: " + initLargestShape;

			File.WriteAllText(path, oldOutput + "\n" + output);
		}
	}

	void Update()
	{
		if(currPack > numPacks && packStart)
		{
			Application.Quit();
		}
		else
		{
			if(packStart)
			{
				packStart = false;

				sw.Restart();
				sw.Start();
				if(runAblation)
				{
					pack.EvolvePackAblation(fileName:fileName,
						                    shapeNet:shapeNet,
					                        savePackPerGeneration:10,
					                        finalGeneration:1000);
				}
				else
				{
					pack.GeneratePack(fileName,
					                  shapeNet,
					                  useEmptySpace: useEmptySpace,
					                  initLargestShape: initLargestShape);
				}
				
				sw.Stop();

				Debug.Log("Generated packs: " + currPack);
				currPack++;
			}
			else
			{
				Debug.Log("Time taken: " + sw.ElapsedMilliseconds);
				packStart = true;
			}
		}
	}
}
