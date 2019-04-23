using System;
using System.Reflection;
using UnityEditor;

namespace UnityEditor.Experimental.U2D.PSD.Tests
{
    [InitializeOnLoad]
    static class DisableBurst
    {
        static DisableBurst()
        {
            TurnItOff();
        }

        static void TurnItOff()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type.Name == "BurstEditorOptions")
                    {
                        var property = type.GetProperty("EnableBurstCompilation", BindingFlags.Static | BindingFlags.Public);
                        if (property != null)
                            property.SetValue(null, false);
                        return;
                    }
                }
            }
        }
    }
}
