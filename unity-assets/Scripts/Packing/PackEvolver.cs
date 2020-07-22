using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

public class PackEvolver : MonoBehaviour {

	// Define the number of rotations and scales allowed
	const int NUMR1 = 6;
	const int NUMR2 = 4;
	const int NUMSCALE = 5;
	const int GRID_RES = 26;

	// Define the number of chromosmes for packing
	// NUM_CHROMO_GEN: number of chromosomes per generation
	// NUM_GEN: absolute largest number of generations to run the code
	// NUM_GEN_BREAK: break the loop when no increase
	public int NUM_CHROMO_GEN = 50;
	public int NUM_GEN = 1000;
	public int NUM_GEN_BREAK = 100;

	// The relative size of all the shapes are selected in between these
	public float[] ALL_SHAPE_SCALES = new float[2]{0.75f, 1f};
	public float CUR_PACK_SHAPE_SCALE;
	public float RESOLUTION = 0.0125f;

	// All these values are fixed and w.r.t the box in the scene
	// All values are in the local coordinate system of the box
	const float MAX_BOX_HEIGHT = 1f;
	public Vector3 BOX_BLB_CORNER = new Vector3(-0.5f, -0.5f, -0.5f);
	public Vector3 BOX_FRT_CORNER = new Vector3(0.5f,   0.5f,  0.5f);

	public GameObject BOX;
	public GameObject BOX_LID;
	public GameObject BOX_BOTTOM;

	public SortedDictionary<int, Vector2> r1Value= new SortedDictionary<int, Vector2>
	{
		{0, new Vector2(0f  , 0f)},
		{1, new Vector2(90f , 0f)},
		{2, new Vector2(180f, 0f)},
		{3, new Vector2(270f, 0f)},
		{4, new Vector2(0f  , 90f)},
		{5, new Vector2(0f  , 270f)}
	};

	public SortedDictionary<int, float> r2Value= new SortedDictionary<int, float>
	{
		{0, 0f},
		{1, 90f},
		{2, 180f},
		{3, 270f}
	};

	public Quaternion R1R2Rotation(int r1, int r2)
	{
		return Quaternion.Euler(new Vector3(r1Value[r1].x,
		                                    r2Value[r2],
		                                    r1Value[r1].y));
	}

	public System.Tuple<int, int> RotationR1R2(Quaternion rotation)
	{
		for(int i = 0; i < r1Value.Count; i++)
		{
			for(int j = 0; j < r2Value.Count; j++)
			{
				if(Quaternion.Euler(new Vector3(r1Value[i].x,
				                                r2Value[j], 
				                                r1Value[i].y)) 
				   == rotation)
				{
					return System.Tuple.Create<int, int>(i, j);
				}
			}
		}

		return System.Tuple.Create<int, int>(-1, -1);
	}

	void Start()
	{
		BOX = GameObject.Find("Box");
		BOX_LID = GameObject.Find("Box/Lid");
		BOX_BOTTOM = GameObject.Find("Box/Bottom");

//		Debug.Assert(BOX != null                           , "Box not found.");
//		Debug.Assert(BOX.transform.position == Vector3.zero, "Box not is correct position");
//		Debug.Assert(BOX.transform.localScale.y == 1       , "Box is not in correct y scale");
	}

	/**
	 * A helper function for generating random chromosomes
	 * We are only considering roatations in discrete 90 degrees
     * addScale: decide whether to add the scaleParameter to the chormosome or not
	 *         : if True,  chromo =  [shapeOrder; shape1R1; shape1R2; shape1Scale.....; shapeNR1; shapeNR2; shapeNScale]
	 *         : if False, chromo =  [shapeOrder; shape1R1; shape1R2; .....; shapeNR1; shapeNR2]				
	 */
	public int[] _RandomChromo(int size, bool addScale)
	{
		int chromoLen;
		if(addScale)
		{
			chromoLen = size + (size * 3);
		}
		else
		{
			chromoLen = size + (size * 2);
		}

		int[] chromo = new int[chromoLen];

		// Step 1: Initializing the order
		for(int i = 0; i < size; i++)
		{
			chromo[i] = i;
		}
		int r1, r2;
		for(int i = 0; i < size; i++)
		{
			r1 = Random.Range (0, size);
			r2 = chromo[i];
			chromo[i] = chromo[r1];
			chromo[r1] = r2;
		}
		/**
		 * Step 2: Initializing the rotations R1 (0, .... 5)
		 *	     : Initializing the rotations R2 (0, .... 3)
		 *	     : Initializing the scale        (0, .... 4)
		 */

		for(int i = 0; i < size; i++)
		{
			if(addScale)
			{
				chromo[size + (3 * i)] = Random.Range(0, NUMR1);
				chromo[size + (3 * i) + 1] = Random.Range(0, NUMR2);
				chromo[size + (3 * i) + 2] = Random.Range(0, NUMSCALE);
			}
			else
			{
				chromo[size + (2 * i)] = Random.Range(0, NUMR1);
				chromo[size + (2 * i) + 1] = Random.Range(0, NUMR2);
			}

		}

		return chromo;
	}

	/**
	 * Generates a chromo based on the largestShapeChromo Heuristic
	 * Heuristic only applied for the variable chromosomes
	 * Chromosome part for fixShape generated similar as earlier
	 */
	public int[] LargestShapeChromo(int fixSize, int varSize, GameObject[] shapes,
		                            bool useEmptySpace)
	{
		// reusing code for sampling scale and rotation
		int[] varChromo = _RandomChromo(varSize, true);

		// reordering shapes on the basis of their size
		float[] varShapeSize = new float[varSize];
		for(int i = 0; i < varSize; i++)
		{
			Info shape_info = shapes[fixSize + i].GetComponent<Info>();
			float metric;
			if(useEmptySpace)
			{
				metric = shape_info.emptySpace;
			}
			else
			{
				metric = shape_info.mass;
			}
			varShapeSize[i] = metric * Mathf.Pow(2, varChromo[varSize + (3 * i) + 2] - 2);
		}

		// reorder based on size
		var sortedVarShapeSize = varShapeSize.Select((value, index) => new {value, index})
									         .OrderByDescending(vi => vi.value)
									         .Select(vi => vi.index)
									         .ToList();
		for(int i = 0; i < varSize; i++)
		{
			varChromo[i] = sortedVarShapeSize[i];
		}

		return varChromo;
	}

