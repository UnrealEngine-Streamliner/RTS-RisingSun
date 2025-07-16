using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private Rigidbody cameraTarget;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private new Camera camera;
    [SerializeField] private CameraConfig cameraConfig;
    [SerializeField] private LayerMask selectableUnitsLayer;
    [SerializeField] private LayerMask floorLayers;
    [SerializeField] private RectTransform selectedBox;

    private Vector2 startingMousePosition;

    private CinemachineFollow cinemachineFollow;
    private float zoomStartTime;
    private float rotationStartTime;
    private Vector3 startingFollowOffset;
    private float maxRotationAmount;
    private List<ISelectable> selectedUnits = new(12);
    private HashSet<AbstractUnit> addedUnits = new(24);
    private HashSet<AbstractUnit> aliveUnits = new(100);

    private void Awake()
    {
        if (!cinemachineCamera.TryGetComponent<CinemachineFollow>(out cinemachineFollow))
        {
            Debug.LogError("Cinemachine Camera did not have CinemachineFollow. Zoom functionality will not work");
        }

        startingFollowOffset = cinemachineFollow.FollowOffset;
        maxRotationAmount = Mathf.Abs(cinemachineFollow.FollowOffset.z);

        Bus<UnitSelectedEvent>.OnEvent += HandleUnitSelected;
        Bus<UnitDeselectedEvent>.OnEvent += HandleUnitDeselected;
        Bus<UnitSpawnEvent>.OnEvent += HandleUnitSpawned;
    }

    private void OnDestroy()
    {
        Bus<UnitSelectedEvent>.OnEvent -= HandleUnitSelected;
        Bus<UnitDeselectedEvent>.OnEvent -= HandleUnitDeselected;
        Bus<UnitSpawnEvent>.OnEvent -= HandleUnitSpawned;
    }

    private void Update()
    {
        HandleKeyboardInputMovement();
        HandleZooming();
        HandleRotation();
        HandleMovingUnit();
        HandleDragSelectionUnits();
    }

    private void HandleUnitSpawned(UnitSpawnEvent evt) => aliveUnits.Add(evt.Unit);

    private void HandleUnitSelected(UnitSelectedEvent evt) => selectedUnits.Add(evt.Unit);

    private void HandleUnitDeselected(UnitDeselectedEvent evt) => selectedUnits.Remove(evt.Unit);

    private void HandleDragSelectionUnits()
    {
        if (selectedBox == null)
        {
            return;
        }
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleMouseDown();
        }
        else if (Mouse.current.leftButton.isPressed && !Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleMouseDrag();
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            HandleMouseUp();
        }
    }

    private void HandleMouseUp()
    {
        if (!Keyboard.current.shiftKey.isPressed)
        {
            DeselectAllUnits();
        }
        HandleSelectionUnit();
        foreach (AbstractUnit unit in addedUnits)
        {
            unit.Select();
        }
        // disable the UI
        selectedBox.gameObject.SetActive(false);
    }

    private void HandleMouseDrag()
    {
        Bounds selectionBoxBounds = ResizeSelectionBox();
        foreach (AbstractUnit unit in aliveUnits)
        {
            Vector2 unitPosition = camera.WorldToScreenPoint(unit.transform.position);
            if (selectionBoxBounds.Contains(unitPosition))
            {
                // we want to select the unit when the mouse is released
                addedUnits.Add(unit);
            }
        }
    }

    private void HandleMouseDown()
    {
        selectedBox.sizeDelta = Vector2.zero;
        // enable the UI
        selectedBox.gameObject.SetActive(true);
        // store start position
        startingMousePosition = Mouse.current.position.ReadValue();
        addedUnits.Clear();
    }

    private void DeselectAllUnits()
    {
        ISelectable[] currentlySelectedUnits = selectedUnits.ToArray();
        foreach (ISelectable selectable in currentlySelectedUnits)
        {
            selectable.Deselect();
        }
    }

    private Bounds ResizeSelectionBox()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        float width = mousePosition.x - startingMousePosition.x;
        float height = mousePosition.y - startingMousePosition.y;

        selectedBox.anchoredPosition = startingMousePosition + new Vector2(width / 2, height / 2);
        selectedBox.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        return new Bounds(selectedBox.anchoredPosition, selectedBox.sizeDelta);
    }

    private void HandleMovingUnit()
    {
        if (selectedUnits.Count == 0)
        {
            return;
        }
        Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Keyboard.current.mKey.isPressed)
        {
            if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, floorLayers))
            {
                foreach (ISelectable selectable in selectedUnits)
                {
                    if (selectable is IMoveable moveable)
                    {
                        moveable.Move(hit.point);
                    }
                }
            }
        }
    }

    private void HandleSelectionUnit()
    {
       if (camera == null)
        {
            return;
        }

        Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, selectableUnitsLayer)
            && hit.collider.TryGetComponent(out ISelectable selectable))
        {
            selectable.Select();
        }
    }

    private void HandleRotation()
    {
        if (ShouldSetRotationStartTime())
        {
            rotationStartTime = Time.time;
        }

        float rotationTime = Mathf.Clamp01((Time.time - rotationStartTime) * cameraConfig.RotationSpeed);
        Vector3 targetFollowOffset;
        if (Keyboard.current.qKey.isPressed)
        {
            targetFollowOffset = new Vector3(
                maxRotationAmount,
                cinemachineFollow.FollowOffset.y,
                0
            );
        }
        else if (Keyboard.current.eKey.isPressed)
        {
            targetFollowOffset = new Vector3(
                -maxRotationAmount,
                cinemachineFollow.FollowOffset.y,
                0
            );
        }
        else
        {
            targetFollowOffset = new Vector3(
                startingFollowOffset.x,
                cinemachineFollow.FollowOffset.y,
                startingFollowOffset.z
            );
        }

        cinemachineFollow.FollowOffset = Vector3.Slerp(
            cinemachineFollow.FollowOffset,
            targetFollowOffset,
            rotationTime
        );
    }

    private bool ShouldSetRotationStartTime() =>
        Keyboard.current.qKey.wasPressedThisFrame ||
        Keyboard.current.eKey.wasPressedThisFrame ||
        Keyboard.current.qKey.wasReleasedThisFrame ||
        Keyboard.current.eKey.wasReleasedThisFrame;

    private void HandleZooming()
    {
        if (ShouldSetZoomStartTime())
        {
            zoomStartTime = Time.time;
        }

        Vector3 targetFollowOffset;

        float zoomTime = Mathf.Clamp01((Time.time - zoomStartTime) * cameraConfig.ZoomSpeed);

        if (Keyboard.current.zKey.isPressed)
        {
            targetFollowOffset = new Vector3(
                cinemachineFollow.FollowOffset.x,
                cameraConfig.MinZoomDistance,
                cinemachineFollow.FollowOffset.z
            );
        }
        else
        {
            targetFollowOffset = new Vector3(
                cinemachineFollow.FollowOffset.x,
                startingFollowOffset.y,
                cinemachineFollow.FollowOffset.z
            );
        }
        cinemachineFollow.FollowOffset = Vector3.Slerp(
            cinemachineFollow.FollowOffset,
            targetFollowOffset,
            zoomTime
        );
    }

    private bool ShouldSetZoomStartTime()
    {
        return Keyboard.current.zKey.wasPressedThisFrame || Keyboard.current.zKey.wasReleasedThisFrame;
    }

    private void HandleKeyboardInputMovement()
    {
        Vector2 moveAmount = GetKeyboardMoveAmount();
        moveAmount += GetMouseMoveAmount();
        cameraTarget.linearVelocity = new Vector3(moveAmount.x, 0, moveAmount.y);
    }

    private Vector2 GetKeyboardMoveAmount()
    {
        Vector2 moveAmount = Vector2.zero;

        if (Keyboard.current.upArrowKey.isPressed)
        {
            moveAmount.y += cameraConfig.KeyboardPanSpeed;
        }
        if (Keyboard.current.leftArrowKey.isPressed)
        {
            moveAmount.x -= cameraConfig.KeyboardPanSpeed;
        }
        if (Keyboard.current.rightArrowKey.isPressed)
        {
            moveAmount.x += cameraConfig.KeyboardPanSpeed;
        }
        if (Keyboard.current.downArrowKey.isPressed)
        {
            moveAmount.y -= cameraConfig.KeyboardPanSpeed;
        }
        return moveAmount;
    }

    private Vector2 GetMouseMoveAmount()
    {
        Vector2 moveAmount = Vector2.zero;
        if (!cameraConfig.EnableEdgePan)
        {
            return moveAmount;
        }
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        if (mousePosition.x <= cameraConfig.EdgePanSize)
        {
            moveAmount.x -= cameraConfig.MousePanSpeed;
        }
        if (mousePosition.y >= screenHeight - cameraConfig.EdgePanSize)
        {
            moveAmount.y += cameraConfig.MousePanSpeed;
        }

        if (mousePosition.x >= screenWidth - cameraConfig.EdgePanSize)
        {
            moveAmount.x += cameraConfig.MousePanSpeed;
        }

        if (mousePosition.y <= cameraConfig.EdgePanSize)
        {
            moveAmount.y -= cameraConfig.MousePanSpeed;
        }
        return moveAmount;
    }
}
