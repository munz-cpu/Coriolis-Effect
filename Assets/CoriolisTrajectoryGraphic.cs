using UnityEngine;
using UnityEngine.UI;

public sealed class CoriolisTrajectoryGraphic : MaskableGraphic
{
    private const int SamplesPerShot = 320;
    private const float LineHalfWidth = 3f;

    private Vector2[,] positions;
    private float[,] angles;
    private int[] counts;
    private int[] next;
    private RectTransform canvasRoot;
    private Vector2 diskCenter;
    private float diskRadius;
    private bool rotatingView;
    private float viewStartAngle;
    private float currentAngle;
    private bool recording;
    private bool hasSamples;
    [SerializeField] private Color flightTrailColor = new Color(.75f, .08f, .58f, .95f);

    public void Initialize(int shotCount, RectTransform canvas, Vector2 center, float radius)
    {
        positions = new Vector2[shotCount, SamplesPerShot];
        angles = new float[shotCount, SamplesPerShot];
        counts = new int[shotCount];
        next = new int[shotCount];
        canvasRoot = canvas;
        diskCenter = center;
        diskRadius = radius;
        raycastTarget = false;
    }

    public void SetView(bool rotating, float startAngle)
    {
        rotatingView = rotating;
        viewStartAngle = startAngle;
        SetVerticesDirty();
    }

    public void SetAngle(float angle)
    {
        currentAngle = angle;
        if (recording && hasSamples) SetVerticesDirty();
    }

    public void SetRecording(bool enabled)
    {
        recording = enabled;
        if (!enabled && counts != null)
        {
            System.Array.Clear(counts, 0, counts.Length);
            hasSamples = false;
        }
        SetVerticesDirty();
    }

    public void BeginShot(int index, Vector2 position, float angle)
    {
        if (!recording) return;
        counts[index] = 0;
        next[index] = 0;
        AddPoint(index, position, angle);
    }

    public void EndShot(int index)
    {
        if (counts == null) return;
        counts[index] = 0;
        hasSamples = false;
        for (int i = 0; i < counts.Length; i++)
            hasSamples |= counts[i] > 0;
        SetVerticesDirty();
    }

    public void AddPoint(int index, Vector2 position, float angle)
    {
        if (!recording) return;
        int slot = next[index];
        positions[index, slot] = position;
        angles[index, slot] = angle;
        next[index] = (slot + 1) % SamplesPerShot;
        counts[index] = Mathf.Min(counts[index] + 1, SamplesPerShot);
        hasSamples = true;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!recording || positions == null || canvasRoot == null) return;

        Matrix4x4 canvasToGraphic = rectTransform.worldToLocalMatrix * canvasRoot.localToWorldMatrix;
        Color32 diskTrailColor = color;
        Color32 ballTrailColor = flightTrailColor;
        for (int shot = 0; shot < counts.Length; shot++)
        {
            if (counts[shot] < 2) continue;
            int first = (next[shot] - counts[shot] + SamplesPerShot) % SamplesPerShot;
            Vector2 previousBall = VisibleBallPoint(shot, first);
            Vector2 previousDisk = VisibleDiskPoint(shot, first);
            for (int sample = 1; sample < counts[shot]; sample++)
            {
                int slot = (first + sample) % SamplesPerShot;
                Vector2 currentBall = VisibleBallPoint(shot, slot);
                Vector2 currentDisk = VisibleDiskPoint(shot, slot);
                Vector2 nextDisk = currentDisk;
                if (ClipToDisk(ref previousDisk, ref currentDisk))
                    AddLine(mesh, canvasToGraphic.MultiplyPoint3x4(previousDisk),
                        canvasToGraphic.MultiplyPoint3x4(currentDisk), diskTrailColor);
                AddLine(mesh, canvasToGraphic.MultiplyPoint3x4(previousBall),
                    canvasToGraphic.MultiplyPoint3x4(currentBall), ballTrailColor);
                previousBall = currentBall;
                previousDisk = nextDisk;
            }
        }
    }

    private static void AddLine(VertexHelper mesh, Vector2 start, Vector2 end, Color32 lineColor)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= .01f) return;
        Vector2 side = new Vector2(-direction.y, direction.x).normalized * LineHalfWidth;
        int vertex = mesh.currentVertCount;
        mesh.AddVert(start - side, lineColor, Vector2.zero);
        mesh.AddVert(start + side, lineColor, Vector2.zero);
        mesh.AddVert(end + side, lineColor, Vector2.zero);
        mesh.AddVert(end - side, lineColor, Vector2.zero);
        mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
        mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
    }

    private Vector2 VisibleBallPoint(int shot, int sample)
    {
        Vector2 point = positions[shot, sample];
        if (!rotatingView) return point;
        return diskCenter + (Vector2)(Quaternion.Euler(0, 0,
            viewStartAngle - currentAngle) * (point - diskCenter));
    }

    private Vector2 VisibleDiskPoint(int shot, int sample)
    {
        Vector2 point = positions[shot, sample];
        float diskAngle = rotatingView ? viewStartAngle : currentAngle;
        return diskCenter + (Vector2)(Quaternion.Euler(0, 0,
            diskAngle - angles[shot, sample]) * (point - diskCenter));
    }

    private bool ClipToDisk(ref Vector2 start, ref Vector2 end)
    {
        Vector2 delta = end - start;
        float lengthSquared = delta.sqrMagnitude;
        if (lengthSquared <= .01f) return false;

        float radius = Mathf.Max(0f, diskRadius - LineHalfWidth);
        Vector2 offset = start - diskCenter;
        float startDistance = offset.sqrMagnitude;
        float endDistance = (end - diskCenter).sqrMagnitude;
        if (startDistance <= radius * radius && endDistance <= radius * radius)
            return true;

        float b = 2f * Vector2.Dot(offset, delta);
        float c = startDistance - radius * radius;
        float discriminant = b * b - 4f * lengthSquared * c;
        if (discriminant <= 0f) return false;

        float root = Mathf.Sqrt(discriminant);
        float enter = Mathf.Max(0f, (-b - root) / (2f * lengthSquared));
        float exit = Mathf.Min(1f, (-b + root) / (2f * lengthSquared));
        if (enter >= exit) return false;

        Vector2 originalStart = start;
        start = originalStart + delta * enter;
        end = originalStart + delta * exit;
        return true;
    }
}
