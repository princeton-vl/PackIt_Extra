using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAgents;
using System;
using System.Linq;
using System.IO;

/***
 * The purpose of the agent is to reset game, collect observation,
 * collect reward and execute action
 */

public class PackingAgent : Agent
{
    /***
     * step is meant to identify the current status of the game
     * step | stepNum |             Observation                   |    Obs. Size    |  Action          | Act. Size
     * ------------------------------------------------------------------------------------------------------
     * 0    | 0       | Number of objects in the pack##           | 1               |   null           | null
     * 1    | i       | Voxel representation of obj. i            | 100 * 100 * 100 |   null           | null
     * 2    | i       | Voxel representation of box i             | 100 * 100 * 100 | shape index      | i in (0, N-1)
     * 3    | i       | Avail. loc. in box i for obj.-order i     | 25  * 25  * 25  | location index   | (i, j, k) in (0, 24)^3
     * 4    | i       | Avail. rot. in for obj.-order i in loc. i | 24              | rotaion index    | (i) in (0, 23)
     *
     * Seq: (0,0), (1,0), ..... (1, N-1), (2, 0), (3, 0), (4, 0), (2, 1), (3, 1), (4, 1) .... (2, N-1), (3, N-1), (4, N-1)
     * Modes:
     *  only editorMode: all calculations are true, shape renders are kept for viewing
     *  demoMode and editorMode: calculates the gt and blindly puts objects there, lightweight for demo purpose
     * Getting Actions:
     *  getGT: returns groundtruth actions
     *  getSavedAct: for visualizing some recorded set of actions
     * Others:
     *  loadPrecompute: if true, precomputed shape voxels are used in Step 1. Shapes are loaded one at at time later on.
     *  	Hence, it should be false for demoMode as all shapes need to be loaded at once.
     */

    int step;
    int stepNum;
    int GRID_RES;
    float GRID_SIZE;

    int[,,,,] validPos;

    public int VOX_RES = 100;
    public int MOV_RES = 25;

    const bool editorMode = true; // false for unity agent, true for visualizer
    const bool demoMode = true; // false for unity agent, true for visualizer


    // incase of demo or editor mode, which actions to use 
    bool getGT = false; // true for unity agent, false for visualizer
    bool getSavedAct = true;  // false for unity agent, true for visualizer

    bool loadPrecompute = false; // true for unity agent, false for visualizer
    bool rotBeforeMov = true; // true for unity agent and visualizer

    GameObject[] shapes;
    Pack pack;
    GtPack gtPack;
    PackEvolver packEvol;
    Helper helper;
    Precompute precompute;
    List <StepAction> gtStepAction;

    // variables read from the pythonAPI
    int packID = 0;
    string packFileName = "pack_va/0_va";
    string actFileName = "visualize/pack_va_0_va_0_gt";

    // global variables for actions
    int actObjNum = 0;
    Vector3Int actPosIdx;
    int actRotIdx;

    // for storing the sparse shape representations
    List<float> vox;

    void Start()
    {

        // getting the required components
        packEvol = GetComponent<PackEvolver>();
        gtPack = GetComponent<GtPack>();
        helper = GetComponent<Helper>();
        precompute = GetComponent<Precompute>();

        // setting up global variables
        GRID_RES = MOV_RES + 1;
        GRID_SIZE = 1f / (float) GRID_RES;
        helper.RESOLUTION = 0.0125f;

        if(!Application.isEditor)
        {
            // getting the command line arguments
            var args = System.Environment.GetCommandLineArgs();
            for(int i = 0; i < args.Length; i++)
            {
                if(args[i].ToString() == "-fileName" && args.Length > i + 1)
                {
                    packFileName = args[i+1].ToString();
                }

                if(args[i].ToString() == "-packID" && args.Length > i + 1)
                {
                    packID = Int32.Parse(args[i+1]);
                }

                if(args[i].ToString() == "-getGT" && args.Length > i + 1)
                {
                    string _getGT = args[i+1].ToString();
                    if(_getGT == "true")
                    {
                        getGT = true;
                    }
                    else
                    {
                        getGT = false;
                    }
                }

                if(args[i].ToString() == "-getSavedAct" && args.Length > i + 1)
                {
                    string _getSavedAct = args[i+1].ToString();
                    if(_getSavedAct == "true")
                    {
                        getSavedAct = true;
                    }
                    else
                    {
                        getSavedAct = false;
                    }
                }

                if(args[i].ToString() == "-actFileName" && args.Length > i + 1)
                {
                    actFileName = args[i+1].ToString();
                }
                
		if(args[i].ToString() == "-rotBeforeMov" && args.Length > i + 1)
                {
                    string _rotBeforeMov = args[i+1].ToString();
                    if(_rotBeforeMov == "true")
                    {
                        rotBeforeMov = true;
                    }
                    else
                    {
                        rotBeforeMov = false;
                    }
                }

                if(args[i].ToString() == "-loadPrecompute" && args.Length > i + 1)
                {
                    string _loadPrecompute = args[i+1].ToString();
                    if(_loadPrecompute == "true")
                    {
                        loadPrecompute = true;
                    }
                    else
                    {
                        loadPrecompute = false;
                    }
                }
            }
        }

        if(editorMode)
        {
            Debug.Assert(getGT || getSavedAct);
        }

        if(demoMode)
        {
            Debug.Assert((getGT || getSavedAct));
	    Debug.Assert(editorMode);
	    Debug.Assert(!loadPrecompute);
	}
	
	if(getSavedAct)
	{
            Debug.Assert(!getGT);
	    Debug.Assert(editorMode || demoMode);
	    Debug.Assert(rotBeforeMov);
	}
    }

