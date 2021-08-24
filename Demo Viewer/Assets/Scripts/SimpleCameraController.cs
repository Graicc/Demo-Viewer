﻿using UnityEngine;

namespace UnityTemplateProjects
{
    public class SimpleCameraController : MonoBehaviour
    {
        class CameraState
        {
            public float yaw;
            public float pitch;
            public float roll;
            public float x;
            public float y;
            public float z;

            public void SetFromTransform(Transform t, Vector3 origin)
            {
                pitch = t.eulerAngles.x;
                yaw = t.eulerAngles.y;
                roll = t.eulerAngles.z;
                x = t.position.x + origin.x;
                y = t.position.y + origin.y;
                z = t.position.z + origin.z;
            }

            public void Translate(Vector3 translation)
            {
                Vector3 rotatedTranslation = Quaternion.Euler(0, yaw, roll) * translation;

                x += rotatedTranslation.x;
                y += rotatedTranslation.y;
                z += rotatedTranslation.z;
            }

            public void LerpTowards(CameraState target, float positionLerpPct, float rotationLerpPct)
            {
                yaw = Mathf.Lerp(yaw, target.yaw, rotationLerpPct);
                pitch = Mathf.Lerp(pitch, target.pitch, rotationLerpPct);
                roll = Mathf.Lerp(roll, target.roll, rotationLerpPct);
                
                x = Mathf.Lerp(x, target.x, positionLerpPct);
                y = Mathf.Lerp(y, target.y, positionLerpPct);
                z = Mathf.Lerp(z, target.z, positionLerpPct);
            }

            public void UpdateTransform(Transform t)
            {
                t.eulerAngles = new Vector3(pitch, yaw, roll);
                t.position = new Vector3(x, y, z);
            }
        }
        
        CameraState m_TargetCameraState = new CameraState();
        CameraState m_InterpolatingCameraState = new CameraState();

        private Transform povTransform = null;
        public Transform defaultParentTransform = null;
        private Vector3 origin = Vector3.zero;

        public Transform PovTransform
        {
            set
            {
                if (value == null)
                {
                    if (defaultParentTransform == null)
                    {
                        
                    }
                    else
                    {
                        transform.SetParent(defaultParentTransform);
                        defaultParentTransform = null;
                        povTransform = null;
                    }
                }
                else
                {
                    if (defaultParentTransform == null)
                    {
                        defaultParentTransform = transform.parent;
                    }
                    povTransform = value;
                    transform.SetParent(value);
                }
            }
        }
        
        public Vector3 Origin {
            set
            {
                Vector3 diff = value - origin;
                m_TargetCameraState.x += diff.x;
                m_TargetCameraState.y += diff.y;
                m_TargetCameraState.z += diff.z;
                origin = value;
            }
            get
            {
                return origin;
            }
        }
        
        private Quaternion targetRotation = Quaternion.identity;
        public Quaternion TargetRotation {
            set
            {
                //targetRotation diff = value - origin;
                //m_TargetCameraState.x += diff.x;
                //m_TargetCameraState.y += diff.y;
                //m_TargetCameraState.z += diff.z;
                Quaternion diff = value * Quaternion.Inverse(targetRotation);
                Quaternion cameraStateRotation = Quaternion.Euler(m_TargetCameraState.pitch, m_TargetCameraState.yaw, m_TargetCameraState.roll);
                Quaternion newRotation = diff * cameraStateRotation;
                m_TargetCameraState.pitch = newRotation.eulerAngles.x;
                m_TargetCameraState.yaw = newRotation.eulerAngles.y;
                m_TargetCameraState.roll = newRotation.eulerAngles.z;
                targetRotation = value;
            }
        }
        [Header("Movement Settings")]
        [Tooltip("Exponential boost factor on translation, controllable by mouse wheel.")]
        public float boost = 3.5f;

        [Tooltip("Time it takes to interpolate camera position 99% of the way to the target."), Range(0.001f, 1f)]
        public float positionLerpTime = 0.2f;

