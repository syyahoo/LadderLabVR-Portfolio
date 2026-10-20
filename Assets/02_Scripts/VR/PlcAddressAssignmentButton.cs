using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    public sealed class PlcAddressAssignmentButton : MonoBehaviour
    {
        [SerializeField] private PlcAddressBinding binding;
        [SerializeField] private Vector3 buttonLocalPosition = new(0f, 0.38f, 0.06f);
        [SerializeField] private Vector3 buttonLocalScale = new(0.58f, 0.14f, 0.24f);
        [SerializeField] private string label = "ASSIGN";
        [SerializeField] private Color buttonColor = new(0.25f, 0.28f, 0.34f, 1f);

        private XRSimpleInteractable interactable;
        private TMP_Text labelText;

        public void Configure(PlcAddressBinding targetBinding, string targetLabel)
        {
            binding = targetBinding;
            label = targetLabel;
            RefreshLabel();
        }

        private void Awake()
        {
            EnsureReferences();
            EnsureWorldButton();
            RefreshLabel();
        }

        private void OnEnable()
        {
            EnsureWorldButton();
            if (interactable != null)
                interactable.selectEntered.AddListener(HandleSelected);

            if (binding != null)
                binding.AddressChanged += RefreshLabel;
        }

        private void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(HandleSelected);

            if (binding != null)
                binding.AddressChanged -= RefreshLabel;
        }

        private void OnMouseDown()
        {
            Cycle();
        }

        private void HandleSelected(SelectEnterEventArgs eventArgs)
        {
            Cycle();
        }

        private void Cycle()
        {
            binding?.CycleAddress();
            RefreshLabel();
        }

        private void EnsureReferences()
        {
            if (binding == null)
                binding = GetComponent<PlcAddressBinding>();
        }

        private void EnsureWorldButton()
        {
            EnsureReferences();
            if (interactable != null)
                return;

            var buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            buttonObject.name = "PLC Address Assignment Button";
            buttonObject.transform.SetParent(transform, false);
            buttonObject.transform.localPosition = buttonLocalPosition;
            buttonObject.transform.localScale = buttonLocalScale;
            var renderer = buttonObject.GetComponent<Renderer>();
            if (renderer != null)
                SetRendererColor(renderer, buttonColor);

            interactable = buttonObject.AddComponent<XRSimpleInteractable>();

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshPro));
            textObject.transform.SetParent(buttonObject.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            textObject.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            textObject.transform.localScale = Vector3.one * 0.08f;
            labelText = textObject.GetComponent<TextMeshPro>();
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 2f;
            labelText.color = Color.white;
        }

        private void RefreshLabel()
        {
            if (labelText == null)
                return;

            labelText.text = binding == null ? label : $"{label}\n{binding.Address}";
        }

        private static void SetRendererColor(Renderer targetRenderer, Color color)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
