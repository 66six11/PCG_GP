using System;
using UnityEditor;

namespace RockTools
{
    public class LogicEditorBase : UnityEditor.Editor
    {
        public event Action OnPropertyChanged;

        protected void PropertyChanged()
        {
            OnPropertyChanged?.Invoke();
        }
    }
}
