using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/**
 * To generate the groundtruth actions from the pack
 */

public class GtPack : MonoBehaviour {

	PackingAgent packingAgent;
	PackEvolver packEvol;
	int GRID_RES;
	float GRID_SIZE;

	void Start()
	{
		packingAgent = GetComponent<PackingAgent>();
		packEvol = GetComponent<PackEvolver>();

		// setting up global variables
        GRID_RES = packingAgent.MOV_RES + 1;
        GRID_SIZE = 1f / (float) GRID_RES;
	}

	/**
	 * Based on the MoveVarShape function in packEvolver
	 * Returns: the maximum the current shape can move down without overlapping
	 *		  : the shape with which it overlaps on moving down any further
	 */
	Tuple<int, int> FindNextCon(GameObject shape, int totalShapes)
	{
		int layerMask = 1 << 11;
		layerMask = ~layerMask;

		Vector3 startPointGridBoxCor =  new Vector3();
        Vector3 endPointGridBoxCor = new Vector3();
        Vector3[] boxesCenterShape = null;
        Vector3[] boxesHalfExtends = null;
        packingAgent.FindShapeCorners(
            shape,
            ref startPointGridBoxCor,
            ref endPointGridBoxCor,
            ref boxesCenterShape,
            ref boxesHalfExtends);

        // here start point is the current position
        // end end point is the lowest possible position
		Vector3Int startPointGrid = packingAgent.ConToIdxPos(shape.transform.position, isStart0:false);
		int startPointGridY = startPointGrid.y;

		int endPointGridY =  packingAgent.FloatToInt(startPointGridBoxCor.y);
		int curPointGridY = startPointGridY;
		Vector3 curPointContWorld = shape.transform.position;

		bool isShapeColliding;
		int shapeCollidingIdx = -1;
		while(true)
		{
			isShapeColliding = false;
			for(int i = 0; i < boxesCenterShape.Length; i++)
			{
				var colliders = Physics.OverlapBox(boxesCenterShape[i] + curPointContWorld, 
				                                   boxesHalfExtends[i],
				                                   Quaternion.identity,
				                                   layerMask);
				if(colliders.Length > 0)
				{
					isShapeColliding = true;
					foreach(var collider in colliders)
					{
						if(collider.gameObject.transform.name.Contains("shape"))
						{
							shapeCollidingIdx = int.Parse(collider.gameObject.transform.name.Replace("shape", ""));
							break;
						}
					}
					break;
				}
			}
			if(isShapeColliding)
			{
				break;
			}
			if(curPointGridY == endPointGridY)
			{
				break;
			}

			curPointGridY--;
			curPointContWorld.y -= GRID_SIZE;
		}

		if(!isShapeColliding)
		{
			shapeCollidingIdx = totalShapes;
			curPointGridY--;
		}

		return Tuple.Create<int, int>(shapeCollidingIdx, startPointGridY - curPointGridY - 1);
	}


	// To create 2D SubArray
	public T[,] SubArray2D<T>(T[,] data, int index1, int length1, int index2, int length2)
	{
		T[,] result = new T[length1, length2];
		for(int i = 0; i < length1; i++)
		{
			for(int j = 0; j < length2; j++)
			{
				result[i, j] = data[index1 + i, index2 + j];
			}	
		}
		return result;
	}

	// BFS search to return the list of all connected nodes to node i
	List<int> GetConnectedNodes(int i, int[,] adjMat)
	{
		List<int> conNode = new List<int>();
		Stack<int> nodesToPop = new Stack<int>();
		bool[] nodesSeen = new bool[adjMat.GetLength(0)]; 

		nodesToPop.Push(i);
		nodesSeen[i] = true;
		conNode.Add(i);
		while(nodesToPop.Count != 0)
		{
			int newI = nodesToPop.Pop();
			for(int j = 0; j < adjMat.GetLength(0); j++)
			{
				if(adjMat[newI, j] == 1 && (!nodesSeen[j]))
				{
					nodesToPop.Push(j);
					nodesSeen[j] = true;
					conNode.Add(j);
				}
			}
		}

		return conNode;
	}