	/** 
	 * Generates a random chromosome
	 * chromo: [fixShapeOrders; fixShape1R1; fixShape1R2; .....; fixShapeNR1; fixShapeNR2;
	 * 			varShapeOrders; varShape1R1; fixShape1R2; varShape1Scale.....; varShapeNR1; varShapeNR2; varShapeNScale]
	 * if largestShape is true, largestShapeChromo heuristic applied to variable part of the chromo
	 *
	 */
	public int[] RandomChromo(int fixSize, int varSize,
		                      bool initLargestShape,
		                      bool useEmptySpace,
		                      GameObject[] shapes=null)
	{
		int fixChromoLen = fixSize + (fixSize * 2);
		int varChromoLen = varSize + (varSize * 3);
		int chromoLen = fixChromoLen + varChromoLen;
		int[] chromo = new int[chromoLen]; 

		// Adding the fixChromo
		int[] _chromo; 
		_chromo = _RandomChromo(fixSize, false);
		for(int i = 0; i < fixChromoLen; i++)
		{
			chromo[i] = _chromo[i];
		}

		// Adding the varChromo
		if(initLargestShape)
		{
			_chromo = LargestShapeChromo(fixSize, varSize, shapes,
				                         useEmptySpace: useEmptySpace);
		}
		else
		{
			_chromo = _RandomChromo(varSize, true);
		}
		for(int i = 0; i < varChromoLen; i++)
		{
			chromo[fixChromoLen + i] = _chromo[i];
		}

		return chromo;
	}

	// Checks whether _a is in array a
	bool InArray(int[] a, int _a)
	{
		for(var i = 0; i < a.Length; i++)
		{
			if(a[i] == _a)
			{
				return true;
			}
		}

		return false;
	}

	// To create subArray
	// source: https://stackoverflow.com/questions/943635/getting-a-sub-array-from-an-existing-array
	public T[] SubArray<T>(T[] data, int index, int length)
	{
		T[] result = new T[length];
		for(int i = 0; i < length; i++)
		{
			result[i] = data[index + i];
		}
		return result;
	}

	// helper function to resturn the cross-over of two chromosomes
	int[] _CrossOver(int[] a, int[] b, bool hasScale)
	{
		if(a == null || a.Length == 0)
		{
			return a;
		}

		int numShapes;
		if(hasScale)
		{
			numShapes = a.Length / 4;
		}
		else
		{
			numShapes = a.Length / 3;
		}

		// Deciding parent1 and parent2
		int[] parent1, parent2;
		int[] child = new int[a.Length];

		int i;
		for(i = 0; i < a.Length; i++)
		{
			child[i] = -1;
		}

		if(Random.value > 0.5)
		{
			parent1 = a;
			parent2 = b;
		} 
		else
		{
			parent1 = b;
			parent2 = a;
		}

		/**
		 * Step 1: Order crossover
		 *       : Copy parent1's chromosomes from orderCrossPoint1 to orderCrossPoint2
		 *       : Then copy parent2
		 */
		int orderCrossPoint = Random.Range(0, numShapes);
		int swapLength = Random.Range(2, numShapes-1);
		int iParent2 = (orderCrossPoint + swapLength) % numShapes;
		for(int _i = 0; _i < numShapes; _i++)
		{
			i = (_i + orderCrossPoint) % numShapes;
			if(_i < swapLength)
			{
				child[i] = parent1[i];
			}
			else
			{
				while(InArray(child, parent2[iParent2]))
				{
					iParent2 = (iParent2 + 1) % numShapes;
				}
				child[i] = parent2[iParent2];
				iParent2 = (iParent2 + 1) % numShapes;
			}
		}

		// Step 2: Crossover of the orientation and scale of the shapes
		int orShCrossPoint = Random.Range(1, a.Length - numShapes);
		for(i = 0; i < orShCrossPoint; i++)
		{
			child[numShapes + i] = parent1[numShapes + i];
		}
		for(i = orShCrossPoint; i < a.Length - numShapes; i++)
		{
			child[numShapes + i] = parent2[numShapes + i];
		}

		return child;
	}

	public int[] CrossOver(int[] a, int[] b, int numFixShapes)
	{
		int fixChromoLen = numFixShapes * 3;
		int varChromoLen = a.Length - fixChromoLen;
		int[] child = new int[a.Length]; 

		// CrossOver of fixChromo part
		int[] _child; 
		_child = _CrossOver(SubArray(a, 0, fixChromoLen),
		                    SubArray(b, 0, fixChromoLen),
		                    false);
		for(int i = 0; i < fixChromoLen; i++)
		{
			child[i] = _child[i];
		}

		// CrossOver of varChromo part
		_child = _CrossOver(SubArray(a, fixChromoLen, varChromoLen),
		                    SubArray(b, fixChromoLen, varChromoLen),
		                    true);
		for(int i = 0; i < varChromoLen; i++)
		{
			child[fixChromoLen + i] = _child[i];
		}

		return child;
	}

	/**
	 * Returns a chromosome with a mutation in a shape
	 * Happens for both fix and var shapes
	 * Mutation in shape means the angle/scale for some shape is changed
	 * packQuality determines if the scale of a shape will be mutated or not
	 */
	public void MutationShape(int[] a, int numFixShapes, bool packQuality=false)
	{
		int fixChromoLen = numFixShapes * 3;
		int numVarShapes = (a.Length - (fixChromoLen)) / 4;

		if(numFixShapes != 0)
		{
			// Mutation in fix shape
			int mutFixPoint = Random.Range(numFixShapes, fixChromoLen);
			switch((mutFixPoint - numFixShapes) % 2)
			{
			case 0:
				a[mutFixPoint] = Random.Range(0, NUMR1);
				break;
			case 1:
				a[mutFixPoint] = Random.Range(0, NUMR2);
				break;
			}
		}

		if(numVarShapes != 0)
		{
			// Mutation in var shape
			int mutVarPoint = Random.Range(fixChromoLen + numVarShapes, a.Length);

			// When only checking the packQuality, we make sure to mutate only rotation
			// This is because mutation in shape does not matter
			if(packQuality)
			{
				while((mutVarPoint - (fixChromoLen + numVarShapes)) % 3 == 2)
				{
					mutVarPoint = Random.Range(fixChromoLen + numVarShapes, a.Length);
				}
			}

			switch((mutVarPoint - (fixChromoLen + numVarShapes)) % 3)
			{
			case 0:
				a[mutVarPoint] = Random.Range(0, NUMR1);
				break;
			case 1:
				a[mutVarPoint] = Random.Range(0, NUMR2);
				break;
			case 2:
				a[mutVarPoint] = Random.Range(0, NUMSCALE);
				break;
			}
		}
	}

	// Returns a chromosome with a mutation in order
	// Happens for both fix and var shapes
	public void MutationOrder(int[] a, int numFixShapes)
	{
		int fixChromoLen = numFixShapes * 3;
		int numVarShapes = (a.Length - (fixChromoLen)) / 4;
		int temp;

		if(numFixShapes != 0)
		{
			// Mutation in fix shape
			int mutFixPoint1 = Random.Range(0, numFixShapes);
			int mutFixPoint2 = Random.Range(0, numFixShapes);

			temp = a[mutFixPoint1];
			a[mutFixPoint1] = a[mutFixPoint2];
			a[mutFixPoint2] = temp;
		}

		if(numVarShapes != 0)
		{
			// Mutation in var shape
			int mutVarPoint1 = Random.Range(0, numVarShapes);
			int mutVarPoint2 = Random.Range(0, numVarShapes);

			temp = a[fixChromoLen + mutVarPoint1];
			a[fixChromoLen + mutVarPoint1] = a[fixChromoLen + mutVarPoint2];
			a[fixChromoLen + mutVarPoint2] = temp;
		}
	}