    /***
     * Call an observation-action-reward cycle
     * Updates the step and stepNum
     */
    void Update()
    {
        if(editorMode)
        {
            if(Input.GetKeyDown("a"))
            {
                Debug.Log("Next Step");
                RequestDecision();
            }
        }
        else
        {
            RequestDecision();
        }

    }

    public Tuple<int, int> UpdateStepStepNum(int localStep, int localStepNum, int numShapes)
    {
        switch(localStep)
        {
        case 0:
            if(!loadPrecompute)
            {
                localStep = 1;
                localStepNum = 0;
            }
            // skipping step 1 for the precompute case
            else
            {
                localStep = 2;
                localStepNum = 0;
            }
            break;

        case 1:
            localStep = 2;
            localStepNum = 0;
            break;

        case 2:
            localStep = 3;
            break;

        case 3:
            localStep = 4;
            break;

        case 4:
            if(localStepNum == numShapes- 1)
            {
                return Tuple.Create<int, int>(-1, -1);
            }
            else
            {
                localStep = 2;
                localStepNum++;
            }
            break;
        }

        return Tuple.Create<int, int>(localStep, localStepNum);
    }

    public List<StepAction> GetStepAction()
    {
        // Read the data
        string path = Application.streamingAssetsPath + "/" + actFileName;
        string jsonString = File.ReadAllText(path);
        StepActions _stepActions = JsonUtility.FromJson<StepActions>(jsonString);
	return _stepActions.data;
    }

    /***
     * Fetch a pack
     * Remove old shapes
     * Get the new shapes
     */
    public override void AgentReset()
    {
        step = 0;
        stepNum = 0;
        pack = packEvol.ReadPack(packFileName, packID);
        GameObject _brain = GameObject.Find("Academy/Brain");
        Brain brain = _brain.transform.GetComponent<Brain>();

        // Destroying the already present shapes
        if(shapes != null)
        {
            for(int i = 0; i < shapes.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(shapes[i]);
            }
            shapes = null;
        }

        if(rotBeforeMov)
        {
            validPos = new int[1, 1, MOV_RES, MOV_RES, MOV_RES];
        }
        else
        {
            validPos = new int[packEvol.r1Value.Count, packEvol.r2Value.Count, MOV_RES, MOV_RES, MOV_RES];
        }

        if(!loadPrecompute)
        {
            if(getGT)
            {
                gtStepAction = gtPack.GetGtStepAction(ref pack, rotBeforeMov: rotBeforeMov);
            }

            if(getSavedAct)
            {
                gtStepAction = GetStepAction();
            }

            // Loading the new shapes
            string[] _null = null;
            packEvol.GetShapes(ref _null, ref pack.sources, ref shapes, loadFromResources: false, keepRenderer: editorMode);
            for(int i = 0; i < shapes.Length; i++)
            {
                shapes[i].transform.localScale = pack.scales[i];
            }

            // Move shapes in visible pisitions in demoMode
            // Also removing box colliders for faster processing
            if(demoMode)
            {
                float theta = (2.0f * Mathf.PI) / shapes.Length;
                for(int i = 0; i < shapes.Length; i++)
                {
                    BoxCollider[] boxes = shapes[i].GetComponents<BoxCollider>();
                    foreach (BoxCollider box in boxes)
                    {
                        Destroy(box);
                    }

                    shapes[i].transform.position = new Vector3(2.0f * Mathf.Sin(i * theta),
				    			       0f,
                                                               2.0f * Mathf.Cos(i * theta));
                }
            }
            else
            {
                // intial estimate added so as to ensure faster computation
                vox = new List<float> (Mathf.CeilToInt(pack.efficiency * VOX_RES *  VOX_RES * VOX_RES));
                for(int i = 0; i < shapes.Length; i++)
                {
                    shapes[i].transform.position = Vector3.zero;
                    boxVoxel(ref vox, i);
                    shapes[i].transform.position = 100 * Vector3.one;
                }
                vox.TrimExcess();

                // setting up the number of observarions
                brain.brainParameters.vectorObservationSize =
                    ((VOX_RES * VOX_RES * VOX_RES) > (vox.Count + 1)) ? (VOX_RES * VOX_RES * VOX_RES) + 2 : vox.Count + 3;
            }
        }
        else
        {
            shapes = new GameObject[pack.sources.Length];
            precompute.loadPrecompute(packFileName, packID, ref gtStepAction, rotBeforeMov);
            brain.brainParameters.vectorObservationSize = (VOX_RES * VOX_RES * VOX_RES) + 2;
            if (getSavedAct)
	    {
                gtStepAction = GetStepAction();
	    }
	}
    }

