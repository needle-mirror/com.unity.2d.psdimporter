using System;
using System.Collections.Generic;
using System.Linq;
using PDNWrapper;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    internal static class PSDImporterImportProcess
    {
        public static void BuildLayerToExtract(IEnumerable<BitmapLayer> layers,
            IImportConfigProvider importData,
            out PSDExtractLayerData[] extractData)
        {
            // Layers sharing an identifier, e.g. duplicated layer names, must pair one to one with the
            // import settings and the previous layer data so that each layer keeps its own Sprite ID.
            BuildLayerToExtract(layers, importData, out extractData, new HashSet<IPSDLayerImportSettingRecord>(), new HashSet<PSDLayerData>());
        }

        static void BuildLayerToExtract(IEnumerable<BitmapLayer> layers,
            IImportConfigProvider importData,
            out PSDExtractLayerData[] extractData,
            HashSet<IPSDLayerImportSettingRecord> mappedImportSettings,
            HashSet<PSDLayerData> mappedPsdLayerData)
        {
            IReadOnlyList<IPSDLayerImportSettingRecord> importSettings = importData.layerImportSettings;
            bool importHiddenLayers = importData.importHiddenLayers;
            extractData = new PSDExtractLayerData[layers.Count()];
            IPSDLayerMappingStrategy mappingStrategy = ImportUtilities.GetLayerMappingStrategy(importData.layerMappingOption);
            PSDLayerImportSetting importSetting = null;
            for (int i = 0; i < layers.Count(); ++i)
            {
                BitmapLayer bitmapLayer = layers.ElementAt(i);
                importSetting = null;
                IPSDLayerImportSettingRecord importSettingRecord = null;
                if (importSettings != null)
                {
                    int index = importSettings.FindIndex(x => !mappedImportSettings.Contains(x) && mappingStrategy.Compare(x, bitmapLayer));
                    if (index >= 0)
                    {
                        importSettingRecord = importSettings[index];
                        mappedImportSettings.Add(importSettingRecord);
                    }
                }

                if (importSettingRecord != null)
                {
                    importSetting = new PSDLayerImportSetting(importSettingRecord);
                }
                else
                {
                    importSetting = new PSDLayerImportSetting()
                    {
                        flatten = false,
                        importLayer = bitmapLayer.ShouldImport(importHiddenLayers),
                    };
                }

                GUID spriteId = default;
                int psdLayerDataIndex = importData.psdLayerData.FindIndex(x => !mappedPsdLayerData.Contains(x) && mappingStrategy.Compare(x, bitmapLayer));
                if (psdLayerDataIndex >= 0)
                {
                    //Debug.LogWarning($"Unable to find matching layer import setting for layer {bitmapLayer.Name} with id {bitmapLayer.LayerID}. This layer will be ignored during import. Please check your PSD Layer Import Settings.", null);
                    PSDLayerData psdLayerData = importData.psdLayerData[psdLayerDataIndex];
                    mappedPsdLayerData.Add(psdLayerData);
                    spriteId = psdLayerData.spriteID;
                }

                extractData[i] = new PSDExtractLayerData()
                {
                    bitmapLayer = bitmapLayer,
                    importSetting = importSetting,
                    spriteId = spriteId,
                };

                if (bitmapLayer.ChildLayer != null)
                {
                    BuildLayerToExtract(bitmapLayer.ChildLayer, importData, out extractData[i].children, mappedImportSettings, mappedPsdLayerData);
                }
            }
        }

        static public int FindIndex<T>(this IReadOnlyList<T> list, Func<T, bool> comparer)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                if (comparer(list[i]))
                    return i;
            }

            return -1;
        }
    }
}
