# PSD Importer Inspector properties
The PSD Importer is available after you import a .psb file into your Project. Select the .psb Asset file and set its **Texture Type** to [Sprite (2D and UI)](https://docs.unity3d.com/Manual/texture-type-sprite.html). The PSD Importer properties are split between two main tabs, with the following properties available.

## Settings tab
The Settings tab allows you to customize how the PSD Importer imports a file. The settings are categorized into individual section fold-outs.
### General

| **Property** | **Description** |
|-|-|
| **Texture Type**         | Select [Sprite (2D and UI)](https://docs.unity3d.com/Manual/texture-type-sprite.html) to import the Texture as a [Sprite](https://docs.unity3d.com/Manual/Sprites.html). This is required to begin using the imported Texture with the [2D Animation](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest/) package. |
| **Sprite Mode**          | Use this property to specify how Unity extracts the Sprite graphic from the image. This property is set to **Multiple** by default. The options are:<ul><li><strong>Single</strong>: Select this option to have Unity treat the imported Texture as a single Sprite Asset without multiple individual parts. This is ideal for characters which are drawn on a single layer in the source file instead of being split onto multiple layers.</li><li><strong>Multiple</strong>: This is the default option. Select this option to have Unity create a Sprite for each layer in the source file. This is ideal for complex artwork which has different parts split between multiple layers in the source file, and prepares the imported Textures for animation with the [2D Animation](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest) package.</li></ul> |
| **Pixels Per Unit**      | Sets the number of pixels that equal to one Unity unit.  |
| **Mesh Type**            | Sets the Mesh type that Unity generates for the Sprite. This is set to **Tight** by default. The options are:<ul><li>[**Full Rect**](https://docs.unity3d.com/Documentation/ScriptReference/SpriteMeshType.FullRect.html): Unity maps the Sprite onto a rectangular Mesh.</li><li>[**Tight**](https://docs.unity3d.com/Documentation/ScriptReference/SpriteMeshType.Tight.html): Unity generates a Mesh based on the outline of the Sprite. If the Sprite is smaller than 32 x 32 pixels, Unity always maps it onto a **Full Rect** quad Mesh, even if you select **Tight**.</li></ul> |
| **Extrude Edges**        | Use the slider to determine how much to extend the Mesh from the edge of the Sprite. |
| **Generate Physics Shape** | Enable this option to generate a default [Physics Shape](https://docs.unity3d.com/Documentation/Manual/sprite/sprite-editor/custom-physics-shape/generate-physics-shape.html) from the outline of the Sprite, if a [Custom Physics Shape](https://docs.unity3d.com/Manual/CustomPhysicsShape.html) has not been defined. |
| **Automatic Reslice**    | This is available only when the **Import Mode** is set to [Individual Sprites (Mosaic)](#Mosaic). Enable this setting to regenerate the Sprite from the imported layers and clear any changes you have made to the Sprite and its metadata. |

#### Automatic Reslice
Enable this setting to discard all user modifications for the current set of [SpriteRect](https://docs.unity3d.com/Packages/com.unity.2d.sprite@1.0/api/UnityEditor.SpriteRect.html) data and regenerate all SpriteRects based on the current source file. Extra SpriteRect metadata (such as weights and bones data) persist if they remain valid with the regenerated SpriteRects.

### Layer Import
The following section is only available if the **Texture Type** is set to **Multiple**.

| **Property** | **Description** |
|-|-|
| **Include Hidden Layers** | Enable this property to include the hidden [layers](https://helpx.adobe.com/photoshop/using/layer-basics.html#layers_panel_overview) of the .psb file in the import. This produces the same import result as making all layers visible in the source file unhiding all layers in the source file before you importing it into Unity. Clear this option if you want to only import the visible layers in the .psb file. |
| **Keep Duplicate Name** | Enable this setting to make the PSD Importer generate Sprites from the source files with the exact same name as their source layers, even when there are multiple layers with the same name.|
| **Use Layer Group** | This setting is only available when you enable **Character rig**. Enable this setting to make the importer generate a Prefab that follows the layer and grouping hierarchy of the imported .psb. file. |
| **Layer Mapping** | Select this option to use the internal ID provided by the .psb file to map between the .psb file’s layer and the generated Sprite. The options are: <ul><li>**Use Layer ID**: Select this to only import the visible layers in the .psb file.</li><li>**Use Layer Name**: Select this option to use the name of the layer in the .psb file to map between the .psb file’s layer and the generated Sprite. Note that for this option to work correctly, each layer's name needs to be unique. Duplicated names might cause layers to be mapped to the wrong Sprite.</li><li>**Use Layer Name (Case Sensitive)**: Select this option to use the name of the layer (with case sensitivity) in the .psb file to map between the .psb file’s layer and the generated Sprite. Note that for this option to work correctly, each layer's name needs to be unique. Duplicated names might cause layers to be mapped to the wrong Sprite.</li></ul>|
| **Import Mode** | Use this property to specify how the layers from the source file are imported. This property is set **Individual Sprites (Mosaic)** by default. The options are: <ul><li>[**Individual Sprites (Mosaic)**](#Mosaic): Select this option to have the PSD Importer generate individual Sprites from the individual layers of the source file, and combines them into a single Texture in a Sprite sheet layout.</li><li>[**Merged**](#merged): Select this to have the PSD Importer generate a Texture with all layers merged.</li></ul>|
| **Mosaic Padding** | Settings to control the padding space between each layer in the texture when **Import Mode** is set to **Individual Sprites (Mosaic)**. |
| **Sprite Padding** | Settings to increase the size of each Sprite's rect in the texture when **Import Mode** is set to **Individual Sprites (Mosaic)**. |

#### <a name="Mosaic"></a>Individual Sprites (Mosaic)
Enable this to have the PSD Importer attempt to create a Texture with each layer from the source file laid out in a mosaic manner.

![Left: A character sprite. Right: The Sprite Editor window, with the parts of the character arranged in one texture.](images/individual-sprites-mosaic-22.2.png)<br/>Individual layers are imported as separate Sprites merged into a single Texture in the form of a mosaic.

#### <a name="merged"></a>Merged
Enable this option to have the PSD Importer create a Texture with the layers from the Photoshop source file as if all [layers were flattened](https://helpx.adobe.com/photoshop/using/layers.html).

![Left: A character sprite. Right: The Sprite Editor window, with a texture that's identical to the character sprite.](images/merged-layers-22.2.png)

#### Keep Duplicate Names
Unity's default import behavior when there are duplicate names is to append "_[number]" to the Sprites and SpriteRects it generates from source layers with identical names. Enable this feature to instead have Unity give both Sprites and SpriteRects the exact same name as their source layer, even when they are duplicate names.

#### Layer Group

By default, the importer only generates GameObjects for layers in the source file. This is for performance reasons to minimize the number of GameObjects needed for the Prefab.

![A character sprite in the Scene view. The Hierarchy window lists one GameObject for each layer in the source file.](images/ignore-layer-groups-22.2.png)<br/>The generated Prefab with **Layer Group** set to **Ignore Layer Groups**.

To include and maintain the group and hierarchy structure as per the source file, you can set the **Layer Group** to **As Per Source File**, as shown in the example below.

![The same character sprite, but the Hierarchy window now lists the GameObjects in a nested structure.](images/as-per-source-22.2.png)<br/>The generated Prefab of the same source file with **Layer Group** set to **As Per Source File**.

### Character Rig
This section is only available if the **Texture Type** is set to **Multiple**, **Import Mode** is set to **Individual Sprites (Mosaic)** and the [2D Animation package](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest) is installed.


| **Property** | **Description** |
| - | - |
| [Use as Rig](#use-as-rig)   |Enable this property to have the PSD Importer generate a Prefab based on the imported source file. The PSD Importer generates Sprites from the imported layers of the source file, and the Sprites’ [hierarchy](https://docs.unity3d.com/Manual/Hierarchy.html) and positions are based on their [layer hierarchy](https://helpx.adobe.com/photoshop/using/layer-basics.html#layers_panel_overview) and their positions in the source file.  |
| [Main Skeleton](#main-skeleton)  | This is only available when **Use as Rig** is enabled. Assign the **Skeleton Asset** that this character Prefab’s bone hierarchy will reference. If no Skeleton Asset is assigned, the importer will automatically generate a Skeleton Asset as a sub-Asset of this character. The Skeleton Asset contains the bone hierarchy of the Asset that was defined in the 2D Animation package's [Skinning Editor](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest?subfolder=/manual/SkinningEditor.html) (refer to [Skeleton Sharing](http://docs.unity3d.com/Packages/com.unity.2d.psdimporter@latest?subfolder=/manual/skeleton-sharing.html) for more information).|
| **Pivot**   | This is only available when **Use as Rig** is enabled. Select the pivot point of the Sprite. The options are: <ul><li>**Custom:** Define the X and Y coordinates of a custom Pivot location.</li> <li>**(All location choices):** Select the location where you want to place the pivot on the Sprite from the dropdown menu.</li></ul> |


#### Use as Rig
Enable this property to have the PSD Importer generate a Prefab containing Sprites based on the layers of the imported source file. The PSD Importer also automatically gives the Sprites an [Order in Layer](https://docs.unity3d.com/Manual/2DSorting.html) value that sorts them according to their positions in the layer hierarchy in the source file. As a result, the generated Prefab recreates the arrangement and appearance of the assets in the original source file as closely as possible.

The name of each Sprite in the Prefab is the same as the source layer it is based on, unless a **name collision error** occurs, which is usually due to duplicate names in the source layers.

If the Sprite contains [bone](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest?subfolder=/manual/SkinEdToolsShortcuts.html%23bone-tools) or [weight](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest?subfolder=/manual/SkinEdToolsShortcuts.html%23weight-tools) data, the PSD Importer automatically adds the Sprite Skin component to it. This happens if the Sprite has been [rigged](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest?subfolder=/manual/CharacterRig.html) with bones and weights in the [Skinning Editor](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest?subfolder=/manual/SkinningEditor.html) already and the source file is being reimported, or you have manually [copied and pasted](https://docs.unity3d.com/Packages/com.unity.2d.animation@latest?subfolder=/manual/SkinEdToolsShortcuts.html%23copy-and-paste-behavior) the bone and weight data onto the Sprites.

#### Main Skeleton
A skeleton Asset (.skeleton) is an Asset that contains the bone hierarchy structure that can be animated with the 2D Animation package. The **Main Skeleton** property is only available when you import a .psb file with the **Use As Rig** importer setting enabled. After importing the .psb file, assign a .skeleton Asset to the **Main Skeleton** property to have the generated prefab character be automatically rigged with the bone hierarchy structure contained in that .skeleton Asset.

If there is no .skeleton Asset assigned to the importer’s **Main Skeleton** property, then a .skeleton Asset is automatically generated as a  of the imported source file and it will be named ‘[Asset File Name] Skeleton’. You can **share .skeleton Assets** between different generated Prefabs by assigning the same .skeleton as their **Main Skeleton** property when they're imported.

When you open and edit the character in 2D Animation package’s **Skinning Editor**, the module will display the bone hierarchy provided by the skeleton Asset assigned to **Main Skeleton** for rigging.

### Tile Palette
The following section allows generating Tile assets and Tile Palette where each layer in the source file will also generate a tile asset

## Layer Management Tab
The **Layer Management Tab** allows you to customize how the Importer imports the layers from the Photoshop file.

### Layer hierarchy tree
[Group layers](https://helpx.adobe.com/photoshop/using/selecting-grouping-linking-layers.html) in Photoshop are represented with a fold-out folder icon ![Group layers icon.](images/group-layers-icon.png) in the hierarchy tree of the Layer Management tab, while regular Photoshop layers in Photoshop represented only by their names.

### Layer visibility
Groups or layers that are hidden in the source file are indicated with different color text compared to visible groups or layers.

### Layer Importing
The checkbox on each Group/Layer indicates if the Group or Layer from the Photoshop file should be imported. The Group or Layer will be imported when the checkbox is selected.  

Clear the **Include Hidden Layers** option in the **Layer Management Tab** or [Settings Tab](#settings-tab) will only import visible layers from the source file.

If a hidden group or layer is mark for import in this state, a warning icon will appear.

To import a hidden layer, select the **Include Hidden Layers** checkbox either in the **Settings Tab** or in the **Layer Management Tab**

To batch select or deselect layers, you can use the drop down menu located at header of the Layer Importing column.
![The dropdown menu at the top of the left column. The properties are **Select All Visible Layers**, **Deselect All Visible Layers**, **Select All Hidden Layers**, and **Deselect All Hidden Layers**.](images/import-selection-dropdown-22.2.png)

### Merge Groups
The Photoshop layers in a Group can be merged into a single Sprite when imported. Hover the cursor over a Group Layer and the **Merge** icon appears on its left.

Click the icon to indicate that the layers in the selected Group should be imported as a single Sprite.

### Separate Groups
Hovering over a Merged Group layer reveals the Separate icon (the arrow faces upwards).

Select the icon again to separate the Group layer and to import all Layers in the Group as separate Sprites.

#### Subgroups within Group layers
If a Group contains other Group layers and is merged, then the layers in the subgroups will also be merged into a single Sprite.

If a child Group is currently set to be merged, then the parent group will have separate icons indicating that are child Groups currently set to merge.
