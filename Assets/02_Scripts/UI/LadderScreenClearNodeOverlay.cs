using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LadderLab.UI
{
    [DisallowMultipleComponent]
    public sealed class LadderScreenClearNodeOverlay : MonoBehaviour
    {
        private const string ClearNodeName = "Node CLR";
        private const string ParallelNodeName = "Node PAR";

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureClearNode();
        }

        [ContextMenu("Ensure Clear Nodes")]
        public void EnsureClearNode()
        {
            if (this == null || !isActiveAndEnabled || transform is not RectTransform)
                return;

            if (transform.Find(ParallelNodeName) == null)
                CreateNode(ParallelNodeName, "PAR", LadderScreenNodeKind.ParallelBranch, new Vector2(0.14f, 0.20f), new Vector2(0.86f, 0.30f));

            if (transform.Find(ClearNodeName) == null)
                CreateNode(ClearNodeName, "CLR", LadderScreenNodeKind.Clear, new Vector2(0.14f, 0.08f), new Vector2(0.86f, 0.18f));

            SetNodeAnchors("Node NO", new Vector2(0.14f, 0.66f), new Vector2(0.86f, 0.78f));
            SetNodeAnchors("Node NC", new Vector2(0.14f, 0.51f), new Vector2(0.86f, 0.63f));
            SetNodeAnchors("Node COIL", new Vector2(0.14f, 0.36f), new Vector2(0.86f, 0.48f));
            SetNodeAnchors(ParallelNodeName, new Vector2(0.14f, 0.21f), new Vector2(0.86f, 0.33f));
            SetNodeAnchors(ClearNodeName, new Vector2(0.14f, 0.08f), new Vector2(0.86f, 0.18f));
        }

        private void CreateNode(string name, string label, LadderScreenNodeKind kind, Vector2 anchorMin, Vector2 anchorMax)
        {
            var node = CreatePanel(transform, name, anchorMin, anchorMax, LadderScreenNodeDragItem.ResolveColor(kind));
            node.gameObject.AddComponent<LadderScreenNodeDragItem>().Configure(kind, label);
            CreateText(node.transform, "Label", label, Vector2.zero, Vector2.one, 38f, TextAlignmentOptions.Center, Color.white);
        }

        private void SetNodeAnchors(string nodeName, Vector2 anchorMin, Vector2 anchorMax)
        {
            var node = transform.Find(nodeName) as RectTransform;
            if (node == null)
                return;

            node.anchorMin = anchorMin;
            node.anchorMax = anchorMax;
            node.offsetMin = Vector2.zero;
            node.offsetMax = Vector2.zero;
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
            tmp.fontSizeMin = Mathf.Max(12f, fontSize * 0.58f);
            tmp.fontSizeMax = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            return tmp;
        }
    }
}
