using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CoriolisSimulator : MonoBehaviour
{
    private const float PinSnapDistance = 18f;

    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private RectTransform disk;
    [SerializeField] private Image diskBackingImage;
    [SerializeField] private Sprite earthSprite;
    [SerializeField] private Toggle earthToggle;
    [SerializeField] private RectTransform[] pins = new RectTransform[2];
    [SerializeField] private Button[] fireButtons = new Button[2];
    [SerializeField] private Button[] viewButtons = new Button[3];
    [SerializeField] private Slider rotationSpeedSlider;
    [SerializeField] private Slider projectileSpeedSlider;
    [SerializeField] private Slider trajectoryLifetimeSlider;
    [SerializeField] private Text[] pinSpeedLabels = new Text[2];
    [SerializeField] private RectTransform[] projectilePool = new RectTransform[20];
    [SerializeField] private CoriolisTrajectoryGraphic trajectory;
    [SerializeField] private float degreesPerSecond = 35f;
    [SerializeField] private float ballSpeed = 350f;

    private readonly Vector2[] pinOnDisk = new Vector2[2];
    private readonly Vector2[] pinStartPositions = new Vector2[2];
    private readonly bool[] placed = new bool[2];
    private Shot[] shots;
    private Quaternion diskStartRotation;
    private Quaternion backingStartRotation;
    private Vector2 diskCenter;
    private Vector2 backingCenter;
    private Sprite originalBackingSprite;
    private Color originalBackingColor;
    private int draggingPin = -1;
    private int viewPin = -1;
    private float angle;
    private float viewStartAngle;
    private bool paused;
    private int nextTrailSlot;

    private struct Shot
    {
        public bool active;
        public Vector2 position;
        public Vector2 velocity;
        public float age;
        public int trailSlot;
    }

    private void Awake()
    {
        diskStartRotation = disk.localRotation;
        backingStartRotation = diskBackingImage.rectTransform.localRotation;
        diskCenter = canvasRoot.InverseTransformPoint(disk.position);
        backingCenter = canvasRoot.InverseTransformPoint(diskBackingImage.rectTransform.position);
        originalBackingSprite = diskBackingImage.sprite;
        originalBackingColor = diskBackingImage.color;
        earthToggle.onValueChanged.AddListener(SetEarthBackground);
        SetEarthBackground(earthToggle.isOn);
        for (int i = 0; i < pins.Length; i++)
            pinStartPositions[i] = pins[i].anchoredPosition;

        shots = new Shot[projectilePool.Length];
        trajectory.Initialize(shots.Length, canvasRoot, diskCenter,
            Mathf.Min(disk.rect.width, disk.rect.height) * .5f);
        trajectory.SetRecording(true);
        trajectory.SetLifetime(trajectoryLifetimeSlider.value);
        trajectory.SetView(false, 0f);
        foreach (RectTransform projectile in projectilePool)
            projectile.gameObject.SetActive(false);

        fireButtons[0].onClick.AddListener(FireFromPinOne);
        fireButtons[1].onClick.AddListener(FireFromPinTwo);
        //viewButtons[0].onClick.AddListener(SetGlobalView);
        //viewButtons[1].onClick.AddListener(SetRedView);
        //viewButtons[2].onClick.AddListener(SetBlueView);
        rotationSpeedSlider.SetValueWithoutNotify(degreesPerSecond);
        projectileSpeedSlider.SetValueWithoutNotify(ballSpeed);
        rotationSpeedSlider.onValueChanged.AddListener(SetRotationSpeed);
        projectileSpeedSlider.onValueChanged.AddListener(SetProjectileSpeed);
        trajectoryLifetimeSlider.onValueChanged.AddListener(trajectory.SetLifetime);
        UpdateButtons();
        UpdateViewButtons();
        UpdatePinSpeeds();
    }

    private void OnDestroy()
    {
        if (fireButtons[0] != null) fireButtons[0].onClick.RemoveListener(FireFromPinOne);
        if (fireButtons[1] != null) fireButtons[1].onClick.RemoveListener(FireFromPinTwo);
        if (viewButtons[0] != null) viewButtons[0].onClick.RemoveListener(SetGlobalView);
        if (viewButtons[1] != null) viewButtons[1].onClick.RemoveListener(SetRedView);
        if (viewButtons[2] != null) viewButtons[2].onClick.RemoveListener(SetBlueView);
        if (rotationSpeedSlider != null) rotationSpeedSlider.onValueChanged.RemoveListener(SetRotationSpeed);
        if (projectileSpeedSlider != null) projectileSpeedSlider.onValueChanged.RemoveListener(SetProjectileSpeed);
        if (trajectoryLifetimeSlider != null) trajectoryLifetimeSlider.onValueChanged.RemoveListener(trajectory.SetLifetime);
        if (earthToggle != null) earthToggle.onValueChanged.RemoveListener(SetEarthBackground);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            paused = !paused;
        if (paused) return;

        trajectory.AdvanceTime(Time.deltaTime);
        angle += degreesPerSecond * Time.deltaTime;
        trajectory.SetAngle(angle);
        float visibleAngle = angle - ViewRotation();
        disk.localRotation = diskStartRotation * Quaternion.Euler(0, 0, visibleAngle);
        disk.position = canvasRoot.TransformPoint(ViewPoint(diskCenter));
        diskBackingImage.rectTransform.localRotation = backingStartRotation * Quaternion.Euler(0, 0, visibleAngle);
        diskBackingImage.rectTransform.position = canvasRoot.TransformPoint(ViewPoint(backingCenter));

        for (int i = 0; i < 2; i++)
        {
            if (!placed[i] || draggingPin == i) continue;
            pins[i].position = canvasRoot.TransformPoint(ViewPoint(PhysicalPin(i)));
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
            else
            {
                projectilePool[i].position = canvasRoot.TransformPoint(ViewPoint(shot.position));
                trajectory.AddPoint(shot.trailSlot, shot.position, angle);
            }
            shots[i] = shot;
        }
    }

    private Vector2 PhysicalPin(int index)
    {
        Vector2 local = disk.rect.center + Vector2.Scale(pinOnDisk[index], disk.rect.size * .5f);
        Quaternion rotation = diskStartRotation * Quaternion.Euler(0, 0, angle);
        return diskCenter + (Vector2)(rotation * local);
    }

    private Vector2 ViewPoint(Vector2 physicalPoint)
    {
        if (viewPin < 0) return physicalPoint;
        Vector2 relative = physicalPoint - diskCenter;
        return diskCenter + (Vector2)(Quaternion.Euler(0, 0, -ViewRotation()) * relative);
    }

    private float ViewRotation() => viewPin < 0 ? 0f : angle - viewStartAngle;

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
        if (TryGetPinPosition(data, out Vector2 normalized, out bool snapped) && snapped)
        {
            Vector2 local = disk.rect.center + Vector2.Scale(normalized, disk.rect.size * .5f);
            pins[index].position = disk.TransformPoint(local);
        }
        else pins[index].position = canvasRoot.TransformPoint(CanvasPoint(data));
    }

    private bool TryGetPinPosition(PointerEventData data, out Vector2 normalized, out bool snapped)
    {
        snapped = false;
        normalized = Vector2.zero;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                disk, data.position, data.pressEventCamera, out Vector2 point)) return false;

        Vector2 halfSize = disk.rect.size * .5f;
        Vector2 fromCenter = point - disk.rect.center;
        normalized = new Vector2(fromCenter.x / halfSize.x, fromCenter.y / halfSize.y);

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
        {
            float radius = fromCenter.magnitude;
            float ringSpacing = Mathf.Min(halfSize.x, halfSize.y) / 3f;
            float ring = Mathf.Clamp(Mathf.Round(radius / ringSpacing), 0f, 3f) * ringSpacing;
            if (Mathf.Abs(radius - ring) <= PinSnapDistance)
            {
                normalized = ring == 0f ? Vector2.zero : normalized * (ring / radius);
                snapped = true;
            }
        }

        return snapped || normalized.sqrMagnitude <= 1f;
    }

    public void EndDrag(int index, PointerEventData data)
    {
        if (draggingPin != index) return;

        if (TryGetPinPosition(data, out Vector2 normalized, out _))
        {
            pinOnDisk[index] = normalized;
            placed[index] = true;
        }

        draggingPin = -1;
        if (placed[index])
        {
            pins[index].position = canvasRoot.TransformPoint(ViewPoint(PhysicalPin(index)));
        }
        else pins[index].anchoredPosition = pinStartPositions[index];
        UpdateButtons();
        UpdateViewButtons();
        UpdatePinSpeeds();
    }

    private void UpdateButtons()
    {
        bool ready = placed[0] && placed[1];
        foreach (Button button in fireButtons) button.interactable = ready;
    }

    private void UpdateViewButtons()
    {
        //viewButtons[0].interactable = viewPin != -1;
        //viewButtons[1].interactable = placed[0] && viewPin != 0;
        //viewButtons[2].interactable = placed[1] && viewPin != 1;
    }

    private void SetView(int pin)
    {
        if (pin >= 0)
        {
            Vector2 radius = PhysicalPin(pin) - diskCenter;
            if (radius.sqrMagnitude > .001f)
            {
                float bearing = Mathf.Atan2(radius.y, radius.x) * Mathf.Rad2Deg;
                viewStartAngle = angle - (bearing - 90f);
            }
            else viewStartAngle = angle - ViewRotation();
        }
        viewPin = pin;
        trajectory.SetView(viewPin >= 0, viewStartAngle);
        UpdateViewButtons();
    }

    private void SetGlobalView() => SetView(-1);
    private void SetRedView() => SetView(0);
    private void SetBlueView() => SetView(1);

    private void FireFromPinOne() => Fire(0);
    private void FireFromPinTwo() => Fire(1);
    private void SetRotationSpeed(float value)
    {
        degreesPerSecond = value;
        UpdatePinSpeeds();
    }

    private void UpdatePinSpeeds()
    {
        for (int i = 0; i < pinSpeedLabels.Length; i++)
        {
            if (pinSpeedLabels[i] == null) continue;
            string pinName = i == 0 ? "빨간 핀" : "파란 핀";
            string speed = placed[i]
                ? (Mathf.Abs(degreesPerSecond) * Mathf.Deg2Rad *
                   Vector2.Scale(pinOnDisk[i], disk.rect.size * .5f).magnitude).ToString("F1") + " px/s"
                : "--";
            pinSpeedLabels[i].text = pinName + " 속도: " + speed;
        }
    }
    private void SetProjectileSpeed(float value) => ballSpeed = value;
    private void SetEarthBackground(bool enabled)
    {
        diskBackingImage.sprite = enabled ? earthSprite : originalBackingSprite;
        diskBackingImage.color = enabled ? Color.white : originalBackingColor;
    }

    private void Fire(int source)
    {
        if (paused || !placed[0] || !placed[1]) return;

        Vector2 start = PhysicalPin(source);
        Vector2 target = PhysicalPin(1 - source);
        Vector2 direction = target - start;
        if (direction.sqrMagnitude < .001f) return;
        float angularSpeed = degreesPerSecond * Mathf.Deg2Rad;
        Vector2 radius = start - diskCenter;
        Vector2 tangentialVelocity = new Vector2(-radius.y, radius.x) * angularSpeed;

        int projectileIndex = -1;
        int oldestIndex = 0;
        for (int i = 0; i < shots.Length; i++)
        {
            if (!shots[i].active)
            {
                projectileIndex = i;
                break;
            }
            if (shots[i].age > shots[oldestIndex].age) oldestIndex = i;
        }

        if (projectileIndex < 0) projectileIndex = oldestIndex;
        int trailSlot = shots[projectileIndex].active
            ? shots[projectileIndex].trailSlot
            : FindTrailSlot();
        shots[projectileIndex] = new Shot
        {
            active = true,
            position = start,
            velocity = direction.normalized * ballSpeed,
            trailSlot = trailSlot
        };
        projectilePool[projectileIndex].position = canvasRoot.TransformPoint(ViewPoint(start));
        projectilePool[projectileIndex].gameObject.SetActive(true);
        trajectory.BeginShot(trailSlot, start, angle);
    }

    private int FindTrailSlot()
    {
        int fallback = -1;
        for (int offset = 0; offset < shots.Length; offset++)
        {
            int slot = (nextTrailSlot + offset) % shots.Length;
            bool inUse = false;
            for (int i = 0; i < shots.Length; i++)
                inUse |= shots[i].active && shots[i].trailSlot == slot;
            if (inUse) continue;
            if (!trajectory.HasSamples(slot))
            {
                nextTrailSlot = (slot + 1) % shots.Length;
                return slot;
            }
            if (fallback < 0) fallback = slot;
        }
        nextTrailSlot = (fallback + 1) % shots.Length;
        return fallback;
    }
}
