using UnityEngine;
using UnityEngine.UI;

public sealed class CoriolisTrajectoryGraphic : MaskableGraphic
{
    private const int SamplesPerShot = 320;
    private const float LineHalfWidth = 3f;
    private const float DashHalfWidth = 2f;
    private const float DashLength = 5f;
    private const float DashPeriod = 14f;

    private Vector2[,] positions;
    private float[,] angles;
    private float[,] sampleTimes;
    private float[,] pathDistances;
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
    private float elapsedTime;
    private float lifetime = 2f;
    [SerializeField] private Color flightTrailColor = new Color(.15f, .72f, 1f, 1f);

    public void Initialize(int shotCount, RectTransform canvas, Vector2 center, float radius)
    {
        positions = new Vector2[shotCount, SamplesPerShot];
        angles = new float[shotCount, SamplesPerShot];
        sampleTimes = new float[shotCount, SamplesPerShot];
        pathDistances = new float[shotCount, SamplesPerShot];
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

    public void SetLifetime(float seconds)
    {
        lifetime = Mathf.Max(0f, seconds);
        if (lifetime <= 0f)
        {
            if (counts != null) System.Array.Clear(counts, 0, counts.Length);
            hasSamples = false;
            SetVerticesDirty();
            return;
        }
        PruneExpiredPoints();
    }

    public void AdvanceTime(float deltaTime)
    {
        elapsedTime += deltaTime;
        PruneExpiredPoints();
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
        if (!recording || lifetime <= 0f) return;
        counts[index] = 0;
        next[index] = 0;
        AddPoint(index, position, angle);
    }

    public bool HasSamples(int index) => counts != null && counts[index] > 0;

    private void PruneExpiredPoints()
    {
        if (counts == null) return;
        hasSamples = false;
        bool removed = false;
        for (int i = 0; i < counts.Length; i++)
        {
            while (counts[i] > 0)
            {
                int oldest = (next[i] - counts[i] + SamplesPerShot) % SamplesPerShot;
                if (elapsedTime - sampleTimes[i, oldest] < lifetime) break;
                counts[i]--;
                removed = true;
            }
            hasSamples |= counts[i] > 0;
        }
        if (removed) SetVerticesDirty();
    }

    public void AddPoint(int index, Vector2 position, float angle)
    {
        if (!recording || lifetime <= 0f) return;
        int slot = next[index];
        int previous = (slot - 1 + SamplesPerShot) % SamplesPerShot;
        pathDistances[index, slot] = counts[index] == 0 ? 0f :
            pathDistances[index, previous] + Vector2.Distance(positions[index, previous], position);
        positions[index, slot] = position;
        angles[index, slot] = angle;
        sampleTimes[index, slot] = elapsedTime;
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
                int previousSlot = (slot - 1 + SamplesPerShot) % SamplesPerShot;
                AddDashedLine(mesh, canvasToGraphic.MultiplyPoint3x4(previousBall),
                    canvasToGraphic.MultiplyPoint3x4(currentBall),
                    pathDistances[shot, previousSlot], pathDistances[shot, slot], ballTrailColor);
                previousBall = currentBall;
                previousDisk = nextDisk;
            }
        }
    }

    private static void AddDashedLine(VertexHelper mesh, Vector2 start, Vector2 end,
        float startDistance, float endDistance, Color32 lineColor)
    {
        if (endDistance - startDistance <= .01f) return;
        for (float dash = Mathf.Floor(startDistance / DashPeriod) * DashPeriod;
             dash < endDistance; dash += DashPeriod)
        {
            float from = Mathf.Max(startDistance, dash);
            float to = Mathf.Min(endDistance, dash + DashLength);
            if (to <= from) continue;
            float length = endDistance - startDistance;
            AddLine(mesh, Vector2.Lerp(start, end, (from - startDistance) / length),
                Vector2.Lerp(start, end, (to - startDistance) / length), lineColor, DashHalfWidth);
        }
    }

    private static void AddLine(VertexHelper mesh, Vector2 start, Vector2 end,
        Color32 lineColor, float halfWidth = LineHalfWidth)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= .01f) return;
        Vector2 side = new Vector2(-direction.y, direction.x).normalized * halfWidth;
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
