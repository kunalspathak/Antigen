using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
        // Optional parent scope.
        private Scope parent = null;
        public readonly ScopeKind ScopeType;
        public TestCase TestCase;

        // Combines local variables, properties of members, and array properties.  This is the
        // only list from which GetRandom*Variable will draw -- the other lists are only to track
        // variables so that the TestCase can initialize them and print them.
        private Dictionary<ValueType, List<string>> ListOfVariables = new Dictionary<ValueType, List<string>>();

        // List of local variables in current scope. 
        private Dictionary<ValueType, List<string>> LocalVariables = new Dictionary<ValueType, List<string>>();

        public List<string> AllVariables => LocalVariables.SelectMany(dict => dict.Value).ToList();

        // List of string vars in the current scope.
        private List<string> LocalStringVariables = new List<string>();

        #region Contructors
        public Scope(TestCase tc)
        {
            TestCase = tc;
            ScopeType = ScopeKind.FunctionScope;
        }

        public Scope(TestCase tc, ScopeKind t, Scope parentScope)
        {
            TestCase = tc;
            ScopeType = t;
            parent = parentScope;
        }

        #endregion

        #region Get variables from the scope
        public string GetRandomVariable(ValueType variableType)
        {
            List<string> allUsableVariables = GetUsableVariables(variableType);
            return allUsableVariables[PRNG.Next(allUsableVariables.Count)];
        }
        #endregion

        #region Gets From Scope
        public int GetVariablesCount()
        {
            return ListOfVariables.Count;
        }
        #endregion

        #region Add variables to scope
        public void AddLocal(ValueType variableType, string variableName)
        {
#if DEBUG
            foreach (var valueType in LocalVariables.Keys)
            {
                Debug.Assert(!LocalVariables[valueType].Contains(variableName));
            }

            foreach (var valueType in ListOfVariables.Keys)
            {
                Debug.Assert(!ListOfVariables[valueType].Contains(variableName));
            }
#endif
            // Add to local variables
            if (!LocalVariables.ContainsKey(variableType))
            {
                LocalVariables[variableType] = new List<string>();
            }

            LocalVariables[variableType].Add(variableName);

            // Add to variables
            if (!ListOfVariables.ContainsKey(variableType))
            {
                ListOfVariables[variableType] = new List<string>();
            }

            ListOfVariables[variableType].Add(variableName);
        }
        #endregion

        #region Aggregate Scopes methods
        /// <summary>
        /// Combines the list of Variables from all parent scopes.  Respects strict mode restrictions
        /// </summary>
        /// <returns></returns>
        ///  
        private List<string> GetUsableVariables(ValueType variableType)
        {
            List<string> variables = new List<string>();

            Scope curr = this;

            while (curr != null)
            {
                if (curr.ListOfVariables.ContainsKey(variableType))
                {
                    variables.AddRange(curr.ListOfVariables[variableType]);
                }
                curr = curr.parent;
            }
            return variables;
        }
        #endregion
    }
}
