using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RulerManager : MonoBehaviour {

	public GameObject instance;
	public GameObject prefab;
	public AudioSource remove;

	// Update is called once per frame
	void Update ()
	{
		if (Input.GetKeyDown(KeyCode.R) && instance != null)
        {
			remove.Play();
			Destroy(instance);
		}
		else if (Input.GetKeyDown(KeyCode.R) && instance == null)
			instance = Instantiate(prefab, new Vector3(-Screen.width * 5f / Screen.height, -4f, -1f), Quaternion.AngleAxis(60f, Vector3.forward));
	}
}