    // Checks whether a float is closer than 0.001f to an interger
    public bool IsFloatInt(float x)
    {
        return (x - Mathf.Floor(x) < 0.001f) || (Mathf.Ceil(x) - x < 0.001f);
    }

    // Returns the interger for a float where is closer than 0.001f to an interger
    public int FloatToInt(float x)
    {
        if((x - Mathf.Floor(x) < 0.001f))
        {
            return Mathf.FloorToInt(x);
        }
        else if((Mathf.Ceil(x) - x < 0.001f))
        {
            return Mathf.CeilToInt(x);
        }
        else
        {
            Debug.Log("Float can't be converted to integer");
            return -1;
        }
    }

    // Converts index to continous position
    // if isStart0 is false, idx assummed to be in (1, 25)^3
    public Vector3 IdxToConPos(Vector3 idx, bool isStart0)
    {
        // local import so as to run the packing agent code without the start function
        packEvol = GetComponent<PackEvolver>();
        if(isStart0)
        {
            idx += Vector3.one;
        }
        return GRID_SIZE * idx + packEvol.BOX_BLB_CORNER;
    }



    // Converts continuos postion to index
    // if isStart0 is false, idx assummed to be in (1, 25)^3
    public Vector3Int ConToIdxPos(Vector3 con, bool isStart0)
    {
        // local import so as to run the packing agent code without the start function
        packEvol = GetComponent<PackEvolver>();
        Vector3 _idx = (con - packEvol.BOX_BLB_CORNER) * ((float) GRID_RES);
        if(!IsFloatInt(_idx.x) || !IsFloatInt(_idx.y) || !IsFloatInt(_idx.z))
        {
            Debug.Log("Error converting wrong cont. position to idx.");
        }

        Vector3Int idx = new Vector3Int(FloatToInt(_idx.x),
                                        FloatToInt(_idx.y),
                                        FloatToInt(_idx.z));
        if(isStart0)
        {
            idx -= Vector3Int.one;
        }
        return idx;
    }


    // pads zeros to an observatation
    // numObs is the already added observations
    void padZeroObs(int numObs)
    {
        // -2 to take care of the fact that step and stepNum already added
        int brainParameters = brain.brainParameters.vectorObservationSize - 2;
        int numZeros = brainParameters - numObs;
        for(int i = 0; i < numZeros; i++)
        {
            AddVectorObs(0);
        }
    }

    // Creates a voxel representation of everything that is in the box
    public void boxVoxel(ref int[,,] vox)
    {
        vox =  new int[VOX_RES, VOX_RES, VOX_RES];

        float boxHalfExtends = 1f / (2 * VOX_RES);
        Vector3 boxCenter = Vector3.zero;
        for(int i = 0; i < VOX_RES; i++)
        {
            for(int j = 0; j < VOX_RES; j++)
            {
                for(int k = 0; k < VOX_RES; k++)
                {
                    boxCenter.x = -0.5f + (2 * i * boxHalfExtends) + boxHalfExtends;
                    boxCenter.y = -0.5f + (2 * j * boxHalfExtends) + boxHalfExtends;
                    boxCenter.z = -0.5f + (2 * k * boxHalfExtends) + boxHalfExtends;
                    var colliders = Physics.OverlapBox(boxCenter, boxHalfExtends * Vector3.one);
                    vox[i, j, k] = (colliders.Length > 0) ? 1 : 0;
                }
            }
        }
    }

    // overloaded method for creating sparse voxel representaitions
    public void boxVoxel(ref List<float> vox, int shapeNum)
    {
        int l2 = VOX_RES * VOX_RES;
        int l3 = VOX_RES;

        float boxHalfExtends = 1f / (2 * VOX_RES);
        Vector3 boxCenter = Vector3.zero;
        for(int i = 0; i < VOX_RES; i++)
        {
            for(int j = 0; j < VOX_RES; j++)
            {
                for(int k = 0; k < VOX_RES; k++)
                {
                    boxCenter.x = -0.5f + (2 * i * boxHalfExtends) + boxHalfExtends;
                    boxCenter.y = -0.5f + (2 * j * boxHalfExtends) + boxHalfExtends;
                    boxCenter.z = -0.5f + (2 * k * boxHalfExtends) + boxHalfExtends;
                    var colliders = Physics.OverlapBox(boxCenter, boxHalfExtends * Vector3.one);
                    if(colliders.Length > 0)
                    {
                        vox.Add(shapeNum);
                        vox.Add((i * l2) + (j * l3) + k);
                    }
                }
            }
        }
    }