	/**
	 * Returns the next generation of chromosomes
	 * We store the oldChromoEffi, i.e. the set of chromosomes for which the
	 * efficiency is already calculated to prevent recalculations
	 * packQuality determines if the scale of the shape plays a role in next generation generation
	 * useEmptySpace: determines if we want to use empty space as metric or object mass as metric
	 */
	public int[][] NextGeneration(GameObject[] shapes, int numFixShapes, int[][] chromosomes,
								  ref float[] oldChromoEffi, bool useEmptySpace, bool packQuality=false)
	{
		int[][] nextGeneration = new int[chromosomes.Length][];

		/**
		 * Step 1: Calculate the efficiency of the current chromosomes
		 * 		 : Reusing the already calculated chromosome efficiencies
         */
		float[] chromoEffi = new float[chromosomes.Length];
		int oldChromoEffiLength = (oldChromoEffi == null) ? 0 : oldChromoEffi.Length;
		for(int i = 0; i < chromosomes.Length; i++ )
		{
			if(i < oldChromoEffiLength)
			{
				chromoEffi[i] = oldChromoEffi[i];
			}
			else
			{
				chromoEffi[i] = CalcEfficiency(shapes, numFixShapes, chromosomes[i],
											   resetLater:true, packQuality:packQuality,
											   useEmptySpace:useEmptySpace);
			}

		}

		// Step 2: Select the best chromosomes
		var sortedChromos = chromoEffi.Select((value, index) => new {value, index})
									  .OrderByDescending(vi => vi.value)
									  .ToList();
		List<int> sortedChromoIdx = sortedChromos.Select(x => x.index).ToList();

		if(oldChromoEffiLength != Mathf.FloorToInt(chromosomes.Length / 2f))
		{
			oldChromoEffi = new float[Mathf.FloorToInt(chromosomes.Length / 2f)];
		}

		int currChromoIdx;
		for(int i = 0; i < Mathf.CeilToInt(chromosomes.Length / 4f); i++)
		{
			currChromoIdx = sortedChromoIdx[0];
			nextGeneration[i] = chromosomes[currChromoIdx];
			oldChromoEffi[i] = chromoEffi[currChromoIdx];
			sortedChromoIdx.RemoveAt(0);
		}

		// Step 3: Randomly select some unselected chromosomes to maintain diversity
		int random;
		for(int i = Mathf.CeilToInt(chromosomes.Length / 4f); 
		        i < Mathf.FloorToInt(chromosomes.Length / 2f); i++)
		{
			random = Random.Range(0, sortedChromoIdx.Count);
			currChromoIdx = sortedChromoIdx[random];
			nextGeneration[i] = chromosomes[currChromoIdx];
			oldChromoEffi[i] = chromoEffi[currChromoIdx];
			sortedChromoIdx.RemoveAt(random);
		}

		// Step 4: Create new children
		int random1;
		int random2;
		for(int i = Mathf.FloorToInt(chromosomes.Length / 2f); i < chromosomes.Length; i++)
		{
			random1 = Random.Range(0, Mathf.FloorToInt(chromosomes.Length / 2f));
			random2 = Random.Range(0, Mathf.FloorToInt(chromosomes.Length / 2f));
			// To make sure tha we get two different parents
			int _=0;
			while(Mathf.FloorToInt(chromosomes.Length / 2f) != 1 && random1 == random2)
			{
				random2 = Random.Range(0, Mathf.FloorToInt(chromosomes.Length / 2f));
			}
			if(oldChromoEffi[random1] == -1f && oldChromoEffi[random2] == -1f)
			{
				nextGeneration[i] = RandomChromo(numFixShapes, shapes.Length - numFixShapes,
					                             initLargestShape: false, useEmptySpace: useEmptySpace);
			}
			else
			{
				nextGeneration[i] = CrossOver(nextGeneration[random1], 
				                              nextGeneration[random2], numFixShapes);
			}

			// observed that shapes are converging prematurely, so added multiple mutations
			if(packQuality)
			{
				//numFixShapes=0 when packQuality
				MutationOrder(nextGeneration[i], 0);
				MutationOrder(nextGeneration[i], 0);
				MutationShape(nextGeneration[i], 0, packQuality:true);
				MutationShape(nextGeneration[i], 0, packQuality:true);
			}
			else
			{
				MutationOrder(nextGeneration[i], numFixShapes);
				MutationOrder(nextGeneration[i], numFixShapes);
				MutationShape(nextGeneration[i], numFixShapes, packQuality:false);
				MutationShape(nextGeneration[i], numFixShapes, packQuality:false);
			}

		}

		return nextGeneration;
	}

	// Helper function to change the layer for all childs of a shape
	public void SetLayerRecursively(GameObject obj, int newLayer )
	{
		obj.layer = newLayer;

		foreach(Transform child in obj.transform )
		{
			SetLayerRecursively( child.gameObject, newLayer );
		}
	}

	// Colliding Shapes List
	LinkedList<GameObject> ShapeCollidingList(GameObject shape)
	{
		BoxCollider[] triggerBoxes = shape.transform.GetComponents<BoxCollider>();
		LinkedList<GameObject> collidingShapeList = new LinkedList<GameObject>();
		Collider[] collidingShapeColliders;

		// Set the current object to a layer.
		// We use the 10th layer.
		// This layer number must not be used for anythng else.
		int oldLayer = shape.layer;
		SetLayerRecursively(shape, newLayer:10);
		int layerMask = 1 << 10;
		layerMask = ~layerMask;

		BoxCollider box;
		for(int i = 0; i < triggerBoxes.Length; i++)
		{
			box = triggerBoxes[i];
			Vector3 boxCenter = shape.transform.TransformPoint(box.center);
			Vector3 boxHalfExtends = 0.5f * Vector3.Scale(shape.transform.lossyScale, box.size);
			Quaternion boxOrientation = shape.transform.rotation;

			collidingShapeColliders = Physics.OverlapBox(boxCenter, boxHalfExtends, boxOrientation, layerMask);
			foreach(Collider collider in collidingShapeColliders)
			{
				if(!collidingShapeList.Contains(collider.gameObject)
				   && collider.gameObject.transform.name.Contains("shape"))
				{
					collidingShapeList.AddLast(collider.gameObject);
				}
			}
		}

		SetLayerRecursively(shape, oldLayer);
		return collidingShapeList;
	}

