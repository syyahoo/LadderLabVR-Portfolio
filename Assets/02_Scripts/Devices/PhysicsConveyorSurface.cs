using System;
using System.Collections.Generic;
using LadderLab.VR;
using LadderLab.Wiring;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LadderLab.Devices
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PhysicsConveyorSurface : MonoBehaviour, IPlcAddressBindable
    {
        private const string SurfaceTriggerName = "Physics Conveyor Surface";
        private const string SupportColliderName = "Physics Conveyor Support";

        [SerializeField] private PlcAddressBinding addressBinding;
        [SerializeField] private Vector3 localMoveDirection = Vector3.forward;
        [SerializeField, Min(0f)] private float beltSpeed = 1.15f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField, Min(0f)] private float acceleration = 8f;
        [SerializeField, Min(0f)] private float maxHorizontalSpeed = 2.4f;
        [SerializeField, Min(0.02f)] private float triggerHeight = 0.28f;
        [SerializeField, Min(0.01f)] private float supportHeight = 0.08f;
        [SerializeField] private bool createSupportCollider = true;
        [SerializeField, Min(0f)] private float downwardGripAcceleration = 0.4f;
        [SerializeField] private Renderer beltRenderer;
        [SerializeField] private Vector2 textureScrollPerSecond = new(0.8f, 0f);
        [SerializeField] private AudioClip runningAudioClip;
        [SerializeField, Range(0f, 1f)] private float runningSoundVolume = 0.16f;

        private readonly HashSet<Rigidbody> bodiesOnBelt = new();
        private BoxCollider triggerCollider;
        private BoxCollider supportCollider;
        private Material runtimeBeltMaterial;
        private Vector2 textureOffset;
        [SerializeField] private AudioSource runningAudioSource;
        private bool ownsRuntimeBeltMaterial;
        private bool warnedMissingAudioClip;

        public bool IsPowered
        {
            get { return addressBinding != null && addressBinding.HasController && addressBinding.IsAssigned && addressBinding.Value; }
        }

        public bool HasBinding => addressBinding != null && addressBinding.HasController && addressBinding.IsAssigned;
        public Vector3 WorldMoveDirection => transform.TransformDirection(localMoveDirection.normalized);
        public float SpeedMultiplier => speedMultiplier;

        public void Configure(Vector3 targetLocalMoveDirection, float targetBeltSpeed)
        {
            localMoveDirection = targetLocalMoveDirection == Vector3.zero ? Vector3.forward : targetLocalMoveDirection;
            beltSpeed = Mathf.Max(0f, targetBeltSpeed);
            EnsureSurfaceColliders();
        }

        public void SetSpeedMultiplier(float targetMultiplier)
        {
            speedMultiplier = Mathf.Max(0f, targetMultiplier);
        }

        public void ConfigureBinding(PlcAddressBinding targetAddressBinding)
        {
            addressBinding = targetAddressBinding;
            ValidateOutputBinding();
        }

        public void RebuildSurfaceFromRenderers()
        {
            EnsureSurfaceColliders();
        }

        private void Awake()
        {
            if (addressBinding == null)
                addressBinding = GetComponent<PlcAddressBinding>();

            ValidateOutputBinding();
            EnsureSurfaceColliders();
            EnsureBeltVisual();
            EnsureRunningAudio();
            RefreshRunningAudio();
        }

        private void Update()
        {
            TickBeltVisual(Time.deltaTime);
            RefreshRunningAudio();
        }

        private void FixedUpdate()
        {
            RemoveInvalidBodies();
            if (!IsPowered || beltSpeed <= 0f || acceleration <= 0f)
                return;

            var direction = WorldMoveDirection;
            if (direction == Vector3.zero)
                return;

            foreach (var body in bodiesOnBelt)
                PushBody(body, direction);
        }

        public void NotifyTriggerEnter(Collider other)
        {
            TryTrack(other);
        }

        public void NotifyTriggerStay(Collider other)
        {
            TryTrack(other);
        }

        public void NotifyTriggerExit(Collider other)
        {
            var body = other != null ? other.attachedRigidbody : null;
            if (body != null)
                bodiesOnBelt.Remove(body);
        }

        private void TryTrack(Collider other)
        {
            var body = other != null ? other.attachedRigidbody : null;
            if (body == null || body.isKinematic)
                return;

            bodiesOnBelt.Add(body);
        }

        private void PushBody(Rigidbody body, Vector3 direction)
        {
            if (body == null || body.isKinematic)
                return;

            var velocity = body.linearVelocity;
            var along = Vector3.Dot(velocity, direction);
            var targetSpeed = beltSpeed * speedMultiplier;
            var targetDelta = Mathf.Clamp(targetSpeed - along, -acceleration * Time.fixedDeltaTime, acceleration * Time.fixedDeltaTime);
            body.AddForce(direction * (targetDelta / Mathf.Max(Time.fixedDeltaTime, 0.0001f)), ForceMode.Acceleration);

            if (downwardGripAcceleration > 0f)
                body.AddForce(Vector3.down * downwardGripAcceleration, ForceMode.Acceleration);

            ClampHorizontalSpeed(body);
        }

        // 벨트 텍스처 오프셋 갱신
        private void TickBeltVisual(float deltaTime)
        {
            EnsureBeltVisual();
            if (runtimeBeltMaterial == null)
                return;

            if (IsPowered && beltSpeed > 0f)
                textureOffset += textureScrollPerSecond * (beltSpeed * speedMultiplier * deltaTime);

            SetTextureOffset(runtimeBeltMaterial, textureOffset);
        }

        // 구동음 재생 상태 갱신
        private void RefreshRunningAudio()
        {
            EnsureRunningAudio();
            if (runningAudioSource == null || runningAudioSource.clip == null)
                return;

            runningAudioSource.volume = runningSoundVolume;
            var shouldPlay = IsPowered && beltSpeed > 0f;
            if (shouldPlay && !runningAudioSource.isPlaying)
                runningAudioSource.Play();
            else if (!shouldPlay && runningAudioSource.isPlaying)
                runningAudioSource.Stop();
        }

        private void ClampHorizontalSpeed(Rigidbody body)
        {
            if (maxHorizontalSpeed <= 0f)
                return;

            var velocity = body.linearVelocity;
            var horizontal = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontal.sqrMagnitude <= maxHorizontalSpeed * maxHorizontalSpeed)
                return;

            var clamped = horizontal.normalized * maxHorizontalSpeed;
            body.linearVelocity = new Vector3(clamped.x, velocity.y, clamped.z);
        }

        private void RemoveInvalidBodies()
        {
            bodiesOnBelt.RemoveWhere(body => body == null || body.isKinematic);
        }

        private void EnsureSurfaceColliders()
        {
            var localBounds = CalculateLocalRendererBounds();
            triggerCollider = EnsureChildBoxCollider(SurfaceTriggerName, true);
            triggerCollider.center = new Vector3(localBounds.center.x, localBounds.max.y + triggerHeight * 0.5f, localBounds.center.z);
            triggerCollider.size = new Vector3(Mathf.Max(0.1f, localBounds.size.x), triggerHeight, Mathf.Max(0.1f, localBounds.size.z));

            if (!createSupportCollider)
                return;

            supportCollider = EnsureChildBoxCollider(SupportColliderName, false);
            supportCollider.center = new Vector3(localBounds.center.x, localBounds.max.y - supportHeight * 0.5f, localBounds.center.z);
            supportCollider.size = new Vector3(Mathf.Max(0.1f, localBounds.size.x), supportHeight, Mathf.Max(0.1f, localBounds.size.z));
        }

        private BoxCollider EnsureChildBoxCollider(string childName, bool isTrigger)
        {
            var child = transform.Find(childName);
            if (child == null)
            {
                var created = new GameObject(childName);
                created.transform.SetParent(transform, false);
                child = created.transform;
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            var box = child.GetComponent<BoxCollider>();
            if (box == null)
                box = child.gameObject.AddComponent<BoxCollider>();

            box.isTrigger = isTrigger;
            if (isTrigger)
            {
                var relay = child.GetComponent<PhysicsConveyorSurfaceRelay>();
                if (relay == null)
                    relay = child.gameObject.AddComponent<PhysicsConveyorSurfaceRelay>();

                relay.Configure(this);
            }

            return box;
        }

        // 벨트 렌더러와 런타임 머티리얼 준비
        private void EnsureBeltVisual()
        {
            if (beltRenderer == null)
                beltRenderer = FindBeltRenderer();

            if (runtimeBeltMaterial != null || beltRenderer == null)
                return;

            var source = Application.isPlaying ? beltRenderer.material : beltRenderer.sharedMaterial;
            if (source == null)
                return;

            runtimeBeltMaterial = new Material(source);
            ownsRuntimeBeltMaterial = true;

            if (Application.isPlaying)
                beltRenderer.material = runtimeBeltMaterial;
            else
                beltRenderer.sharedMaterial = runtimeBeltMaterial;
        }

        // 구동음 오디오 소스 준비
        private void EnsureRunningAudio()
        {
            if (runningAudioSource == null)
            {
                var existingSource = GetComponent<AudioSource>();
                runningAudioSource = existingSource != null ? existingSource : gameObject.AddComponent<AudioSource>();
            }

            ConfigureRunningAudioSource(true);
        }

        // 벨트 사운드 AudioSource 기본값을 적용한다.
        private void ConfigureRunningAudioSource(bool warnIfClipMissing)
        {
            if (runningAudioSource == null)
                return;

            runningAudioSource.playOnAwake = false;
            runningAudioSource.loop = true;
            runningAudioSource.spatialBlend = 1f;
            runningAudioSource.rolloffMode = AudioRolloffMode.Linear;
            runningAudioSource.minDistance = 0.4f;
            runningAudioSource.maxDistance = 6f;

            if (runningAudioSource.clip == null && runningAudioClip != null)
                runningAudioSource.clip = runningAudioClip;

            if (warnIfClipMissing && runningAudioSource.clip == null && !warnedMissingAudioClip)
            {
                Debug.LogWarning($"[LadderLab] '{name}' needs an AudioClip or AudioSource.clip for conveyor running sound.");
                warnedMissingAudioClip = true;
            }
        }

        private Bounds CalculateLocalRendererBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 2f));

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.transform == transform.Find(SurfaceTriggerName) || renderer.transform == transform.Find(SupportColliderName))
                    continue;

                foreach (var corner in GetWorldBoundsCorners(renderer.bounds))
                {
                    var local = transform.InverseTransformPoint(corner);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            if (!hasBounds)
                bounds = new Bounds(Vector3.zero, new Vector3(1f, 0.1f, 2f));

            return bounds;
        }

        private Renderer FindBeltRenderer()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            Renderer fallback = null;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.transform == transform.Find(SurfaceTriggerName) || renderer.transform == transform.Find(SupportColliderName))
                    continue;

                fallback ??= renderer;
                var material = renderer.sharedMaterial;
                var materialName = material != null ? material.name : "";
                if (materialName.IndexOf("belt", StringComparison.OrdinalIgnoreCase) >= 0)
                    return renderer;
            }

            return fallback;
        }

        private static IEnumerable<Vector3> GetWorldBoundsCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            yield return new Vector3(min.x, min.y, min.z);
            yield return new Vector3(min.x, min.y, max.z);
            yield return new Vector3(min.x, max.y, min.z);
            yield return new Vector3(min.x, max.y, max.z);
            yield return new Vector3(max.x, min.y, min.z);
            yield return new Vector3(max.x, min.y, max.z);
            yield return new Vector3(max.x, max.y, min.z);
            yield return new Vector3(max.x, max.y, max.z);
        }

        private void OnValidate()
        {
            if (localMoveDirection == Vector3.zero)
                localMoveDirection = Vector3.forward;

            beltSpeed = Mathf.Max(0f, beltSpeed);
            speedMultiplier = Mathf.Max(0f, speedMultiplier);
            acceleration = Mathf.Max(0f, acceleration);
            triggerHeight = Mathf.Max(0.02f, triggerHeight);
            supportHeight = Mathf.Max(0.01f, supportHeight);
            runningSoundVolume = Mathf.Clamp01(runningSoundVolume);
            textureScrollPerSecond.y = 0f;

            EnsureEditorAudioSource();
        }

        // 에디터에서 벨트 사운드용 AudioSource를 유지한다.
        private void Reset()
        {
            EnsureEditorAudioSource();
        }

        // 에디터에서 사운드 소스 기본값을 준비한다.
        private void EnsureEditorAudioSource()
        {
            if (Application.isPlaying)
                return;

            if (runningAudioSource == null)
                runningAudioSource = GetComponent<AudioSource>();

#if UNITY_EDITOR
            if (runningAudioSource == null && gameObject != null)
            {
                EditorApplication.delayCall += EnsureEditorAudioSourceDelayed;
                return;
            }
#endif

            if (runningAudioSource == null)
                return;

            ConfigureRunningAudioSource(false);
        }

