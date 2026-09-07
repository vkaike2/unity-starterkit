using UnityEditor;
using UnityEngine;

namespace Vkaike2.StarterKit.Editor.Inspectors
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class MonoBehaviourButtonInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            InspectorButtons.Draw(this);
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true)]
    public class ScriptableObjectButtonInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            InspectorButtons.Draw(this);
        }
    }
}