    // To visualize the voxel representation created by boxVoxel()
    void TestVoxel(int[,,] vox)
    {
        int voxRes = vox.GetLength(0);
        GameObject [,,] cubeCollection = new GameObject[voxRes, voxRes, voxRes];
        GameObject shape = new GameObject("shape");
        float boxHalfExtends = 1f / (2 * voxRes);
        Vector3 boxCenter = Vector3.zero;
        for(int i = 0; i < voxRes; i++)
        {
            for(int j = 0; j < voxRes; j++)
            {
                for(int k = 0; k < voxRes; k++)
                {
                    if(vox[i, j, k] == 1)
                    {
                        boxCenter.x = -0.5f + (2 * i * boxHalfExtends) + boxHalfExtends;
                        boxCenter.y = -0.5f + (2 * j * boxHalfExtends) + boxHalfExtends;
                        boxCenter.z = -0.5f + (2 * k * boxHalfExtends) + boxHalfExtends;

                        cubeCollection[i, j, k] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cubeCollection[i, j, k].transform.position = boxCenter;
                        cubeCollection[i, j, k].transform.localScale = 2 * boxHalfExtends * Vector3.one;
                        cubeCollection[i, j, k].transform.parent = shape.transform;
                    }
                }
            }
        }
    }

    /**
     * Note that the shape is already rotated before calling this function
     * startPointGridBoxCor is the starting grid point where the shape can be placed
     * endPointGridBoxCor is the ending grid point where the shape can be placed
     * boxesCenterShape and boxesHalfExtends can be further used for veryfing shape collision as done in FindValidPos
     */
    public void FindShapeCorners(
        GameObject shape,
        ref Vector3 startPointGridBoxCor,
        ref Vector3 endPointGridBoxCor,
        ref Vector3[] boxesCenterShape,
        ref Vector3[] boxesHalfExtends)
    {

        Vector3 boxCor = -0.5f * Vector3.one;
        BoxCollider[] boxes = shape.transform.GetComponents<BoxCollider>();

        // Center of the box w.r.t. the rotated shape
        boxesCenterShape = new Vector3[boxes.Length];
        boxesHalfExtends = new Vector3[boxes.Length];
        Vector3 _boxesHalfExtends;
        float offLX = 0.0f, offLY = 0.0f, offLZ = 0.0f;
        float offHX = 0.0f, offHY = 0.0f, offHZ = 0.0f;
        for(int i = 0; i < boxes.Length; i++)
        {
            boxesCenterShape[i] = shape.transform.TransformPoint(boxes[i].center)  - shape.transform.position;
            _boxesHalfExtends = shape.transform.TransformPoint(0.5f * boxes[i].size) - shape.transform.position;
            boxesHalfExtends[i] = new Vector3(Mathf.Abs(_boxesHalfExtends.x),
                                  Mathf.Abs(_boxesHalfExtends.y),
                                  Mathf.Abs(_boxesHalfExtends.z));

            // Getting the corners of the rotated object
            offLX = ((boxesCenterShape[i].x - boxesHalfExtends[i].x) < offLX) ?
                (boxesCenterShape[i].x - boxesHalfExtends[i].x) : offLX;
            offLY = ((boxesCenterShape[i].y - boxesHalfExtends[i].y) < offLY) ?
                (boxesCenterShape[i].y - boxesHalfExtends[i].y) : offLY;
            offLZ = ((boxesCenterShape[i].z - boxesHalfExtends[i].z) < offLZ) ?
                (boxesCenterShape[i].z - boxesHalfExtends[i].z) : offLZ;
            offHX = ((boxesCenterShape[i].x + boxesHalfExtends[i].x) > offHX) ?
                (boxesCenterShape[i].x + boxesHalfExtends[i].x) : offHX;
            offHY = ((boxesCenterShape[i].y + boxesHalfExtends[i].y) > offHY) ?
                (boxesCenterShape[i].y + boxesHalfExtends[i].y) : offHY;
            offHZ = ((boxesCenterShape[i].z + boxesHalfExtends[i].z) > offHZ) ?
                (boxesCenterShape[i].z + boxesHalfExtends[i].z) : offHZ;
        }

        /**
         * Final corer offset to make sure object is inside the
         * we select the lower and upper offset half of the shape
         * corOffShapeWorldL: Lower offset half of the shape
         * corOffShapeWorldH: Upper offset half of the shape
         * startPointGridBoxCor: the exact grid point to start putting the shape
         * endPointGridBoxCor:   the exact grid point to end putting the shape
         */

        float corOffLX, corOffLY, corOffLZ;
        float corOffHX, corOffHY, corOffHZ;

        corOffLX = Mathf.Abs(offLX);
        corOffLY = Mathf.Abs(offLY);
        corOffLZ = Mathf.Abs(offLZ);
        corOffHX = offHX;
        corOffHY = offHY;
        corOffHZ = offHZ;

        Vector3 corOffShapeWorldL = new Vector3(corOffLX, corOffLY, corOffLZ);
        Vector3 corOffShapeWorldH = new Vector3(corOffHX, corOffHY, corOffHZ);

        startPointGridBoxCor = new Vector3(Mathf.CeilToInt(GRID_RES * corOffShapeWorldL.x),
                                           Mathf.CeilToInt(GRID_RES * corOffShapeWorldL.y),
                                           Mathf.CeilToInt(GRID_RES * corOffShapeWorldL.z));

        endPointGridBoxCor = new Vector3(GRID_RES - Mathf.CeilToInt(GRID_RES * corOffShapeWorldH.x),
                                         GRID_RES - Mathf.CeilToInt(GRID_RES * corOffShapeWorldH.y),
                                         GRID_RES - Mathf.CeilToInt(GRID_RES * corOffShapeWorldH.z));
    }