#if UNITY_EDITOR
        // 에디터 검증 이후 AudioSource를 안전하게 추가한다.
        private void EnsureEditorAudioSourceDelayed()
        {
            if (this == null || Application.isPlaying)
                return;

            if (runningAudioSource == null)
                runningAudioSource = GetComponent<AudioSource>();

            if (runningAudioSource == null)
                runningAudioSource = gameObject.AddComponent<AudioSource>();

            ConfigureRunningAudioSource(false);
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(runningAudioSource);
        }
#endif

        private void ValidateOutputBinding()
        {
            var targetAddress = addressBinding != null ? addressBinding.Address : "";
            if (string.IsNullOrWhiteSpace(targetAddress))
                return;

            if (!targetAddress.Trim().StartsWith("Y", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{nameof(PhysicsConveyorSurface)} can only read PLC Y output addresses.");
        }

        private void OnDestroy()
        {
            if (ownsRuntimeBeltMaterial && runtimeBeltMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(runtimeBeltMaterial);
                else
                    DestroyImmediate(runtimeBeltMaterial);
            }

        }

        private static void SetTextureOffset(Material material, Vector2 offset)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseMap"))
                material.SetTextureOffset("_BaseMap", offset);

            if (material.HasProperty("_MainTex"))
                material.SetTextureOffset("_MainTex", offset);
        }

    }

}
