# PSD Importer Inspector properties
The PSD Importer is available after you import a .psb file into your Project. Select the .psb Asset file and set its **Texture Type** to [Sprite (2D and UI)](https://docs.unity3d.com/Manual/TextureTypes.html#Sprite). The following PSD Importer properties become available in the Inspector window.

![](images/21_2-PSDImporter-properties.png) <br/>PSD Importer Inspector properties

| Property                                                     | Function                                                     |
| ------------------------------------------------------------ | ------------------------------------------------------------ |
| __Texture Type__                                             | Select [Sprite (2D and UI)](https://docs.unity3d.com/Manual/TextureTypes.html#Sprite) to import the Texture as a [Sprite](https://docs.unity3d.com/Manual/Sprites.html). This is necessary to begin using the imported images with the [2D Animation](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest/) package. |
| <a name="SpriteMode"></a>__Sprite Mode__                                              | Use this property to specify how Unity extracts the Sprite graphic from the image. This property is set to __Multiple__ by default. |
| &nbsp;&nbsp;&nbsp;&nbsp;Single                               | Select **Single** to have Unity treat the imported texture as a single Sprite Asset without multiple individual parts. This is ideal for characters that are a single layer in the source .psb file, which are not split into multiple layers. |
| &nbsp;&nbsp;&nbsp;&nbsp;Multiple                             |If the imported texture has multiple parts, select **Multiple** to have Unity create a Sprite for each layer. This is ideal for complex characters which have different parts split between multiple layers in the source .psb file, because it prepares the characters for animation with the [2D Animation](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest) package. |
| __Pixels Per Unit__                                          | Set the number of pixels that equal to one Unity unit.           |
| __Mesh Type__                                                | Set the Mesh type that Unity generates for the Sprite. This is set to __Tight__ by default. |
| &nbsp;&nbsp;&nbsp;&nbsp;[Full Rect](https://docs.unity3d.com/Documentation/ScriptReference/SpriteMeshType.FullRect.html) | Unity maps the Sprite onto a rectangular Mesh.               |
| &nbsp;&nbsp;&nbsp;&nbsp;[Tight](https://docs.unity3d.com/Documentation/ScriptReference/SpriteMeshType.Tight.html) | Unity generates a Mesh based on the outline of the Sprite. If the Sprite is smaller than 32 x 32 pixels, Unity always maps it onto a __Full Rect__ quad Mesh, even if you select __Tight__. |
| [__Extrude Edges__](https://docs.unity3d.com/Manual/Glossary.html#ExtrudeEdges) | Use the slider to determine how much to extend the Mesh from the edge of the Sprite. |
| __Import Hidden__                                            | Enable this property to include the hidden [layers](https://helpx.adobe.com/photoshop/using/layer-basics.html#layers_panel_overview) of the .psb file in the import. This is the equivalent of making all layers visible in the source file before it is imported into Unity. Disable this property if you want to only import the visible layers in the .psb file. |
|  <a name="Mosaic"></a>__Mosaic__                              |Enable this property to make the PSD Importer generate Sprites from the imported layers and combine them into a single Texture in a Sprite sheet layout.</br>This setting is only available if you set the **Texture Type** to **Multiple**. |   |   |
| <a name="Rig"></a>__Character Rig__   | Enable this property to make the importer generate a [Prefab](https://docs.unity3d.com/Manual/Prefabs.html) based on the imported source file. The PSD Importer generates Sprites from the imported layers of the source file, and the Sprites’ [hierarchy](https://docs.unity3d.com/Manual/Hierarchy.html) and positions are based on their [layer hierarchy](https://helpx.adobe.com/photoshop/using/layer-basics.html#layers_panel_overview) and their positions in the .psb file.<br/>For more details, see [Property details: Character Rig](#character-rig), below. |
| __Use Layer Grouping__<a name="LayerGrouping"></a>                                       | Enable this setting to make the importer generate a Prefab that follows the layer and grouping hierarchy of the imported .psb file.<br/>This setting is only available when you enable **Character Rig**.  |
| __Pivot__                                                    |Select the pivot point of the Sprite.<br/>This is only available when **Character Rig** is enabled. |
| &nbsp;&nbsp;&nbsp;&nbsp; Custom                              | Define the X and Y coordinates of a custom __Pivot__ location. |
| __Main Skeleton__                               | Assign the [Skeleton Asset](#main-skeleton) that this character Prefab’s bone hierarchy should reference. <br/>If no Skeleton Asset is assigned, the importer automatically generates a Skeleton Asset as a sub-asset of this character. The Skeleton Asset contains the bone hierarchy of the Asset defined in the [Skinning Module]() (see [Skeleton Sharing](skeleton-sharing.md) for more information about using Skeleton Assets).<br/>This property is only available when Character Rig is enabled.<br/>For more details, see [Property details: Main Skeleton](#main-skeleton), below. |
| __Reslice__    |Enable this setting to regenerate the Sprite from the imported layers and clear any changes you have made to the Sprite and its metadata.<br/>This is available only when Mosaic is enabled.<br/>For more details, see [Property details: Reslice](#reslice) below. |
| __Keep Duplicate Name__                  | Enable this setting to make the PSD Importer generate Sprites from the source files with the exact same name as their source layers, even when there are multiple layers with the same name.<br/>For more details, see [Property details: Keep Duplicate Name](#keep-duplicate-name) below.|

## Property details
### Character Rig
When **Character Rig** is enabled, the PSD Importer generates a Prefab that contains [Sprites](https://docs.unity3d.com/Manual/Sprites.html) based on the layers of the imported source file. The PSD Importer also automatically gives the Sprites an [Order in Layer](https://docs.unity3d.com/Manual/2DSorting.html#sortlayer) value that sorts them according to their positions in the layer hierarchy in the source file. As a result, the generated Prefab recreates the arrangement and appearance of the assets in the original source file as closely as possible.

The name of each Sprite in the Prefab is the same as the source layer it is based on, unless a [name collision error](PSD-importer-SpriteRect.md#name-collision-errors) occurs, which is usually due to duplicate names in the source layers.

If the Sprite contains [bone](https://docs.unity3d.com/Packages/com.unity.2d.animation@6.0/manual/SkinEdToolsShortcuts.html#bone-tools) or [weight](https://docs.unity3d.com/Packages/com.unity.2d.animation@6.0/manual/SkinEdToolsShortcuts.html#weight-tools) data, the PSD Importer automatically adds the __Sprite Skin__ component to it. This happens if the Sprite is already [rigged](https://docs.unity3d.com/Packages/com.unity.2d.animation@6.0/manual/CharacterRig.html) with bones and weights in the [Skinning Editor](https://docs.unity3d.com/Packages/com.unity.2d.animation@6.0/manual/SkinningEditor.html) already and you are reimporting the source file, or if you have manually [copied and pasted](https://docs.unity3d.com/Packages/com.unity.2d.animation@6.0/manual/CopyPasteSkele.html) the bone and weight data onto the Sprites.

The examples below demonstrate a character designed in Photoshop, and how its original layer hierarchy is transposed to the Prefab.

![](images/PhotoshopSetup.png) <br/> Example 1: Character artwork in Photoshop with different parts separated into different Photoshop layers.

![](images/21_2-Fei-nogrouping.png) <br/> Example 2: The generated Prefab with Sprites sorted according to the source file’s layer hierarchy.

![](images/21_2-Fei-grouping-layers.png) <br/> Example 3: The generated Prefab of the same source file but with [Layer Grouping](#LayerGrouping) enabled.

### Main Skeleton
A skeleton Asset (.skeleton) is an Asset that contains the bone hierarchy structure that you can animate with the 2D Animation package. The **Main Skeleton** property is only available when you import a .psb file with the [**Character Rig**](#character-rig) importer setting enabled. After importing the .psb file, assign a .skeleton Asset to the **Main Skeleton** property to have Unity automatically rig generated prefab character with the bone hierarchy structure contained in that .skeleton Asset.

If no Skeleton Asset is assigned to the importer’s **Main Skeleton** property, then Unity automatically generates a Skeleton Asset as a sub-asset of the imported source file and it will be named ‘[Asset File Name] Skeleton’. You can [share .skeleton Assets](skeleton-sharing.md) between different generated Prefabs; to do this, assign the same .skeleton as their **Main Skeleton** property when they're imported.

When you open and edit the character in 2D Animation package’s [Skinning Editor](https://docs.unity3d.com/Packages/com.unity.2d.animation@6.0/manual/SkinningEditor.html), the module displays the bone hierarchy provided by the Skeleton Asset assigned to **Main Skeleton** for rigging.  

### Reslice
Enable this setting to discard all user modifications for the current set of [`SpriteRect`](https://docs.unity3d.com/Packages/com.unity.2d.sprite@1.0/api/UnityEditor.SpriteRect.html) data and regenerate all `SpriteRects` based on the current source file. Extra `SpriteRect` metadata (such as weights and bones data) persist if they remain valid with the regenerated `SpriteRects`.

### Keep Duplicate Name
Unity’s default import behavior when there are duplicate names is to append `_[number]` to Sprites and `SpriteRects` it generates from source layers with identical names. Enable this feature to instead have Unity give Sprites and `SpriteRects` the same name as their source layer even if they have duplicate names.
