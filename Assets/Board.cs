﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class SaveImage
{
    public static void SaveImageToFile(RenderTexture img, string directory, string filename)
    {
        if (!System.IO.Directory.Exists(directory))
            System.IO.Directory.CreateDirectory(directory);

        Texture2D temp = ReturnImg(img);
        System.IO.File.WriteAllBytes(directory + filename + ".png", temp.EncodeToPNG());
        Object.DestroyImmediate(temp, true);
    }
    public static Texture2D ReturnImg(RenderTexture rt)
    {
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D outputImage = new Texture2D(rt.width, rt.height, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
        outputImage.ReadPixels(new Rect(0.0f, 0.0f, rt.width, rt.height), 0, 0);
        outputImage.Apply();
        RenderTexture.active = active;
        return outputImage;
    }
}
public class Board : MonoBehaviour
{
    [Header("Materials")]
    public static Board myBoard;
    public Texture2D blackboard, chalk;
    public Material mat;

    [Header("Cursor")]
    public Texture2D chalkUp;
    public Texture2D chalkDown;
    public Texture2D eraser;

    [Header("Mouse")]
    public Vector2 mousePos;
    public Vector2 oldMousePos;
    RenderTexture texture, mask;

    [Header("Audio")]
    public AudioSource stroke;
    public AudioSource erase;
    public AudioSource clack;

    public List<AudioClip> clackSounds;
    public List<int> shufflePseudoRandom;

    public float stroke_strength = 0f;
    public float erase_strength = 0f;

    [Header("Compute Shaders")]
    public ComputeShader draw;
    int Draw, Erase, Clear;

    int frameCount = 0;

    // Start is called before the first frame update
    void Start()
    {
        Draw = draw.FindKernel("Draw");
        Erase = draw.FindKernel("Erase");
        Clear = draw.FindKernel("Clear");

        myBoard = this;
        mat = GetComponent<MeshRenderer>().material;
        transform.localScale = new Vector3(((float)Screen.width) / Screen.height, 1.0f, 1.0f);

        mask = new RenderTexture(Screen.width, Screen.height, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_SFloat) { enableRandomWrite = true };
        texture = new RenderTexture(Screen.width, Screen.height, 0) { enableRandomWrite = true };
        mask.Create(); texture.Create();

        shufflePseudoRandom = new List<int>();
        for (int i = 0; i < clackSounds.Count; i++)
            shufflePseudoRandom.Add(i);
        for (int i = 0; i < 10; i++)
        {
            int rng = shufflePseudoRandom[Random.Range(0, Mathf.Min(clackSounds.Count - 1, 5))];
            shufflePseudoRandom.Remove(rng);
            shufflePseudoRandom.Add(rng);
        }

        RenderTexture temp = RenderTexture.active;
        RenderTexture.active = texture;
        Graphics.Blit(blackboard, texture, new Vector2(transform.localScale.x / 1.77777777778f, 1f), Vector2.zero);
        RenderTexture.active = temp;

        draw.SetTexture(Clear, "mask", mask);
        draw.SetTexture(Clear, "screen", texture);
        draw.SetTexture(Clear, "blackboard", blackboard);
        draw.SetTexture(Draw, "blackboard", blackboard);
        draw.SetTexture(Draw, "chalk", chalk);
        draw.SetTexture(Erase, "blackboard", blackboard);
        draw.SetTexture(Erase, "chalk", chalk);

        Cursor.SetCursor(chalkUp, new Vector2(0, 0), CursorMode.Auto);
    }

    private void LateUpdate()
    {
        bool walterLewin = Input.GetKey(KeyCode.LeftShift);
        if (Input.GetMouseButton(0) && (!walterLewin || frameCount != 0))
        {
            if (frameCount == 1 && walterLewin)
                oldMousePos = mousePos;

            draw.SetTexture(Draw, "mask", mask);
            draw.SetTexture(Draw, "screen", texture);
            draw.SetFloats("old_pos", oldMousePos.x, oldMousePos.y);
            draw.SetFloats("offset", mousePos.x - oldMousePos.x, mousePos.y - oldMousePos.y);
            draw.SetInts("resolution", texture.width, texture.height);
            draw.Dispatch(Draw, Mathf.CeilToInt(texture.width / 16f), Mathf.CeilToInt(texture.height / 16f), 1);
        }
        if (Input.GetMouseButton(1))
        {
            draw.SetTexture(Erase, "mask", mask);
            draw.SetTexture(Erase, "screen", texture);
            draw.SetFloats("old_pos", oldMousePos.x, oldMousePos.y);
            draw.SetFloats("offset", mousePos.x - oldMousePos.x, mousePos.y - oldMousePos.y);
            draw.SetInts("resolution", texture.width, texture.height);
            draw.SetInt("globalSeed", Random.Range(int.MinValue, int.MaxValue));
            draw.Dispatch(Erase, Mathf.CeilToInt(texture.width / 16f), Mathf.CeilToInt(texture.height / 16f), 1);
        }
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            draw.SetInts("resolution", texture.width, texture.height);
            draw.Dispatch(Clear, Mathf.CeilToInt(texture.width / 16f), Mathf.CeilToInt(texture.height / 16f), 1);
        }
        mat.SetTexture("_MainTex", texture);

        if (Input.GetKeyDown(KeyCode.C) && Input.GetKey(KeyCode.LeftControl))
            CopyToClipboard(texture);
    }

    void HandleCursor()
    {
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
            Cursor.SetCursor(chalkUp, new Vector2(0, 0), CursorMode.Auto);
        if (Input.GetMouseButtonDown(0))
            Cursor.SetCursor(chalkDown, new Vector2(0, 64f), CursorMode.Auto);
        if (Input.GetMouseButtonDown(1))
            Cursor.SetCursor(eraser, new Vector2(0, 32f), CursorMode.Auto);
    }

    void HandleAudio()
    {
        float mouseVel1 = 0f, mouseVel2 = 0f;
        if (Input.GetMouseButton(0))
            mouseVel1 = (mousePos - oldMousePos).magnitude * 5f / Screen.width;
        if (Input.GetMouseButton(1))
            mouseVel2 = (mousePos - oldMousePos).magnitude * 5f / Screen.width;
        float lerpFactor = Mathf.Exp(-Time.deltaTime * 25f);

        stroke_strength = stroke_strength * lerpFactor + mouseVel1 * (1f - lerpFactor);
        stroke.volume = stroke_strength;

        erase_strength = erase_strength * lerpFactor + mouseVel2 * (1f - lerpFactor);
        erase.volume = erase_strength;

        stroke.panStereo = mousePos.x / Screen.width - .5f;
        erase.panStereo = mousePos.x / Screen.width - .5f;
        clack.panStereo = mousePos.x / Screen.width - .5f;

        frameCount = (frameCount + 1) % 3;
        if (Input.GetMouseButtonDown(0) || (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftShift) && frameCount == 0))
        {
            int rng = shufflePseudoRandom[Random.Range(0, Mathf.Min(clackSounds.Count - 1, 5))];
            clack.PlayOneShot(clackSounds[rng]);
            shufflePseudoRandom.Remove(rng);
            shufflePseudoRandom.Add(rng);
        }
    }

    // Update is called once per frame
    void Update()
    {
        oldMousePos = mousePos;
        mousePos = Vector2.Lerp(Input.mousePosition, mousePos, Mathf.Exp(-Time.deltaTime * 20f));

        HandleAudio();
        HandleCursor();
    }

    public static void CopyToClipboard(RenderTexture texture)
    {
        Texture2D temp = SaveImage.ReturnImg(texture);
        System.IO.Stream s = new System.IO.MemoryStream(temp.width * temp.height);
        byte[] bits = temp.EncodeToPNG();
        s.Write(bits, 0, bits.Length);
        System.Drawing.Image image = System.Drawing.Image.FromStream(s);
        System.Windows.Forms.Clipboard.SetImage(image);
        s.Close(); s.Dispose();
        Destroy(temp);
    }
}