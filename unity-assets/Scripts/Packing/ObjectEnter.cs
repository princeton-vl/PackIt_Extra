using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * To avoid objects entering one another, while 
 * 'kinetically' moving the grabbed object. Trigger 
 * is a 'invisible object' inside the actual object.
 * If some object collides with the trigger, we 
 * reverse the original object motion in the ObjectMovement
 * script.
 */

/**
 * To prevent the player from colliding with other object
 * We make a trigger object, which approximately covers the
 * space that the player would occupy if it moves. If anything
 * enters that space, we add it to the Dictionay and ignore the 
 * player movement call.
 */

/**
 * Used to check if the cubes have entered the box.
 */

public class ObjectEnter : MonoBehaviour {

	public SortedDictionary<string, int> objectEntered = new SortedDictionary<string, int>();

	void OnTriggerEnter(Collider colliderInfo)
	{
		// TODO: Make sure that the scale of all triggers is 0.99 or less of the scale of of objects
		if (colliderInfo.name != "Trigger") 
		{
			if (objectEntered.ContainsKey (colliderInfo.name))
			{
				objectEntered [colliderInfo.name] += 1;	
			} 
			else
			{
				objectEntered.Add (colliderInfo.name, 1);
			}
		}
	}

	void OnTriggerExit(Collider colliderInfo)
	{
		if (colliderInfo.name != "Trigger") 
		{
			objectEntered [colliderInfo.name] -= 1;
			if (objectEntered [colliderInfo.name] == 0) 
			{
				objectEntered.Remove (colliderInfo.name);
			}
		}
	}
}
