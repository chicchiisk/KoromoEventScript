using UnityEngine;
using UnityEngine.UI;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Choice Item View")]
public sealed class KesChoiceItemView : MonoBehaviour
{
    [SerializeField]
    private Image selectionIcon;

    [SerializeField]
    private Text label;

    public string Label => label == null ? string.Empty : label.text;

    public bool IsSelected => selectionIcon != null && selectionIcon.enabled;

    public Image SelectionIcon => selectionIcon;

    public void SetReferences(Image newSelectionIcon, Text newLabel)
    {
        selectionIcon = newSelectionIcon;
        label = newLabel;
    }

    public void SetContent(string text, bool selected)
    {
        if (label != null)
        {
            label.text = text ?? string.Empty;
        }

        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectionIcon != null)
        {
            selectionIcon.enabled = selected;
        }
    }
}

}