	/**
	 * Moves a variable shape to the best possible position in the box
	 * The bext possible location is determined by back-left-botton first scheme
	 * the gridSize determines the magnitude and direction of movement,
	 * use all +ve gridSize for back-left-bottom corner
	 * use all -ve gridSize for front-right-top  corner
	 * WARNING: gridSize only verified for the above two cases
	 * Returns: true,  if the shape can be placed inside the box
	 * 		  : false, otherwise
	 * The ceter of mass of each shape is put at a location belonging to [1, GRID_RES-1]^3
	 */
	bool MoveVarShape(GameObject shape, int numFixShapes, Vector3 boxCor, Vector3 gridSize, Transform boxTransform,
	                  bool moveVarShape=true)
	{
		BoxCollider[] boxes = shape.transform.GetComponents<BoxCollider>();
		// Center of the box w.r.t. the rotated shape
		Vector3[] boxesCenterShape = new Vector3[boxes.Length];
		Vector3[] boxesHalfExtends = new Vector3[boxes.Length];
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
         * Depending on the corner (also determined by gridsize),
         * we select the lower and upper offset half of the shape
         * corOffShapeWorldL: Lower offset half of the shape
         * corOffShapeWorldH: Upper offset half of the shape
         * startPointGridBoxCor: the exact grid point to start putting the shape
         * endPointGridBoxCor:   the exact grid point to end putting the shape
		 */ 

		float corOffLX, corOffLY, corOffLZ;
		float corOffHX, corOffHY, corOffHZ;

		corOffLX = (gridSize.x > 0) ? Mathf.Abs(offLX) : offHX;
		corOffLY = (gridSize.y > 0) ? Mathf.Abs(offLY) : offHY;
		corOffLZ = (gridSize.z > 0) ? Mathf.Abs(offLZ) : offHZ;
		corOffHX = (gridSize.x < 0) ? Mathf.Abs(offLX) : offHX;
		corOffHY = (gridSize.y < 0) ? Mathf.Abs(offLY) : offHY;
		corOffHZ = (gridSize.z < 0) ? Mathf.Abs(offLZ) : offHZ;

		Vector3 corOffShapeWorldL = new Vector3(corOffLX, corOffLY, corOffLZ);
		Vector3 corOffShapeWorldH = new Vector3(corOffHX, corOffHY, corOffHZ);

		Vector3 startPointGridBoxCor = new Vector3(Mathf.CeilToInt(GRID_RES * corOffShapeWorldL.x),
		                                           Mathf.CeilToInt(GRID_RES * corOffShapeWorldL.y),
		                                           Mathf.CeilToInt(GRID_RES * corOffShapeWorldL.z));

		Vector3 endPointGridBoxCor = new Vector3(GRID_RES * boxTransform.localScale.x - Mathf.CeilToInt(GRID_RES * corOffShapeWorldH.x),
		                                         GRID_RES * boxTransform.localScale.y - Mathf.CeilToInt(GRID_RES * corOffShapeWorldH.y),
		                                         GRID_RES * boxTransform.localScale.z - Mathf.CeilToInt(GRID_RES * corOffShapeWorldH.z));

		/**
		 * curPointGridBoxCor: We define a grid space w.r.t. the box corner
		 * 					 : (0, 0, 0) equates to the boxCorner
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
		bool posFit;
		float fitness;
		LinkedList<FixPosFit> fixPosFit = new LinkedList<FixPosFit>();;
		LinkedList<GameObject> collidingShapeList;
		Collider[] collidingShapeColliders;

		int collideLayer = 1 << 11;
		collideLayer = ~collideLayer;

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
			curPointContBoxCor = Vector3.Scale(curPointGridBoxCor, gridSize);
			curPointContBox = curPointContBoxCor + boxCor;
			curPointContWorld = boxTransform.TransformPoint(curPointContBox);

			if(moveVarShape)
			{
				shapeColliding = false;
				for(int i = 0; i < boxes.Length; i++)
				{
					if(Physics.CheckBox(boxesCenterShape[i] + curPointContWorld, boxesHalfExtends[i],
					                    Quaternion.identity, collideLayer))
					{
						shapeColliding = true;
						break;
					}
				}

				if(!shapeColliding)
				{
					break;
				}
			}
			else
			{
				fitness = 0f;
				posFit = true;
				collidingShapeList = new LinkedList<GameObject>();

				for(int i = 0; i < boxes.Length; i++)
				{
					collidingShapeColliders = Physics.OverlapBox(boxesCenterShape[i] + curPointContWorld,
					                                             boxesHalfExtends[i], Quaternion.identity,
					                                             collideLayer);
					foreach(Collider collider in collidingShapeColliders)
					{
						// Colliding with a fixShape or the box
						if(    (    collider.gameObject.transform.name.Contains("shape")
						        && int.Parse(collider.gameObject.transform.name.Replace("shape", "")) < numFixShapes)
						   || (    collider.gameObject.transform.parent != null
						        && collider.gameObject.transform.parent.name.Contains("Box")))
						{
							fitness = -100f;
							posFit = false;
							break;
						}
						else
						{
							if(!collidingShapeList.Contains(collider.gameObject)
							   && collider.gameObject.transform.name.Contains("shape"))
							{
								fitness -=   collider.gameObject.transform.localScale.x
										   * collider.gameObject.transform.localScale.y
										   * collider.gameObject.transform.localScale.z
										   * collider.gameObject.GetComponent<Info>().mass;

								collidingShapeList.AddLast(collider.gameObject);
								SetLayerRecursively(collider.gameObject, 11);
							}
						}
					}

					if(!posFit)
					{
						break;	
					}
				}

				foreach(GameObject collidingShape in collidingShapeList)
				{
					SetLayerRecursively(collidingShape, 0);
				}
				fixPosFit.AddLast(new FixPosFit(curPointContWorld, fitness));
			}

			if(curPointGridBoxCor.x <= endPointGridBoxCor.x)
			{
				curPointGridBoxCor.x++;
			}
			else if(curPointGridBoxCor.z <= endPointGridBoxCor.z)
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

		if(moveVarShape)
		{
			if(curPointGridBoxCor.y > endPointGridBoxCor.y)
			{
				// Move the shape away that cannot fit in the box
				shape.transform.position = new Vector3(100, 100, 100);
				return false;

			}
			else
			{
				// Move shape in the box if it fits
				shape.transform.position = curPointContWorld;
				return true;
			}
		}
		else
		{
			// -1f * v.fitness for descending order
			List<FixPosFit> sortedFixPosFit = fixPosFit.OrderBy(v => -1f * v.fitness)
													   .ToList<FixPosFit>();
			FixPosFit bestPosFit = sortedFixPosFit.First();

			if(bestPosFit.fitness == -100f)
			{
				shape.transform.position = new Vector3(100, 100, 100);
				return false;
			}
			else
			{
				shape.transform.position = bestPosFit.pos;
				LinkedList<GameObject> _collidingShapesList = ShapeCollidingList(shape);
				foreach(GameObject _shape in _collidingShapesList)
				{
					// WARNING: Always make sure to add the term "shape"
					//          to any object that neeeds to be packed
					if(_shape.transform.name.Contains("shape"))
					{
						_shape.transform.position = new Vector3(100, 100, 100);
						SetLayerRecursively(_shape, 11);
					}
				}

				return true;
			}
		}
	}

	/**
	 * Assumptions: The box internal structure is fixed. Corners at (+-0.5, +-0.5, +-0.5) in box coordinate system
	 *				Center of the box at (0,0)
	 *				localScale.y is always 1
	 *				the box should be empty before calling this function
	 * packQuality: determines if the scale of the shape is considered in packing
	 * useEmptySpace: determines if we want to use empty space as metri or object mass as metric
	 * WARNING: Only use empty spaces when numFixShapes is zero
	 */
	public float CalcEfficiency(GameObject[] shapes, int numFixShapes, int[] chromo,
		                        bool useEmptySpace, bool resetLater=true, bool packQuality=false)
	{
		// Step 0: Put each shape to layer 11
		for(int i = 0; i < shapes.Length; i++)
		{
			SetLayerRecursively(shapes[i], 11);
		}

		// Step 1: Get the seperate fix and var chromo
		int fixChromoLen = numFixShapes * 3;
		int varChromoLen = chromo.Length - fixChromoLen;
		int numVarShapes = varChromoLen / 4;
		int[] fixChromo = SubArray(chromo,            0, fixChromoLen);
		int[] varChromo = SubArray(chromo, fixChromoLen, varChromoLen);
		Debug.Assert(shapes.Length == numFixShapes + numVarShapes, "Chromosome and the shapes are not compatible");

		// Step 1: Modify the orientation and scale of the shapes according to the chromosome
		// Step 1a: Modify the orientation of fix shapes
		Quaternion rotTemp;
		for(int i = 0; i < numFixShapes; i++)
		{
			rotTemp = R1R2Rotation(fixChromo[numFixShapes + (2 * i)],
			                       fixChromo[numFixShapes + (2 * i) + 1]);
			shapes[i].transform.rotation = rotTemp;
			shapes[i].transform.localScale = CUR_PACK_SHAPE_SCALE * Vector3.one;
		}

		// Step 1b: Modify the orientation and scale of var shapes
		for(int i = 0; i < numVarShapes; i++)
		{
			rotTemp = R1R2Rotation(varChromo[numVarShapes + (3 * i)],
			                       varChromo[numVarShapes + (3 * i) + 1]);
			shapes[numFixShapes + i].transform.rotation   = rotTemp;
			if(!packQuality)
			{
				shapes[numFixShapes + i].transform.localScale = Mathf.Pow(Mathf.Pow (2, varChromo[numVarShapes + (3 * i) + 2] - 2),
				                                                          1f / 3f)
					* CUR_PACK_SHAPE_SCALE
					* Vector3.one;
			}
		}

		// Step 2: Pack the shapes one at a time in the box
		Vector3 gridSize = (1f / GRID_RES) 
						   * new Vector3(1f / BOX.transform.localScale.x,
			                             1f / BOX.transform.localScale.y,
			                             1f / BOX.transform.localScale.z);

		// Step 2a: Move as many var shapes in the box as possible
		for(int i = 0; i < numVarShapes; i++)
		{
			if(MoveVarShape(shapes[numFixShapes + varChromo[i]], numFixShapes, BOX_BLB_CORNER, gridSize, BOX.transform))
			{
				SetLayerRecursively(shapes[numFixShapes + varChromo[i]], 0);
			}
		}

		// Step 2b. Move the fix shapes in the box
		bool fitFix;
		float efficiency = 0f;
		for(int i = 0; i < numFixShapes; i++)
		{
			fitFix = MoveVarShape(shapes[fixChromo[i]], numFixShapes, BOX_BLB_CORNER, gridSize, BOX.transform, false);
			// When unable to fit a fix shape in the box
			if(!fitFix)
			{
				efficiency = -1f;
				break;
			}
			else
			{
				SetLayerRecursively(shapes[fixChromo[i]], 0);
			}
		}

		// Step 4: Calculate efficiency
		float boxVol =   BOX.transform.localScale.x
					   * BOX.transform.localScale.z
					   * BOX.transform.localScale.y;

		int shapesToConsider;
		if(efficiency == 0f)
		{
			shapesToConsider = shapes.Length;
		}
		else
		{
			// Case when efficiency == -1
			// Here we consider only the fix shapes inside the box
			shapesToConsider = numFixShapes;
		}

		float occuVol = 0f;
		float shapeVol = 0f;
		GameObject _shape;
		for(int i = 0; i < shapesToConsider; i++)
		{
			_shape = shapes[i];
			if(_shape.transform.position != new Vector3(100, 100, 100)
			       && _shape.transform.position.y <= BOX_LID.transform.position.y
			       && _shape.transform.position.y >= BOX_BOTTOM.transform.position.y)
			{
				float metric;
				if(useEmptySpace)
				{
					metric = _shape.GetComponent<Info>().emptySpace;
				}
				else
				{
					metric = _shape.GetComponent<Info>().mass;
				}

				occuVol += (metric
				            * _shape.transform.localScale.x
				            * _shape.transform.localScale.y
				            * _shape.transform.localScale.z);
			}
		}
		efficiency += occuVol / boxVol;

		// Reset everything
		// When explicitly called to reset
		// when unable to fit anyone of the fixShapes, all the shapes are reset to signify empty box
		if(resetLater || efficiency < 0f)
		{
			for(int i = 0; i < shapes.Length; i++)
			{
				shapes[i].transform.position = new Vector3(100, 100, 100);
			}
		}

		for(int i = 0; i < shapes.Length; i++)
		{
			SetLayerRecursively(shapes[i], 0);
		}

		return efficiency;
	}


	public GameObject LoadShape(string shapePath, bool loadFromResources)
	{
		GameObject _shape;
		if(loadFromResources)
		{
			string _shapePath = shapePath.Replace(".obj", "");
			_shape = (GameObject)Instantiate(Resources.Load(_shapePath));
		}
		else
		{
			string[] directories = Regex.Split(shapePath ,  "/")
										.Where(x => x != string.Empty)
										.ToArray();
			string[] _split = Regex.Split(directories[0], "_")
				                   .Where(x => x != string.Empty)
								   .ToArray();
			string split = _split[1];

			// Keep all shapes in a folder named data
			// data should be placed in folder named data_<tr, va, te>
			// inside the StreamingAssets folder
			// data should have assets of the format "shapes_<tr, va, te>_<shape_vategory>
			string assetBundlePath = Application.streamingAssetsPath
									 + "/data_"
									 + split + "/"
								     + directories[0].ToLower() + "_"
				                     + directories[1].ToLower() + "_"
									 + directories[2].ToLower();
			AssetBundle assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
			_shape = (GameObject)Instantiate(assetBundle.LoadAsset("Assets/Resources/" + shapePath));
			assetBundle.Unload(false);
		}

		return _shape;
	}

	T[] CleanArray<T>(T[] arr, LinkedList<int> indexRemove)
	{
		int arrLength = (arr == null) ? 0 : arr.Length;
		T[] newArr = new T[arrLength - indexRemove.Count];
		int newI = 0;
		for(int i = 0; i < arrLength; i++)
		{
			if(indexRemove.Contains(i))
			{
				continue;
			}
			else
			{
				newArr[newI] = arr[i];
				newI++;
			}
		}
		return newArr;
	}

	/**
	 * Gives back the shapes from their names
	 * Removes those shapes and their names that are "EmptyObject"
	 */
	public void GetShapes(ref string[] fixShapeNames, ref string[] varShapeNames,
	                      ref GameObject[] shapes, bool loadFromResources=false, bool keepRenderer=false)
	{
		int fixShapeNamesLength = (fixShapeNames == null) ? 0 : fixShapeNames.Length;
		int varShapeNamesLength = (varShapeNames == null) ? 0 : varShapeNames.Length;

		int totalShapes = fixShapeNamesLength + varShapeNamesLength;
		Helper helper = GetComponent<Helper>();
		helper.RESOLUTION = RESOLUTION;
		shapes = new GameObject[totalShapes];

		GameObject _shape;
		for(int i = 0; i < fixShapeNamesLength; i++)
		{
			_shape = LoadShape(fixShapeNames[i], loadFromResources);
			shapes[i] = helper.AddShape(_shape, "shape" + i, new Vector3(100f, 100f, 100f), 
			                            Quaternion.identity, Vector3.one, false, false, keepRenderer);
		}

		for(int i = 0; i < varShapeNamesLength; i++)
		{
			_shape = LoadShape(varShapeNames[i], loadFromResources);
			shapes[fixShapeNamesLength + i] = helper.AddShape(_shape, "shape" + (fixShapeNamesLength + i),
			                                                  new Vector3(100f, 100f, 100f), Quaternion.identity,
			                                                  Vector3.one, false, false, keepRenderer);
		}

		LinkedList<int> badShapes = new LinkedList<int>();
		LinkedList<int> badFix = new LinkedList<int>();
		LinkedList<int> badVar = new LinkedList<int>();

		for(int i = 0; i < shapes.Length; i++)
		{
			if(shapes[i].tag == "EmptyObject")
			{
				Debug.Log("BAD SHAPE " + i);
				badShapes.AddLast(i);
				if(i < fixShapeNamesLength)
				{
					badFix.AddLast(i);
				}
				else
				{
					badVar.AddLast(i - fixShapeNamesLength);
				}
			}
		}

		foreach(int i in badShapes)
		{
			Debug.Log("Destroy BAD SHAPE " + i);
			Object.DestroyImmediate(shapes[i]);
		}

		shapes = CleanArray<GameObject>(shapes, badShapes);
		fixShapeNames = CleanArray<string>(fixShapeNames, badFix);
		varShapeNames = CleanArray<string>(varShapeNames, badVar);
	}

	/**
	 * Generates a pack for the given set of shapes via evolution
	 * bool loadFromResources determines from where we load the data
	 * During testing data loaded from resources
	 * During production data loaded from streaming assets
	 * bool keepRenderer determines if the renderes to be removed
	 * bool keepLater keeps the shape after evolving
	 * bool initLargestShape to initialize a tenth of initial shapes with largest shape
	 *	first heuristic
	 * useEmptySpace: determines if we want to use empty space as metri or object mass as metric
	 */
	public Pack EvolvePack(string[] fixShapeNames, string[] varShapeNames, string fileName,
						   bool initLargestShape, bool useEmptySpace, bool loadFromResources=false,
						   bool keepRenderer=false, bool keepLater=false)
	{
		// Step 1: Get the fix and variable shapes
		GameObject[] shapes = null;
		GetShapes(ref fixShapeNames, ref varShapeNames, ref shapes, loadFromResources:loadFromResources,
		          keepRenderer:keepRenderer);

		// Step 2: Get the initial random chromosomes
		int[][] chromosomes = new int[NUM_CHROMO_GEN][];
		for(int i = 0; i < NUM_CHROMO_GEN; i++)
		{
			if(i < NUM_CHROMO_GEN / 10)
			{
				if(initLargestShape)
				{
					chromosomes[i] = RandomChromo(fixShapeNames.Length,
											      varShapeNames.Length,
											      initLargestShape: true,
											      shapes: shapes,
											      useEmptySpace: useEmptySpace);
				}
				else
				{
					chromosomes[i] = RandomChromo(fixShapeNames.Length, varShapeNames.Length,
						                          initLargestShape: false, useEmptySpace: useEmptySpace);
				}
			}
			else
			{
				chromosomes[i] = RandomChromo(fixShapeNames.Length, varShapeNames.Length,
					                          initLargestShape: false, useEmptySpace: useEmptySpace);
			}
		}

		// Step 3: Evolve Pack
		int gen = 0;
		int bestGen = 0;
		float bestChromoEffi = -1.0f;
		float[] oldChromoEffi = null;
		while(true)
		{
			gen = gen + 1;
			chromosomes = NextGeneration(shapes,
										 fixShapeNames.Length,
										 chromosomes,
										 ref oldChromoEffi,
										 useEmptySpace: useEmptySpace);
			string output = "Generation: " + gen + " Efficiency: " + string.Join(", ", new List<float>(oldChromoEffi)
			                                                                           .ConvertAll(k => k.ToString())
			                                                                           .ToArray());
			Debug.Log(output);
			if(gen == 1)
			{
				string path = Application.streamingAssetsPath + "/" + fileName + "_log";
				string oldOutput = "";
				if(File.Exists(path))
				{
					oldOutput = File.ReadAllText(path);
				}
				File.WriteAllText(path, oldOutput + "\n" + output);
			}

			if(oldChromoEffi[0] > bestChromoEffi)
			{
				bestGen = gen;
				bestChromoEffi = oldChromoEffi[0];
			}

			if((gen >= NUM_GEN) || ((gen - bestGen) >= NUM_GEN_BREAK))
			{
				string path = Application.streamingAssetsPath + "/" + fileName + "_log";
				string oldOutput = "";
				if(File.Exists(path))
				{
					oldOutput = File.ReadAllText(path);
				}
				File.WriteAllText(path, oldOutput + "\n" + output);

				break;
			}
		}

		// Step 4: Generate pack configuration from the best pack
		Pack pack = new Pack(shapes, fixShapeNames.Length, chromosomes[0], fixShapeNames, varShapeNames, this);

		// Step 5: Destroy the shapes to free the memory while not testing
		//       : While testing we display the best pack
		if(keepLater)
		{
			CalcEfficiency(shapes, fixShapeNames.Length,
				           chromosomes[0], useEmptySpace: useEmptySpace,
				           resetLater:false, packQuality:false);
		}
		else
		{
			foreach(GameObject shape in shapes)
			{
				Object.DestroyImmediate(shape);
			}
		}

		return pack;
	}

	/**
	 * Returns a shape from a particular directory
	 * Shape path is the folder name along size model
	 */
	string SampleShapeCat(DirectoryInfo shapeCategory)
	{
		DirectoryInfo[] shapes = shapeCategory.GetDirectories();
		Debug.Assert(shapes.Length != 0, "Shape Category: "
                                         + shapeCategory.Name
		                                 + " does not contain any shape in ShapeNet format.");
		int shapeNum = Random.Range(0, shapes.Length);
		string shapePath = shapes[shapeNum].Name + "/model.obj";
		return shapePath;
	}

	/**
	 * Sample fixed and variable shape names
	 * Fixed shape will alwyas be present in the pack
	 * Variable shapes may or maynot remain in the final pack
	 * shapeNet is the path where the folder is present contating the shapes
	 * the retuend path for each shape is of the format:
	 * "<shapeDirName>/<shapeCategoryName>/<shapeName>/model.obj"
	 */
	public void SampleShapes(string shapeNet, ref string[] fixShapeNames, ref string[] varShapeNames,
	                         int numFixed=5, int numRandomPerCat=1)
	{
		DirectoryInfo shapeDir = new DirectoryInfo(shapeNet);
		DirectoryInfo[] shapeCategories = shapeDir.GetDirectories();
		fixShapeNames = new string[numFixed];
		varShapeNames = new string[numRandomPerCat * shapeCategories.Length];

		int _shapeCategory;
		for(int i = 0; i < numFixed; i++)
		{
			_shapeCategory = Random.Range(0, shapeCategories.Length);
			fixShapeNames[i] = shapeDir.Name + "/"
							   + shapeCategories[_shapeCategory].Name + "/"
				               + SampleShapeCat(shapeCategories[ _shapeCategory]);
		}

		for(int i = 0; i < shapeCategories.Length; i++)
		{
			for(int j = 0; j < numRandomPerCat; j++)
			{
				varShapeNames[i * numRandomPerCat + j] = shapeDir.Name + "/"
					+ shapeCategories[i].Name + "/"
					+ SampleShapeCat(shapeCategories[i]);
			}
		}
	}


	/**
	 * Samples numShapes shapes
	 * All the shapes put in variable shapes
	 * For each shape, first sample a category, then sample a shape from the category
	 * shapeNet is the path where the folder is present contating the shapes
	 * the retuend path for each shape is of the format:
	 * "<shapeDirName>/<shapeCategoryName>/<shapeName>/model.obj"
	 */
	public void SampleShapesNew(string shapeNet, ref string[] fixShapeNames, ref string[] varShapeNames,
	                            int numShapes=50)
	{
		DirectoryInfo shapeDir = new DirectoryInfo(shapeNet);
		DirectoryInfo[] shapeCategories = shapeDir.GetDirectories();
		fixShapeNames = new string[0];
		varShapeNames = new string[numShapes];

		int _shapeCategory;
		for(int i = 0; i < numShapes; i++)
		{
			_shapeCategory = Random.Range(0, shapeCategories.Length);
			varShapeNames[i] = shapeDir.Name + "/"
							   + shapeCategories[_shapeCategory].Name + "/"
				               + SampleShapeCat(shapeCategories[ _shapeCategory]);
		}
	}

	public void SampleShapeScale()
	{
		CUR_PACK_SHAPE_SCALE = Random.Range(ALL_SHAPE_SCALES[0], ALL_SHAPE_SCALES[1]);
	}

	/**
	 * Returns a randomly sampled and evolved pack
	 * numFixed: is the number of fixed shapes
	 * numRandomPerCate: how many random shapes are sampled per category to consider for packing
	 * fineName: the file onto which the json data is added
	 * 		   : this file must exist beforehand
	 */
	public void GeneratePack(string fileName, string shapeNet,
		                     bool useEmptySpace,
		                     bool initLargestShape,
		                     bool loadFromResources=false)
	{
		// Step 1: Sample fixed and variable shapes
		string[] fixShapeNames = null;
		string[] varShapeNames = null;
		SampleShapesNew(shapeNet, ref fixShapeNames, ref varShapeNames);

		// Step 2: Sample the current pack shape scale
		SampleShapeScale();

		// Step 3: Evolve a pack
		Pack pack = EvolvePack(fixShapeNames,
			                   varShapeNames,
			                   fileName,
			                   loadFromResources: loadFromResources,
			                   keepRenderer: false,
			                   keepLater: false,
			                   initLargestShape: initLargestShape,
			                   useEmptySpace: useEmptySpace);

		// Step 4: Write the packs to the file
		// Only saving those packs that are valid
		if(pack.efficiency > 0f)
		{
			string path = Application.streamingAssetsPath + "/" + fileName;
			Packs allData;
			if(!File.Exists(path))
			{
				allData = new Packs();
			}
			else
			{
				string jsonString = File.ReadAllText(path);
				allData = JsonUtility.FromJson<Packs>(jsonString);
				if(allData == null)
				{
					allData = new Packs();
				}
			}

			allData.data.Add(pack);

			File.WriteAllText(path, JsonUtility.ToJson(allData));
		}
	}

	/**
	 * Evolves pack for the ablation study
	 * Used parts from GeneratePack and EvolvePack functions
	 * Args:
	 *  fileName: identifier for the file where the packs are saved
	 *  shapeNet: path to the data
	 *  savePackPerGeneration: the point where a pack is saved, eg: 50 for saving after every 50th
	 *  	generation
	 *  finalGeneration: final generation till which evolution is done
	 */
	public void EvolvePackAblation(string fileName, string shapeNet, int savePackPerGeneration,
		                           int finalGeneration)
	{
		// fixed variables for the ablation study
		int num_chromo_gen = 100;
		bool loadFromResources = false;
		bool initLargestShape = false;
		bool useEmptySpace = false;

		// Step 1: Sample fixed and variable shapes
		string[] fixShapeNames = null;
		string[] varShapeNames = null;
		SampleShapesNew(shapeNet, ref fixShapeNames, ref varShapeNames);

		// Step 2: Sample the current pack shape scale
		SampleShapeScale();

		// Step 3: Get the fix and variable shapes
		GameObject[] shapes = null;
		GetShapes(ref fixShapeNames, ref varShapeNames, ref shapes, loadFromResources:loadFromResources,
		          keepRenderer:false);

		// Step 4: Get the initial random chromosomes
		int[][] chromosomes = new int[num_chromo_gen][];
		for(int i = 0; i < num_chromo_gen; i++)
		{
			
			chromosomes[i] = RandomChromo(fixShapeNames.Length, varShapeNames.Length,
				                          initLargestShape:initLargestShape, useEmptySpace:useEmptySpace);
		}

		float[] oldChromoEffi = null;
		for(int i = 0; i < finalGeneration; i++)
		{
			// Generate pack configuration from the best pack
			// write the pack to file
			if((i % savePackPerGeneration) == 0)
			{
				Pack pack = new Pack(shapes, fixShapeNames.Length, chromosomes[0], fixShapeNames, varShapeNames, this);
				string path = Application.streamingAssetsPath + "/" + fileName + "_" + i + "_ab";
				Packs allData;
				if(!File.Exists(path))
				{
					allData = new Packs();
				}
				else
				{
					string jsonString = File.ReadAllText(path);
					allData = JsonUtility.FromJson<Packs>(jsonString);
					if(allData == null)
					{
						allData = new Packs();
					}
				}

				allData.data.Add(pack);
				File.WriteAllText(path, JsonUtility.ToJson(allData));
			}
			chromosomes = NextGeneration(shapes,
										 fixShapeNames.Length,
										 chromosomes,
										 ref oldChromoEffi,
										 useEmptySpace: useEmptySpace);

		}
	}


	// Returns the quality of a pack
	// The Quality of a pack is defined as number of chromosomes evaluated for making that pack
	// The scale of each shape is fixed, only the order and rotations matter
	public int PackQuality(Pack pack, bool initLargestShape, bool useEmptySpace,
						   bool loadFromResources=false, bool keepRenderer=false,
						   bool resetLater=false)
	{
		// Step 1: Get the shapes
		GameObject[] shapes = null;
		string[] _null = null;
		GetShapes(ref _null, ref pack.sources, ref shapes, loadFromResources, keepRenderer);

		// Step 2: Fix the scale of the shapes
		for(int i = 0; i < shapes.Length; i++)
		{
			shapes[i].transform.localScale = pack.scales[i];
		}

		// Step 3: Get the initial random chromosomes
		int[][] chromosomes = new int[NUM_CHROMO_GEN][];
		for(int i = 0; i < NUM_CHROMO_GEN; i++)
		{
			chromosomes[i] = RandomChromo(0, pack.sources.Length,
			                              initLargestShape: initLargestShape,
			                              useEmptySpace: useEmptySpace);
		}

		// Step 4: Evolve Pack
		float[] oldChromoEffi = null;
		int gen = 0;
		for(gen = 0; gen < 5 *  NUM_GEN; gen++)
		{
			chromosomes = NextGeneration(shapes, 0 ,chromosomes, ref oldChromoEffi,
				                         useEmptySpace: useEmptySpace, packQuality:true);
			Debug.Log("PQ Generation: "
			          +  gen
			          + "Efficiency: "
			          + string.Join(", ", new List<float>(oldChromoEffi).ConvertAll(k => k.ToString())
			                                                            .ToArray()));
			if(oldChromoEffi[0] >= pack.efficiency)
			{
				break;
			}
		}

		if(!resetLater)
		{
			CalcEfficiency(shapes, 0, chromosomes[0], false, true);
		}
		return gen;
	}

	public GameObject[] VisualizePack(Pack pack, bool loadFromResources=true, bool keepRenderer=true)
	{
		// Step 1: Get the shapes
		GameObject[] shapes = null;
		string[] _null = null;
		GetShapes(ref _null, ref pack.sources, ref shapes,
		          loadFromResources:loadFromResources,
		          keepRenderer:keepRenderer);

		for(int i = 0; i < shapes.Length; i++)
		{
			shapes[i].transform.localScale = pack.scales[i];
			shapes[i].transform.position   = pack.positions[i];
			shapes[i].transform.rotation   = pack.rotations[i];
		}

		return shapes;
	}

	public Pack ReadPack(string fileName, int idx=0)
	{
		string path = Application.streamingAssetsPath + "/" + fileName;
		Packs allData;
		string jsonString = File.ReadAllText(path);
		allData = JsonUtility.FromJson<Packs>(jsonString);
		Pack pack = allData.data.ElementAt(idx);
		return pack;
	}

	public void ReadVisualizePack(string fileName, int idx=0)
	{
		Pack pack = ReadPack(fileName, idx);
		Debug.Log("Pack ID: " + idx + " ,Efficiency: " + pack.efficiency);
		VisualizePack(pack);
	}
}

public class FixPosFit
{
	public Vector3 pos;
	public float fitness;
	public FixPosFit(Vector3 pos, float fitness)
	{
		this.pos = pos;
		this.fitness = fitness;
	}
}

[System.Serializable]
public class Packs
{
	public List<Pack> data;

