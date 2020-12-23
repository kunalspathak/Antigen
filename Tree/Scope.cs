namespace Antigen.Tree
{
    /*
     * Scopes need information about their "kind" - which construct pushed a new scope
     * This information is used in parts of ExprGen that need to do backtracking - ex.
     * the logic behind generating new label names
     */
    public enum ScopeKind
    {
        ConditionalScope,               //introduced by any "conditional" construct - try/catch, loops, if, switch
        LoopScope,                      //introduced by any  loops
        GetterSetterScope,              //introduced by getters, setters and other ImplicitCallKind
        FunctionScope,                  //default, scope introduced by a new function
        BracesScope                     //introduced by { }
    }

    /// <summary>
    /// Represents a scope
    /// </summary>
    public class Scope
    {
    }
}
