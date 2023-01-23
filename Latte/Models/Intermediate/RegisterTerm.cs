namespace Latte.Models.Intermediate;

using Compiler;
using Scopes;

public class RegisterTerm : Term
{
    public RegisterTerm(
        string name,
        string type,
        string identifier = null,
        bool isParam = false,
        IScope scope = null,
        FieldAccessTerm fat = null)
    {
        Name = name;
        Type = type;
        Identifier = identifier;
        IsParam = isParam;
        Scope = scope;
        FieldAccessTerm = fat;
    }

    public string Name { get; set; }

    public string Identifier { get; set; }

    public bool IsParam { get; set; }

    public string Type { get; set; }

    public IScope Scope { get; set; }

    public string VirtualRegister { get; set; }
    
    public Register PhysicalRegister { get; set; }
    
    public int NextUse { get; set; }
    
    public bool MemoryAddress { get; set; }
    
    public FieldAccessTerm FieldAccessTerm { get; set; }

    // public override string ToString() => string.IsNullOrEmpty(VirtualRegister) ? Name : VirtualRegister;
    public override string ToString()
    {
        return FieldAccessTerm != null ? FieldAccessTerm.ToString() : Name;
    }

    public override List<string> GetStringLiterals() => new();
    
    public override List<RegisterTerm> GetUsedRegisters()
    {
        var result = new List<RegisterTerm>() { this };

        if (FieldAccessTerm != null)
        {
            result.AddRange(FieldAccessTerm.GetUsedRegisters());
        }

        return result;
    }

    public override void SwitchRegisters(string used, string newRegister)
    {
        if (Name == used)
        {
            Name = newRegister;
        }
        
        FieldAccessTerm?.SwitchRegisters(used, newRegister);
    }
}