	// Tighten and Connect the Pack
	public int[,] TightConPack(ref Pack pack)
	{
		PackEvolver packEvol = GetComponent<PackEvolver>();
		GameObject[] shapes = packEvol.VisualizePack(pack, loadFromResources:false, keepRenderer:false);

		// An Adjagency matrix. The (n+1)th node represents the bottom
		int[,] shapeCon = new int[shapes.Length + 1, shapes.Length + 1];
		for(int i = 0; i < shapes.Length; i++)
		{
			List <int> conBotIdx = GetConnectedNodes(shapes.Length, shapeCon);
			// unConBotIdx: unconnected to bottom index
			int unConBotIdx = -1;
			for(int j = 0; j < shapes.Length; j++)
			{
				if(!conBotIdx.Contains(j))
				{
					unConBotIdx = j;
					break;
				}	
			}
			Debug.Assert(unConBotIdx != -1, "Error in code as all nodes already connected to bottom");


			// List of nodes that are connected to unConBotIdx
			List <int> unConBotIdxs = GetConnectedNodes(unConBotIdx, SubArray2D(shapeCon, 0, shapes.Length, 0, shapes.Length));


			foreach(int idx in unConBotIdxs)
			{
				packEvol.SetLayerRecursively(shapes[idx], 11);
			}

			int minUnConBotIdx = -1;
			int minUnConBotVal = 100;
			int minUnConBotCon = -1;
			foreach(int idx in unConBotIdxs)
			{
				var temp = FindNextCon(shapes[idx], shapes.Length);
				if(temp.Item2 < minUnConBotVal)
				{
					minUnConBotIdx = idx;
					minUnConBotVal = temp.Item2;
					minUnConBotCon = temp.Item1;
				}
			}
			shapeCon[minUnConBotIdx, minUnConBotCon] = 1;
			shapeCon[minUnConBotCon, minUnConBotIdx] = 1;

			foreach(int idx in unConBotIdxs)
			{
				Vector3 temp = shapes[idx].transform.position;
				temp.y -= ((float) minUnConBotVal) * GRID_SIZE;
				shapes[idx].transform.position = temp;
				packEvol.SetLayerRecursively(shapes[idx], 0);
			}
		}

		int shapesLength = shapes.Length;
		for(int i = 0; i < shapesLength; i++)
		{
			pack.positions[i] = shapes[i].transform.position;
			UnityEngine.Object.DestroyImmediate(shapes[i]);
		}
		return shapeCon;
	}

	public Vector3Int GetGtAction(int step, int stepNum, List<StepAction> stepAct)
	{
		foreach(StepAction curStepAct in stepAct)
		{
			if(curStepAct.step == step && curStepAct.stepNum == stepNum)
			{
				return curStepAct.action;
			}
		}

		return Vector3Int.down;
	}

	// return an n X (n+1) matrix
	// the ith row represents all the "shapes" that lie in the bottom of ith shape
	// (n+1)th column represents the bottom of the box
	// Assumption: shapes should already be in the box before calling this function
	int[,] BottomShapeMatrix(GameObject[] shapes)
	{
		int[,] shapeCon = new int[shapes.Length, shapes.Length + 1];
		for(int i = 0; i < shapes.Length; i++)
		{
			List<int> shapeCollidingIdx = FindAllBottom(shapes[i], shapes.Length);

			foreach(int shapeIdx in shapeCollidingIdx)
			{
				shapeCon[i, shapeIdx] = 1;
			}
		}
		return shapeCon;
	}


