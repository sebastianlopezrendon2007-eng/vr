using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;

namespace Telemedicina
{
    public sealed class QuestRig : MonoBehaviour
    {
        public static bool Active => Application.platform == RuntimePlatform.Android && !Application.isEditor;
        private Transform desk;
        private Camera view;
        private InputAction menuLeft, menuRight, position, rotation;

        public Camera Create(Transform panels)
        {
            desk = panels;
            var offset = new GameObject("Altura del visor"); offset.transform.SetParent(transform, false);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera"; cameraObject.transform.SetParent(offset.transform, false);
            view = cameraObject.GetComponent<Camera>();
            view.nearClipPlane = .1f; view.farClipPlane = 30;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Color.clear;
            view.allowHDR = false; view.allowMSAA = true;
            var origin = gameObject.AddComponent<XROrigin>();
            origin.Camera = view; origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            position = new InputAction("Cabeza posición", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            rotation = new InputAction("Cabeza rotación", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            var pose = cameraObject.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(position); pose.rotationInput = new InputActionProperty(rotation);
            pose.ignoreTrackingState = true;
            position.Enable(); rotation.Enable();
            cameraObject.AddComponent<ARCameraManager>();
            cameraObject.AddComponent<ARCameraBackground>();
            gameObject.AddComponent<ARSession>();
            var events = new GameObject("Controles Quest", typeof(UnityEngine.EventSystems.EventSystem));
            events.AddComponent<QuestPointer>().Initialize(view, offset.transform);
            menuLeft = new InputAction("Centrar consultorio izquierdo", InputActionType.Button, "<XRController>{LeftHand}/menu"); menuLeft.Enable();
            menuRight = new InputAction("Centrar consultorio derecho", InputActionType.Button, "<XRController>{RightHand}/menu"); menuRight.Enable();
            StartCoroutine(FirstPosition()); return view;
        }
        private IEnumerator FirstPosition()
        {
            yield return null; Recenter();
            yield return new WaitForSecondsRealtime(1); Recenter();
        }
        private void Update()
        {
            // Cualquiera de los dos botones de menú permite recuperar la posición
            // después de perder seguimiento o cambiar de manos a controles.
            if ((menuLeft != null && menuLeft.WasPressedThisFrame()) ||
                (menuRight != null && menuRight.WasPressedThisFrame())) Recenter();
        }
        public void Recenter()
        {
            if (!view || !desk) return;
            var forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            forward.Normalize();
            desk.SetPositionAndRotation(view.transform.position + forward * 1.8f - Vector3.up * .06f, Quaternion.LookRotation(forward));
        }
        private void OnDestroy()
        {
            menuLeft?.Dispose(); menuRight?.Dispose(); position?.Dispose(); rotation?.Dispose();
            if (UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.GetComponent<QuestPointer>())
                Destroy(UnityEngine.EventSystems.EventSystem.current.gameObject);
        }
    }
}
