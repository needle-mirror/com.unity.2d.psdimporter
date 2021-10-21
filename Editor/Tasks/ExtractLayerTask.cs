using System;
using System.Collections.Generic;
using System.Linq;
using PDNWrapper;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace UnityEditor.U2D.PSD
{
    class ExtractLayerTask
    {
        struct LayerExtractData
        {
            public int start;
            public int end;
            public int width;
            public int height;
        }
        
        struct ConvertBufferJob : IJobParallelFor
        {
            
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<IntPtr> original;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<LayerExtractData> flattenIndex;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<int> width;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<int> height;

            [DeallocateOnJobCompletion]
            public NativeArray<IntPtr> output;
            public unsafe void Execute(int index)
            {
                Color32* outputColor = (Color32*)output[index];
                for (int i = flattenIndex[index].start; i <= flattenIndex[index].end; ++i)
                {
                    if (original[i] == IntPtr.Zero)
                        continue;
                    Color32* originalColor = (Color32*)original[i];
                    int bufferWidth = width[i];
                    int bufferHeight = height[i];
                    for (int h = 0; h < bufferHeight; ++h)
                    {
                        int originalYOffset = h * bufferWidth;
                        int outputYOffset = (bufferHeight - h - 1) * bufferWidth;
                        for (int w = 0; w < bufferWidth; ++w)
                        {
                            var outColor = outputColor[w + outputYOffset];
                            var inColor = originalColor[w + originalYOffset];
                            float alpha = outColor.a / 255.0f;
                            outColor.r = (byte)(alpha * (float)(outColor.r) + (float)((1.0f - alpha) * (float)inColor.r));
                            outColor.g = (byte)(alpha * (float)(outColor.g) + (float)((1.0f - alpha) * (float)inColor.g));
                            outColor.b = (byte)(alpha * (float)(outColor.b) + (float)((1.0f - alpha) * (float)inColor.b));
                            outColor.a = (byte)(alpha * (float)(outColor.a) + (float)((1.0f - alpha) * (float)inColor.a));
                            outputColor[w + outputYOffset] = outColor;
                        }
                    }
                }
            }
        }

        public static unsafe void Execute(List<PSDLayer> extractedLayer, PSDExtractLayerData[] layers, bool importHiddenLayer)
        {
            UnityEngine.Profiling.Profiler.BeginSample("ExtractLayer_PrepareJob");
            List<LayerExtractData> layerToExtract = new List<LayerExtractData>();
            ExtractLayer(extractedLayer, layers, importHiddenLayer, false, layerToExtract, true);
            if (layerToExtract.Count == 0)
            {
                foreach (var l in extractedLayer)
                    l.texture = default;
                return;
            }
                
            var job = new ConvertBufferJob();
            job.original = new NativeArray<IntPtr>(extractedLayer.Count, Allocator.TempJob);
            job.output = new NativeArray<IntPtr>(layerToExtract.Count, Allocator.TempJob);
            job.width = new NativeArray<int>(extractedLayer.Count, Allocator.TempJob);
            job.height = new NativeArray<int>(extractedLayer.Count, Allocator.TempJob);
            
            for (int i = 0, extractLayerIndex = 0; i < extractedLayer.Count; ++i)
            {
                var el = extractedLayer[i];
                job.original[i] = el.texture.IsCreated ? new IntPtr(el.texture.GetUnsafePtr()) : IntPtr.Zero;
                if (extractLayerIndex < layerToExtract.Count && layerToExtract[extractLayerIndex].start == i)
                {
                    el.texture = new NativeArray<Color32>(layerToExtract[extractLayerIndex].width * layerToExtract[extractLayerIndex].height, Allocator.Persistent);
                    job.output[extractLayerIndex] = el.texture.IsCreated ? new IntPtr(el.texture.GetUnsafePtr()) : IntPtr.Zero;
                    ++extractLayerIndex;
                }
                else
                {
                    el.texture = default;
                }
                
                job.width[i] = el.width;
                job.height[i] = el.height;
            }
            job.flattenIndex = new NativeArray<LayerExtractData>(layerToExtract.ToArray(), Allocator.TempJob);

            var jobsPerThread = layerToExtract.Count / (SystemInfo.processorCount == 0 ? 8 : SystemInfo.processorCount);
            jobsPerThread = Mathf.Max(jobsPerThread, 1);
            var handle = job.Schedule(layerToExtract.Count, jobsPerThread);
            UnityEngine.Profiling.Profiler.EndSample();
            handle.Complete();
        }

        static (int width, int height) ExtractLayer(List<PSDLayer> extractedLayer, PSDExtractLayerData[] layers, bool importHiddenLayer, bool flatten, List<LayerExtractData> layerExtract, bool parentGroupVisible)
        {
            // parent is the previous element in extracedLayer
            int parentGroupIndex = extractedLayer.Count - 1;
            int maxWidth = 0, maxHeight = 0;
            int width = 0, height = 0;
            foreach (var l in layers)
            {
                var bitmapLayer = l.bitmapLayer;
                var importsetting = l.importSetting;
                bool layerVisible = bitmapLayer.Visible && parentGroupVisible;
                if (l.bitmapLayer.IsGroup)
                {
                    var layer = new PSDLayer(bitmapLayer.Surface.color, parentGroupIndex, bitmapLayer.IsGroup, bitmapLayer.Name, 0, 0, bitmapLayer.LayerID, bitmapLayer.Visible)
                    {
                        spriteID = l.importSetting.spriteId
                    };
                    layer.flatten = l.importSetting.flatten;
                    layer.isImported = (importHiddenLayer || layerVisible) && !flatten && layer.flatten && importsetting.importLayer;
                    int startIndex = extractedLayer.Count;
                    extractedLayer.Add(layer);
                    (width, height) = ExtractLayer(extractedLayer, l.children, importHiddenLayer, flatten || layer.flatten, layerExtract, layerVisible);
                    int endIndex = extractedLayer.Count - 1;
                    // If this group is to be flatten and there are flatten layers
                    if (flatten == false && layer.flatten && startIndex  < endIndex)
                    {
                        layerExtract.Add(new LayerExtractData()
                        {
                            start = startIndex,
                            end = endIndex,
                            width = width,
                            height = height
                        });   
                    }
                }
                else
                {
                    var surface = (importHiddenLayer || bitmapLayer.Visible) && importsetting.importLayer ? bitmapLayer.Surface.color : default; 
                    var layer = new PSDLayer(surface, parentGroupIndex, bitmapLayer.IsGroup, bitmapLayer.Name, bitmapLayer.Surface.width, bitmapLayer.Surface.height, bitmapLayer.LayerID,bitmapLayer.Visible)
                    {
                        spriteID = importsetting.spriteId
                    };
                    layer.isImported = (importHiddenLayer || layerVisible) && !flatten && importsetting.importLayer;
                    extractedLayer.Add(layer);
                    if (layer.isImported)
                    {
                        layerExtract.Add(new LayerExtractData()
                        {
                            start = extractedLayer.Count-1,
                            end = extractedLayer.Count-1,
                            width = bitmapLayer.Surface.width,
                            height = bitmapLayer.Surface.height,
                        });
                    }

                    width = bitmapLayer.Surface.width;
                    height = bitmapLayer.Surface.height;
                }
                if (maxWidth < width)
                    maxWidth = width;
                if (maxHeight < height)
                    maxHeight = height;
            }
            return (maxWidth, maxHeight);
        }
    }
}