	List<int> ShapeOrderMixedBFSDFS(Pack pack)
	{
		GameObject[] shapes = packEvol.VisualizePack(pack, loadFromResources:false, keepRenderer:false);
		int[,] shapeCon = BottomShapeMatrix(shapes);

		int shapesLength = shapes.Length;
		for(int i = 0; i < shapesLength; i++)
		{
			UnityEngine.Object.DestroyImmediate(shapes[i]);
		}

		List<int> shapesAlreadyAdded = new List<int>();

		shapesAlreadyAdded.Add(pack.sources.Length);
		for(int i = 0; i < pack.sources.Length; i++)
		{
			// find all possible shapes that can be added next
			List<int> possibleShapesToAdd = new List<int>();
			foreach(int shapeAlreadyAdded in shapesAlreadyAdded)
			{
				for(int j = 0; j < pack.sources.Length; j++)
				{
					if(shapeCon[j, shapeAlreadyAdded] == 1)
					{
						if(!possibleShapesToAdd.Contains(j) && !shapesAlreadyAdded.Contains(j))
						{
							possibleShapesToAdd.Add(j);
						}
					}
				}
			}

			// chose one of the possible shapes and add it
			int shapeIndex = UnityEngine.Random.Range(0, possibleShapesToAdd.Count);
			shapesAlreadyAdded.Add(possibleShapesToAdd.ElementAt(shapeIndex));
		}

		return shapesAlreadyAdded;
	}


	public List<StepAction> GetGtStepAction(ref Pack pack, bool rotBeforeMov=false)
	{
		TightConPack(ref pack);
		List<int> shapeOrder = ShapeOrderMixedBFSDFS(pack);

		// remove as the first shape represents the bottom of the box
		shapeOrder.RemoveAt(0);
		List<StepAction> stepActList= new List<StepAction>();
		int step = 2;
		int stepNum = 0;
		while(step != -1)
		{
			Vector3Int curAct = Vector3Int.zero;
			int curShapeIdx = shapeOrder.ElementAt(stepNum);
			switch(step)
			{
			case 2:
				curAct.x = curShapeIdx;
				break;
			case 3:
				if(rotBeforeMov)
				{
					Tuple <int, int> _curAct = packEvol.RotationR1R2(pack.rotations[curShapeIdx]);
					curAct.x = (_curAct.Item1 * packEvol.r2Value.Count) + _curAct.Item2;
				}
				else
				{
					curAct = packingAgent.ConToIdxPos(pack.positions[curShapeIdx], isStart0:true);
				}
				break;
			case 4:
				if(rotBeforeMov)
				{
					curAct = packingAgent.ConToIdxPos(pack.positions[curShapeIdx], isStart0:true);
				}
				else
				{
					Tuple <int, int> _curAct = packEvol.RotationR1R2(pack.rotations[curShapeIdx]);
					curAct.x = (_curAct.Item1 * packEvol.r2Value.Count) + _curAct.Item2;
				}
				break;
			}

			stepActList.Add(new StepAction(step, stepNum, curAct));
			Tuple<int, int> temp = packingAgent.UpdateStepStepNum(step, stepNum, pack.sources.Length);
			step = temp.Item1;
			stepNum = temp.Item2;
		}

        return stepActList;
	}