    /**
     * Based on the MoveVarShape function in packEvolver
     * Note that here the shape is currently at (100, 100, 100)
     * Returns the possible location for putting a shape
     * The ceter of mass of each shape is put at a location belonging to [0, MOVE_RES-1]^3
     */
    void FindValidPos(GameObject shape, int r1=0, int r2=0)
    {
        Vector3 boxCor = -0.5f * Vector3.one;
        Vector3 startPointGridBoxCor =  new Vector3();
        Vector3 endPointGridBoxCor = new Vector3();
        Vector3[] boxesCenterShape = null;
        Vector3[] boxesHalfExtends = null;
        FindShapeCorners(
            shape,
            ref startPointGridBoxCor,
            ref endPointGridBoxCor,
            ref boxesCenterShape,
            ref boxesHalfExtends);

        /**
         * curPointGridBoxCor: We define a grid space w.r.t. the box corner
         *                   : (0, 0, 0) equates to the boxCorner
         *                   : start with (1, 1, 1) so that nothing surely touches
         * curPointContBoxCor: Location of current point w.r.t to box corner in continuous space
         * curPointContBox   : Location of current point w.r.t to box coordinate system
         * curPointContWorld : Location of current point w.r.t to world coordinate system
         */

        Vector3 curPointGridBoxCor = startPointGridBoxCor;
        Vector3 curPointContBoxCor;
        Vector3 curPointContBox;
        Vector3 curPointContWorld = Vector3.zero;

        bool shapeColliding;
        while(true)
        {
            if(curPointGridBoxCor.y > endPointGridBoxCor.y)
            {
                break;
            }

            // Series of tranformations
            // curPointGridBoxCor -> curPointContBoxCor
            // curPointContBoxCor -> curPointContBox
            // curPointContBox    -> curPointContWorld
            curPointContBoxCor = GRID_SIZE * curPointGridBoxCor;
            curPointContBox = curPointContBoxCor + boxCor;
            curPointContWorld = curPointContBox;
            shapeColliding = false;
            for(int i = 0; i < boxesCenterShape.Length; i++)
            {
                if(Physics.CheckBox(boxesCenterShape[i] + curPointContWorld,
                                    boxesHalfExtends[i],
                                    Quaternion.identity))
                {
                    shapeColliding = true;
                    break;
                }
            }
            if(!shapeColliding)
            {
                validPos[r1, r2,
                         (int)curPointGridBoxCor.x - 1,
                         (int)curPointGridBoxCor.y - 1,
                         (int)curPointGridBoxCor.z - 1] = 1;
            }

            if(curPointGridBoxCor.x < endPointGridBoxCor.x)
            {
                curPointGridBoxCor.x++;
            }
            else if(curPointGridBoxCor.z < endPointGridBoxCor.z)
            {
                curPointGridBoxCor.x = startPointGridBoxCor.x;
                curPointGridBoxCor.z++;
            }
            else
            {
                curPointGridBoxCor.x = startPointGridBoxCor.x;
                curPointGridBoxCor.z = startPointGridBoxCor.z;
                curPointGridBoxCor.y++;
            }
        }
    }


