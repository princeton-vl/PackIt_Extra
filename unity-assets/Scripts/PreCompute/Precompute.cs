using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

public class Precompute : MonoBehaviour {

    PackEvolver packEvol;
    GtPack gtPack;
    PackingAgent packingAgent;

    // Use this for initialization
	void Start () {
        packEvol = GetComponent<PackEvolver>();
        gtPack = GetComponent<GtPack>();
        packingAgent = GetComponent<PackingAgent>();
	}

    public void DirFileName(string packFileName, ref string dirName, ref string fileName)
    {
        string[] packFilePath = Regex.Split(packFileName ,  "/")
                                     .Where(x => x != string.Empty)
                                     .ToArray();
        
        Debug.Assert(packFilePath.Length <= 2);
        if(packFilePath.Length == 2)
        {
            dirName = packFilePath[0];
            fileName = packFilePath[1];
        }
        else
        {
            dirName = "";
            fileName = packFilePath[0];
        }
    }

    // This is meant to identify noisy packs
    // storing and reading packs in json format can make certain packs wrong because of numerical precision errors
    // here we identify some of those packs and clear them from the memory
    // for each pack, we check if the shapes are overlapping
    // if shapes overlap then some numerical precision errors must have occured
    public bool IdentifyRemoveNoisyPack(string packFileName, int packID)
    {
        Pack pack = packEvol.ReadPack(packFileName, packID);
        GameObject[] shapes = packEvol.VisualizePack(pack, loadFromResources:false, keepRenderer:false);
        bool noisyPack = false;
        for(int i = 0; i < shapes.Length; i++)
        {
            // move shape out of box
            shapes[i].transform.position = new Vector3(100f, 100f, 100f);
            // check if the shape collides
            bool shapeCollide = gtPack.WillShapeCollide(shapes[i], pack.positions[i]);
            if(shapeCollide)
            {
                noisyPack = true;
                break;
            }
            // move shape back in
            shapes[i].transform.position = pack.positions[i];
        }

        int shapesLength = shapes.Length;
        for(int i = 0; i < shapes.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(shapes[i]);
        }

        return noisyPack;
    }

    // packFileName is the file name relative to the steamingAssets folder 
    public void PrecomptePacks(string packFileName)
    {   
        string path = Application.streamingAssetsPath + "/" + packFileName;
        string jsonString = File.ReadAllText(path);
        Packs allData = JsonUtility.FromJson<Packs>(jsonString);

        string dirName = "";
        string fileName = "";
        DirFileName(packFileName, ref dirName, ref fileName);

        // First we identify and remove all the noisy packs
        Packs newAllData = new Packs();
        for(int i = 0; i < allData.data.Count; i++)
        {
            bool noisyPack = IdentifyRemoveNoisyPack(packFileName, i);
            if(!noisyPack)
            {
                newAllData.data.Add(allData.data.ElementAt(i));
            }
            else
            {
                Debug.Log("packFileName, i: " + packFileName + ", " + i + " is noisy and removed from data");
            }
        }
        File.WriteAllText(path, JsonUtility.ToJson(newAllData));

        for(int i = 0; i < newAllData.data.Count; i++)
        {
            PrecomptePack(packFileName, i,
                          dirName + "_precompute_unity/" + fileName + "_" + i,
                          dirName + "_precompute_python/" + fileName + "_" + i);
        }
    }

