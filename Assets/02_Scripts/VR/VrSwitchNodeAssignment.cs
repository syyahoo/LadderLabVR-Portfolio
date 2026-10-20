using System;
using LadderLab.PLC;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace LadderLab.VR
{
    [DisallowMultipleComponent]
    public sealed class VrSwitchNodeAssignment : MonoBehaviour
    {
        [SerializeField] private VrButtonInput buttonInput;
        [SerializeField] private VrButtonVisualFeedback visualFeedback;
        [SerializeField] private string[] assignableInputAddresses = { "X0", "X1", "X2", "X3" };
        [SerializeField] private Vector3 labelLocalPosition = new(0f, 0.34f, 0.04f);
        [SerializeField] private Vector3 assignButtonLocalPosition = new(0f, 0.24f, 0.04f);
        [SerializeField] private float labelScale = 0.018f;
        [SerializeField] private Color assignButtonColor = new(0.14f, 0.28f, 0.42f, 1f);

        private TMP_Text assignmentText;
        private XRSimpleInteractable assignInteractable;

        public string AssignedInputAddress => buttonInput != null ? buttonInput.InputAddress : "";

        public void CycleAssignment()
        {
            EnsureReferences();
            if (buttonInput == null || assignableInputAddresses == null || assignableInputAddresses.Length == 0)
                return;

            var currentIndex = Math.Max(0, Array.IndexOf(assignableInputAddresses, buttonInput.InputAddress));
            var nextAddress = assignableInputAddresses[(currentIndex + 1) % assignableInputAddresses.Length];
            AssignInputNode(nextAddress);
        }

        public void AssignInputNode(string inputAddress)
        {
            ValidateInputAddress(inputAddress);
            EnsureReferences();
            buttonInput?.SetInputAddress(inputAddress);
            visualFeedback?.SetInputAddress(inputAddress);
            RefreshLabel();
        }

        private void Awake()
        {
            EnsureReferences();
            EnsureWorldUi();
            RefreshLabel();
        }

        private void OnEnable()
        {
            EnsureWorldUi();
            if (assignInteractable != null)
                assignInteractable.selectEntered.AddListener(HandleAssignSelected);
        }

        private void OnDisable()
        {
            if (assignInteractable != null)
                assignInteractable.selectEntered.RemoveListener(HandleAssignSelected);
        }

        private void Update()
        {
            RefreshLabel();
        }

        private void HandleAssignSelected(SelectEnterEventArgs eventArgs)
        {
            CycleAssignment();
        }

        private void EnsureReferences()
        {
            if (buttonInput == null)
                buttonInput = GetComponent<VrButtonInput>();

            if (visualFeedback == null)
                visualFeedback = GetComponent<VrButtonVisualFeedback>();
        }

        private void EnsureWorldUi()
        {
            if (assignmentText == null)
            {
                var labelObject = new GameObject("Switch Assignment Label", typeof(RectTransform), typeof(TextMeshPro));
                labelObject.transform.SetParent(transform, false);
                labelObject.transform.localPosition = labelLocalPosition;
                labelObject.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
                labelObject.transform.localScale = Vector3.one * labelScale;
                assignmentText = labelObject.GetComponent<TextMeshPro>();
                assignmentText.alignment = TextAlignmentOptions.Center;
                assignmentText.fontSize = 3.4f;
                assignmentText.color = Color.white;
                assignmentText.textWrappingMode = TextWrappingModes.NoWrap;
            }

            if (assignInteractable != null)
                return;

            var assignObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            assignObject.name = "Switch Assignment Button";
            assignObject.transform.SetParent(transform, false);
            assignObject.transform.localPosition = assignButtonLocalPosition;
            assignObject.transform.localRotation = Quaternion.identity;
            assignObject.transform.localScale = new Vector3(0.62f, 0.16f, 0.28f);
            var renderer = assignObject.GetComponent<Renderer>();
            if (renderer != null)
                SetRendererColor(renderer, assignButtonColor);

            assignInteractable = assignObject.AddComponent<XRSimpleInteractable>();

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshPro));
            textObject.transform.SetParent(assignObject.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            textObject.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            textObject.transform.localScale = Vector3.one * 0.08f;
            var text = textObject.GetComponent<TextMeshPro>();
            text.text = "ASSIGN";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 2.2f;
            text.color = Color.white;
        }

        private void RefreshLabel()
        {
            if (assignmentText == null)
                return;

            var inputAddress = AssignedInputAddress;
            assignmentText.text = string.IsNullOrWhiteSpace(inputAddress)
                ? "Input: -"
                : $"{inputAddress}\n{AddressLabel(inputAddress)}";
        }

        private static void ValidateInputAddress(string inputAddress)
        {
            var address = PlcAddress.Parse(inputAddress);
            if (address.Area != PlcMemoryArea.Input)
                throw new ArgumentException("Switch assignment can only target PLC X input nodes.", nameof(inputAddress));
        }

        private static string AddressLabel(string address)
        {
            return address switch
            {
                "X0" => "START",
                "X1" => "STOP",
                "X2" => "SENSOR",
                "X3" => "EMERGENCY",
                _ => "INPUT"
            };
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
