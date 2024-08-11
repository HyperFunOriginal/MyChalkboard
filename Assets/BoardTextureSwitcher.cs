using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardTextureSwitcher : MonoBehaviour {

    public Texture2D blackboard1, blackboard2;
	bool current = false;

	void Start()
    {
		current = false;
    }

	void Update () {
		if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.S))
        {
			current = !current;
			if (current)
				Board.myBoard.blackboard = blackboard2;
			else
				Board.myBoard.blackboard = blackboard1;
			Board.myBoard.SetBoard();
		}
	}
}