    // packFileName is the file name relative to the steamingAssets folder
    // outNameUnity is the file name for the precomputed unity file relative to the steamingAssets folder
    // outNamePython is the file name for the precomputed python file relative to the steamingAssets folder
    public void PrecomptePack(string packFileName, int packID, string outNameUnity, string outNamePython)
    {   

        // Step 0: not recomputing if files already exist
        if(System.IO.File.Exists(Application.streamingAssetsPath + "/" + outNameUnity) &&
           System.IO.File.Exists(Application.streamingAssetsPath + "/" + outNamePython))
        {
            return;
        }

        // Step 1: Read the pack
        Pack pack = packEvol.ReadPack(packFileName, packID);


        // Step 2a: Get gt action
        List<StepAction> gtRotBeforeMov = gtPack.GetGtStepAction(ref pack, rotBeforeMov: true);
        List<StepAction> gtNotRotBeforeMov = gtPack.GetGtStepAction(ref pack, rotBeforeMov: false);

        // Step 2b: Get gt2 action
        List<StepAction2> gtRotBeforeMov2 = gtPack.GetGtStepAction2(ref pack, gtRotBeforeMov, rotBeforeMov: true);
        List<StepAction2> gtNotRotBeforeMov2 = gtPack.GetGtStepAction2(ref pack, gtNotRotBeforeMov, rotBeforeMov: false);

        // Step 3: Get shapes
        GameObject[] shapes = null;
        string[] _null = null;
        packEvol.GetShapes(ref _null, ref pack.sources, ref shapes, loadFromResources: false, keepRenderer: false);
        for(int i = 0; i < shapes.Length; i++)
        {
            shapes[i].transform.localScale = pack.scales[i];
        }

        // Step 4: Get shape voxels
        List<float> vox = new List<float> (Mathf.CeilToInt(pack.efficiency 
                                                           * packingAgent.VOX_RES
                                                           * packingAgent.VOX_RES
                                                           * packingAgent.VOX_RES));
        for(int i = 0; i < shapes.Length; i++)
        {
            shapes[i].transform.position = Vector3.zero;
            packingAgent.boxVoxel(ref vox, i);
            shapes[i].transform.position = 100 * Vector3.one;
        }
        vox.TrimExcess();

        // Destroy the shapes
        for(int i = 0; i < shapes.Length; i++)
        {
            Object.DestroyImmediate(shapes[i]);
        }

        // Step 5: Calculate and Write PrecomputeUnity
        string path = Application.streamingAssetsPath + "/" + outNameUnity;
        PrecomputeUnity2 precomputeUnity = new PrecomputeUnity2(gtRotBeforeMov, gtNotRotBeforeMov,
                                                                gtRotBeforeMov2, gtNotRotBeforeMov2);
        File.WriteAllText(path, JsonUtility.ToJson(precomputeUnity));

        // Step 6: Calculate and Write PrecomputePython
        path = Application.streamingAssetsPath + "/" + outNamePython;
        PrecomputePython precomputePython = new PrecomputePython(vox);
        File.WriteAllText(path, JsonUtility.ToJson(precomputePython));
    }


    // packFileName is the file name relative to the steamingAssets folder
    public void loadPrecompute(string packFileName, int packID,
                               ref List<StepAction> gtStepAction,
                               bool rotBeforeMov)
    {
        string dirName = "";
        string fileName = "";
        DirFileName(packFileName, ref dirName, ref fileName);

        // Read the data
        string path = Application.streamingAssetsPath 
                + "/" + dirName + "_precompute_unity" 
                + "/" + fileName + "_" + packID;
        string jsonString = File.ReadAllText(path);
        PrecomputeUnity2 precomputeUnity = JsonUtility.FromJson<PrecomputeUnity2>(jsonString);

        if(rotBeforeMov)
        {
            gtStepAction = precomputeUnity.gtRotBeforeMov;
        }
        else
        {
            gtStepAction = precomputeUnity.gtNotRotBeforeMov;
        }

    }
}

 [System.Serializable]
 public class ListVector3
 {
      public List<Vector3> myList;

      public ListVector3()
      {
        this.myList = new List<Vector3>();
      }
 }

[System.Serializable]
public class PrecomputeUnity
{
    public List<StepAction> gtRotBeforeMov;
    public List<StepAction> gtNotRotBeforeMov;

    public PrecomputeUnity(
        // GameObject[] shapes,
        List<StepAction> gtRotBeforeMov,
        List<StepAction> gtNotRotBeforeMov)

    {
        this.gtRotBeforeMov = gtRotBeforeMov;
        this.gtNotRotBeforeMov = gtNotRotBeforeMov;
    }
}

[System.Serializable]
public class PrecomputeUnity2
{
    public List<StepAction> gtRotBeforeMov;
    public List<StepAction> gtNotRotBeforeMov;
    public List<StepAction2> gtRotBeforeMov2;
    public List<StepAction2> gtNotRotBeforeMov2;

    public PrecomputeUnity2(
        // GameObject[] shapes,
        List<StepAction> gtRotBeforeMov,
        List<StepAction> gtNotRotBeforeMov,
        List<StepAction2> gtRotBeforeMov2,
        List<StepAction2> gtNotRotBeforeMov2)

    {
        this.gtRotBeforeMov = gtRotBeforeMov;
        this.gtNotRotBeforeMov = gtNotRotBeforeMov;
        this.gtRotBeforeMov2 = gtRotBeforeMov2;
        this.gtNotRotBeforeMov2 = gtNotRotBeforeMov2;

    }
}

[System.Serializable]
public class PrecomputePython
{
    public List<float> vox;

    public PrecomputePython(List<float> vox)
    {
        this.vox = vox; 
    }
}

