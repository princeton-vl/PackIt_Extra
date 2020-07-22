using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * Meant to contian general purpose functions
 */

public class Helper : MonoBehaviour {

	public float RESOLUTION = 0.02f;
	/***
	 * Add boxColliders to an object composed of mess renderers
	 * All boxColliders are added to the topmost game object
	 */
	public void AddBoxColliders(GameObject gameObject)
	{
		MeshRenderer meshRender = gameObject.GetComponent<MeshRenderer> ();
		if (meshRender != null)
		{
			// NonConcexMeshCollider changes the location and position
			// We are putting the object back to its original location
			Vector3 originalPosition = gameObject.transform.position;
			Vector3 originalScale = gameObject.transform.localScale;
			Quaternion originalRotation = gameObject.transform.rotation;

			NonConvexMeshColliderNew nonConvexMeshCollider = gameObject.AddComponent (typeof(NonConvexMeshColliderNew)) 
																						  as NonConvexMeshColliderNew;
			nonConvexMeshCollider.createColliderChildGameObject = false;
			// Change this value to increase/decrease the number of primitive boxes
			nonConvexMeshCollider.resolution = RESOLUTION;
			nonConvexMeshCollider.Calculate ();
			// Sometime nonConvexMeshCollider cannot add box colliders if
			// boxPerEdge is large
			while(gameObject.GetComponents<BoxCollider>().Length == 0)
			{
				nonConvexMeshCollider.resolution = nonConvexMeshCollider.resolution * 2;
				nonConvexMeshCollider.Calculate();
				if(nonConvexMeshCollider.resolution >= 1)
				{
					if(gameObject.GetComponents<BoxCollider> ().Length == 0)
					{
						break;
					}
				}
			}

			gameObject.transform.position = originalPosition;
			gameObject.transform.rotation = originalRotation;
			gameObject.transform.localScale = originalScale;
		}

		// First creating a static list
		// Because transform.GetChild(i) fails with changing childs 
		List<Transform> childTransform = new List<Transform> ();
		for (int i = 0; i < gameObject.transform.childCount; i++)
		{
			childTransform.Add (gameObject.transform.GetChild (i));
		}
			
		GameObject child;
		for (int i = 0; i < gameObject.transform.childCount; i++) 
		{
			child = childTransform [i].gameObject;
			AddBoxColliders (child);

			// The center locations are expressed interms of the "pivot" and not the "center"
			BoxCollider[] childBoxes = child.GetComponents<BoxCollider> ();
			foreach (BoxCollider childBox in childBoxes)
			{
				BoxCollider newBox = gameObject.AddComponent (typeof(BoxCollider)) as BoxCollider;
				newBox.center = newBox.transform.InverseTransformPoint(childBox.transform.TransformPoint(childBox.center));
				newBox.size = Vector3.Scale(childBox.transform.localScale, childBox.size);
			}

			foreach (BoxCollider childBox in childBoxes) 
			{
				Destroy (childBox);
			}
		}
	}

	/***
	 * Adds a suitable trigger to the object
	 * Each box is scaled by the scaling factor
	 * Assumption: All box colliders are already added
	 *             Generally, called after AddBoxColliders()
     */
	public void AddTrigger(GameObject gameObject, float scale=0.98f)
	{
		GameObject trigger = new GameObject ("Trigger");
		trigger.transform.position = gameObject.transform.position;
		trigger.transform.rotation = gameObject.transform.rotation;
		trigger.transform.parent = gameObject.transform;
		trigger.transform.localScale = Vector3.one;

		BoxCollider[] boxes = gameObject.GetComponents<BoxCollider> ();
		foreach (BoxCollider box in boxes) 
		{
			BoxCollider triggerBox = trigger.AddComponent (typeof(BoxCollider)) as BoxCollider;
			triggerBox.center = box.center;
			triggerBox.size = scale * box.size;
			triggerBox.isTrigger = true;
		}

		ObjectEnter _ = trigger.AddComponent (typeof(ObjectEnter)) as ObjectEnter;
	}


