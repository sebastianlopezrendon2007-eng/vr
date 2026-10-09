using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Telemedicina
{
    // Punteros para Touch Plus y el perfil OpenXR Hand Interaction.
    public sealed class QuestPointer : BaseInputModule
    {
        private sealed class Hand
        {
            internal InputAction position, rotation, tracked, trigger, scroll;
            internal InputAction handPosition, handRotation, handTracked, pinch;
            internal PointerEventData data;
            internal LineRenderer line;
            internal bool pressed;
            internal bool sourceKnown, sourceHand;
        }
        private readonly List<Hand> hands = new List<Hand>();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private Camera view;
        private Transform origin;
        private Material laser;
        private Canvas[] canvases;
        private float nextScan;
        public void Initialize(Camera camera, Transform trackingSpace)
        {
            view = camera; origin = trackingSpace;
            laser = new Material(Resources.Load<Material>("ModeloLigero")); laser.SetColor("_BaseColor", Ui.Teal);
            AddHand("LeftHand", -21); AddHand("RightHand", -22);
        }
        private void AddHand(string side, int id)
        {
            string binding = "<XRController>{" + side + "}/";
            string handBinding = "<HandInteraction>{" + side + "}/";
            var hand = new Hand
            {
                position = new InputAction(side + " aim posición", InputActionType.Value, binding + "pointerPosition", expectedControlType: "Vector3"),
                rotation = new InputAction(side + " aim rotación", InputActionType.Value, binding + "pointerRotation", expectedControlType: "Quaternion"),
                tracked = new InputAction(side + " seguimiento", InputActionType.Button, binding + "isTracked"),
                trigger = new InputAction(side + " gatillo", InputActionType.Button, binding + "triggerPressed"),
                scroll = new InputAction(side + " scroll", InputActionType.Value, binding + "thumbstick", expectedControlType: "Vector2"),
                handPosition = new InputAction(side + " mano aim posición", InputActionType.Value, handBinding + "pointerPosition", expectedControlType: "Vector3"),
                handRotation = new InputAction(side + " mano aim rotación", InputActionType.Value, handBinding + "pointerRotation", expectedControlType: "Quaternion"),
                handTracked = new InputAction(side + " mano seguimiento", InputActionType.Button, handBinding + "isTracked"),
                pinch = new InputAction(side + " pellizco", InputActionType.Value, handBinding + "pinchValue", expectedControlType: "Axis"),
                data = new PointerEventData(eventSystem) { pointerId = id, button = PointerEventData.InputButton.Left }
            };
            var obj = new GameObject("Rayo " + side); obj.transform.SetParent(transform, false);
            hand.line = obj.AddComponent<LineRenderer>(); hand.line.positionCount = 2;
            hand.line.startWidth = .002f; hand.line.endWidth = .001f; hand.line.sharedMaterial = laser;
            hand.position.Enable(); hand.rotation.Enable(); hand.tracked.Enable(); hand.trigger.Enable(); hand.scroll.Enable();
            hand.handPosition.Enable(); hand.handRotation.Enable(); hand.handTracked.Enable(); hand.pinch.Enable();
            hands.Add(hand);
        }
        public override void Process()
        {
            if (!view || !origin) return;
            if (canvases == null || Time.unscaledTime >= nextScan)
            {
                canvases = FindObjectsOfType<Canvas>(true); nextScan = Time.unscaledTime + .25f;
            }
            bool handsAvailable = hands.Exists(h => h.handTracked.IsPressed());
            foreach (var hand in hands) ProcessHand(hand, handsAvailable);
        }
        private void ProcessHand(Hand hand, bool handsAvailable)
        {
            bool useHand = handsAvailable && hand.handTracked.IsPressed();
            bool tracked = useHand || (!handsAvailable && hand.tracked.IsPressed());
            if (!tracked)
            {
                Release(hand, false); hand.sourceKnown = false;
                HandlePointerExitAndEnter(hand.data, null); hand.line.enabled = false; return;
            }
            // Al cambiar de dispositivo se cancela cualquier pulsación en curso.
            // El nuevo dispositivo empieza a pulsar en el siguiente frame, evitando
            // clics duplicados si ambos orígenes estaban activos durante el cambio.
            bool sourceChanged = hand.sourceKnown && hand.sourceHand != useHand;
            if (sourceChanged) Release(hand, false);
            hand.sourceKnown = true; hand.sourceHand = useHand;
            var localPosition = useHand ? hand.handPosition.ReadValue<Vector3>() : hand.position.ReadValue<Vector3>();
            var localRotation = useHand ? hand.handRotation.ReadValue<Quaternion>() : hand.rotation.ReadValue<Quaternion>();
            var ray = new Ray(origin.TransformPoint(localPosition), origin.rotation * localRotation * Vector3.forward);
            var found = Find(ray, out var hit, out var screen, out var length);
            var data = hand.data;
            data.delta = screen - data.position; data.position = screen;
            data.pointerCurrentRaycast = found ? hit : new RaycastResult();
            var current = found ? hit.gameObject : null;
            HandlePointerExitAndEnter(data, current);
            hand.line.enabled = true; hand.line.SetPosition(0, ray.origin); hand.line.SetPosition(1, ray.GetPoint(found ? length : 2));
            bool pressed = !sourceChanged && (useHand ? hand.pinch.ReadValue<float>() >= .72f : hand.trigger.IsPressed());
            if (pressed && !hand.pressed)
            {
                data.eligibleForClick = true; data.dragging = false; data.pressPosition = data.position;
                data.pointerPressRaycast = data.pointerCurrentRaycast;
                data.pointerPress = ExecuteEvents.ExecuteHierarchy(current, data, ExecuteEvents.pointerDownHandler);
                if (!data.pointerPress) data.pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(current);
                data.rawPointerPress = current;
                data.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(current);
                if (data.pointerDrag) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.initializePotentialDrag);
            }
            if (pressed && data.pointerDrag && (data.dragging || (data.position - data.pressPosition).sqrMagnitude > 16))
            {
                if (!data.dragging)
                {
                    ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.beginDragHandler); data.dragging = true;
                    data.eligibleForClick = false;
                }
                ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.dragHandler);
            }
            if (!pressed && hand.pressed) Release(hand, true);
            hand.pressed = pressed;
            data.scrollDelta = useHand ? Vector2.zero : hand.scroll.ReadValue<Vector2>() * (12f * Time.unscaledDeltaTime);
            if (current && data.scrollDelta.sqrMagnitude > .00001f) ExecuteEvents.ExecuteHierarchy(current, data, ExecuteEvents.scrollHandler);
        }
        private bool Find(Ray ray, out RaycastResult result, out Vector2 screen, out float length)
        {
            result = new RaycastResult(); length = 10;
            screen = view.WorldToScreenPoint(ray.GetPoint(2));
            foreach (var canvas in canvases)
            {
                if (!canvas || !canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.WorldSpace) continue;
                var plane = new Plane(canvas.transform.forward, canvas.transform.position);
                if (!plane.Raycast(ray, out var distance) || distance < .01f || distance > length) continue;
                var raycaster = canvas.GetComponent<GraphicRaycaster>(); if (!raycaster || !raycaster.isActiveAndEnabled) continue;
                var point = view.WorldToScreenPoint(ray.GetPoint(distance));
                var probe = new PointerEventData(eventSystem) { position = point };
                hits.Clear(); raycaster.Raycast(probe, hits);
                if (hits.Count == 0) continue;
                result = hits[0]; result.distance = distance; result.worldPosition = ray.GetPoint(distance);
                length = distance; screen = point;
            }
            return result.gameObject;
        }
        private void Release(Hand hand, bool click)
        {
            var data = hand.data;
            if (data.pointerPress)
            {
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
                if (click && data.eligibleForClick && clicked == data.pointerPress) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
            }
            if (data.dragging && data.pointerDrag) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.endDragHandler);
            data.pointerPress = data.rawPointerPress = data.pointerDrag = null;
            data.dragging = data.eligibleForClick = false; hand.pressed = false;
        }
        protected override void OnDisable()
        {
            foreach (var hand in hands) Release(hand, false);
            base.OnDisable();
        }
        protected override void OnDestroy()
        {
            foreach (var hand in hands)
            {
                hand.position.Dispose(); hand.rotation.Dispose(); hand.tracked.Dispose(); hand.trigger.Dispose(); hand.scroll.Dispose();
                hand.handPosition.Dispose(); hand.handRotation.Dispose(); hand.handTracked.Dispose(); hand.pinch.Dispose();
            }
            if (laser) Destroy(laser); base.OnDestroy();
        }
    }
}