	public Packs()
	{
		this.data = new List<Pack>();
	}
}

[System.Serializable]
public class Pack
{
	public float efficiency;
	public string[] sources;
	public Vector3[] positions;
	public Vector3[] scales;
	public Quaternion[] rotations;
	public float shapeScale;

	public Pack(GameObject[] shapes, int numFixShapes, int[] chromosome,
	            string[] fixShapeNames, string[] varShapeNames, PackEvolver _pack)
	{
		// Adding box parameters
		this.shapeScale = _pack.CUR_PACK_SHAPE_SCALE;

		// Rearranging shapes and calculating the number of shapes inside the box
		this.efficiency = _pack.CalcEfficiency(shapes, numFixShapes, chromosome,
											   resetLater:false, packQuality:false,
											   useEmptySpace:false);
		Debug.Log("Final packing efficiency: " + efficiency);
		int numShapes = 0;
		for(int i = 0; i < shapes.Length; i++)
		{
			if(shapes[i].transform.position != new Vector3(100f, 100f, 100f))
			{
				numShapes++;
			}
		}

		// Noting the information of shapes inside the box
		this.sources = new string[numShapes];
		this.positions = new Vector3[numShapes];
		this.scales    = new Vector3[numShapes];
		this.rotations = new Quaternion[numShapes];

		int shapeNum = 0;
		for(int i = 0; i < shapes.Length; i++)
		{
			if(shapes[i].transform.position != new Vector3(100f, 100f, 100f))
			{
				if(i < numFixShapes)
				{
					sources[shapeNum] = fixShapeNames[i];
				}
				else
				{
					sources[shapeNum] = varShapeNames[i - numFixShapes];
				}

				positions[shapeNum] = shapes[i].transform.position;
				scales[shapeNum]    = shapes[i].transform.localScale;
				rotations[shapeNum] = shapes[i].transform.rotation;
				shapeNum++;
			}
		}

		// resetting the shapes
		foreach(GameObject shape in shapes)
		{
			shape.transform.position = new Vector3(100f, 100f, 100f);
		}
	}
}