	/**
	 * Based on the MoveVarShape function in packEvolves
	 * Returns: index of all the shapes that are at bottom of a shape
	 * index == totalShapes represents the bottom of the box
	 * there are suttle idfferences betweeen FindAllBottom and FindNextCon like the use of layerMask
	 */
	List<int> FindAllBottom(GameObject shape, int totalShapes)
	{
		int layerMask = 1 << 11;
		layerMask = ~layerMask;
		packEvol.SetLayerRecursively(shape, 11);

		Vector3 startPointGridBoxCor =  new Vector3();
        Vector3 endPointGridBoxCor = new Vector3();
        Vector3[] boxesCenterShape = null;
        Vector3[] boxesHalfExtends = null;
        packingAgent.FindShapeCorners(
            shape,
            ref startPointGridBoxCor,
            ref endPointGridBoxCor,
            ref boxesCenterShape,
            ref boxesHalfExtends);

        // here start point is the current position
        // end end point is the lowest possible position
		Vector3Int startPointGrid = packingAgent.ConToIdxPos(shape.transform.position, isStart0:false);
		int startPointGridY = startPointGrid.y;

		int endPointGridY =  packingAgent.FloatToInt(startPointGridBoxCor.y);
		int curPointGridY = startPointGridY;
		Vector3 curPointContWorld = shape.transform.position;

		bool isShapeColliding;
		List<int> shapeCollidingIdx = new List<int>();
		while(true)
		{
			isShapeColliding = false;
			for(int i = 0; i < boxesCenterShape.Length; i++)
			{
				var colliders = Physics.OverlapBox(boxesCenterShape[i] + curPointContWorld, 
				                                   boxesHalfExtends[i],
				                                   Quaternion.identity,
				                                   layerMask);
				if(colliders.Length > 0)
				{
					isShapeColliding = true;
					foreach(var collider in colliders)
					{
						if(collider.gameObject.transform.name.Contains("shape"))
						{
							int _shapeCollidingIdx = int.Parse(collider.gameObject.transform.name.Replace("shape", ""));
							if(!shapeCollidingIdx.Contains(_shapeCollidingIdx))
							{
								shapeCollidingIdx.Add(_shapeCollidingIdx);
							}
						}
					}
				}
			}

			if(isShapeColliding)
			{
				break;
			}

			// box bottom should only be added is the shape is not colliding with other shapes
			// at the bottomost position
			if(curPointGridY == endPointGridY)
			{
				shapeCollidingIdx.Add(totalShapes);
				break;
			}

			curPointGridY--;
			curPointContWorld.y -= GRID_SIZE;
		}

		packEvol.SetLayerRecursively(shape, 0);
		return shapeCollidingIdx;
	}

	/**
	 * Assumpition is that the shape is outside the box and rotated
	 */
	public bool WillShapeCollide(GameObject shape, Vector3 position)
	{
		Vector3 startPointGridBoxCor =  new Vector3();
        Vector3 endPointGridBoxCor = new Vector3();
        Vector3[] boxesCenterShape = null;
        Vector3[] boxesHalfExtends = null;
        packingAgent.FindShapeCorners(
            shape,
            ref startPointGridBoxCor,
            ref endPointGridBoxCor,
            ref boxesCenterShape,
            ref boxesHalfExtends);

        Vector3Int shapePosIdx = packingAgent.ConToIdxPos(position, isStart0: false);

        bool shapeColliding = true;
        // enshure shape in inside the box completely
        if(shapePosIdx.x >= startPointGridBoxCor.x
			&& shapePosIdx.y >= startPointGridBoxCor.y
			&& shapePosIdx.z >= startPointGridBoxCor.z
			&& shapePosIdx.x <= endPointGridBoxCor.x
			&& shapePosIdx.y <= endPointGridBoxCor.y
			&& shapePosIdx.z <= endPointGridBoxCor.z)
        {
			shapeColliding = false;
			for(int i = 0; i < boxesCenterShape.Length; i++)
			{
                if(Physics.CheckBox(boxesCenterShape[i] + position, 
                                    boxesHalfExtends[i],
                                    Quaternion.identity))
                {
                    shapeColliding = true;
                    break;
                }
            }
        }

        return shapeColliding;
	}

