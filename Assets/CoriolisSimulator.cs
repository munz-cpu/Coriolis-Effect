using System.Collections.Generic;
using UnityEngine;

/// <summary>A small screen-space Coriolis experiment. Ball motion is inertial; the disk rotates beneath it.</summary>
public sealed class CoriolisSimulator : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 35f;
    [SerializeField] private float ballSpeed = 350f;

    private readonly Vector2[] pinLocal = { new Vector2(-0.48f, 0.12f), new Vector2(0.48f, -0.12f) };
    private readonly Color[] pinColors = { new Color(0.98f, 0.37f, 0.31f), new Color(0.22f, 0.68f, 1f) };
    private readonly bool[] placed = new bool[2];
    private readonly List<Shot> shots = new List<Shot>();
    private Texture2D pixel;
    private Texture2D circle;
    private float diskAngle;
    private int draggingPin = -1;

    private sealed class Shot
    {
        public Vector2 position;
        public Vector2 velocity;
        public float age;
        public readonly List<Vector2> trail = new List<Vector2>();
        public Color color;
    }

    private void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        circle = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++)
        {
            float radius = new Vector2(x - 63.5f, y - 63.5f).magnitude / 63.5f;
            circle.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - radius) * 12f)));
        }
        circle.Apply();
    }

    private void OnDestroy()
    {
        Destroy(pixel);
        Destroy(circle);
    }

    private void Update()
    {
        diskAngle += degreesPerSecond * Time.deltaTime;
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            Shot shot = shots[i];
            shot.position += shot.velocity * Time.deltaTime;
            shot.age += Time.deltaTime;
            if (shot.trail.Count == 0 || Vector2.Distance(shot.trail[shot.trail.Count - 1], shot.position) > 5f)
                shot.trail.Add(shot.position);
            if (shot.age > 5f || shot.position.x < -100 || shot.position.x > Screen.width + 100 ||
                shot.position.y < -100 || shot.position.y > Screen.height + 100)
                shots.RemoveAt(i);
        }
    }

    private Vector2 Center => new Vector2(Screen.width * 0.39f, Screen.height * 0.52f);
    private float Radius => Mathf.Min(Screen.height * 0.37f, Screen.width * 0.29f);

    private Vector2 Rotate(Vector2 point, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(point.x * cosine - point.y * sine, point.x * sine + point.y * cosine);
    }

    private Vector2 PinPosition(int index)
    {
        if (index == draggingPin) return Event.current != null ? Event.current.mousePosition : Input.mousePosition;
        return placed[index] ? Center + Rotate(pinLocal[index] * Radius, diskAngle)
            : new Vector2(Screen.width * 0.08f, Screen.height * (index == 0 ? 0.43f : 0.61f));
    }

    private void Fire(int source)
    {
        if (!placed[0] || !placed[1]) return;
        Vector2 start = PinPosition(source);
        Vector2 target = PinPosition(1 - source);
        shots.Add(new Shot
        {
            position = start,
            velocity = (target - start).normalized * ballSpeed,
            color = pinColors[source]
        });
    }

    private void DrawLine(Vector2 from, Vector2 to, float width, Color color)
    {
        Vector2 delta = to - from;
        Color previous = GUI.color;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
        GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width), pixel);
        GUI.matrix = Matrix4x4.identity;
        GUI.color = previous;
    }

    private void DrawCircle(Vector2 center, float diameter, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), circle);
        GUI.color = previous;
    }

    private void OnGUI()
    {
        Event current = Event.current;
        Vector2 mouse = current.mousePosition;
        float radius = Radius;
        Vector2 center = Center;

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            for (int i = 0; i < 2; i++)
                if (Vector2.Distance(mouse, PinPosition(i)) < 24f)
                {
                    draggingPin = i;
                    current.Use();
                    break;
                }
        }
        else if (current.type == EventType.MouseDrag && draggingPin >= 0) current.Use();
        else if (current.rawType == EventType.MouseUp)
        {
            if (draggingPin >= 0 && Vector2.Distance(mouse, center) <= radius)
            {
                pinLocal[draggingPin] = Rotate((mouse - center) / radius, -diskAngle);
                placed[draggingPin] = true;
            }
            draggingPin = -1;
        }

        DrawCircle(center, radius * 2f + 12f, new Color(0.28f, 0.55f, 0.68f));
        DrawCircle(center, radius * 2f, new Color(0.08f, 0.16f, 0.24f));
        for (int ring = 1; ring <= 4; ring++)
        {
            float r = radius * ring / 4f;
            for (int segment = 0; segment < 72; segment++)
            {
                float a = segment * Mathf.PI * 2f / 72f;
                float b = (segment + 1) * Mathf.PI * 2f / 72f;
                DrawLine(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r, 1f,
                    new Color(0.28f, 0.43f, 0.52f, 0.6f));
            }
        }
        for (int i = 0; i < 8; i++)
        {
            Vector2 direction = Rotate(Vector2.right, diskAngle + i * 45f);
            DrawLine(center, center + direction * radius, 1.5f, new Color(0.37f, 0.56f, 0.64f, 0.65f));
        }
        DrawCircle(center, 12f, Color.white);

        foreach (Shot shot in shots)
        {
            for (int i = 1; i < shot.trail.Count; i++)
                DrawLine(shot.trail[i - 1], shot.trail[i], 2f, new Color(shot.color.r, shot.color.g, shot.color.b, 0.6f));
            DrawCircle(shot.position, 15f, shot.color);
        }
        for (int i = 0; i < 2; i++)
        {
            Vector2 position = PinPosition(i);
            DrawCircle(position, 32f, Color.white);
            DrawCircle(position, 24f, pinColors[i]);
            GUI.Label(new Rect(position.x - 7, position.y - 12, 20, 24), (i + 1).ToString());
        }

        float panelX = Screen.width * 0.76f;
        float panelWidth = Mathf.Max(170f, Screen.width * 0.20f);
        GUI.Label(new Rect(panelX, Screen.height * 0.29f, panelWidth, 32), "CORIOLIS EFFECT");
        GUI.Label(new Rect(panelX, Screen.height * 0.35f, panelWidth, 54), "Drag pins onto the disk.\nFire toward the other pin.");
        GUI.enabled = placed[0] && placed[1];
        if (GUI.Button(new Rect(panelX, Screen.height * 0.48f, panelWidth, 48f), "Pin 1  →  Pin 2")) Fire(0);
        if (GUI.Button(new Rect(panelX, Screen.height * 0.56f, panelWidth, 48f), "Pin 2  →  Pin 1")) Fire(1);
        GUI.enabled = true;
    }
}
