using TMPro;
using UnityEngine;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    public sealed class PlcAddressIndicator : MonoBehaviour
    {
        [SerializeField] private PlcAddressBinding binding;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Light targetLight;
        [SerializeField] private TMP_Text targetLabel;
        [SerializeField] private bool createWorldLamp = true;
        [SerializeField] private Vector3 lampLocalPosition = new(0f, 0.46f, 0.06f);
        [SerializeField] private Vector3 labelLocalPosition = new(0f, 0.55f, 0.06f);
        [SerializeField] private float labelScale = 0.016f;
        [SerializeField] private Color offColor = new(0.11f, 0.11f, 0.1f, 1f);
        [SerializeField] private Color onColor = new(1f, 0.76f, 0.16f, 1f);
        [SerializeField, Min(0f)] private float onIntensity = 2.2f;

        private MaterialPropertyBlock propertyBlock;

        public bool IsPowered => binding != null && binding.Value;

        public void Configure(PlcAddressBinding targetBinding)
        {
            binding = targetBinding;
            Refresh();
        }

        private void Awake()
        {
            EnsureReferences();
            EnsureWorldLamp();
            Refresh();
        }

        private void OnEnable()
        {
            if (binding != null)
                binding.AddressChanged += Refresh;
        }

        private void OnDisable()
        {
            if (binding != null)
                binding.AddressChanged -= Refresh;
        }

        private void Update()
        {
            Refresh();
        }

        private void EnsureReferences()
        {
            if (binding == null)
                binding = GetComponent<PlcAddressBinding>();
        }

        private void EnsureWorldLamp()
        {
            if (!createWorldLamp)
                return;

            if (targetRenderer == null)
            {
                var lampObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lampObject.name = "PLC Address Indicator Lamp";
                lampObject.transform.SetParent(transform, false);
                lampObject.transform.localPosition = lampLocalPosition;
                lampObject.transform.localScale = new Vector3(0.22f, 0.22f, 0.08f);
                targetRenderer = lampObject.GetComponent<Renderer>();
                targetLight = lampObject.AddComponent<Light>();
                targetLight.type = LightType.Point;
                targetLight.range = 0.45f;
            }

            if (targetLabel == null)
            {
                var labelObject = new GameObject("PLC Address Indicator Label", typeof(RectTransform), typeof(TextMeshPro));
                labelObject.transform.SetParent(transform, false);
                labelObject.transform.localPosition = labelLocalPosition;
                labelObject.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
                labelObject.transform.localScale = Vector3.one * labelScale;
                targetLabel = labelObject.GetComponent<TextMeshPro>();
                targetLabel.alignment = TextAlignmentOptions.Center;
                targetLabel.fontSize = 3f;
                targetLabel.color = Color.white;
                targetLabel.textWrappingMode = TextWrappingModes.NoWrap;
            }

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }

        private void Refresh()
        {
            EnsureReferences();
            EnsureWorldLamp();

            var powered = IsPowered;
            SetRendererColor(powered ? onColor : offColor, powered ? onColor * 1.8f : Color.black);

            if (targetLight != null)
            {
                targetLight.enabled = powered;
                targetLight.color = onColor;
                targetLight.intensity = powered ? onIntensity : 0f;
            }

            if (targetLabel != null)
                targetLabel.text = binding == null ? "-" : binding.Address;
        }

        private void SetRendererColor(Color color, Color emissionColor)
        {
            if (targetRenderer == null)
                return;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_EmissionColor", emissionColor);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
