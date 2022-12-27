namespace Latte.Models.Intermediate;

public class RegisterTerm : Term
{
    public RegisterTerm(string name, string identifier = null, bool isParam = false)
    {
        Name = name;
        Identifier = identifier;
        IsParam = isParam;
    }

    public string Name { get; set; }
    
    public string Identifier { get; set; }
    
    public bool IsParam { get; set; }

    public override string ToString()
    {
        return Name;
    }
}
