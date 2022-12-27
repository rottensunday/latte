namespace Latte.Models.Intermediate;

public class LabelTerm : Term
{
    public LabelTerm(string label)
    {
        Label = label;
    }

    public string Label { get; set; }
}
