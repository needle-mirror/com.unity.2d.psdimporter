using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace UnityEditor.U2D.PSD
{
    internal static class ExtractLayerTask
    {
        struct LayerGroupData
        {
            public int startIndex { get; set; }
            public int endIndex { get; set; }
            
            /// <summary>
            /// The layer's bounding box in document space.
            /// </summary>
            public int4 documentRect { get; set; }

            public int width => documentRect.z;
            public int height => documentRect.w;
        }
        
        [BurstCompile]
        struct ConvertBufferJob : IJobParallelFor
        {
            [ReadOnly, DeallocateOnJobCompletion] 
            public NativeArray<int> inputTextureBufferSizes;
            [ReadOnly, DeallocateOnJobCompletion]
            public NativeArray<IntPtr> inputTextures;
            [ReadOnly, DeallocateOnJobCompletion]
            public NativeArray<int4> inputLayerRects;
            [ReadOnly, DeallocateOnJobCompletion]
            public NativeArray<LayerGroupData> layerGroupDataData;

            [ReadOnly, DeallocateOnJobCompletion] 
            public NativeArray<int2> outputTextureSizes;
            [DeallocateOnJobCompletion]
            public NativeArray<IntPtr> outputTextures;
            public unsafe void Execute(int index) 
            {
                var outputColor = (Color32*)outputTextures[index];
                var groupStartIndex = layerGroupDataData[index].startIndex;
                var groupEndIndex = layerGroupDataData[index].endIndex;
                var groupRect = layerGroupDataData[index].documentRect;
                
                var outWidth = outputTextureSizes[index].x;
                var outHeight = outputTextureSizes[index].y;

                for (var i = groupEndIndex; i >= groupStartIndex; --i)
                {
                    if (inputTextures[i] == IntPtr.Zero)
                        continue;

                    var inputColor = (Color32*)inputTextures[i];
                    var inWidth = inputLayerRects[i].z;
                    var inHeight = inputLayerRects[i].w;
                    
                    var layerToGroupSpace = new int2(inputLayerRects[i].x - groupRect.x, inputLayerRects[i].y - groupRect.y);
                    // Flip Y position
                    layerToGroupSpace.y = outHeight - layerToGroupSpace.y - inHeight;

                    for (var y = 0; y < inHeight; ++y)
                    {
                        var inY = y * inWidth;
                        var outY = (outHeight - 1 - y - layerToGroupSpace.y) * outWidth;
                        
                        for (var x = 0; x < inWidth; ++x)
                        {
                            var inX =  inY + x;
                            var outX = outY + x + layerToGroupSpace.x;
                            
                            if (inX >= inputTextureBufferSizes[i])
                                break;
                            if (outX >= (outWidth * outHeight))
                                break;

                            Color inColor = inputColor[inX];
                            Color prevOutColor = outputColor[outX];
                            var outColor = new Color();
                            
                            var destAlpha = prevOutColor.a * (1 - inColor.a);
                            outColor.a = inColor.a + prevOutColor.a * (1 - inColor.a);
                            var premultiplyAlpha = 1 / outColor.a;
                            outColor.r = (inColor.r * inColor.a + prevOutColor.r * destAlpha) * premultiplyAlpha;
                            outColor.g = (inColor.g * inColor.a + prevOutColor.g * destAlpha) * premultiplyAlpha;
                            outColor.b = (inColor.b * inColor.a + prevOutColor.b * destAlpha) * premultiplyAlpha;
                            
                            outputColor[outX] = outColor;                            
                        }
                    }
                }
            }
        }

        public static unsafe void Execute(in PSDExtractLayerData[] inputLayers, out List<PSDLayer> outputLayers, bool importHiddenLayer, Vector2Int documentSize)
        {
            outputLayers = new List<PSDLayer>();
            UnityEngine.Profiling.Profiler.BeginSample("ExtractLayer_PrepareJob");
            
            var layerGroupData = new List<LayerGroupData>();
            ExtractLayer(in inputLayers, ref outputLayers, ref layerGroupData, importHiddenLayer, false, true, documentSize);
            
            if (layerGroupData.Count == 0)
            {
                foreach (var layer in outputLayers)
                    layer.texture = default;
                return;
            }
            
            var job = new ConvertBufferJob();
            job.inputTextureBufferSizes = new NativeArray<int>(outputLayers.Count, Allocator.TempJob);
            job.inputTextures = new NativeArray<IntPtr>(outputLayers.Count, Allocator.TempJob);
            job.inputLayerRects = new NativeArray<int4>(outputLayers.Count, Allocator.TempJob);
            job.outputTextureSizes = new NativeArray<int2>(layerGroupData.Count, Allocator.TempJob);
            job.outputTextures = new NativeArray<IntPtr>(layerGroupData.Count, Allocator.TempJob);
            
            for (int i = 0, outputLayerIndex = 0; i < outputLayers.Count; ++i)
            {
                var outputLayer = outputLayers[i];
                
                job.inputTextures[i] = outputLayer.texture.IsCreated ? new IntPtr(outputLayer.texture.GetUnsafePtr()) : IntPtr.Zero;

                if (outputLayerIndex < layerGroupData.Count && layerGroupData[outputLayerIndex].startIndex == i)
                {
                    var inputLayer = layerGroupData[outputLayerIndex];
                    
                    outputLayer.texture = new NativeArray<Color32>(inputLayer.width * inputLayer.height, Allocator.Persistent);
                    job.outputTextureSizes[outputLayerIndex] = new int2(inputLayer.width, inputLayer.height);
                    job.outputTextures[outputLayerIndex] = outputLayer.texture.IsCreated ? new IntPtr(outputLayer.texture.GetUnsafePtr()) : IntPtr.Zero;
                    job.inputTextureBufferSizes[i] = outputLayer.texture.IsCreated ? outputLayer.texture.Length : -1;
                    ++outputLayerIndex;
                }
                else
                {
                    job.inputTextureBufferSizes[i] = outputLayer.texture.IsCreated ? outputLayer.texture.Length : -1;
                    outputLayer.texture = default;
                }
                
                job.inputLayerRects[i] = new int4((int)outputLayer.layerPosition.x, (int)outputLayer.layerPosition.y, outputLayer.width, outputLayer.height);
            }
            job.layerGroupDataData = new NativeArray<LayerGroupData>(layerGroupData.ToArray(), Allocator.TempJob);

            var jobsPerThread = layerGroupData.Count / (SystemInfo.processorCount == 0 ? 8 : SystemInfo.processorCount);
            jobsPerThread = Mathf.Max(jobsPerThread, 1);
            var handle = job.Schedule(layerGroupData.Count, jobsPerThread);
            UnityEngine.Profiling.Profiler.EndSample();
            handle.Complete();
        }

        static Rect ExtractLayer(in PSDExtractLayerData[] inputLayers, ref List<PSDLayer> outputLayers, ref List<LayerGroupData> layerGroupData, bool importHiddenLayer, bool flatten, bool parentGroupVisible, Vector2Int documentSize)
        {
            // parent is the previous element in extractedLayer
            var parentGroupIndex = outputLayers.Count - 1;
            var layerBoundingBox = default(Rect);

            foreach (var inputLayer in inputLayers)
            {
                var bitmapLayer = inputLayer.bitmapLayer;
                var importSettings = inputLayer.importSetting;
                var layerVisible = bitmapLayer.Visible && parentGroupVisible;

                var layerRect = new Rect(float.MaxValue, float.MaxValue, 0f, 0f);
                if (inputLayer.bitmapLayer.IsGroup)
                {
                    var outputLayer = new PSDLayer(bitmapLayer.Surface.color, parentGroupIndex, bitmapLayer.IsGroup, bitmapLayer.Name, 0, 0, bitmapLayer.LayerID, bitmapLayer.Visible)
                    {
                        layerPosition = Vector2.zero,
                        spriteID = inputLayer.importSetting.spriteId,
                        flatten = inputLayer.importSetting.flatten
                    };
                    outputLayer.isImported = (importHiddenLayer || layerVisible) && !flatten && outputLayer.flatten;
                    
                    var startIndex = outputLayers.Count;
                    outputLayers.Add(outputLayer);
                    layerRect = ExtractLayer(in inputLayer.children, ref outputLayers, ref layerGroupData, importHiddenLayer, flatten || outputLayer.flatten, layerVisible, documentSize);
                    var endIndex = outputLayers.Count - 1;
                    
                    // If this group is to be flatten and there are flatten layers
                    if (flatten == false && outputLayer.flatten && startIndex  < endIndex)
                    {
                        layerGroupData.Add(new LayerGroupData()
                        {
                            startIndex = startIndex,
                            endIndex = endIndex,
                            documentRect = new int4((int)layerRect.x, (int)layerRect.y, (int)layerRect.width, (int)layerRect.height)
                        });

                        outputLayer.texture = default;
                        outputLayer.layerPosition = new Vector2(layerRect.x, layerRect.y);
                        outputLayer.width = (int)layerRect.width;
                        outputLayer.height = (int)layerRect.height;
                    }
                }
                else
                {
                    var layerRectDocSpace = bitmapLayer.documentRect;
                    // From Photoshop "space" into Unity "space"
                    layerRectDocSpace.Y = (documentSize.y - layerRectDocSpace.Y) - layerRectDocSpace.Height;
                    
                    var surface = (importHiddenLayer || bitmapLayer.Visible)  ? bitmapLayer.Surface.color : default;
                    var outputLayer = new PSDLayer(surface, parentGroupIndex, bitmapLayer.IsGroup, bitmapLayer.Name, bitmapLayer.Surface.width, bitmapLayer.Surface.height, bitmapLayer.LayerID,bitmapLayer.Visible)
                    {
                        spriteID = importSettings.spriteId,
                        layerPosition = new Vector2(layerRectDocSpace.X, layerRectDocSpace.Y)
                    };
                    outputLayer.isImported = (importHiddenLayer || layerVisible) && !flatten;
                    outputLayers.Add(outputLayer);
                    if (outputLayer.isImported)
                    {
                        layerGroupData.Add(new LayerGroupData()
                        {
                            startIndex = outputLayers.Count - 1,
                            endIndex = outputLayers.Count - 1,
                            documentRect = new int4(layerRectDocSpace.X, layerRectDocSpace.Y, layerRectDocSpace.Width, layerRectDocSpace.Height)
                        });
                    }

                    layerRect.x = layerRectDocSpace.X;
                    layerRect.y = layerRectDocSpace.Y;
                    layerRect.width = bitmapLayer.Surface.width;
                    layerRect.height = bitmapLayer.Surface.height;
                }

                if (layerBoundingBox == default)
                    layerBoundingBox = layerRect;
                else
                {
                    if (layerBoundingBox.xMin > layerRect.xMin)
                        layerBoundingBox.xMin = layerRect.xMin;
                    if (layerBoundingBox.yMin > layerRect.yMin)
                        layerBoundingBox.yMin = layerRect.yMin;
                    if (layerBoundingBox.xMax < layerRect.xMax)
                        layerBoundingBox.xMax = layerRect.xMax;
                    if (layerBoundingBox.yMax < layerRect.yMax)
                        layerBoundingBox.yMax = layerRect.yMax;
                }

                layerBoundingBox.width = Mathf.Min(layerBoundingBox.width, documentSize.x);
                layerBoundingBox.height = Mathf.Min(layerBoundingBox.height, documentSize.y);
            }
            return layerBoundingBox;
        }
    }
}