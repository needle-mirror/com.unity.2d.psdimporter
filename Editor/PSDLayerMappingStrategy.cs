using System.Collections;
using System.Collections.Generic;
using PDNWrapper;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    internal interface IPSDLayerMappingStrategy
    {
        bool Compare(PSDLayer a, PSDLayer b);
        bool Compare(PSDImporter.FlattenLayerData a, BitmapLayer b);
        string LayersUnique(IEnumerable<PSDLayer> layers);
    }
    
    internal abstract class LayerMappingStrategy<T> : IPSDLayerMappingStrategy
    {
        string m_DuplicatedStringError = L10n.Tr("The following layers have duplicated identifier.");
        protected abstract T GetID(PSDLayer layer);
        protected abstract T GetID(BitmapLayer layer);
        protected abstract T GetID(PSDImporter.FlattenLayerData layer);

        protected virtual bool IsGroup(PSDLayer layer)
        {
            return layer.isGroup;
        }
        
        protected virtual bool IsGroup(BitmapLayer layer)
        {
            return layer.IsGroup;
        }
        
        protected virtual bool IsGroup(PSDImporter.FlattenLayerData layer)
        {
            return true;
        }
        
        public bool Compare(PSDImporter.FlattenLayerData x, BitmapLayer y)
        {
            return Comparer<T>.Default.Compare(GetID(x), GetID(y)) == 0 && IsGroup(x) == IsGroup(y);
        }
        
        public bool Compare(PSDLayer x, PSDLayer y)
        {
            return Comparer<T>.Default.Compare(GetID(x), GetID(y)) == 0 && IsGroup(x) == IsGroup(y);
        }

        public string LayersUnique(IEnumerable<PSDLayer> layers)
        {
            var layerNameHash = new HashSet<T>();
            var layerGroupHash = new HashSet<T>();
            return LayersUnique(layers, layerNameHash, layerGroupHash);
        }

        string LayersUnique(IEnumerable<PSDLayer> layers, HashSet<T> layerNameHash, HashSet<T> layerGroupHash)
        {
            List<string> duplicateLayerName = new List<string>();
            string duplicatedStringError = null;
            foreach (var layer in layers)
            {
                var id = GetID(layer);
                var hash = layer.isGroup ? layerGroupHash : layerNameHash;
                if (hash.Contains(id))
                    duplicateLayerName.Add(layer.name);
                else
                    hash.Add(id);
            }

            if (duplicateLayerName.Count > 0)
            {
                duplicatedStringError = m_DuplicatedStringError + "\n";
                duplicatedStringError += string.Join(", ", duplicateLayerName);
            }
            return duplicatedStringError;
        }
    }
    
    internal class LayerMappingUseLayerName : LayerMappingStrategy<string>
    {
        protected override string GetID(PSDLayer x)
        {
            return x.name.ToLower();
        }
        
        protected override string GetID(BitmapLayer x)
        {
            return x.Name.ToLower();
        }
        
        protected override string GetID(PSDImporter.FlattenLayerData x)
        {
            return x.name.ToLower();
        }
    }

    internal class LayerMappingUseLayerNameCaseSensitive : LayerMappingStrategy<string>
    {
        protected override string GetID(PSDLayer x)
        {
            return x.name;
        }
        
        protected override string GetID(BitmapLayer x)
        {
            return x.Name;
        }
        
        protected override string GetID(PSDImporter.FlattenLayerData x)
        {
            return x.name;
        }
    }

    internal class LayerMappingUserLayerID : LayerMappingStrategy<int>
    {
        protected override int GetID(PSDLayer x)
        {
            return x.layerID;
        }
        
        protected override int GetID(BitmapLayer x)
        {
            return x.LayerID;
        }
        
        protected override int GetID(PSDImporter.FlattenLayerData x)
        {
            return x.layerId;
        }
    }
}