	/**
	 * An imporved version of GetGtStepAction where we capture all the possible valid action for a particular
	 * pack configuration. Note that the actions are only valid in context of the currect pack configuration
	 * and not all possible ways of packing the shapes.
	 */
	public List<StepAction2> GetGtStepAction2(ref Pack pack, List<StepAction> gtStepAction, bool rotBeforeMov=false)
	{
		GameObject[] shapes = packEvol.VisualizePack(pack, loadFromResources:false, keepRenderer:false);

		List<StepAction2> stepActList2 = new List<StepAction2>();
		int stepNum;
		// add all the movements
		for(int i = 0; i <  pack.sources.Length; i++)
		{
			stepNum = i;
			int step;
			if(rotBeforeMov)
			{
				step = 4;
			}
			else
			{
				step = 3;
			}

			List<Vector3Int> action  = new List<Vector3Int>()
			{
				GetGtAction(step, stepNum, gtStepAction)
			};

			stepActList2.Add(new StepAction2(step, stepNum, action));
		}

		// add all the possible rotations
		for(int i = 0; i < pack.sources.Length; i++)
		{
			stepNum = i;
			// old gt shape index
			Vector3Int _ = GetGtAction(2, stepNum, gtStepAction);
			int shapeIndex = _.x;
			GameObject shape = shapes[shapeIndex];
			shape.transform.position = new Vector3(100, 100f, 100f);

			List<Vector3Int> action  = new List<Vector3Int>();
			for(int r1 = 0; r1 < packEvol.r1Value.Count; r1++)
			{
				for(int r2 = 0; r2 < packEvol.r2Value.Count; r2++)
				{
					Quaternion rotTemp = packEvol.R1R2Rotation(r1, r2);
                    shape.transform.rotation = rotTemp;
                    if(!WillShapeCollide(shape, pack.positions[shapeIndex]))
					{
						action.Add(new Vector3Int(r1 * packEvol.r2Value.Count + r2, 0, 0));
			        }
				}
			}

			// put the shape back
			shape.transform.rotation = pack.rotations[shapeIndex];
			shape.transform.position = pack.positions[shapeIndex];

			if(rotBeforeMov)
			{
				stepActList2.Add(new StepAction2(3, stepNum, action));
			}
			else
			{
				stepActList2.Add(new StepAction2(4, stepNum, action));
			}
		}

		// assert all shapes in correct rotation and postition
		for(int i = 0; i < pack.sources.Length; i++)
		{
			Debug.Assert(shapes[i].transform.rotation ==  pack.rotations[i]);
			Debug.Assert(shapes[i].transform.position ==  pack.positions[i]);

		}

		// An n x (n+1) matrix
		// The (n+1)th node represents the bottom
		// represents the botton shapes for each shape
		int[,] shapeCon = BottomShapeMatrix(shapes);

		List<int> shapesAlreadyAdded = new List<int>();
		shapesAlreadyAdded.Add(pack.sources.Length);
		for(int i = 0; i < pack.sources.Length; i++)
		{
			List<Vector3Int> possibleShapesToAdd = new List<Vector3Int>();
			foreach(int shapeAlreadyAdded in shapesAlreadyAdded)
			{
				for(int j = 0; j < pack.sources.Length; j++)
				{
					if(shapeCon[j, shapeAlreadyAdded] == 1)
					{
						if(!possibleShapesToAdd.Contains(new Vector3Int(j, 0, 0)) && !shapesAlreadyAdded.Contains(j))
						{
							possibleShapesToAdd.Add(new Vector3Int(j, 0, 0));
						}
					}
				}
			}

			stepNum = i;
			stepActList2.Add(new StepAction2(2, stepNum, possibleShapesToAdd));

			// add the next shape
			Vector3Int _ = GetGtAction(2, stepNum, gtStepAction);
			// making sure that the next shape added is one of the possibleShapesToAdd
			Debug.Assert(possibleShapesToAdd.Contains(_));
			int shapeIndex = _.x;
			GameObject shape = shapes[shapeIndex];
			shape.transform.position = pack.positions[shapeIndex];
			shapesAlreadyAdded.Add(shapeIndex);
		}

		int shapesLength = shapes.Length;
		for(int i = 0; i < shapesLength; i++)
		{
			UnityEngine.Object.DestroyImmediate(shapes[i]);
		}

		return stepActList2;
	}
}

[System.Serializable]
public class StepAction
{
	public int step;
	public int stepNum;
	public Vector3Int action;

	public StepAction(int step, int stepNum, Vector3Int action)
	{
		this.step = step;
		this.stepNum = stepNum;
		this.action = action;
	}
}

[System.Serializable]
public class StepActions
{
	public List<StepAction> data;

	public StepActions()
	{
		this.data = new List<StepAction>();
	}
}


[System.Serializable]
public class StepAction2
{
	public int step;
	public int stepNum;
	public List<Vector3Int> action;

	public StepAction2(int step, int stepNum, List<Vector3Int> action)
	{
		this.step = step;
		this.stepNum = stepNum;
		this.action = action;
	}
}
