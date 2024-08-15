using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ruler : MonoBehaviour {

	public Vector2 oldMousePos;
	public Vector2 mousePos;
    public AudioSource slide;
	bool selected = false;
	bool temp = false;
	float sign = 0;

	// Use this for initialization
	void Start () {
		sign = 0;
		temp = false;
		selected = false;
	}

	// Update is called once per frame
	void Update() {
		oldMousePos = mousePos;
		mousePos = (Vector2)(Input.mousePosition * 5f / Screen.height) - new Vector2(Screen.width * 2.5f / Screen.height, 2.5f);
		Vector2 relPos = mousePos - (Vector2)transform.position * .5f;
		Vector2 intPos = Quaternion.Inverse(transform.rotation) * relPos;

		bool inside = Mathf.Abs(intPos.x * 4f / transform.localScale.x) < 1f && Mathf.Abs(intPos.y * 4f / transform.localScale.y) < 1f;
		if (Input.GetMouseButtonDown(0))
			selected = inside;
		if (Input.GetMouseButtonUp(0))
        {
			selected = false;
			slide.volume = 0f;
		}

		if (selected)
			Board.myBoard.SetLocked();

		if (Input.GetMouseButton(0) && selected)
		{
			Vector2 delta = (mousePos - oldMousePos) * 2f;
			Vector3 cross = Vector3.Cross(delta, relPos);
			Vector3 parallelComp = Vector3.Dot(delta, relPos) * relPos / relPos.sqrMagnitude;
			transform.position += ((Vector3)delta - parallelComp) / (relPos.sqrMagnitude * .09f + 1f) + parallelComp;
			transform.rotation *= Quaternion.AngleAxis(-cross.magnitude, cross / (cross.magnitude + 1E-9f));
			slide.volume = Mathf.Lerp(slide.volume, Mathf.Sqrt(delta.magnitude * 3f), Time.deltaTime * 5f);
		}

		if (Input.GetMouseButtonDown(0) && !inside)
        {
			temp = Mathf.Abs(Mathf.Abs(intPos.x) - .25f * transform.localScale.x) > Mathf.Abs(Mathf.Abs(intPos.y) - .25f * transform.localScale.y);
			sign = temp ? Mathf.Sign(intPos.y) : Mathf.Sign(intPos.x);
		}

		if (inside)
        {
			if (temp)
				intPos.y = sign * .25f * transform.localScale.y;
			else
				intPos.x = sign * .25f * transform.localScale.x;
			Board.myBoard.mousePos = transform.rotation * intPos * .2f * Screen.height + transform.position * .5f * .2f * Screen.height + new Vector3(Screen.width * .5f, .5f * Screen.height);
		}
	}
}
