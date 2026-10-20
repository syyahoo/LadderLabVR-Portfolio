using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LadderLab.UI
{
    [DisallowMultipleComponent]
    public sealed class LadderScreenParallelBranchOverlay : MonoBehaviour
    {
        private const string LayerName = "Parallel Branch Layer";
        private static readonly float[] RungY = { 0.73f, 0.50f, 0.27f };
        private static readonly float[] ContactX = { 0.24f, 0.42f, 0.60f };

        [SerializeField] private Color wireColor = new Color(0.46f, 0.69f, 0.66f, 1f);
        [SerializeField] private Color slotColor = new Color(0.04f, 0.085f, 0.095f, 0.88f);

        private void OnEnable()
        {
        }

        private void OnValidate()
        {
        }

        public void Rebuild()
        {
            if (!(transform is RectTransform))
                return;

            var existing = transform.Find(LayerName);
            if (existing != null)
            {
                if (Application.isPlaying)
                    Destroy(existing.gameObject);
                else
                    DestroyImmediate(existing.gameObject);
            }

            var layer = CreatePanel(transform, LayerName, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0f));
            layer.GetComponent<Image>().raycastTarget = false;

            for (var rung = 0; rung < RungY.Length; rung++)
                CreateParallelBranch(layer.transform, rung, RungY[rung]);
        }

        private void CreateParallelBranch(Transform parent, int rung, float mainY)
        {
            var branchY = mainY - 0.092f;
            var branchMinX = 0.18f;
            var branchMaxX = 0.68f;

            CreateVerticalLine(parent, $"R{rung + 1} Branch Left Drop", new Vector2(branchMinX, branchY), new Vector2(branchMinX, mainY), wireColor, 4f);
            CreateHorizontalLine(parent, $"R{rung + 1} Branch Wire", new Vector2(branchMinX, branchY), new Vector2(branchMaxX, branchY), wireColor, 4f);
            CreateVerticalLine(parent, $"R{rung + 1} Branch Right Rise", new Vector2(branchMaxX, branchY), new Vector2(branchMaxX, mainY), wireColor, 4f);

            CreateText(
                parent,
                $"R{rung + 1} Branch Label",
                "PARALLEL",
                new Vector2(0.095f, branchY - 0.025f),
                new Vector2(0.17f, branchY + 0.025f),
                13f,
                TextAlignmentOptions.Left,
                new Color(0.52f, 0.73f, 0.70f, 1f));

            for (var index = 0; index < ContactX.Length; index++)
            {
                var x = ContactX[index];
                CreateDiagramSlot(
                    parent,
                    $"R{rung + 1} Branch Slot {index + 1}",
                    new Vector2(x - 0.045f, branchY - 0.043f),
                    new Vector2(x + 0.045f, branchY + 0.043f),
                    "BRANCH");
            }
        }

        private void CreateDiagramSlot(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string placeholder)
        {
            var slot = CreatePanel(parent, name, anchorMin, anchorMax, slotColor);
            var background = slot.GetComponent<Image>();
            var label = CreateText(slot.transform, "Label", "", new Vector2(0f, 0.40f), Vector2.one, 20f, TextAlignmentOptions.Center, Color.white);
            var addressLabel = CreateText(slot.transform, "Address", "", new Vector2(0f, 0f), new Vector2(1f, 0.43f), 12f, TextAlignmentOptions.Center, new Color(0.89f, 0.98f, 0.92f, 1f));
            CreateText(slot.transform, "Placeholder", placeholder, new Vector2(0f, -0.38f), new Vector2(1f, -0.08f), 11f, TextAlignmentOptions.Center, new Color(0.38f, 0.57f, 0.56f, 1f));
            slot.gameObject.AddComponent<LadderScreenDiagramSlot>().Configure(label, addressLabel, background);
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private static TMP_Text CreateText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Max(8f, fontSize * 0.58f);
            tmp.fontSizeMax = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return tmp;
        }

        private static void CreateHorizontalLine(Transform parent, string name, Vector2 startAnchor, Vector2 endAnchor, Color color, float thickness)
        {
            var line = CreatePanel(parent, name, new Vector2(startAnchor.x, startAnchor.y), new Vector2(endAnchor.x, endAnchor.y), color);
            line.GetComponent<Image>().raycastTarget = false;
            line.offsetMin = new Vector2(0f, -thickness * 0.5f);
            line.offsetMax = new Vector2(0f, thickness * 0.5f);
        }

        private static void CreateVerticalLine(Transform parent, string name, Vector2 startAnchor, Vector2 endAnchor, Color color, float thickness)
        {
            var line = CreatePanel(parent, name, new Vector2(startAnchor.x, startAnchor.y), new Vector2(endAnchor.x, endAnchor.y), color);
            line.GetComponent<Image>().raycastTarget = false;
            line.offsetMin = new Vector2(-thickness * 0.5f, 0f);
            line.offsetMax = new Vector2(thickness * 0.5f, 0f);
        }
    }
}