    void RemoveUnnessPos(int r1=0, int r2=0)
    {
        int[,,,,] tempValidPos = (int[,,,,])validPos.Clone();

        for(int i1 = 1; i1 < MOV_RES - 1; i1++)
        {
            for(int i2 = 1; i2 < MOV_RES - 1; i2++)
            {
                for(int i3 = 1; i3 < MOV_RES - 1; i3++)
                {
                    // Not a surface condition
                    if(    tempValidPos[r1, r2, i1,     i2,     i3    ] == 1
                        && tempValidPos[r1, r2, i1 - 1, i2,     i3    ] == 1
                        && tempValidPos[r1, r2, i1,     i2 - 1, i3    ] == 1
                        && tempValidPos[r1, r2, i1,     i2,     i3 - 1] == 1
                        && tempValidPos[r1, r2, i1 + 1, i2,     i3    ] == 1
                        && tempValidPos[r1, r2, i1,     i2 + 1, i3    ] == 1
                        && tempValidPos[r1, r2, i1,     i2,     i3 + 1] == 1)
                    {
                        validPos[r1, r2, i1, i2, i3] = 0;
                    }
                }
            }
        }
    }


    void UnionPos(ref int[,,] uniValidPos)
    {
        for(int i1 = 0; i1 < MOV_RES; i1++)
        {
            for(int i2 = 0; i2 < MOV_RES; i2++)
            {
                for(int i3 = 0; i3 < MOV_RES; i3++)
                {
                    bool breakFlag = false;
                    for(int r1 = 0; r1 < packEvol.r1Value.Count; r1++)
                    {
                        for(int r2 = 0; r2 < packEvol.r2Value.Count; r2++)
                        {
                            if(validPos[r1, r2, i1, i2, i3] == 1)
                            {
                                breakFlag = true;
                                break;
                            }
                        }
                        if(breakFlag)
                        {
                            break;
                        }
                    }
                    if(breakFlag)
                    {
                        uniValidPos[i1, i2, i3] = 1;
                    }
                }
            }
        }
    }


    // Returns the possible rotations for a particular position
    void PossiRot(Vector3Int chosPos, ref int[,] posRot)
    {
        for(int r1 = 0; r1 < packEvol.r1Value.Count; r1++)
        {
            for(int r2 = 0; r2 < packEvol.r2Value.Count; r2++)
            {
                if(validPos[r1, r2, chosPos.x, chosPos.y, chosPos.z] == 1)
                {
                    posRot[r1, r2] = 1;
                }
            }
        }
    }


    // Adds the observations from a 3D array
    void Add3DObs(ref int[,,] arr3D)
    {
        for(int i = 0; i < arr3D.GetLength(0); i++)
        {
            for(int j = 0; j < arr3D.GetLength(1); j++)
            {
                for(int k = 0; k < arr3D.GetLength(2); k++)
                {
                    AddVectorObs(arr3D[i, j, k]);
                }
            }
        }
    }


    // overloaded method
    // Adds the observations from a 5D array
    // on the slice with x=r1, y=r2 is added
    void Add3DObs(ref int[,,,,] arr5D, int r1=0, int r2=0)
    {
        for(int i = 0; i < arr5D.GetLength(2); i++)
        {
            for(int j = 0; j < arr5D.GetLength(3); j++)
            {
                for(int k = 0; k < arr5D.GetLength(4); k++)
                {
                    AddVectorObs(arr5D[r1, r2, i, j, k]);
                }
            }
        }
    }


    // Adds the observations from a 2D array
    void Add2DObs(ref int[,] arr2D)
    {
        for(int i = 0; i < arr2D.GetLength(0); i++)
        {
            for(int j = 0; j < arr2D.GetLength(1); j++)
            {
                    AddVectorObs(arr2D[i, j]);
            }
        }
    }


    // if rotBeforeMov == True, we are assuming that the shape is already rotated
    void UpdateValidPos()
    {
        if(rotBeforeMov)
        {
            // Make all valid pos 0
            for(int i1 = 0; i1 < MOV_RES; i1++)
            {
                for(int i2 = 0; i2 < MOV_RES; i2++)
                {
                    for(int i3 = 0; i3 < MOV_RES; i3++)
                    {
                        validPos[0, 0, i1, i2, i3] = 0;
                    }
                }
            }

            FindValidPos(shapes[actObjNum], 0, 0);
            RemoveUnnessPos(0, 0);
        }
        else
        {
            // Make all valid pos 0
            for(int i1 = 0; i1 < packEvol.r1Value.Count; i1++)
            {
                for(int i2 = 0; i2 < packEvol.r2Value.Count; i2++)
                {
                    for(int i3 = 0; i3 < MOV_RES; i3++)
                    {
                        for(int i4 = 0; i4 < MOV_RES; i4++)
                        {
                            for(int i5 = 0; i5 < MOV_RES; i5++)
                            {
                                validPos[i1, i2, i3, i4, i5] = 0;
                            }
                        }
                    }
                }
            }

            Quaternion rotTemp;
            for(int i = 0; i < packEvol.r1Value.Count; i++)
            {
                for(int j = 0; j < packEvol.r2Value.Count; j++)
                {
                    // Rotate the shape accordingly
                    rotTemp = packEvol.R1R2Rotation(i, j);
                    shapes[actObjNum].transform.rotation = rotTemp;
                    FindValidPos(shapes[actObjNum], i, j);
                    RemoveUnnessPos(i, j);
                }
            }
        }
    }


