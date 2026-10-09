using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Telemedicina
{
    // Esquema original generado con primitivas. No representa al paciente y no
    // pretende ser una reconstrucción anatómica, diagnóstico o imagen DICOM.
    public sealed class EducationalModel : MonoBehaviour
    {
        private Transform pivot;
        private Camera view;
        private float yaw = -25, pitch = 8, zoom = 1;
        private bool dragging;
        public bool InteractionEnabled = true;
        private readonly List<Material> materials = new List<Material>();

        public void Create(Camera camera)
        {
            view = camera;
            pivot = new GameObject("Esquema manipulable").transform; pivot.SetParent(transform, false);
            var bone = Material(new Color32(230, 219, 192, 255));
            var disc = Material(new Color32(51, 194, 190, 255));
            var nerve = Material(new Color32(244, 182, 77, 255));
            for (int i = 0; i < 5; i++)
            {
                float y = (i - 2) * .36f;
                Part("Vértebra esquemática " + (5 - i), PrimitiveType.Cylinder, new Vector3(0, y, 0), new Vector3(.66f, .12f, .53f), bone);
                Part("Apófisis esquemática", PrimitiveType.Cube, new Vector3(0, y + .015f, .35f), new Vector3(.16f, .11f, .45f), bone);
                Part("Extensión izquierda", PrimitiveType.Cube, new Vector3(-.36f, y, .15f), new Vector3(.31f, .085f, .11f), bone);
                Part("Extensión derecha", PrimitiveType.Cube, new Vector3(.36f, y, .15f), new Vector3(.31f, .085f, .11f), bone);
                if (i < 4)
                    Part("Disco esquemático", PrimitiveType.Cylinder, new Vector3(0, y + .18f, 0), new Vector3(.59f, .04f, .48f), disc);
            }
            Part("Trayecto nervioso esquemático", PrimitiveType.Cylinder, new Vector3(0, 0, .22f), new Vector3(.06f, .88f, .06f), nerve);
            var collider = gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(1.6f, 2.6f, 1.7f);
            ResetView();
        }
        private Material Material(Color color)
        {
            var template = Resources.Load<Material>("ModeloLigero");
            var material = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color); materials.Add(material); return material;
        }
        private void Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(pivot, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            Object.Destroy(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        public void Rotate(float horizontal, float vertical = 0) { yaw += horizontal; pitch = Mathf.Clamp(pitch + vertical, -65, 65); Apply(); }
        public void Zoom(float amount) { zoom = Mathf.Clamp(zoom + amount, .65f, 1.2f); Apply(); }
        public void ResetView() { yaw = -25; pitch = 8; zoom = 1; Apply(); }
        private void Apply()
        {
            if (!pivot) return;
            pivot.localRotation = Quaternion.Euler(pitch, yaw, 0); pivot.localScale = Vector3.one * zoom;
        }
        private void Update()
        {
            if (!InteractionEnabled || !view || !pivot) { dragging = false; return; }
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            bool typing = selected && selected.GetComponent<InputField>() && selected.GetComponent<InputField>().isFocused;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                bool hit = Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()), out var rayHit, 30f) && rayHit.collider.gameObject == gameObject;
                bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                if (mouse.leftButton.wasPressedThisFrame && hit && !overUi) dragging = true;
                if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed) dragging = false;
                if (dragging) { var delta = mouse.delta.ReadValue(); Rotate(-delta.x * .3f, delta.y * .25f); }
                if (hit && !overUi) Zoom(mouse.scroll.ReadValue().y * .0015f);
            }
            var keys = Keyboard.current;
            if (keys == null || typing) return;
            float amount = 70 * Time.unscaledDeltaTime;
            if (keys.leftArrowKey.isPressed) Rotate(-amount);
            if (keys.rightArrowKey.isPressed) Rotate(amount);
            if (keys.upArrowKey.isPressed) Rotate(0, -amount);
            if (keys.downArrowKey.isPressed) Rotate(0, amount);
            if (keys.rKey.wasPressedThisFrame) ResetView();
        }
        private void OnDestroy() { foreach (var material in materials) Object.Destroy(material); }
    }
}