	public Vector3 GetEnclBoxDim(GameObject shape)
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
			// different from similar function used in MoveVarShape
			// here we directly use the center/size of the boxes
			// this is done as we want a scale/rotation agnostic box dimension
			boxesCenterShape[i] = boxes[i].center;
			_boxesHalfExtends = 0.5f * boxes[i].size;
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
         * we select the lower and upper offset half of the shape
         * corOffShapeWorldL: Lower offset half of the shape
         * corOffShapeWorldH: Upper offset half of the shape
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

		return corOffShapeWorldL + corOffShapeWorldH;
	}

	/***
	 * Adds a rigidbody componenet to an object with appropriate mass
	 * Also adjusts the COM of the gameObject's children: shape and trigger
	 * Must call this function with addRigidBody even when not adding a rigidbody
	 * As this component ajust the COM of the shape
	 * Can be also used to return the Center Of Mass (COM)
	 * Assumption: All box colliders are already added
	 * 			   Generally called after AddBoxColliders()	and AddTrigger()
	 */
	public bool AddRigidBody(GameObject gameObject, bool addRigidBody, bool addInfo=true)
	{
		float _mass, mass = 0f;
		Vector3 com = Vector3.zero;
		BoxCollider[] boxes = gameObject.GetComponents<BoxCollider> ();

		foreach (BoxCollider box in boxes)
		{
			_mass = box.size.x * box.size.y * box.size.z;
			mass += _mass;
			com += (_mass * box.center);
		}

		if (addInfo)
		{
			Info info = gameObject.AddComponent<Info>() as Info;
			info.mass = mass;
			info.encBoxDim = GetEnclBoxDim(gameObject);
			info.emptySpace = (info.encBoxDim.x * info.encBoxDim.y * info.encBoxDim.z)
							   - info.mass;
		}

		if(mass == 0)
		{
			return false;
		}
		com /= mass;


		foreach(Transform childTransform in gameObject.transform)
		{
			childTransform.localPosition = -com;
		}

		foreach (BoxCollider box in boxes) 
		{
			box.center += -com; 
		}

		if(addRigidBody) 
		{
			Rigidbody rigidBody = gameObject.AddComponent (typeof(Rigidbody)) as Rigidbody;
			rigidBody.mass =  mass;
		}
		return true;
	}

	/**
	 * High level function to load a shape from the resources 
	 * GameObject format (Top -> Child1, Child2)
	 * Top: primitive box colliders for the shape
	 * 	    rigidbody of suitable mass
	 * Child1: actual loaded object, containing the mesh renderers
	 * Child2: prmitive boxes with isTrigger, and smaller in size
	 * destroyChild used to make the packing code faster as removing the unnecessary objects
	 */
	public GameObject AddShape(GameObject _shape, string shapeName, Vector3 position, Quaternion rotation, Vector3 scale,
		                       bool addTrigger=true, bool addRigidBody=true, bool keepChild=false)
	{
		// To change the pivot to the center of the game object
		// Important for expected movement of the object
		GameObject shape = new GameObject (shapeName);
		shape.tag = "Object";
		shape.transform.position = position;
		shape.transform.rotation = rotation;
		_shape.transform.position = position;
		_shape.transform.rotation = rotation;
		_shape.transform.parent = shape.transform;
		shape.transform.localScale = scale;

		AddBoxColliders (shape);
		if(addTrigger)
		{
			AddTrigger (shape);
		}
		// Chaage the tag of the object when the object is empty
		bool success = AddRigidBody (shape, addRigidBody);
		if(!success)
		{
			shape.tag = "EmptyObject";
		}

       if(!keepChild)
       {
			// First creating a static list
	        // Because transform.GetChild(i) fails with changing childs 
	        List<Transform> childrenTransform = new List<Transform> ();
	        for (int i = 0; i < shape.transform.childCount; i++)
	        {
				childrenTransform.Add (shape.transform.GetChild (i));
	        }

	        for (int i = 0; i < shape.transform.childCount; i++)
			{
				GameObject child = childrenTransform [i].gameObject;
                Object.DestroyImmediate(child);
            }
		}

		return shape;
	}
}

public class Info: MonoBehaviour
{
	// all information is agnostic of scale of object
	// mass is same as volume
	// encBoxDim is the dimension of the box that tightly encloses the shape
	// emptySpace is the enclosing boxes volume minus objects volume
	public float mass;
	public float emptySpace;
	public Vector3 encBoxDim;
	
}