    void Obs0()
    {
        AddVectorObs(pack.sources.Length);
        if(getGT)
        {
            foreach(StepAction stepAction in gtStepAction)
            {
                AddVectorObs(stepAction.step);
                AddVectorObs(stepAction.stepNum);
                AddVectorObs(stepAction.action.x);
                AddVectorObs(stepAction.action.y);
                AddVectorObs(stepAction.action.z);
            }
            padZeroObs(1 + (pack.sources.Length * 3 * 5));
        }
        else
        {
            padZeroObs(1);
        }
    }


    void Obs1()
    {
        AddVectorObs(vox.Count);
        AddVectorObs(vox);
        padZeroObs(1 + vox.Count);
        vox.Clear();
    }


    void Obs2()
    {
        int[,,] _vox = null;
        boxVoxel(ref _vox);
        Add3DObs(ref _vox);
        padZeroObs(VOX_RES * VOX_RES * VOX_RES);
    }


    void Obs3()
    {
        if(rotBeforeMov)
        {
            padZeroObs(0);
        }
        else
        {
            if(!demoMode)
            {
                UpdateValidPos();
            }
            int[,,] uniValPos = new int[MOV_RES, MOV_RES, MOV_RES];
            UnionPos(ref uniValPos);
            Add3DObs(ref uniValPos);
            padZeroObs(MOV_RES * MOV_RES * MOV_RES);
        }
    }


    void Obs4()
    {
        if(rotBeforeMov)
        {
            if(!demoMode)
            {
                UpdateValidPos();
            }
            Add3DObs(ref validPos, r1: 0, r2: 0);
            padZeroObs(MOV_RES * MOV_RES * MOV_RES);
        }
        else
        {
            int[,] posRot = new int[packEvol.r1Value.Count, packEvol.r2Value.Count];
            PossiRot(actPosIdx, ref posRot);
            Add2DObs(ref posRot);
            padZeroObs(packEvol.r1Value.Count * packEvol.r2Value.Count);
        }
    }

    /***
     * Observation collected according to the gameStep
     */
    public override void CollectObservations()
    {
        Debug.Log("Step: " + step + "StepNum: " + stepNum);
        AddVectorObs(step);
        AddVectorObs(stepNum);
        if(!demoMode)
        {
            switch(step)
            {
            case 0:
                Obs0();
                break;

            case 1:
                Obs1();
                break;

            case 2:
                Obs2();
                break;

            case 3:
                Obs3();
                break;

            case 4:
                Obs4();
                break;
            }
        }
        else
        {
            Obs0();
        }
    }

    // which object to choose
    void Act2(Vector3Int act)
    {
        actObjNum = act.x;
        if(loadPrecompute)
        {
            GameObject _shape = packEvol.LoadShape(pack.sources[actObjNum], loadFromResources: false);
            shapes[actObjNum] = helper.AddShape(_shape, "shape" + actObjNum,
                                                new Vector3(100f, 100f, 100f), Quaternion.identity,
                                                Vector3.one, false, false, keepChild: editorMode);

            shapes[actObjNum].transform.localScale = pack.scales[actObjNum];
        }
    }

    void Act3(Vector3Int act)
    {
        if(rotBeforeMov)
        {
            shapes[actObjNum].transform.rotation = packEvol.R1R2Rotation(act.x / packEvol.r2Value.Count,
                                                                         act.x % packEvol.r2Value.Count);
        }
        else
        {
            actPosIdx = act;
        }
    }


