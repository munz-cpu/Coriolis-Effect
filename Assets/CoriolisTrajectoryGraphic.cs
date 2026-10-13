using UnityEngine;
using UnityEngine.UI;

public sealed class CoriolisTrajectoryGraphic : MaskableGraphic
{
    private const int SamplesPerShot = 128;

    private Vector2[,] positions;
    private float[,] angles;
    private int[] counts;
    private int[] next;
    private RectTransform canvasRoot;
    private Vector2 diskCenter;
    private bool rotatingView;
    private float viewStartAngle;

    public void Initialize(int shotCount, RectTransform canvas, Vector2 center)
    {
        positions = new Vector2[shotCount, SamplesPerShot];
        angles = new float[shotCount, SamplesPerShot];
        counts = new int[shotCount];
        next = new int[shotCount];
        canvasRoot = canvas;
        diskCenter = center;
        raycastTarget = false;
    }

    public void SetView(bool rotating, float startAngle)
    {
        rotatingView = rotating;
        viewStartAngle = startAngle;
        SetVerticesDirty();
    }

    public void BeginShot(int index, Vector2 position, float angle)
    {
        counts[index] = 0;
        next[index] = 0;
        AddPoint(index, position, angle);
    }

    public void AddPoint(int index, Vector2 position, float angle)
    {
        int slot = next[index];
        positions[index, slot] = position;
        angles[index, slot] = angle;
        next[index] = (slot + 1) % SamplesPerShot;
        counts[index] = Mathf.Min(counts[index] + 1, SamplesPerShot);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (positions == null || canvasRoot == null) return;

        Matrix4x4 canvasToGraphic = rectTransform.worldToLocalMatrix * canvasRoot.localToWorldMatrix;
        Color32 vertexColor = color;
        for (int shot = 0; shot < counts.Length; shot++)
        {
            if (counts[shot] < 2) continue;
            int first = (next[shot] - counts[shot] + SamplesPerShot) % SamplesPerShot;
            Vector2 previous = VisiblePoint(shot, first, canvasToGraphic);
            for (int sample = 1; sample < counts[shot]; sample++)
            {
                int slot = (first + sample) % SamplesPerShot;
                Vector2 current = VisiblePoint(shot, slot, canvasToGraphic);
                Vector2 direction = current - previous;
                if (direction.sqrMagnitude > 0.01f)
                {
                    Vector2 side = new Vector2(-direction.y, direction.x).normalized * 1.5f;
                    int vertex = mesh.currentVertCount;
                    mesh.AddVert(previous - side, vertexColor, Vector2.zero);
                    mesh.AddVert(previous + side, vertexColor, Vector2.zero);
                    mesh.AddVert(current + side, vertexColor, Vector2.zero);
                    mesh.AddVert(current - side, vertexColor, Vector2.zero);
                    mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
                    mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
                }
                previous = current;
            }
        }
    }

    private Vector2 VisiblePoint(int shot, int sample, Matrix4x4 canvasToGraphic)
    {
        Vector2 point = positions[shot, sample];
        if (rotatingView)
        {
            Vector2 relative = point - diskCenter;
            point = diskCenter + (Vector2)(Quaternion.Euler(0, 0,
                viewStartAngle - angles[shot, sample]) * relative);
        }
        return canvasToGraphic.MultiplyPoint3x4(point);
    }
}
