using System.Collections;
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
    bool toolUsed = false;
    bool update = false;
    int frameCount = 0;

    void ClearChalk()
    {
        draw.SetInts("resolution", texture.width, texture.height);
        draw.Dispatch(Clear, Mathf.CeilToInt(texture.width / 16f), Mathf.CeilToInt(texture.height / 16f), 1);
        update = true;
    }

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

        draw.SetTexture(Clear, "mask", mask);
        draw.SetTexture(Clear, "screen", texture);
        draw.SetTexture(Draw, "chalk", chalk);
        draw.SetTexture(Erase, "chalk", chalk);

        Cursor.SetCursor(chalkUp, new Vector2(0, 0), CursorMode.Auto); SetBoard();
    }
    
    public void SetBoard()
    {
        draw.SetTexture(Clear, "blackboard", blackboard);
        draw.SetTexture(Draw, "blackboard", blackboard);
        draw.SetTexture(Erase, "blackboard", blackboard);
        ClearChalk(); toolUsed = false;
    }

    IEnumerator CircleTool()
    {
        if (toolUsed)
            yield break;

        toolUsed = true;
        Vector2 center = mousePos;
        yield return new WaitUntil(() => { return Input.GetMouseButtonUp(0); });
        float radius = (mousePos - center).magnitude;
        float step = 50f / Mathf.Max(100f, radius);

        for (float r = 0; r < Mathf.PI * 2f; r += step)
        {
            Vector2 pos_temp = new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * radius + center + Random.insideUnitCircle * 1.5f;
            DrawLine(pos_temp, new Vector2(Mathf.Cos(r + step * .3f), Mathf.Sin(r + step * .3f)) * radius + center + Random.insideUnitCircle * 1.5f);
            PlayClack(.3f, pos_temp.x);
            yield return new WaitForSecondsRealtime(0.03f);
        }
        PlayClack(1f, center.x);
        toolUsed = false;
    }
    IEnumerator DrawDottedLine(Vector2 start, Vector2 end)
    {
        float step = 50f / Mathf.Max(200f, (start - end).magnitude), r = 0;

        for (; r < 1f; r += step)
        {
            Vector2 pos_temp = Vector2.Lerp(start, end, r) + Random.insideUnitCircle * 1.5f;
            DrawLine(pos_temp, Vector2.Lerp(start, end, r + step * .3f) + Random.insideUnitCircle * 1.5f);
            PlayClack(.3f, pos_temp.x);
            yield return new WaitForSecondsRealtime(0.03f);
        }

        DrawLine(Vector2.Lerp(start, end, r), end);
        PlayClack(1f, end.x);
        yield return new WaitForEndOfFrame();
    }
    IEnumerator DrawFullLine(Vector2 start, Vector2 end)
    {
        float step = 100f / Mathf.Max(100f, (start - end).magnitude), r = 0;
        Vector2 temp1 = start, temp2 = start; stroke.volume = 0.3f;

        for (; r < 1f; r += step)
        {
            temp2 = Vector2.Lerp(start, end, r + step) + Random.insideUnitCircle * 1.5f;
            DrawLine(temp1, temp2); temp1 = temp2;
            stroke.panStereo = 2f * temp2.x / Screen.width - 1f;
            yield return new WaitForSecondsRealtime(0.03f);
        }

        DrawLine(temp2, end);
        PlayClack(1f, end.x);
        yield return new WaitForEndOfFrame();
    }
    IEnumerator DottedLineTool()
    {
        if (toolUsed)
            yield break;

        toolUsed = true;
        Vector2 start = mousePos;
        yield return new WaitUntil(() => { return Input.GetMouseButtonUp(0); });
        Vector2 end = mousePos;

        yield return DrawDottedLine(start, end);
        toolUsed = false;
    }
    IEnumerator LineTool()
    {
        if (toolUsed)
            yield break;

        toolUsed = true;
        Vector2 start = mousePos;
        yield return new WaitUntil(() => { return Input.GetMouseButtonUp(0); });
        Vector2 end = mousePos;

        yield return DrawFullLine(start, end);
        toolUsed = false;
    }
    IEnumerator BoxTool()
    {
        if (toolUsed)
            yield break;

        toolUsed = true;
        Vector2 start = mousePos;
        yield return new WaitUntil(() => { return Input.GetMouseButtonUp(0); });
        Vector2 end = mousePos;

        yield return DrawDottedLine(start, new Vector2(start.x, end.y));
        yield return DrawDottedLine(new Vector2(start.x, end.y), end);
        yield return DrawDottedLine(end, new Vector2(end.x, start.y));
        yield return DrawDottedLine(new Vector2(end.x, start.y), start);
        toolUsed = false;
    }

    void DrawLine(Vector2 oldPos, Vector2 newPos)
    {
        draw.SetTexture(Draw, "mask", mask);
        draw.SetTexture(Draw, "screen", texture);
        draw.SetFloats("old_pos", oldPos.x, oldPos.y);
        draw.SetFloats("offset", newPos.x - oldPos.x, newPos.y - oldPos.y);
        draw.SetInts("resolution", texture.width, texture.height);
        draw.Dispatch(Draw, Mathf.CeilToInt(texture.width / 16f), Mathf.CeilToInt(texture.height / 16f), 1);
        update = true;
    }
    void EraseLine(Vector2 oldPos, Vector2 newPos)
    {
        draw.SetTexture(Erase, "mask", mask);
        draw.SetTexture(Erase, "screen", texture);
        draw.SetFloat("strength", Input.GetKey(KeyCode.LeftShift) ? 0.9f : 1f);
        draw.SetFloat("radius", Input.GetKey(KeyCode.LeftShift) ? 50f : 20f);
        draw.SetFloats("old_pos", oldPos.x, oldPos.y);
        draw.SetFloats("offset", newPos.x - oldPos.x, newPos.y - oldPos.y);
        draw.SetInts("resolution", texture.width, texture.height);
        draw.SetInt("globalSeed", Random.Range(int.MinValue, int.MaxValue));
        draw.Dispatch(Erase, Mathf.CeilToInt(texture.width / 16f), Mathf.CeilToInt(texture.height / 16f), 1);
        update = true;
    }

    private void LateUpdate()
    {
        if (!toolUsed)
        {
            bool walterLewin = Input.GetKey(KeyCode.LeftShift);
            if (Input.GetMouseButton(0) && (!walterLewin || frameCount != 0))
                DrawLine((frameCount == 1 && walterLewin) ? mousePos : oldMousePos, mousePos);
            if (Input.GetMouseButton(1))
                EraseLine(oldMousePos, mousePos);
            if (Input.GetKeyDown(KeyCode.Delete))
                ClearChalk();
            if (Input.GetKeyDown(KeyCode.C) && Input.GetKey(KeyCode.LeftControl))
                CopyToClipboard(texture);
        }

        if (Input.GetKey(KeyCode.C) && Input.GetMouseButtonDown(0))
            StartCoroutine(CircleTool());

        if (Input.GetKey(KeyCode.D) && Input.GetMouseButtonDown(0))
            StartCoroutine(DottedLineTool());

        if (Input.GetKey(KeyCode.L) && Input.GetMouseButtonDown(0))
            StartCoroutine(LineTool());

        if (Input.GetKey(KeyCode.B) && Input.GetMouseButtonDown(0))
            StartCoroutine(BoxTool());

        if (update)
        {
            mat.SetTexture("_MainTex", texture);
            update = false;
        }
    }

    void HandleCursor()
    {
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
            Cursor.SetCursor(chalkUp, new Vector2(8f, 8f), CursorMode.Auto);
        if (Input.GetMouseButtonDown(0))
            Cursor.SetCursor(chalkDown, new Vector2(0, 64f), CursorMode.Auto);
        if (Input.GetMouseButtonDown(1))
            Cursor.SetCursor(eraser, new Vector2(0, 32f), CursorMode.Auto);
    }

    void PlayClack(float volume, float playPos)
    {
        clack.volume = volume;
        int rng = shufflePseudoRandom[Random.Range(0, Mathf.Min(clackSounds.Count - 1, 5))];

        clack.panStereo = 2f * playPos / Screen.width - 1f;
        clack.PlayOneShot(clackSounds[rng]);
        shufflePseudoRandom.Remove(rng);
        shufflePseudoRandom.Add(rng);
    }

    void HandleAudio()
    {
        float mouseVel1 = 0f, mouseVel2 = 0f;
        if (Input.GetMouseButton(0))
            mouseVel1 = (mousePos - oldMousePos).magnitude * 5f / Screen.width;
        if (Input.GetMouseButton(1))
            mouseVel2 = (mousePos - oldMousePos).magnitude * (Input.GetKey(KeyCode.LeftShift) ? 25f : 5f) / Screen.width;
        float lerpFactor = Mathf.Exp(-Time.deltaTime * 25f);

        stroke_strength = stroke_strength * lerpFactor + mouseVel1 * (1f - lerpFactor);
        stroke.volume = stroke_strength;

        erase_strength = erase_strength * lerpFactor + mouseVel2 * (1f - lerpFactor);
        erase.volume = erase_strength;

        stroke.panStereo = 2f * mousePos.x / Screen.width - 1f;
        erase.panStereo  = 2f * mousePos.x / Screen.width - 1f;

        frameCount = (frameCount + 1) % 3;
        bool walterLewin = Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftShift) && frameCount == 0;
        if (Input.GetMouseButtonDown(0) || walterLewin)
            PlayClack(walterLewin ? Mathf.Clamp01(mouseVel1 * 5f) : 1f, mousePos.x);
    }

    // Update is called once per frame
    void Update()
    {
        oldMousePos = mousePos;
        mousePos = Vector2.Lerp(Input.mousePosition, mousePos, Mathf.Exp(-Time.deltaTime * 20f));

        HandleCursor();
        if (!toolUsed)
            HandleAudio();
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