        [Header("Rotation Settings")]
        [Tooltip("X = Change in mouse position.\nY = Multiplicative factor for camera rotation.")]
        public AnimationCurve mouseSensitivityCurve = new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 5f), new Keyframe(1f, 2.5f, 0f, 0f));

        [Tooltip("Time it takes to interpolate camera rotation 99% of the way to the target."), Range(0.001f, 1f)]
        public float rotationLerpTime = 0.01f;

        [Tooltip("Whether or not to invert our Y axis for mouse input to rotation.")]
        public bool invertY = false;

        private void OnEnable()
        {
            m_TargetCameraState.SetFromTransform(transform, Vector3.zero);
            m_InterpolatingCameraState.SetFromTransform(transform, Vector3.zero);
        }

        public void ApplyPosition()
        {
            m_TargetCameraState.SetFromTransform(transform, Vector3.zero);
            m_InterpolatingCameraState.SetFromTransform(transform, Vector3.zero);
        }

        private static Vector3 GetInputTranslationDirection()
        {
            Vector3 direction = new Vector3();
            if (Input.GetKey(KeyCode.W))
            {
                direction += Vector3.forward;
            }
            if (Input.GetKey(KeyCode.S))
            {
                direction += Vector3.back;
            }
            if (Input.GetKey(KeyCode.A))
            {
                direction += Vector3.left;
            }
            if (Input.GetKey(KeyCode.D))
            {
                direction += Vector3.right;
            }
            if (Input.GetKey(KeyCode.Q))
            {
                direction += Vector3.down;
            }
            if (Input.GetKey(KeyCode.E))
            {
                direction += Vector3.up;
            }
            if(Input.GetButton("LeftBumper")){
                direction += Vector3.down;
            }
            if(Input.GetButton("RightBumper")){
                direction += Vector3.up;
            }
            direction.x += Input.GetAxis("LeftX") * 2.5f;
            direction.z += Input.GetAxis("LeftY") * -2.5f;
            return direction;
        }

        private void Update()
        {
            if (povTransform != null)
            {
                return;
            }
            // Hide and lock cursor when right mouse button pressed
            if (Input.GetMouseButtonDown(1))
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            // Unlock and show cursor when right mouse button released
            if (Input.GetMouseButtonUp(1))
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            // Rotation
            if (Input.GetMouseButton(1))
            {
                var mouseMovement = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y") * (invertY ? 1 : -1));
                
                var mouseSensitivityFactor = mouseSensitivityCurve.Evaluate(mouseMovement.magnitude);

                m_TargetCameraState.yaw += mouseMovement.x * mouseSensitivityFactor;
                m_TargetCameraState.pitch += mouseMovement.y * mouseSensitivityFactor;
            }

            Vector2 controllerRightStick = new Vector2(Input.GetAxis("RightX"), Input.GetAxis("RightY"));
            m_TargetCameraState.yaw += controllerRightStick.x * 1.25f;
            m_TargetCameraState.pitch += controllerRightStick.y * 1.25f;
            // Translation
            var translation = GetInputTranslationDirection() * Time.deltaTime;

            // Speed up movement when shift key held
            if (Input.GetKey(KeyCode.LeftShift))
            {
                translation *= 10.0f;
            }
            
            // Modify movement by a boost factor (defined in Inspector and modified in play mode through the mouse scroll wheel)
            boost += Input.mouseScrollDelta.y * 0.2f;
            translation *= Mathf.Pow(2.0f, boost);

            m_TargetCameraState.Translate(translation);

            // Framerate-independent interpolation
            // Calculate the lerp amount, such that we get 99% of the way to our target in the specified time
            //var positionLerpPct = 1f - Mathf.Exp((Mathf.Log(1f - 0.99f) / positionLerpTime) * Time.deltaTime);
            //var rotationLerpPct = 1f - Mathf.Exp((Mathf.Log(1f - 0.99f) / rotationLerpTime) * Time.deltaTime);
            var positionLerpPct = 1f;
            var rotationLerpPct = 1f;
            
            
            m_InterpolatingCameraState.LerpTowards(m_TargetCameraState, positionLerpPct, rotationLerpPct);

            m_InterpolatingCameraState.UpdateTransform(transform);
        }
    }

}