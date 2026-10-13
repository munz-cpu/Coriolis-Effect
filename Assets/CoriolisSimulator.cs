using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CoriolisSimulator : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private RectTransform disk;
    [SerializeField] private RectTransform[] pins = new RectTransform[2];
    [SerializeField] private Button[] fireButtons = new Button[2];
    [SerializeField] private RectTransform[] projectilePool = new RectTransform[20];
    [SerializeField] private float degreesPerSecond = 35f;
    [SerializeField] private float ballSpeed = 350f;

    private readonly Vector2[] pinOnDisk = new Vector2[2];
    private readonly Vector2[] pinStartPositions = new Vector2[2];
    private readonly bool[] placed = new bool[2];
    private Shot[] shots;
    private Quaternion diskStartRotation;
    private int draggingPin = -1;
    private float angle;

    private struct Shot
    {
        public bool active;
        public Vector2 position;
        public Vector2 velocity;
        public float age;
    }

    private void Awake()
    {
        diskStartRotation = disk.localRotation;
        for (int i = 0; i < pins.Length; i++)
            pinStartPositions[i] = pins[i].anchoredPosition;

        shots = new Shot[projectilePool.Length];
        foreach (RectTransform projectile in projectilePool)
            projectile.gameObject.SetActive(false);

        fireButtons[0].onClick.AddListener(FireFromPinOne);
        fireButtons[1].onClick.AddListener(FireFromPinTwo);
        UpdateButtons();
    }

    private void OnDestroy()
    {
        if (fireButtons[0] != null) fireButtons[0].onClick.RemoveListener(FireFromPinOne);
        if (fireButtons[1] != null) fireButtons[1].onClick.RemoveListener(FireFromPinTwo);
    }

    private void Update()
    {
        angle += degreesPerSecond * Time.deltaTime;
        disk.localRotation = diskStartRotation * Quaternion.Euler(0, 0, angle);

        for (int i = 0; i < 2; i++)
        {
            if (!placed[i] || draggingPin == i) continue;
            Vector2 point = disk.rect.center + Vector2.Scale(pinOnDisk[i], disk.rect.size * .5f);
            pins[i].position = disk.TransformPoint(point);
        }

        for (int i = 0; i < shots.Length; i++)
        {
            if (!shots[i].active) continue;
            Shot shot = shots[i];
            shot.position += shot.velocity * Time.deltaTime;
            shot.age += Time.deltaTime;
            if (shot.age >= 5f)
            {
                shot.active = false;
                projectilePool[i].gameObject.SetActive(false);
            }
            else projectilePool[i].position = canvasRoot.TransformPoint(shot.position);
            shots[i] = shot;
        }
    }

    private Vector2 CanvasPoint(PointerEventData data)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot, data.position, data.pressEventCamera, out Vector2 point);
        return point;
    }

    public void BeginDrag(int index, PointerEventData data)
    {
        draggingPin = index;
        Drag(index, data);
    }

    public void Drag(int index, PointerEventData data)
    {
        if (draggingPin != index) return;
        pins[index].position = canvasRoot.TransformPoint(CanvasPoint(data));
    }

    public void EndDrag(int index, PointerEventData data)
    {
        if (draggingPin != index) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                disk, data.position, data.pressEventCamera, out Vector2 point))
        {
            Vector2 halfSize = disk.rect.size * .5f;
            Vector2 fromCenter = point - disk.rect.center;
            Vector2 normalized = new Vector2(fromCenter.x / halfSize.x, fromCenter.y / halfSize.y);
            if (normalized.sqrMagnitude <= 1f)
            {
                pinOnDisk[index] = normalized;
                placed[index] = true;
            }
        }

        draggingPin = -1;
        if (placed[index])
        {
            Vector2 local = disk.rect.center + Vector2.Scale(pinOnDisk[index], disk.rect.size * .5f);
            pins[index].position = disk.TransformPoint(local);
        }
        else pins[index].anchoredPosition = pinStartPositions[index];
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool ready = placed[0] && placed[1];
        foreach (Button button in fireButtons) button.interactable = ready;
    }

    private void FireFromPinOne() => Fire(0);
    private void FireFromPinTwo() => Fire(1);

    private void Fire(int source)
    {
        if (!placed[0] || !placed[1]) return;

        Vector2 start = canvasRoot.InverseTransformPoint(pins[source].position);
        Vector2 target = canvasRoot.InverseTransformPoint(pins[1 - source].position);
        Vector2 direction = target - start;
        if (direction.sqrMagnitude < .001f) return;

        for (int i = 0; i < shots.Length; i++)
        {
            if (shots[i].active) continue;
            shots[i] = new Shot
            {
                active = true,
                position = start,
                velocity = direction.normalized * ballSpeed
            };
            projectilePool[i].position = canvasRoot.TransformPoint(start);
            projectilePool[i].gameObject.SetActive(true);
            break;
        }
    }
}