    void Act4(Vector3Int act)
    {
        if(rotBeforeMov)
        {
            actPosIdx = act;
            shapes[actObjNum].transform.position = IdxToConPos(actPosIdx, isStart0:true);
        }
        else
        {
            /**
             * Source: docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/division-operator
             * When you divide two integers, the result is always an integer. For example,
             * the result of 7 / 3 is 2. This is not to be confused with floored division,
             * as the / operator rounds towards zero: -7 / 3 is -2.
             */
            shapes[actObjNum].transform.position = IdxToConPos(actPosIdx, isStart0:true);
            shapes[actObjNum].transform.rotation = packEvol.R1R2Rotation(act.x / packEvol.r2Value.Count,
                                                                         act.x % packEvol.r2Value.Count);
        }

        if(!demoMode)
        {
            // if selected a shape that could not have fitted
            if(validPos.Cast<int>().ToList().Sum() == 0)
            {
                AddReward(-1f);
                Done();
            }
            else
            {
                Info shape_info = shapes[actObjNum].GetComponent<Info>();
                float shape_vol = shape_info.mass
                                  * shapes[actObjNum].transform.localScale.x
                                  * shapes[actObjNum].transform.localScale.y
                                  * shapes[actObjNum].transform.localScale.z;
                float reward = shape_vol / pack.efficiency;
                AddReward(reward);
                Debug.Log(reward);
            }
        }

        if(editorMode && !rotBeforeMov)
        {
            int[] verifyGt = new int[packEvol.r1Value.Count * packEvol.r2Value.Count];
            for(int i = 0; i < packEvol.r1Value.Count; i++)
            {
                for(int j = 0; j < packEvol.r2Value.Count; j++)
                {
                    verifyGt[(i * packEvol.r2Value.Count) + j] = validPos[i, j, actPosIdx.x, actPosIdx.y, actPosIdx.z];
                }
            }
            Debug.Log("Possible Rotations: " + string.Join(" ", new List<int>(verifyGt)
                                                                .ConvertAll(r => r.ToString())
                                                                .ToArray()));
            Debug.Log("Chosen Rotation: " + act.x);
        }
    }

    bool VerifyAction(Vector3Int act)
    {
        switch(step)
        {
        case 0:
        case 1:
            return true;
            break;
        case 2:
            if(shapes[act.x] ==  null ||
               shapes[act.x].transform.position == 100f * Vector3.one)
            {
                return true;
            }
            break;
        case 3:
            if(validPos.Cast<int>().ToList().Sum() == 0)
            {
                return true;
            }
            else
            {
                if(rotBeforeMov)
                {
                    return true;
                }
                else
                {
                    for(int r1 = 0; r1 < packEvol.r1Value.Count; r1++)
                    {
                        for(int r2 = 0; r2 < packEvol.r2Value.Count; r2++)
                        {
                            if(validPos[r1, r2, act.x, act.y, act.z] == 1)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            break;

        case 4:
            if(validPos.Cast<int>().ToList().Sum() == 0)
            {
                return true;
            }
            else
            {
                if(rotBeforeMov)
                {
                    if(validPos[0, 0, act.x, act.y, act.z] == 1)
                    {
                        return true;
                    }
                }
                else
                {
                    if(validPos[
                        act.x / packEvol.r2Value.Count,
                        act.x % packEvol.r2Value.Count,
                        actPosIdx.x,
                        actPosIdx.y,
                        actPosIdx.z
                        ] == 1)
                    {
                        return true;
                    }
                }
            }
            break;
        }
        return false;
    }

    /**
     * Action executed according to the gameStep
     */
    public override void AgentAction(float[] vectorAction, string textAction)
    {


        Vector3Int act;
        if(editorMode)
        {
	    // gtStepAction is either from the saved act or the groundtruth act
            act = gtPack.GetGtAction(step, stepNum, gtStepAction);
	    // Hacky way to stop the environment in demo and EditorMode
	    if(step > 1 && act == Vector3Int.down)
	    {
                Done();
		// Hacky way to remove shapes outside box in demoMode
		if(demoMode)
		{
                    for(int i = 0; i < shapes.Length; i++)
                    {
			float _x = shapes[i].transform.position.x;
			float _z = shapes[i].transform.position.z;
			if(((_x * _x) + (_z * _z)) >= 1.4f)
			{
                            UnityEngine.Object.DestroyImmediate(shapes[i]);
			}
                    }

		}
		return;
	    }
        }
        else
        {
            if(!IsFloatInt(vectorAction[0]) || !IsFloatInt(vectorAction[1]) || !IsFloatInt(vectorAction[2]))
            {
                Debug.Log("Wrong Action");
            }
            act = new Vector3Int(FloatToInt(vectorAction[0]),
                                 FloatToInt(vectorAction[1]),
                                 FloatToInt(vectorAction[2]));
        }

        bool valid_action = true;
        if(!demoMode)
        {
            valid_action = VerifyAction(act);
            Debug.Assert(valid_action, "Not possible to execute the chosen action");
        }

        if(!valid_action)
        {
           AddReward(-1f);
           Done();
        }
        else
        {
            switch(step)
            {
            case 0:
                break;

            case 1:
                break;

            case 2:
                Act2(act);
                break;

            case 3:
                Act3(act);
                break;

            case 4:
                Act4(act);
                break;
            }
        }


        // updating the step and stepNum
        Tuple<int, int> temp = UpdateStepStepNum(step, stepNum, pack.sources.Length);
        if(temp.Item1 == -1)
        {
            Done();
        }
        else
        {
            step = temp.Item1;
            stepNum = temp.Item2;
        }
    }
}
