using UnityEngine;
using Object = UnityEngine.Object;

namespace Vkaike2.StarterKit.Base.Abstracts
{
    public abstract class ValidatableFields
    {
        protected Object Context { get; private set; }

        public void ValidateFields(Object context)
        {
            Context = context;

            Validate();
        }

        protected virtual void Validate()
        {
        }

        protected void ValidateNull(Object value, string fieldName)
        {
            if (value != null) return;

            var owner = GetType().DeclaringType?.Name ?? GetType().Name;
            var contextName = Context != null ? Context.name : "unknown";

            Debug.LogError(
                $"[{owner}] '{contextName}' has no {fieldName} assigned in {GetType().Name}.",
                Context);
        }
    }
}
